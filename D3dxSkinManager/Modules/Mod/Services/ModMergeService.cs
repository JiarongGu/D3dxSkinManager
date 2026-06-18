using System.Text.RegularExpressions;
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
/// archive is produced + imported. The merged <c>.ini</c> is built by <see cref="NamespaceMergeBuilder"/>.
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
            // Namespace-based merge (v2): keep each source .ini intact under its own namespace + gate its
            // overrides by the master's swapvar, so every variant's keybinds/vars/resources are preserved
            // as separate sets. See .claude/rules/3dmigoto-ini-interface.md (namespace merge).
            var nsBase = NamespaceToken(safeName);
            var masterNs = $"{nsBase}\\Master";
            var iniCount = 0;
            for (var group = 0; group < modIds.Count; group++)
            {
                ct.ThrowIfCancellationRequested();
                var id = modIds[group];
                _processRegistry.Report(procId, (int)(group * 70.0 / modIds.Count), $"Staging {group + 1}/{modIds.Count}");

                // Resolve the source's files: prefer its active cache, else extract its archive.
                var srcDir = await ResolveSourceFilesAsync(id, staging, group).ConfigureAwait(false);
                if (srcDir == null) throw new OperationException("MOD_MERGE_SOURCE_MISSING", "id", id);

                // Copy the source's files into content/{group}/ (preserving layout + resources).
                var groupDir = Path.Combine(content, group.ToString());
                CopyDirectory(srcDir, groupDir);

                // Transform each active .ini in place: namespace it + gate its overrides by swapvar. The
                // .ini stays ENABLED — the namespace isolates it; the master coordinates which renders.
                var srcNs = $"{nsBase}\\mod{group}";
                var inis = Directory.GetFiles(groupDir, "*.ini", SearchOption.AllDirectories)
                    .Where(p => !Path.GetFileName(p).Contains("disabled", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var iniPath in inis)
                {
                    var text = await File.ReadAllTextAsync(iniPath, ct).ConfigureAwait(false);
                    var transformed = NamespaceMergeBuilder.TransformSource(text, srcNs, masterNs, group);
                    await File.WriteAllTextAsync(iniPath, transformed, ct).ConfigureAwait(false);
                    iniCount++;
                }
            }

            if (iniCount == 0) throw new OperationException("MOD_MERGE_NO_INI");

            // Write the master .ini at the content root (sorted first alphabetically so it loads early).
            var master = NamespaceMergeBuilder.BuildMaster(masterNs, key.Trim(), modIds.Count, activeOnly);
            await File.WriteAllTextAsync(Path.Combine(content, "!merge_master.ini"), master, ct).ConfigureAwait(false);

            // Compress to a temp archive named after the mod (ImportAsync derives the name from it), then
            // import it as a brand-new mod (own GUID, originals untouched).
            _processRegistry.Report(procId, 75, "Compressing");
            var archivePath = Path.Combine(staging, $"{safeName}.7z");
            await _archiveHelper.CompressFolderAsync(content, archivePath, cancellationToken: ct).ConfigureAwait(false);
            _processRegistry.Report(procId, 90, "Importing");
            var mod = await _import.ImportAsync(archivePath).ConfigureAwait(false);

            _processRegistry.Complete(procId);
            _logger.Info($"[ModMerge] Created '{safeName}' (namespace merge) from {modIds.Count} mods ({iniCount} .ini)");
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

    /// <summary>A namespace-safe token (alphanumeric + underscore) — no spaces/backslashes which are namespace separators.</summary>
    private static string NamespaceToken(string name)
    {
        var token = Regex.Replace(name, "[^A-Za-z0-9_]+", "_").Trim('_');
        return token.Length == 0 ? "Merge" : token;
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
