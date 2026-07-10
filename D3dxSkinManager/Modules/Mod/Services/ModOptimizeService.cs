using System.Security.Cryptography;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Optimizes a mod's content by deduplicating byte-identical asset files (the user ask 2026-07-05:
/// "mod optimization (dedup asset files)"). Merged/multi-variant mods often carry the same texture
/// several times; every `filename =` reference to a redundant copy is rewritten to the canonical
/// one and the copies are removed, then the archive is fully recompressed (append can't remove
/// entries — see filesystem-operation-serialization.md).
/// </summary>
public interface IModOptimizeService
{
    /// <summary>Read-only duplicate scan of the mod's extracted cache (active or disabled).</summary>
    Task<ModOptimizeScanResult> ScanAsync(string id);

    /// <summary>Apply the optimization: dedup (rewrite refs, delete redundant copies), optionally
    /// normalize unsafe file names, then recompress if anything changed. Registry-tracked.</summary>
    Task<ModOptimizeResult> ApplyAsync(string id, bool normalizeNames = false);
}

public class ModOptimizeService : IModOptimizeService
{
    private readonly IModCacheService _cacheService;
    private readonly IModArchiveService _archiveService;
    private readonly IModOperationQueue _operationQueue;
    private readonly IProfileEventBus _eventBus;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;

    // filename = <path> [; inline comment] — value resolved relative to the .ini's OWN directory.
    private static readonly Regex FilenameLine = new(
        @"^(\s*filename\s*=\s*)([^;；]+?)(\s*(?:[;；].*)?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ModOptimizeService(
        IModCacheService cacheService,
        IModArchiveService archiveService,
        IModOperationQueue operationQueue,
        IProfileEventBus eventBus,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        _cacheService = cacheService;
        _archiveService = archiveService;
        _operationQueue = operationQueue;
        _eventBus = eventBus;
        _processRegistry = processRegistry;
        _logger = logger;
    }

    public Task<ModOptimizeScanResult> ScanAsync(string id)
    {
        // Read-only, but still under the per-mod lock so a concurrent fix/load can't mutate mid-hash.
        return _operationQueue.EnqueueAsync(id, async () =>
        {
            var cacheDir = ResolveCacheDirOrThrow(id);
            var scan = await BuildScanAsync(cacheDir).ConfigureAwait(false);
            scan.Normalizable = BuildNormalizable(cacheDir);
            return scan;
        });
    }

