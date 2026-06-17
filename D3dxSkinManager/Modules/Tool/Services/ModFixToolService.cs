using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Per-profile fix-tool library. The {profile}/fixtools/ FOLDER is the source of truth: each top-level
/// entry is one fix tool ("toolset") — either a loose executable (.exe/.bat/.cmd/.py) or a subfolder.
/// Listing scans the folder, so dropping a tool in makes it appear (default info) and deleting it
/// removes it; no separate registry to drift. A toolset can expose MULTIPLE runnable entries; with a
/// single lone executable the entry auto-resolves, otherwise the user picks (the choice persists as a
/// marker file inside the folder, keeping it folder-authoritative). Game-agnostic.
/// </summary>
public interface IModFixToolService
{
    Task<List<ModFixTool>> GetAllAsync();

    /// <summary>Import a fix tool by copying a source file OR folder into a new fixtools/{name} folder.</summary>
    Task<ModFixTool> ImportAsync(string name, string sourcePath, bool isFolder, string? description = null);

    /// <summary>Delete a fix tool (id = its top-level name) — removes the folder or the loose file.</summary>
    Task DeleteAsync(string id);

    /// <summary>
    /// Set which files inside a folder tool are its runnable entries (persists a marker). Pass an empty
    /// list to clear the choice and fall back to auto-resolution.
    /// </summary>
    Task SetEntriesAsync(string id, List<string> relativeEntries);
}

public class ModFixToolService : IModFixToolService
{
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    // Entry auto-detection preference: self-contained exe first, then batch, then python.
    private static readonly string[] EntryExtPriority = { ".exe", ".bat", ".cmd", ".py" };
    // Marker file (inside a folder tool) recording the user's chosen entries (one relative path per line).
    private const string EntryMarker = ".fixentry";

    public ModFixToolService(IProfilePathService profilePaths, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _logger = logger;
    }

    public Task<List<ModFixTool>> GetAllAsync()
    {
        var root = _profilePaths.FixToolsDirectory;
        var tools = new List<ModFixTool>();
        if (!Directory.Exists(root)) return Task.FromResult(tools);

        // Top-level loose executables — each is a tool with itself as the only entry.
        foreach (var file in Directory.GetFiles(root))
        {
            if (!IsRunnable(file)) continue;
            var fileName = Path.GetFileName(file);
            tools.Add(new ModFixTool
            {
                Id = fileName,
                Name = Path.GetFileNameWithoutExtension(fileName),
                Entries = new List<ModFixEntry> { new() { Name = fileName, Path = file } },
                RecompressDefault = true,
            });
        }

        // Top-level folders — resolve (or defer) one or more entries.
        foreach (var dir in Directory.GetDirectories(root))
        {
            var name = Path.GetFileName(dir);
            var candidates = RunnableCandidates(dir);
            var entries = ResolveEntries(dir, candidates)
                .Select(rel => new ModFixEntry { Name = rel, Path = Path.Combine(dir, rel) })
                .ToList();
            tools.Add(new ModFixTool
            {
                Id = name,
                Name = name,
                Entries = entries,
                Candidates = candidates,
                RecompressDefault = true,
            });
        }

        tools.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(tools);
    }

