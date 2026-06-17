using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.TestHelpers;

/// <summary>
/// In-memory <see cref="IFileSystem"/> fake for concurrency testing of the file-operation pipeline.
///
/// Capabilities the real System.IO API can't give a test:
/// - <see cref="OperationDelayMs"/> widens the window so any real overlap is observable.
/// - <see cref="MaxConcurrentMutations"/> records the peak number of mutating ops running at once,
///   so a test can assert the pipeline truly serialized them (peak == 1).
/// - <see cref="InjectTransientLock"/> makes a path throw IOException a set number of times before
///   succeeding, to exercise the planner's retry logic deterministically.
///
/// Paths are normalized case-insensitively with '/' separators.
/// </summary>
public class InMemoryFileSystem : IFileSystem
{
    private readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _transientFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private int _activeMutations;

    /// <summary>Peak number of mutating operations observed running simultaneously.</summary>
    public int MaxConcurrentMutations { get; private set; }

    /// <summary>Total mutating operations attempted (including retried failures).</summary>
    public int TotalMutations { get; private set; }

    /// <summary>Artificial delay injected into every mutating op to widen race windows.</summary>
    public int OperationDelayMs { get; set; }

    public void SeedDirectory(string path)
    {
        lock (_gate) { _dirs.Add(Norm(path)); }
    }

    public void SeedFile(string path)
    {
        lock (_gate)
        {
            _files[Norm(path)] = Array.Empty<byte>();
            _dirs.Add(Norm(Path.GetDirectoryName(path) ?? "/"));
        }
    }

    /// <summary>Make the next <paramref name="times"/> mutating ops on <paramref name="path"/> throw IOException.</summary>
    public void InjectTransientLock(string path, int times)
    {
        lock (_gate) { _transientFailures[Norm(path)] = times; }
    }

    // ---- IFileSystem ----

    public bool DirectoryExists(string path)
    {
        lock (_gate) { return _dirs.Contains(Norm(path)); }
    }

    public bool FileExists(string path)
    {
        lock (_gate) { return _files.ContainsKey(Norm(path)); }
    }

    public void DeleteDirectory(string path, bool recursive)
        => Mutate(path, key =>
        {
            if (!_dirs.Contains(key))
                throw new DirectoryNotFoundException(path);
            _dirs.RemoveWhere(d => d == key || d.StartsWith(key + "/", StringComparison.OrdinalIgnoreCase));
            foreach (var f in _files.Keys.Where(f => f.StartsWith(key + "/", StringComparison.OrdinalIgnoreCase)).ToList())
                _files.Remove(f);
        });

    public void MoveDirectory(string sourcePath, string destinationPath)
        => Mutate(sourcePath, src =>
        {
            var dst = Norm(destinationPath);
            if (!_dirs.Contains(src))
                throw new DirectoryNotFoundException(sourcePath);
            if (_dirs.Contains(dst))
                throw new IOException($"Target directory already exists: {destinationPath}");

            foreach (var d in _dirs.Where(d => d == src || d.StartsWith(src + "/", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _dirs.Remove(d);
                _dirs.Add(dst + d.Substring(src.Length));
            }
            foreach (var f in _files.Keys.Where(f => f.StartsWith(src + "/", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                var content = _files[f];
                _files.Remove(f);
                _files[dst + f.Substring(src.Length)] = content;
            }
        });

    public void DeleteFile(string path)
        => Mutate(path, key =>
        {
            if (!_files.Remove(key))
                throw new FileNotFoundException(path);
        });

    public void MoveFile(string sourcePath, string destinationPath)
        => Mutate(sourcePath, src =>
        {
            if (!_files.TryGetValue(src, out var content))
                throw new FileNotFoundException(sourcePath);
            var dst = Norm(destinationPath);
            if (_files.ContainsKey(dst))
                throw new IOException($"Target file already exists: {destinationPath}");
            _files.Remove(src);
            _files[dst] = content;
        });

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        => Mutate(sourcePath, src =>
        {
            if (!_files.TryGetValue(src, out var content))
                throw new FileNotFoundException(sourcePath);
            var dst = Norm(destinationPath);
            if (_files.ContainsKey(dst) && !overwrite)
                throw new IOException($"Target file already exists: {destinationPath}");
            _files[dst] = content;
        });

    // ---- internals ----

    private void Mutate(string path, Action<string> action)
    {
        var key = Norm(path);

        lock (_gate)
        {
            _activeMutations++;
            if (_activeMutations > MaxConcurrentMutations)
                MaxConcurrentMutations = _activeMutations;
            TotalMutations++;
        }

        try
        {
            // Sleep OUTSIDE the gate so genuinely-overlapping ops are visible via MaxConcurrentMutations.
            if (OperationDelayMs > 0)
                Thread.Sleep(OperationDelayMs);

            lock (_gate)
            {
                if (_transientFailures.TryGetValue(key, out var remaining) && remaining > 0)
                {
                    _transientFailures[key] = remaining - 1;
                    throw new IOException($"Simulated transient lock on {path}");
                }

                action(key);
            }
        }
        finally
        {
            lock (_gate) { _activeMutations--; }
        }
    }

    private static string Norm(string path)
        => path.Replace('\\', '/').TrimEnd('/');
}