    public Task<ModOptimizeResult> ApplyAsync(string id, bool normalizeNames = false)
    {
        return _operationQueue.EnqueueAsync(id, async () =>
        {
            var cacheDir = ResolveCacheDirOrThrow(id);
            var procId = _processRegistry.Start(ProcessType.Optimize, $"Optimizing mod: {id}",
                titleKey: "process.optimize", titleArg: id);
            try
            {
                var scan = await BuildScanAsync(cacheDir).ConfigureAwait(false);
                if (normalizeNames) scan.Normalizable = BuildNormalizable(cacheDir);
                var result = new ModOptimizeResult();

                if (scan.Groups.Count == 0 && scan.Normalizable.Count == 0)
                {
                    _processRegistry.Complete(procId);
                    return result; // nothing to do — archive untouched
                }

                // 1. Rewrite every `filename =` reference to a redundant copy → the canonical copy.
                _processRegistry.Report(procId, 20, "Rewriting references", detailKey: "process.stage.rewritingRefs");
                var removableToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var group in scan.Groups)
                {
                    var canonicalFull = Path.GetFullPath(Path.Combine(cacheDir, group.Canonical.Replace('/', Path.DirectorySeparatorChar)));
                    foreach (var dup in group.Duplicates)
                        removableToCanonical[Path.GetFullPath(Path.Combine(cacheDir, dup.Replace('/', Path.DirectorySeparatorChar)))] = canonicalFull;
                }

                foreach (var iniFile in Directory.EnumerateFiles(cacheDir, "*.ini", SearchOption.AllDirectories))
                {
                    result.RewrittenRefs += await RewriteReferencesAsync(iniFile, removableToCanonical).ConfigureAwait(false);
                }

                // 2. Remove the redundant copies — only if no reference to them remains anywhere.
                _processRegistry.Report(procId, 50, "Removing duplicates", detailKey: "process.stage.removingDuplicates");
                var remainingRefs = CollectReferencedFullPaths(cacheDir);
                foreach (var (removable, _) in removableToCanonical)
                {
                    if (remainingRefs.Contains(removable))
                    {
                        _logger.Warn($"[ModOptimize] '{removable}' still referenced after rewrite — kept", "ModOptimizeService");
                        continue;
                    }
                    var info = new FileInfo(removable);
                    if (!info.Exists) continue;
                    var size = info.Length;
                    File.Delete(removable); // cache path, but safe: per-mod queue lock held for the whole op
                    result.RemovedFiles++;
                    result.FreedBytes += size;
                }

                // 2b. Normalize unsafe file names (reuses the ref-rewrite machinery): rename referenced
                // assets whose names have non-ASCII/symbol chars → ASCII-safe, rewrite the refs. Runs
                // AFTER dedup so removed copies aren't renamed. Re-scan (names may have shifted).
                if (normalizeNames)
                {
                    _processRegistry.Report(procId, 60, "Normalizing names", detailKey: "process.stage.normalizingNames");
                    var renameMap = BuildRenameMap(cacheDir); // oldFull → newFull (same dir, safe basename)
                    if (renameMap.Count > 0)
                    {
                        foreach (var iniFile in Directory.EnumerateFiles(cacheDir, "*.ini", SearchOption.AllDirectories))
                            result.RewrittenRefs += await RewriteReferencesAsync(iniFile, renameMap).ConfigureAwait(false);
                        foreach (var (oldFull, newFull) in renameMap)
                        {
                            if (!File.Exists(oldFull) || File.Exists(newFull)) continue;
                            File.Move(oldFull, newFull); // cache path, safe under the per-mod lock
                            result.RenamedFiles++;
                        }
                    }
                }

                // 3. Files were DELETED or RENAMED → full recompress (append can't remove/rename entries).
                if (result.RemovedFiles > 0 || result.RenamedFiles > 0)
                {
                    _processRegistry.Report(procId, 80, "Compressing", detailKey: "process.stage.compressing");
                    if (!await _archiveService.CompressCacheToArchiveAsync(id, cacheDir).ConfigureAwait(false))
                    {
                        throw new OperationException(
                            Core.Constants.ErrorCodes.MOD_ARCHIVE_UPDATE_FAILED,
                            new Dictionary<string, string> { { "id", id } });
                    }
                }

                _processRegistry.Complete(procId);
                _logger.Info($"[ModOptimize] '{id}': removed {result.RemovedFiles} duplicate(s), renamed {result.RenamedFiles}, rewrote {result.RewrittenRefs} ref(s), freed {result.FreedBytes} bytes", "ModOptimizeService");

                // Sizes changed — refresh the mod list (REFRESHED → MOD_LIST_UPDATED).
                await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.REFRESHED).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                _processRegistry.Fail(procId, ex is OperationException op ? op.Code : ex.Message);
                throw;
            }
        });
    }

    private string ResolveCacheDirOrThrow(string id)
    {
        var cacheDir = _cacheService.GetCachePath(id);
        if (cacheDir == null)
        {
            throw new OperationException(
                "MOD_OPTIMIZE_NO_CACHE",
                new Dictionary<string, string> { { "id", id } },
                "The mod must be loaded or have a cache to optimize.");
        }
        return cacheDir;
    }

    /// <summary>
    /// Hash every non-.ini file and group byte-identical ones. `.ini` files are NEVER deduplicated —
    /// sections/namespaces load per-file, so two identical `.ini`s are not interchangeable.
    /// </summary>
    private static async Task<ModOptimizeScanResult> BuildScanAsync(string cacheDir)
    {
        var result = new ModOptimizeScanResult();
        var byHash = new Dictionary<string, List<(string Rel, long Size)>>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(cacheDir, "*", SearchOption.AllDirectories))
        {
            result.TotalFiles++;
            if (Path.GetExtension(path).Equals(".ini", StringComparison.OrdinalIgnoreCase)) continue;

            var info = new FileInfo(path);
            string hash;
            await using (var stream = File.OpenRead(path))
            {
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream).ConfigureAwait(false));
            }
            var key = $"{hash}:{info.Length}";
            if (!byHash.TryGetValue(key, out var list)) byHash[key] = list = new();
            list.Add((Path.GetRelativePath(cacheDir, path).Replace('\\', '/'), info.Length));
        }

        foreach (var list in byHash.Values.Where(l => l.Count > 1))
        {
            // Canonical = shortest relpath, then ordinal — deterministic and biased to the "main" copy.
            var ordered = list.OrderBy(f => f.Rel.Length).ThenBy(f => f.Rel, StringComparer.OrdinalIgnoreCase).ToList();
            var group = new ModDuplicateGroup
            {
                SizeBytes = ordered[0].Size,
                Canonical = ordered[0].Rel,
                Duplicates = ordered.Skip(1).Select(f => f.Rel).ToList(),
            };
            result.Groups.Add(group);
            result.WastedBytes += group.SizeBytes * group.Duplicates.Count;
        }

        result.Groups = result.Groups.OrderByDescending(g => g.SizeBytes * g.Duplicates.Count).ToList();
        return result;
    }

    /// <summary>
    /// Rewrite `filename =` lines whose target resolves to a removable copy so they point at the
    /// canonical copy instead. Paths are relative to the .ini's OWN directory; the rewritten value
    /// keeps the original separator style. Returns the number of rewritten lines.
    /// </summary>
    private static async Task<int> RewriteReferencesAsync(string iniFile, Dictionary<string, string> removableToCanonical)
    {
        var iniDir = Path.GetDirectoryName(iniFile)!;
        var lines = await File.ReadAllLinesAsync(iniFile).ConfigureAwait(false);
        var rewritten = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var m = FilenameLine.Match(lines[i]);
            if (!m.Success) continue;

            var value = m.Groups[2].Value.Trim();
            if (value.Length == 0) continue;

            string targetFull;
            try { targetFull = Path.GetFullPath(Path.Combine(iniDir, value.Replace('/', Path.DirectorySeparatorChar))); }
            catch { continue; } // malformed path — leave the line alone

            if (!removableToCanonical.TryGetValue(targetFull, out var canonicalFull)) continue;

            var newRel = Path.GetRelativePath(iniDir, canonicalFull);
            // Keep the original separator style so the diff stays minimal.
            newRel = value.Contains('/') ? newRel.Replace('\\', '/') : newRel.Replace('/', '\\');
            lines[i] = m.Groups[1].Value + newRel + m.Groups[3].Value;
            rewritten++;
        }

        if (rewritten > 0)
            await File.WriteAllLinesAsync(iniFile, lines).ConfigureAwait(false);
        return rewritten;
    }

    // ---- filename normalization -----------------------------------------------------------------

    /// <summary>Chars kept verbatim in a normalized file name. Everything else (CJK, symbols,
    /// control) is replaced — so ordinary ASCII names (incl. spaces/parens) are left untouched;
    /// only non-ASCII/symbol names get rewritten. Deliberately conservative to minimize churn.</summary>
    private static bool IsSafeChar(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')
          or '.' or '_' or '-' or ' ' or '(' or ')';

    private static string NormalizeToken(string s)
    {
        var sb = new global::System.Text.StringBuilder(s.Length);
        foreach (var c in s) sb.Append(IsSafeChar(c) ? c : '_');
        return Regex.Replace(sb.ToString(), "_{2,}", "_").Trim('_', ' ', '.');
    }

    /// <summary>ASCII-safe file name (stem + ext normalized separately). Empty stem → "asset".</summary>
    private static string SafeFileName(string name)
    {
        var ext = Path.GetExtension(name);
        var stem = NormalizeToken(Path.GetFileNameWithoutExtension(name));
        if (stem.Length == 0) stem = "asset";
        var safeExt = ext.Length > 1 ? "." + NormalizeToken(ext[1..]) : ext;
        return stem + safeExt;
    }

    /// <summary>Referenced, existing, non-.ini asset files whose name is NOT already ASCII-safe →
    /// their (collision-free) normalized target. Returns oldFull → newFull (same directory).</summary>
    private static List<(string OldFull, string NewFull)> ComputeRenames(string cacheDir)
    {
        var renames = new List<(string, string)>();
        var takenPerDir = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var full in CollectReferencedFullPaths(cacheDir).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(full)) continue;
            if (Path.GetExtension(full).Equals(".ini", StringComparison.OrdinalIgnoreCase)) continue;
            var name = Path.GetFileName(full);
            var safe = SafeFileName(name);
            if (string.Equals(safe, name, StringComparison.Ordinal)) continue; // already safe

            var dir = Path.GetDirectoryName(full)!;
            if (!takenPerDir.TryGetValue(dir, out var taken))
            {
                taken = new HashSet<string>(
                    Directory.EnumerateFiles(dir).Select(p => Path.GetFileName(p)!), StringComparer.OrdinalIgnoreCase);
                takenPerDir[dir] = taken;
            }
            var unique = MakeUnique(safe, taken);
            taken.Add(unique);
            renames.Add((full, Path.Combine(dir, unique)));
        }
        return renames;
    }

    private static string MakeUnique(string name, HashSet<string> taken)
    {
        if (!taken.Contains(name)) return name;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var i = 1; ; i++)
        {
            var candidate = $"{stem}_{i}{ext}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    private static List<ModNameFix> BuildNormalizable(string cacheDir) =>
        ComputeRenames(cacheDir).Select(r => new ModNameFix
        {
            From = Path.GetRelativePath(cacheDir, r.OldFull).Replace('\\', '/'),
            To = Path.GetRelativePath(cacheDir, r.NewFull).Replace('\\', '/'),
        }).ToList();

    private static Dictionary<string, string> BuildRenameMap(string cacheDir)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (oldFull, newFull) in ComputeRenames(cacheDir)) map[oldFull] = newFull;
        return map;
    }

    /// <summary>Every full path currently referenced by a `filename =` line in any .ini of the mod.</summary>
    private static HashSet<string> CollectReferencedFullPaths(string cacheDir)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var iniFile in Directory.EnumerateFiles(cacheDir, "*.ini", SearchOption.AllDirectories))
        {
            var iniDir = Path.GetDirectoryName(iniFile)!;
            foreach (var line in File.ReadLines(iniFile))
            {
                var m = FilenameLine.Match(line);
                if (!m.Success) continue;
                var value = m.Groups[2].Value.Trim();
                if (value.Length == 0) continue;
                try { refs.Add(Path.GetFullPath(Path.Combine(iniDir, value.Replace('/', Path.DirectorySeparatorChar)))); }
                catch { /* malformed — ignore */ }
            }
        }
        return refs;
    }
}
