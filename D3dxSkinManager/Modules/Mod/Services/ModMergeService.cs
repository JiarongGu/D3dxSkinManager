using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Combines several mods of one slot into a single new mod that cycles between them with one key
/// (GIMI-style). Non-destructive: the source mods are left untouched in the library; a brand-new mod
/// archive is produced + imported. The merged <c>.ini</c> is built by <see cref="MergeIniBuilder"/>.
/// </summary>
public interface IModMergeService
{
    /// <summary>
    /// Merge <paramref name="modIds"/> (order = swap order; index 0 starts active) into a new mod named
    /// <paramref name="name"/>, cycled by <paramref name="key"/>. Returns the created mod.
    /// </summary>
    Task<ModInfo?> MergeAsync(IReadOnlyList<string> modIds, string name, string key, bool activeOnly = true, CancellationToken ct = default);
}

public class ModMergeService : IModMergeService
{
    private readonly IProfilePathService _paths;
    private readonly IModArchiveService _archive;
    private readonly IModImportService _import;
    private readonly IArchiveHelper _archiveHelper;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;

    public ModMergeService(
        IProfilePathService paths,
        IModArchiveService archive,
        IModImportService import,
        IArchiveHelper archiveHelper,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        _paths = paths;
        _archive = archive;
        _import = import;
        _archiveHelper = archiveHelper;
        _processRegistry = processRegistry;
        _logger = logger;
    }

    public async Task<ModInfo?> MergeAsync(IReadOnlyList<string> modIds, string name, string key, bool activeOnly = true, CancellationToken ct = default)
    {
        if (modIds == null || modIds.Count < 2)
            throw new OperationException("MOD_MERGE_NEED_TWO");
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length != 1)
            throw new OperationException("MOD_MERGE_KEY_INVALID", "key", key ?? "");

        var safeName = SanitizeFileName(string.IsNullOrWhiteSpace(name) ? "Merged" : name.Trim());
        var staging = Path.Combine(_paths.TempDirectory, $"merge-{Guid.NewGuid():N}");
        var content = Path.Combine(staging, "content");
        Directory.CreateDirectory(content);

        var procId = _processRegistry.Start(ProcessType.ModImport, $"Merging {modIds.Count} mods → {safeName}");
        try
        {
            var sources = new List<MergeSourceIni>();
            for (var group = 0; group < modIds.Count; group++)
            {
                ct.ThrowIfCancellationRequested();
                var id = modIds[group];

                // Resolve the source's files: prefer its active cache, else extract its archive.
                var srcDir = await ResolveSourceFilesAsync(id, staging, group).ConfigureAwait(false);
                if (srcDir == null) throw new OperationException("MOD_MERGE_SOURCE_MISSING", "id", id);

                // Copy the source's files into content/{group}/ (preserving layout) so the merged
                // archive carries every variant's resources.
                var groupDir = Path.Combine(content, group.ToString());
                CopyDirectory(srcDir, groupDir);

                // Collect each .ini (skip already-disabled), then disable it in the copy so only the
                // merged .ini is active in-game.
                var inis = Directory.GetFiles(groupDir, "*.ini", SearchOption.AllDirectories)
                    .Where(p => !Path.GetFileName(p).Contains("disabled", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var iniPath in inis)
                {
                    var text = await File.ReadAllTextAsync(iniPath, ct).ConfigureAwait(false);
                    // PathPrefix = the .ini's dir relative to content/, forward-slashed, trailing slash.
                    var relDir = Path.GetRelativePath(content, Path.GetDirectoryName(iniPath)!).Replace('\\', '/');
                    var prefix = relDir == "." ? string.Empty : relDir + "/";
                    sources.Add(new MergeSourceIni { Group = group, IniText = text, PathPrefix = prefix });

                    var disabled = Path.Combine(Path.GetDirectoryName(iniPath)!, "DISABLED" + Path.GetFileName(iniPath));
                    File.Move(iniPath, disabled, overwrite: true);
                }
            }

            if (sources.Count == 0) throw new OperationException("MOD_MERGE_NO_INI");

            // Build + write the merged master .ini at the content root.
            var mergedIni = MergeIniBuilder.Build(sources, key.Trim(), activeOnly);
            await File.WriteAllTextAsync(Path.Combine(content, "merged.ini"), mergedIni, ct).ConfigureAwait(false);

            // Compress to a temp archive named after the mod (ImportAsync derives the name from it), then
            // import it as a brand-new mod (own GUID, originals untouched).
            var archivePath = Path.Combine(staging, $"{safeName}.7z");
            await _archiveHelper.CompressFolderAsync(content, archivePath, cancellationToken: ct).ConfigureAwait(false);
            var mod = await _import.ImportAsync(archivePath).ConfigureAwait(false);

            _processRegistry.Complete(procId);
            _logger.Info($"[ModMerge] Created '{safeName}' from {modIds.Count} mods ({sources.Count} .ini)");
            return mod;
        }
        catch (Exception ex)
        {
            _processRegistry.Fail(procId, ex.Message);
            throw;
        }
        finally
        {
            TryDeleteDir(staging);
        }
    }

    /// <summary>Active cache dir if present; otherwise extract the archive into the staging area. Null if neither.</summary>
    private async Task<string?> ResolveSourceFilesAsync(string id, string staging, int group)
    {
        var activeCache = Path.Combine(_paths.CacheModsDirectory, id);
        if (Directory.Exists(activeCache)) return activeCache;
        var disabledCache = Path.Combine(_paths.CacheModsDirectory, $"DISABLED-{id}");
        if (Directory.Exists(disabledCache)) return disabledCache;

        var extractDir = Path.Combine(staging, $"src-{group}");
        var result = await _archive.ExtractAsync(id, extractDir).ConfigureAwait(false);
        return result.Success ? extractDir : null;
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, dest));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, dest), overwrite: true);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "Merged" : cleaned;
    }

    private void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) { _logger.Warn($"[ModMerge] Failed to clean staging {dir}: {ex.Message}"); }
    }
}
