using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Tunable knobs for the mod-fix runner. Seeded with sensible defaults instead of hard-coding values
/// in the service, so they can later be surfaced as editable (per-profile/global) settings. Nothing
/// here is game-specific — the runner works for any 3DMigoto/XXMI-style manager.
/// </summary>
public class ModFixOptions
{
    /// <summary>Interpreters tried (in order) to run a .py fix script. First one that responds wins.</summary>
    public List<string> PythonCandidates { get; set; } = new() { "py", "python", "python3" };

    /// <summary>Script extensions the runner accepts (lower-case, leading dot).</summary>
    public List<string> SupportedExtensions { get; set; } = new() { ".py", ".exe", ".bat", ".cmd" };

    /// <summary>Per-mod execution timeout. A script exceeding this is killed and the mod marked failed.</summary>
    public TimeSpan PerModTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How many newlines to feed to the script's stdin before closing it. Many community fix scripts
    /// end with an interactive "Press Enter to continue" prompt; auto-feeding lets them run unattended.
    /// </summary>
    public int StdinAutoConfirmLines { get; set; } = 5;

    /// <summary>Max characters of captured output kept per mod (tail) for transport to the UI.</summary>
    public int MaxOutputChars { get; set; } = 4000;
}

/// <summary>
/// Runs a user-supplied "fix" script (typically a 3DMigoto hash-fix .py/.exe) against one or all mods.
/// The script executes with its working directory set to the mod's content folder — the convention these
/// scripts expect. Successful runs are re-compressed back into the mod archive so the fix persists.
/// </summary>
public interface IModFixService
{
    Task<ModFixResult> RunFixAsync(ModFixRequest request, CancellationToken cancellationToken = default);

    /// <summary>Probe the default candidates (py/python/python3) and return the first that responds, or null.</summary>
    string? DetectPython();
}