    public Task<ModFixTool> ImportAsync(string name, string sourcePath, bool isFolder, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationException("FIX_TOOL_NAME_REQUIRED");

        var folderName = UniqueFolderName(Sanitize(name));
        var toolDir = Path.Combine(_profilePaths.FixToolsDirectory, folderName);

        // Detect file vs folder from the path itself (the `isFolder` arg is only a hint — a dropped
        // path doesn't say which, and the file picker/folder picker both yield a valid path).
        _ = isFolder;
        if (Directory.Exists(sourcePath))
        {
            CopyDirectory(sourcePath, toolDir);
        }
        else if (File.Exists(sourcePath))
        {
            if (!IsRunnable(sourcePath))
                throw new OperationException("FIX_TOOL_NO_ENTRY");
            Directory.CreateDirectory(toolDir);
            File.Copy(sourcePath, Path.Combine(toolDir, Path.GetFileName(sourcePath)), overwrite: true);
        }
        else
        {
            throw new OperationException("FIX_TOOL_SOURCE_NOT_FOUND", "path", sourcePath);
        }

        _logger.Info($"[ModFixTool] Imported fix tool '{folderName}'", "ModFixToolService");

        var candidates = RunnableCandidates(toolDir);
        var entries = ResolveEntries(toolDir, candidates)
            .Select(rel => new ModFixEntry { Name = rel, Path = Path.Combine(toolDir, rel) })
            .ToList();
        return Task.FromResult(new ModFixTool
        {
            Id = folderName,
            Name = folderName,
            Entries = entries,
            Candidates = candidates,
            RecompressDefault = true,
            Description = description,
        });
    }

    public Task DeleteAsync(string id)
    {
        var path = Path.Combine(_profilePaths.FixToolsDirectory, id);
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) { _logger.Warn($"[ModFixTool] Failed to delete '{id}': {ex.Message}", "ModFixToolService"); }
        return Task.CompletedTask;
    }

    public Task SetEntriesAsync(string id, List<string> relativeEntries)
    {
        var toolDir = Path.Combine(_profilePaths.FixToolsDirectory, id);
        if (!Directory.Exists(toolDir))
            throw new OperationException("FIX_TOOL_NOT_FOUND", "id", id);

        var markerPath = Path.Combine(toolDir, EntryMarker);
        var valid = (relativeEntries ?? new List<string>())
            .Select(e => e?.Trim() ?? string.Empty)
            .Where(e => e.Length > 0 && File.Exists(Path.Combine(toolDir, e)))
            .Distinct()
            .ToList();

        if (valid.Count == 0)
        {
            // Clear the choice → fall back to auto-resolution.
            try { if (File.Exists(markerPath)) File.Delete(markerPath); } catch { }
        }
        else
        {
            File.WriteAllLines(markerPath, valid);
        }
        return Task.CompletedTask;
    }

    // ---- helpers ----

    private static bool IsRunnable(string file)
        => EntryExtPriority.Contains(Path.GetExtension(file).ToLowerInvariant());

    /// <summary>All runnable files inside a tool folder, as relative paths (marker excluded).</summary>
    private static List<string> RunnableCandidates(string toolDir)
        => Directory.GetFiles(toolDir, "*", SearchOption.AllDirectories)
            .Where(IsRunnable)
            .Select(f => Path.GetRelativePath(toolDir, f))
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Resolve a folder tool's entries: explicit marker choices win (one per line); else, if a single
    /// executable can be auto-detected (lone exe beats helper .py), use it; else none (user must pick).
    /// </summary>
    private static List<string> ResolveEntries(string toolDir, List<string> candidates)
    {
        var markerPath = Path.Combine(toolDir, EntryMarker);
        if (File.Exists(markerPath))
        {
            var chosen = File.ReadAllLines(markerPath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && File.Exists(Path.Combine(toolDir, l)))
                .Distinct()
                .ToList();
            if (chosen.Count > 0) return chosen;
        }

        // Auto: take the highest-priority extension present; use it only if exactly one such file.
        foreach (var ext in EntryExtPriority)
        {
            var ofExt = candidates.Where(c => string.Equals(Path.GetExtension(c), ext, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ofExt.Count == 1) return ofExt;
            if (ofExt.Count > 1) return new List<string>();
        }
        return new List<string>();
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "Fix" : clean;
    }

    private string UniqueFolderName(string baseName)
    {
        var root = _profilePaths.FixToolsDirectory;
        var candidate = baseName;
        var i = 2;
        while (Directory.Exists(Path.Combine(root, candidate)) || File.Exists(Path.Combine(root, candidate)))
            candidate = $"{baseName} ({i++})";
        return candidate;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, dest));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, dest), overwrite: true);
    }
}