public class ModFixService : IModFixService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IModQueryService _query;
    private readonly IModArchiveService _archive;
    private readonly IModCacheService _cacheService;
    private readonly IModOperationQueue _operationQueue;
    private readonly IModRepository _modRepository;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;
    private readonly IProcessRegistry _processRegistry;
    private readonly IProfileContext _profileContext;
    private readonly IProfileService _profileService;
    // Effective runner options. Defaults until a run refreshes them from the profile config (RunFixAsync).
    private ModFixOptions _options;

    public ModFixService(
        IProfilePathService profilePaths,
        IModQueryService query,
        IModArchiveService archive,
        IModCacheService cacheService,
        IModOperationQueue operationQueue,
        IModRepository modRepository,
        IProfileEventBus eventBus,
        ILogHelper logger,
        IProcessRegistry processRegistry,
        IProfileContext profileContext,
        IProfileService profileService,
        ModFixOptions? options = null)
    {
        _profilePaths = profilePaths;
        _query = query;
        _archive = archive;
        _cacheService = cacheService;
        _operationQueue = operationQueue;
        _modRepository = modRepository;
        _eventBus = eventBus;
        _logger = logger;
        _processRegistry = processRegistry;
        _profileContext = profileContext;
        _profileService = profileService;
        _options = options ?? new ModFixOptions();
    }

    public string? DetectPython() => ResolvePythonInterpreter(new ModFixOptions().PythonCandidates);

    /// <summary>Build effective runner options from the profile's FixTools config (falls back to defaults).</summary>
    private async Task<ModFixOptions> BuildEffectiveOptionsAsync()
    {
        var options = new ModFixOptions();
        try
        {
            var config = await _profileService.GetProfileConfigurationAsync(_profileContext.ProfileId).ConfigureAwait(false);
            var fix = config?.FixTools;
            if (fix != null)
            {
                // Explicit interpreter (if set) is tried first, then the defaults as fallback.
                if (!string.IsNullOrWhiteSpace(fix.PythonPath))
                    options.PythonCandidates = new List<string> { fix.PythonPath }.Concat(options.PythonCandidates).ToList();
                if (fix.SupportedExtensions is { Count: > 0 })
                    options.SupportedExtensions = fix.SupportedExtensions
                        .Select(e => e.Trim().ToLowerInvariant())
                        .Where(e => e.Length > 0)
                        .Select(e => e.StartsWith('.') ? e : "." + e)
                        .ToList();
                options.PerModTimeout = TimeSpan.FromMinutes(Math.Max(1, fix.TimeoutMinutes));
                options.StdinAutoConfirmLines = fix.AutoConfirm ? 5 : 0;
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"[ModFix] Failed to load FixTools config, using defaults: {ex.Message}");
        }
        return options;
    }

    public async Task<ModFixResult> RunFixAsync(ModFixRequest request, CancellationToken cancellationToken = default)
    {
        // Refresh effective options from the profile config at the start of each run (UI triggers one
        // fix operation at a time, so this single-assignment is safe).
        _options = await BuildEffectiveOptionsAsync().ConfigureAwait(false);

        // 1. Validate the script up-front so the user gets an immediate, clear error.
        if (string.IsNullOrWhiteSpace(request.ScriptPath) || !File.Exists(request.ScriptPath))
        {
            throw new OperationException("FIX_SCRIPT_NOT_FOUND", "path", request.ScriptPath ?? "");
        }

        var ext = Path.GetExtension(request.ScriptPath).ToLowerInvariant();
        if (!_options.SupportedExtensions.Contains(ext))
        {
            throw new OperationException("FIX_SCRIPT_UNSUPPORTED", "ext", ext);
        }

        // 2. Resolve python interpreter once (only if needed) — fail fast before starting the run.
        string? pythonPath = null;
        if (ext == ".py")
        {
            pythonPath = ResolvePythonInterpreter();
            if (pythonPath == null)
            {
                throw new OperationException("FIX_PYTHON_NOT_FOUND");
            }
        }

        // 3. Resolve the target mod set (id → display name).
        var allMods = await _query.FilterAsync().ConfigureAwait(false);
        var nameById = allMods.ToDictionary(m => m.Id, m => m.Name);

        List<(string id, string name)> targets;
        if (request.ModIds is { Count: > 0 })
        {
            targets = request.ModIds
                .Distinct()
                .Select(id => (id, nameById.TryGetValue(id, out var n) ? n : id))
                .ToList();
        }
        else
        {
            targets = allMods.Select(m => (m.Id, m.Name)).ToList();
        }

        if (targets.Count == 0)
        {
            throw new OperationException("FIX_NO_MODS");
        }

        var scriptName = Path.GetFileName(request.ScriptPath);
        var result = new ModFixResult { Total = targets.Count };

        var procId = _processRegistry.Start(
            ProcessType.ModFix,
            targets.Count == 1 ? $"Fixing mod: {targets[0].name}" : $"Fixing {targets.Count} mods ({scriptName})",
            cancellable: true,
            titleKey: targets.Count == 1 ? "process.fixMod" : "process.fixMods",
            titleArg: targets.Count == 1 ? targets[0].name : targets.Count.ToString());

        // Combine the caller's token with the registry's cancel token (Activity-panel Cancel).
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _processRegistry.GetToken(procId));
        var ct = linked.Token;

        try
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    break;
                }

                var (id, name) = targets[i];

                _processRegistry.Report(procId, (int)((i) * 100.0 / targets.Count), $"{name}");
                await _eventBus.EmitAsync(
                    ModuleNames.TOOL,
                    ToolEvents.MOD_FIX_PROGRESS,
                    new { current = i + 1, total = targets.Count, modId = id, modName = name }).ConfigureAwait(false);

                // Serialize per-mod so a concurrent load/unload/delete can't race the script's file I/O.
                var itemResult = await _operationQueue.EnqueueAsync(id, () =>
                    RunOneAsync(id, name, request, ext, pythonPath, ct)).ConfigureAwait(false);

                result.Results.Add(itemResult);
                if (itemResult.Skipped) result.Skipped++;
                else if (itemResult.Success) result.Succeeded++;
                else result.Failed++;
            }

            _processRegistry.Report(procId, 100);
            _processRegistry.Complete(procId);
            _logger.Info($"[ModFix] Run complete: {result.Succeeded} fixed, {result.Failed} failed, {result.Skipped} skipped, cancelled={result.Cancelled}");
            return result;
        }
        catch (Exception ex)
        {
            _processRegistry.Fail(procId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Fix a single mod: stage its content (live cache in-place, or extract a non-loaded mod to a temp
    /// dir), run the script there, and re-compress into the archive on success.
    /// </summary>
    private async Task<ModFixItemResult> RunOneAsync(
        string id, string name, ModFixRequest request, string ext, string? pythonPath, CancellationToken ct)
    {
        var item = new ModFixItemResult { ModId = id, ModName = name };

        // A retained cache — active {id} OR disabled DISABLED-{id} — IS the working copy the next
        // load deploys (EnableCacheAsync just renames it back). Fix it in place so the cache and the
        // archive stay in sync: fixing only a temp extract left a disabled cache stale, and the fix
        // "didn't apply" when that cache was re-enabled (user report 2026-07-05).
        var cachePath = _cacheService.GetCachePath(id);

        string workDir;
        var isTemp = false;
        if (cachePath != null)
        {
            workDir = cachePath;
        }
        else
        {
            // Not extracted: stage the archive into the profile's own temp dir, fix, recompress, discard.
            workDir = Path.Combine(_profilePaths.TempDirectory, $"fix-{id}-{Guid.NewGuid():N}");
            var extraction = await _archive.ExtractAsync(id, workDir).ConfigureAwait(false);
            if (!extraction.Success)
            {
                item.Skipped = true;
                item.Error = "No content to fix (archive missing or empty).";
                _logger.Warn($"[ModFix] Skipped '{name}' ({id}): extraction failed");
                TryDeleteDir(workDir);
                return item;
            }
            isTemp = true;
        }

        // Snapshot the content before the fix so we can persist only what actually changed (most fix
        // tools rewrite a few .ini and leave the big textures untouched).
        var before = SnapshotDir(workDir);

        try
        {
            var (exitCode, output) = await ExecuteScriptAsync(request.ScriptPath, ext, pythonPath, workDir, ct).ConfigureAwait(false);
            item.ExitCode = exitCode;
            item.Output = Tail(output);
            item.Success = exitCode == 0;

            if (!item.Success)
            {
                item.Error = $"Fix script exited with code {exitCode}.";
                _logger.Warn($"[ModFix] '{name}' ({id}) fix exited {exitCode}");
                return item;
            }

            _logger.Info($"[ModFix] '{name}' ({id}) fixed (exit 0)");

            // Persist the mutated content back into the archive so it survives the next reload.
            if (request.RecompressAfter)
            {
                await PersistFixAsync(id, name, workDir, before).ConfigureAwait(false);
            }

            // Stamp the last-fixed time on the mod (metadata.fix.lastFixedUtc) so the "may need re-fix"
            // watermark (ProfileConfiguration.GameUpdatedUtc) can flag mods fixed before a game update.
            // Best-effort — a stamp failure must never fail the fix itself.
            await StampFixedAsync(id, name).ConfigureAwait(false);

            return item;
        }
        catch (OperationCanceledException)
        {
            item.Skipped = true;
            item.Error = "Cancelled.";
            return item;
        }
        catch (Exception ex)
        {
            item.Success = false;
            item.Error = ex.Message;
            _logger.Error($"[ModFix] '{name}' ({id}) failed: {ex.Message}");
            return item;
        }
        finally
        {
            if (isTemp) TryDeleteDir(workDir);
        }
    }

    /// <summary>
    /// If the changed files are at least this fraction of the mod's total bytes, a full recompress is
    /// worth it; below it, patching the few changed files individually is the big win (textures are the
    /// bulk and a fix usually only rewrites small .ini, so changed bytes stay well under this).
    /// </summary>
    private const double FullRecompressByteFraction = 0.5;

    /// <summary>
    /// Files up to this size are content-hashed in the snapshot. Fix scripts sometimes rewrite a file
    /// preserving its size and timestamp (temp-write + copystat style), which a length+mtime diff
    /// misses entirely — the fix then silently never persisted. Hashing covers exactly the files fix
    /// tools touch (.ini/config, a few KB); the bulk textures stay on the cheap length+mtime check.
    /// </summary>
    private const long HashSizeLimit = 4 * 1024 * 1024;

    /// <summary>Snapshot every file under <paramref name="dir"/> → forward-slash relpath ⇒ (length, lastWriteUtc, hash for small files).</summary>
    private static Dictionary<string, (long Length, DateTime WriteUtc, string? Hash)> SnapshotDir(string dir)
    {
        var map = new Dictionary<string, (long, DateTime, string?)>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dir)) return map;
        foreach (var path in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(path);
            var rel = Path.GetRelativePath(dir, path).Replace('\\', '/');
            string? hash = null;
            if (info.Length <= HashSizeLimit)
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    hash = Convert.ToHexString(SHA256.HashData(stream));
                }
                catch (IOException) { /* locked/unreadable — fall back to length+mtime for this file */ }
            }
            map[rel] = (info.Length, info.LastWriteTimeUtc, hash);
        }
        return map;
    }

    /// <summary>
    /// Persist a successful fix back into the archive the cheap way: patch only the files the script
    /// changed/added via the fast single-file path. Fall back to a full recompress only when a file was
    /// deleted (append can't remove entries) or too many files changed. A no-op fix touches nothing.
    /// </summary>
    private async Task PersistFixAsync(
        string id, string name, string workDir, Dictionary<string, (long Length, DateTime WriteUtc, string? Hash)> before)
    {
        var after = SnapshotDir(workDir);
        var deleted = before.Keys.Where(k => !after.ContainsKey(k)).ToList();
        var changed = after
            .Where(kv => !before.TryGetValue(kv.Key, out var b)
                || b.Length != kv.Value.Length
                || b.WriteUtc != kv.Value.WriteUtc
                || b.Hash != kv.Value.Hash) // hashed small files: catches same-size+mtime rewrites
            .Select(kv => kv.Key)
            .ToList();

        if (changed.Count == 0 && deleted.Count == 0)
        {
            _logger.Info($"[ModFix] '{name}' ({id}) changed no files — archive left untouched");
            return;
        }

        // Deletions (append can't remove entries) or a large changed-byte fraction → full recompress.
        var totalBytes = after.Values.Sum(v => v.Length);
        var changedBytes = changed.Sum(rel => after[rel].Length);
        var bigChange = totalBytes > 0 && changedBytes >= totalBytes * FullRecompressByteFraction;
        if (deleted.Count > 0 || bigChange)
        {
            var reason = deleted.Count > 0
                ? $"{deleted.Count} file(s) deleted"
                : $"{changedBytes}/{totalBytes} bytes changed (>= {FullRecompressByteFraction:P0})";
            _logger.Info($"[ModFix] '{name}' ({id}) full recompress ({reason})");
            if (!await _archive.CompressCacheToArchiveAsync(id, workDir).ConfigureAwait(false))
                _logger.Warn($"[ModFix] '{name}' ({id}) fixed but archive recompress failed");
            return;
        }

        // Fast path: patch just the changed/added files individually.
        foreach (var rel in changed)
        {
            var full = Path.Combine(workDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!await _archive.UpdateFileInArchiveAsync(id, full, rel).ConfigureAwait(false))
            {
                // A single-file patch failed — fall back to a full recompress to stay consistent.
                _logger.Warn($"[ModFix] '{name}' ({id}) single-file patch failed for '{rel}', falling back to full recompress");
                if (!await _archive.CompressCacheToArchiveAsync(id, workDir).ConfigureAwait(false))
                    _logger.Warn($"[ModFix] '{name}' ({id}) fixed but archive recompress failed");
                return;
            }
        }
        _logger.Info($"[ModFix] '{name}' ({id}) patched {changed.Count} file(s) individually (no full recompress)");
    }

    /// <summary>Record the successful fix time in the mod's Metadata JSON (metadata.fix.lastFixedUtc).
    /// Best-effort: a failure here is logged, never thrown (the fix already succeeded).</summary>
    private async Task StampFixedAsync(string id, string name)
    {
        try
        {
            var entity = await _modRepository.GetByIdAsync(id).ConfigureAwait(false);
            if (entity == null) return;
            // Single-column Metadata write — a whole-row UpdateAsync here would clobber a concurrent
            // category/tag edit (ModMetadataService.UpdateAsync isn't on the per-mod queue lock).
            await _modRepository.UpdateMetadataAsync(id, WriteFixMetadata(entity.Metadata, DateTime.UtcNow)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warn($"[ModFix] '{name}' ({id}) fixed but stamping last-fixed time failed: {ex.Message}");
        }
    }

    /// <summary>Merge <c>fix.lastFixedUtc</c> into a Metadata JSON string, preserving other fields
    /// (e.g. the remote identity). Mirrors RemoteImportService.WriteRemoteMetadata.</summary>
    public static string WriteFixMetadata(string? metadata, DateTime lastFixedUtc)
        => Core.Helpers.MetadataJsonHelper.MergeKey(metadata, "fix",
            new global::System.Text.Json.Nodes.JsonObject { ["lastFixedUtc"] = lastFixedUtc.ToString("O") });

    /// <summary>Launch the script with cwd=workDir, auto-confirm stdin prompts, capture output, enforce timeout.</summary>
    private async Task<(int exitCode, string output)> ExecuteScriptAsync(
        string scriptPath, string ext, string? pythonPath, string workDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        switch (ext)
        {
            case ".py":
                psi.FileName = pythonPath!;
                psi.ArgumentList.Add(scriptPath);
                break;
            case ".bat":
            case ".cmd":
                psi.FileName = "cmd.exe";
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(scriptPath);
                break;
            default: // .exe
                psi.FileName = scriptPath;
                break;
        }

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // Read both streams concurrently to avoid pipe-buffer deadlock.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        // Auto-confirm interactive "Press Enter" prompts, then close stdin.
        try
        {
            for (var i = 0; i < _options.StdinAutoConfirmLines; i++)
                await proc.StandardInput.WriteLineAsync().ConfigureAwait(false);
            proc.StandardInput.Close();
        }
        catch { /* script may not read stdin */ }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.PerModTimeout);

        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            if (ct.IsCancellationRequested) throw; // user cancel — propagate
            // otherwise it was a timeout
            var partial = (await SafeAwait(stdoutTask)) + (await SafeAwait(stderrTask));
            return (-1, partial + $"\n[fix timed out after {_options.PerModTimeout.TotalMinutes:0} min]");
        }

        var output = (await SafeAwait(stdoutTask)) + (await SafeAwait(stderrTask));
        return (proc.ExitCode, output);
    }

    private static async Task<string> SafeAwait(Task<string> t)
    {
        try { return await t.ConfigureAwait(false); } catch { return ""; }
    }

    /// <summary>Find the first working Python interpreter from the seeded candidate list (probes `--version`).</summary>
    private string? ResolvePythonInterpreter(List<string>? candidates = null)
    {
        foreach (var candidate in candidates ?? _options.PythonCandidates)
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                p.Start();
                if (p.WaitForExit(3000) && p.ExitCode == 0)
                    return candidate;
                try { if (!p.HasExited) p.Kill(); } catch { }
            }
            catch { /* not on PATH — try next */ }
        }
        return null;
    }

    private string Tail(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.Trim();
        return s.Length <= _options.MaxOutputChars ? s : "…" + s[^_options.MaxOutputChars..];
    }

    private void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) { _logger.Warn($"[ModFix] Failed to delete temp dir {dir}: {ex.Message}"); }
    }
}
