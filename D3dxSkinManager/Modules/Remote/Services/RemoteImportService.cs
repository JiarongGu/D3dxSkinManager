using System.Text.Json;
using System.Text.Json.Nodes;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Downloads a remote mod and imports it into the current profile — the write side of the remote
/// library. The whole flow is ONE cancellable ProcessRegistry entry (resolve → download with byte
/// progress → import → name + previews), kicked off fire-and-forget from the facade (see
/// background-task-tracking.md). Staging lives in {profile}/temp (same volume as the archive store,
/// per use-project-paths.md) and is cleaned in a finally.
/// </summary>
public interface IRemoteImportService
{
    /// <summary>Resolve a download option to file name/size (for the confirm UI). Importable types only.</summary>
    Task<RemoteResolveResult> ResolveAsync(RemoteDownloadOption option, CancellationToken ct = default);

    /// <summary>Start the background download+import. Returns the process id immediately.
    /// <paramref name="listId"/>/<paramref name="entryId"/> record the STANDARDIZED remote identity;
    /// <paramref name="tags"/> feed the library's ordered tag→category rules; a non-null
    /// <paramref name="categoryId"/> is the user's explicit download-time choice and OVERRIDES the rules;
    /// <paramref name="password"/> is a user-entered unzip password (overrides the resolver's site default).</summary>
    string StartDownloadImport(string sourceId, string? listId, string? entryId, List<string>? tags,
        RemoteModDetail detail, RemoteDownloadOption option, string? categoryId = null, string? password = null);

    /// <summary>Maps every mod imported from a remote source to its local mod id(s), for the INDEX_QUERY
    /// join (imported flag + "locate"): standardized identity key ("sourceId|listId|entryId") → mod ids,
    /// and legacy detailUrl → mod ids. LISTS because an entry can be downloaded multiple times. Cached
    /// (rebuilt on TTL / on import / on mod delete) so it doesn't rescan all mods per query.</summary>
    Task<(Dictionary<string, List<string>> KeyToModIds, Dictionary<string, List<string>> UrlToModIds)> GetImportedLookupAsync();

    /// <summary>The entry-ids of this source+list already imported (durable-key matches only) — drives the
    /// "downloaded only" filter. Empty = none.</summary>
    Task<IReadOnlyCollection<string>> GetImportedEntryIdsAsync(string sourceId, string? listId);

    /// <summary>Flag + attach local mod id(s) on each index entry this profile already imported (durable
    /// identity key first, legacy detailUrl fallback). Mutates the passed entries in place.</summary>
    Task AnnotateImportedAsync(IEnumerable<RemoteIndexEntry> entries, string sourceId, string? listId);

    /// <summary>Imported state for ONE entry (durable key first, then detailUrl) — powers the detail
    /// screen's live "already imported" banner.</summary>
    Task<(bool Imported, List<string> LocalModIds)> GetImportedStateAsync(
        string sourceId, string? listId, string? entryId, string? detailUrl);
}

public class RemoteImportService : IRemoteImportService
{
    /// <summary>How many detail-page images become mod previews (first = thumbnail).</summary>
    private const int MaxPreviewImages = 3;

    /// <summary>Imported-lookup cache TTL — bounds staleness after out-of-band changes (mod deletes).</summary>
    private static readonly TimeSpan ImportedCacheTtl = TimeSpan.FromSeconds(30);

    private readonly ICloudreveShareResolver _cloudreve;
    private readonly IQuarkShareResolver _quark;
    private readonly IMegaShareResolver _mega;
    private readonly IKodboxShareResolver _kodbox;
    private readonly IDownloadService _download;
    private readonly IModImportService _import;
    private readonly IModRepository _repository;
    private readonly IImageService _imageService;
    private readonly IRemoteLibraryStore _libraries;
    private readonly IProfilePathService _profilePaths;
    private readonly IProcessRegistry _processRegistry;
    private readonly IArchiveHelper _archiveHelper;
    private readonly ILogHelper _logger;

    // Maps a remote entry to the LOCAL mod id(s) imported from it — a list because the same entry can be
    // downloaded more than once (N local mods). Cached; rebuilt from the mod repo on TTL / invalidation.
    private (Dictionary<string, List<string>> KeyToModIds, Dictionary<string, List<string>> UrlToModIds)? _importedCache;
    private DateTime _importedCacheAtUtc;
    private readonly object _importedCacheLock = new();

    public RemoteImportService(
        ICloudreveShareResolver cloudreve,
        IQuarkShareResolver quark,
        IMegaShareResolver mega,
        IKodboxShareResolver kodbox,
        IDownloadService download,
        IModImportService import,
        IModRepository repository,
        IImageService imageService,
        IRemoteLibraryStore libraries,
        IProfilePathService profilePaths,
        IProcessRegistry processRegistry,
        IArchiveHelper archiveHelper,
        IEventBus events,
        ILogHelper logger)
    {
        _cloudreve = cloudreve;
        _quark = quark;
        _mega = mega;
        _kodbox = kodbox;
        _download = download;
        _import = import;
        _repository = repository;
        _imageService = imageService;
        _libraries = libraries;
        _profilePaths = profilePaths;
        _processRegistry = processRegistry;
        _archiveHelper = archiveHelper;
        _logger = logger;

        // Deleting a mod removes its remote-identity metadata → the imported lookup must drop it, so the
        // remote list/detail stops flagging that entry as imported. Invalidate on mod delete/refresh
        // (singleton service → subscribe once; the rebuild is lazy on the next INDEX_QUERY).
        events.Subscribe(ModuleNames.MOD, ModEvents.DELETED, _ => { InvalidateImportedCache(); return Task.CompletedTask; });
        events.Subscribe(ModuleNames.MOD, ModEvents.REFRESHED, _ => { InvalidateImportedCache(); return Task.CompletedTask; });
    }

    /// <summary>
    /// Download-method dispatch — each resolver `type` in an adapter maps to a strategy here.
    /// "cloudreve" = share-API resolve (anonymous); "kodbox" = kodbox share-API resolve (anonymous, the
    /// IP/VPN Hui盘 mirror); "quark" = Quark share-API resolve using the saved online-storage account
    /// cookie; "mega" = client-side-decrypted folder TREE; "direct" = the URL IS the file (covers simple
    /// sites with zero code); "external"/unknown = browser-only. New methods (other pan APIs, auth'd hosts)
    /// slot in as new cases + adapter resolver types.
    /// </summary>
    public Task<RemoteResolveResult> ResolveAsync(RemoteDownloadOption option, CancellationToken ct = default)
    {
        switch (option.Type.ToLowerInvariant())
        {
            case "cloudreve":
                return _cloudreve.ResolveAsync(option.Url, ct);
            case "quark":
                return _quark.ResolveAsync(option.Url, ct);
            case "mega":
                return _mega.ResolveAsync(option.Url, ct);
            case "kodbox":
                return _kodbox.ResolveAsync(option.Url, ct);
            case "direct":
                // The URL basename is often just an id (e.g. gamebanana.com/dl/123 → "123", no
                // extension → import can't tell the archive type). Prefer the option Name when it
                // looks like a real filename (GameBanana puts _sFile "mod_1.0.7z" there).
                var urlName = Path.GetFileName(new Uri(option.Url).LocalPath);
                var looksLikeFile = option.Name.Contains('.') && !option.Name.Contains(' ');
                var name = looksLikeFile ? option.Name
                    : (Path.HasExtension(urlName) ? urlName : (string.IsNullOrWhiteSpace(urlName) ? "download" : urlName));
                return Task.FromResult(new RemoteResolveResult
                {
                    FileName = name,
                    Size = 0, // unknown until the download's Content-Length
                    DownloadUrl = option.Url,
                });
            default:
                throw new OperationException("REMOTE_DOWNLOAD_UNSUPPORTED", "host", option.Name);
        }
    }

    private static bool IsImportable(string type) =>
        string.Equals(type, "cloudreve", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "quark", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "mega", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "kodbox", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "direct", StringComparison.OrdinalIgnoreCase);

    public string StartDownloadImport(string sourceId, string? listId, string? entryId, List<string>? tags,
        RemoteModDetail detail, RemoteDownloadOption option, string? categoryId = null, string? password = null)
    {
        if (!IsImportable(option.Type))
            throw new OperationException("REMOTE_DOWNLOAD_UNSUPPORTED", "host", option.Name);

        var title = string.IsNullOrWhiteSpace(detail.Title) ? option.Url : detail.Title.Trim();
        var procId = _processRegistry.Start(ProcessType.Download, $"Downloading mod: {title}",
            cancellable: true, titleKey: "process.remoteImport", titleArg: title);

        _ = Task.Run(async () => // fire-and-forget — progress + result flow via the registry
        {
            var ct = _processRegistry.GetToken(procId);
            var staging = Path.Combine(_profilePaths.TempDirectory, $"remote-{Guid.NewGuid():N}");
            List<string>? quarkSavedFids = null; // set when Quark saved a copy to the drive — cleaned up in finally
            try
            {
                Directory.CreateDirectory(staging);

                // Produce `extractDir` (the mod's files, ready to recompress). MEGA is a folder TREE, not one
                // archive → download+decrypt every file into it and skip extract; other hosts resolve to one
                // archive URL → download → extract into it. `contentSha` = the re-download dedup hash.
                var extractDir = Path.Combine(staging, "extract");
                string? contentSha = null;
                string? archivePath = null;  // the single raw archive (non-MEGA); deleted after import
                RemoteResolveResult resolved;

                if (string.Equals(option.Type, "mega", StringComparison.OrdinalIgnoreCase))
                {
                    // MEGA folder share = the mod's file TREE (not one archive). Download every file (AES-CTR
                    // decrypt) into extractDir preserving paths — the reconstructed folder IS the extracted mod.
                    _processRegistry.Report(procId, 2, "Resolving MEGA folder", detailKey: "process.stage.resolving");
                    resolved = await _mega.ResolveAsync(option.Url, ct).ConfigureAwait(false);
                    await DownloadMegaTreeAsync(option.Url, extractDir, procId, ct).ConfigureAwait(false);
                }
                else
                {
                    // Resolve to a downloadable URL. Quark has no direct download: it SAVES the share file into
                    // the user's own drive (转存), downloads from there, and deletes the copy after (finally).
                    Dictionary<string, string>? downloadHeaders;
                    if (string.Equals(option.Type, "quark", StringComparison.OrdinalIgnoreCase))
                    {
                        _processRegistry.Report(procId, 2, "Saving to drive", detailKey: "process.stage.quarkSaving");
                        var prepared = await _quark.PrepareDownloadAsync(option.Url, ct).ConfigureAwait(false);
                        resolved = new RemoteResolveResult { FileName = prepared.FileName, Size = prepared.Size, DownloadUrl = prepared.DownloadUrl };
                        downloadHeaders = prepared.Headers;
                        quarkSavedFids = prepared.SavedFids.ToList();
                    }
                    else
                    {
                        _processRegistry.Report(procId, 2, "Resolving download", detailKey: "process.stage.resolving");
                        resolved = await ResolveAsync(option, ct).ConfigureAwait(false);
                        downloadHeaders = resolved.DownloadHeaders;
                    }

                    // The raw archive lands in the managed downloads folder ({data}/downloads, self-cleaning
                    // after 7 days), NOT profile temp. A guid prefix avoids collisions (many sites name "1.mp4").
                    Directory.CreateDirectory(_download.ManagedDirectory);
                    archivePath = Path.Combine(_download.ManagedDirectory,
                        $"{Guid.NewGuid():N}-{Path.GetFileName(resolved.FileName)}");
                    var progress = new Progress<DownloadProgress>(p =>
                    {
                        // Map download bytes onto the 5–60% band of the overall process.
                        var pct = p.Percent.HasValue ? 5 + (int)(p.Percent.Value * 0.55) : (int?)null;
                        _processRegistry.Report(procId, pct,
                            $"Downloading {FileUtilities.FormatBytes(p.BytesReceived)}{(p.TotalBytes.HasValue ? " / " + FileUtilities.FormatBytes(p.TotalBytes.Value) : "")}");
                    });
                    var downloaded = await _download.DownloadAsync(
                        new DownloadRequest
                        {
                            Url = resolved.DownloadUrl,
                            DestinationPath = archivePath,
                            Headers = downloadHeaders, // auth'd hosts (Quark) need the cookie + UA on the CDN GET
                        },
                        progress, ct).ConfigureAwait(false);
                    contentSha = downloaded.Sha256;

                    ct.ThrowIfCancellationRequested();

                    // Bytes are on disk — the Quark drive copy is no longer needed; delete it now (also
                    // covered by the finally, but freeing it early keeps the user's drive clean).
                    if (quarkSavedFids is { Count: > 0 })
                    {
                        await _quark.CleanupAsync(quarkSavedFids, CancellationToken.None).ConfigureAwait(false);
                        quarkSavedFids = null;
                    }

                    // Normalize into OUR storage format: extract + recompress (a verbatim copy would keep the
                    // site's odd container/password and fail at load). The resolver's config picks the extract
                    // WORKFLOW: opted-in hosts (huihui Quark) run the RECURSIVE UNWRAP (carve a disguised
                    // polyglot + unwrap nested layers, password per layer); everyone else a plain extract.
                    _processRegistry.Report(procId, 62, "Extracting archive", detailKey: "process.stage.extracting");
                    var unzipPassword = string.IsNullOrWhiteSpace(password) ? option.UnzipPassword : password;
                    try
                    {
                        if (option.UnwrapNested)
                        {
                            await _archiveHelper.ExtractArchiveRecursiveAsync(archivePath, extractDir, unzipPassword).ConfigureAwait(false);
                        }
                        else
                        {
                            try
                            {
                                await _archiveHelper.ExtractArchiveAsync(archivePath, extractDir).ConfigureAwait(false);
                            }
                            catch (Exception ex) when (ArchiveHelper.IsPasswordError(ex) && !string.IsNullOrWhiteSpace(unzipPassword))
                            {
                                TryDeleteDir(extractDir);
                                await _archiveHelper.ExtractArchiveAsync(archivePath, extractDir, unzipPassword).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception ex) when (ArchiveHelper.IsPasswordError(ex))
                    {
                        throw new OperationException("REMOTE_ARCHIVE_PASSWORD", "name", resolved.FileName);
                    }
                }
                ct.ThrowIfCancellationRequested();

                _processRegistry.Report(procId, 68, "Repacking", detailKey: "process.stage.repacking");
                var normalized = Path.Combine(staging, "normalized",
                    Path.GetFileNameWithoutExtension(resolved.FileName) + ".7z");
                await _archiveHelper.CompressFolderAsync(extractDir, normalized,
                    progressCallback: p => _processRegistry.Report(procId, 68 + p * 12 / 100, null),
                    cancellationToken: ct).ConfigureAwait(false);

                // MEGA has no single raw archive to hash → hash the normalized .7z for re-download dedup.
                contentSha ??= await Sha256FileAsync(normalized, ct).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();
                _processRegistry.Report(procId, 82, "Importing", detailKey: "process.stage.importing");
                var mod = await _import.ImportAsync(normalized).ConfigureAwait(false)
                          ?? throw new OperationException("REMOTE_IMPORT_FAILED", "reason", "import returned no mod");

                // The archive file name is a hash-ish blob — use the site title as the mod name, and
                // record the remote identity (source, entry, detail URL, content sha256) in Metadata
                // so the library can flag already-imported entries + dedupe re-downloads.
                var entity = await _repository.GetByIdAsync(mod.Id).ConfigureAwait(false);
                if (entity != null)
                {
                    if (!string.IsNullOrWhiteSpace(detail.Title)) entity.Name = detail.Title.Trim();
                    // Local category: the user's download-time choice wins; otherwise the library's
                    // ORDERED rules (tags all-match + optional title regex; first match wins; no
                    // match = uncategorized) — remote-library-redesign.md.
                    var allTags = (tags ?? new List<string>()).Concat(detail.Tags)
                        .Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var category = !string.IsNullOrWhiteSpace(categoryId)
                        ? categoryId
                        : ResolveCategory(sourceId, listId, allTags, detail.Title);
                    if (!string.IsNullOrWhiteSpace(category)) entity.Category = category;
                    // NOTE: we deliberately do NOT copy the remote entry's tags onto the local mod — a
                    // remote "tag" is usually just the character/category name, which the resolved category
                    // already carries, so mirroring it onto the mod's tags is noise (user request 2026-07-13).
                    // The tags still drive category resolution above; the library link (below) marks origin.
                    entity.Metadata = WriteRemoteMetadata(entity.Metadata, sourceId, listId, entryId, detail.DetailUrl, contentSha ?? string.Empty);
                    // FK to the library this mod came from (the library entity owns the display name).
                    entity.RemoteLibraryId = _libraries.FindBySourceList(sourceId, listId)?.Id;
                    await _repository.UpdateAsync(entity).ConfigureAwait(false);
                }
                InvalidateImportedCache();

                _processRegistry.Report(procId, 90, "Importing previews", detailKey: "process.stage.previews");
                await ImportPreviewImagesAsync(mod.Id, detail.Images, staging, ct).ConfigureAwait(false);

                // The raw download was normalized + imported — delete it now instead of leaving it
                // for the 7-day managed sweep. Failed/cancelled runs keep theirs (re-download saver);
                // those leftovers are visible in the cleanup tool's Downloads category. (MEGA streams
                // straight into staging — no managed-download file to delete.)
                if (archivePath != null) TryDeleteFile(archivePath);

                _processRegistry.Complete(procId);
                _logger.Info($"[Remote] Imported '{detail.Title}' as {mod.Id}", "RemoteImportService");
            }
            catch (OperationCanceledException)
            {
                _processRegistry.Cancel(procId);
            }
            catch (Exception ex)
            {
                _logger.Error($"[Remote] Download+import failed for '{title}': {ex.Message}", "RemoteImportService", ex);
                _processRegistry.Fail(procId, ex.Message);
            }
            finally
            {
                // Quark saved a copy to the user's drive but the download/import didn't reach the
                // early cleanup (cancel/fail) — delete it now so nothing is left behind.
                if (quarkSavedFids is { Count: > 0 })
                    await _quark.CleanupAsync(quarkSavedFids, CancellationToken.None).ConfigureAwait(false);
                TryDeleteDir(staging);
            }
        });

        return procId;
    }

    /// <summary>First matching rule (in order) wins; no library / no match = null.</summary>
    private string? ResolveCategory(string sourceId, string? listId, List<string> tags, string title)
    {
        if (string.IsNullOrWhiteSpace(listId)) return null;
        var library = _libraries.FindBySourceList(sourceId, listId);
        return library == null ? null : MatchTagRules(library.TagRules, tags, title);
    }

    /// <summary>The ORDERED rule evaluation (remote-library-redesign.md): a rule matches when its tags
    /// ALL match (case-insensitive, when any are set) AND its title regex matches (when set) — at
    /// least one criterion required. First match wins; no match = null (uncategorized). Title regex is
    /// the lever for tagless sites (huihui has no tag taxonomy).</summary>
    public static string? MatchTagRules(IEnumerable<RemoteTagRule> rules, IReadOnlyCollection<string> tags, string? title = null)
    {
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.CategoryId)) continue;
            var hasTags = rule.Tags.Count > 0;
            var hasPattern = !string.IsNullOrWhiteSpace(rule.TitlePattern);
            if (!hasTags && !hasPattern) continue; // criterionless rule — never matches

            if (hasTags && !rule.Tags.All(rt => tags.Contains(rt, StringComparer.OrdinalIgnoreCase)))
                continue;
            if (hasPattern)
            {
                try
                {
                    if (!global::System.Text.RegularExpressions.Regex.IsMatch(
                            title ?? string.Empty, rule.TitlePattern!,
                            global::System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                            global::System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                            TimeSpan.FromSeconds(1)))
                        continue;
                }
                catch { continue; } // bad user regex / timeout — the rule just doesn't match
            }
            return rule.CategoryId;
        }
        return null;
    }

    public async Task<(Dictionary<string, List<string>> KeyToModIds, Dictionary<string, List<string>> UrlToModIds)> GetImportedLookupAsync()
    {
        lock (_importedCacheLock)
        {
            if (_importedCache != null && DateTime.UtcNow - _importedCacheAtUtc < ImportedCacheTtl)
                return _importedCache.Value;
        }

        var keyToMods = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var urlToMods = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        static void Add(Dictionary<string, List<string>> map, string key, string modId)
        {
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<string>();
            if (!list.Contains(modId)) list.Add(modId);
        }
        foreach (var entity in await _repository.GetAllAsync().ConfigureAwait(false))
        {
            var remote = ReadRemote(entity.Metadata);
            if (remote == null) continue;
            if (!string.IsNullOrEmpty(remote.Value.Key)) Add(keyToMods, remote.Value.Key!, entity.Id);
            if (!string.IsNullOrEmpty(remote.Value.DetailUrl)) Add(urlToMods, remote.Value.DetailUrl!, entity.Id);
        }

        var result = (keyToMods, urlToMods);
        lock (_importedCacheLock)
        {
            _importedCache = result;
            _importedCacheAtUtc = DateTime.UtcNow;
        }
        return result;
    }

    public async Task<IReadOnlyCollection<string>> GetImportedEntryIdsAsync(string sourceId, string? listId)
    {
        var (keyToMods, _) = await GetImportedLookupAsync().ConfigureAwait(false);
        var prefix = ImportedKey(sourceId, listId ?? string.Empty, string.Empty); // "src|list|"
        return keyToMods.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[prefix.Length..])
            .ToList();
    }

    public async Task AnnotateImportedAsync(IEnumerable<RemoteIndexEntry> entries, string sourceId, string? listId)
    {
        var (keyToMods, urlToMods) = await GetImportedLookupAsync().ConfigureAwait(false);
        if (keyToMods.Count == 0 && urlToMods.Count == 0) return;

        foreach (var entry in entries)
        {
            if (keyToMods.TryGetValue(ImportedKey(sourceId, listId ?? string.Empty, entry.Id), out var modIds)
                || urlToMods.TryGetValue(entry.DetailUrl, out modIds))
            {
                entry.Imported = true;
                entry.LocalModIds = modIds;
            }
        }
    }

    public async Task<(bool Imported, List<string> LocalModIds)> GetImportedStateAsync(
        string sourceId, string? listId, string? entryId, string? detailUrl)
    {
        var (keyToMods, urlToMods) = await GetImportedLookupAsync().ConfigureAwait(false);
        List<string>? modIds = null;
        if (!string.IsNullOrEmpty(entryId))
            keyToMods.TryGetValue(ImportedKey(sourceId, listId ?? string.Empty, entryId), out modIds);
        if (modIds == null && !string.IsNullOrEmpty(detailUrl))
            urlToMods.TryGetValue(detailUrl, out modIds);
        return (modIds is { Count: > 0 }, modIds ?? new List<string>());
    }

    private void InvalidateImportedCache()
    {
        lock (_importedCacheLock) { _importedCache = null; }
    }

    public static string ImportedKey(string sourceId, string listId, string entryId) =>
        $"{sourceId}|{listId}|{entryId}";

    /// <summary>Merge the STANDARDIZED remote identity into a Metadata JSON string (other fields
    /// preserved): sourceId+listId+entryId is the durable key (detailUrl breaks when a site moves
    /// hosts); detailUrl kept as a convenience link; sha256 for re-download dedup.</summary>
    public static string WriteRemoteMetadata(string? metadata, string sourceId, string? listId, string? entryId, string detailUrl, string sha256)
    {
        return Core.Helpers.MetadataJsonHelper.MergeKey(metadata, "remote", new JsonObject
        {
            ["sourceId"] = sourceId,
            ["listId"] = listId,
            ["entryId"] = entryId,
            ["detailUrl"] = detailUrl,
            ["sha256"] = sha256,
            ["importedAtUtc"] = DateTime.UtcNow.ToString("O"),
        });
    }

    /// <summary>The identity key (when the import recorded one) + detailUrl from a Metadata JSON.</summary>
    public static (string? Key, string? DetailUrl)? ReadRemote(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return null;
        try
        {
            var remote = (JsonNode.Parse(metadata) as JsonObject)?["remote"] as JsonObject;
            if (remote == null) return null;
            var sourceId = remote["sourceId"]?.GetValue<string>();
            var listId = remote["listId"]?.GetValue<string>();
            var entryId = remote["entryId"]?.GetValue<string>();
            var key = !string.IsNullOrEmpty(sourceId) && !string.IsNullOrEmpty(listId) && !string.IsNullOrEmpty(entryId)
                ? ImportedKey(sourceId!, listId!, entryId!)
                : null;
            return (key, remote["detailUrl"]?.GetValue<string>());
        }
        catch { return null; }
    }

    /// <summary>Legacy helper kept for tests/back-compat reads.</summary>
    public static string? ReadRemoteDetailUrl(string? metadata) => ReadRemote(metadata)?.DetailUrl;

    /// <summary>Download up to N detail-page images and attach them as previews (best-effort).</summary>
    private async Task ImportPreviewImagesAsync(string modId, IReadOnlyList<string> images, string staging, CancellationToken ct)
    {
        var count = 0;
        foreach (var url in images)
        {
            if (count >= MaxPreviewImages) break;
            ct.ThrowIfCancellationRequested();
            try
            {
                var ext = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
                var local = Path.Combine(staging, $"preview-{count}{ext}");
                await _download.DownloadAsync(new DownloadRequest { Url = url, DestinationPath = local }, null, ct)
                    .ConfigureAwait(false);
                if (await _imageService.ImportPreviewImageAsync(modId, local).ConfigureAwait(false)) count++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.Warn($"[Remote] Preview image skipped ({url}): {ex.Message}", "RemoteImportService");
            }
        }
    }

    /// <summary>Download + AES-CTR-decrypt every file of a MEGA folder share into <paramref name="extractDir"/>,
    /// preserving relative paths — the reconstructed folder IS the extracted mod (no archive to extract).
    /// Each file streams to an encrypted temp then decrypts into place; progress rides the 5–60% band.</summary>
    private async Task DownloadMegaTreeAsync(string shareUrl, string extractDir, string procId, CancellationToken ct)
    {
        var files = await _mega.PrepareDownloadAsync(shareUrl, ct).ConfigureAwait(false);
        Directory.CreateDirectory(_download.ManagedDirectory);
        var root = Path.GetFullPath(extractDir);
        var total = files.Sum(f => Math.Max(0, f.Size));
        long done = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            // Contain the target under extractDir (segments are already sanitized; double-check the join).
            var outPath = Path.GetFullPath(Path.Combine(extractDir, file.RelativePath));
            if (!outPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            var encPath = Path.Combine(_download.ManagedDirectory, $"{Guid.NewGuid():N}.megaenc");
            try
            {
                var soFar = done;
                await _download.DownloadAsync(
                    new DownloadRequest { Url = file.DownloadUrl!, DestinationPath = encPath },
                    new Progress<DownloadProgress>(p =>
                    {
                        var overall = soFar + p.BytesReceived;
                        var pct = total > 0 ? 5 + (int)(overall * 55.0 / total) : (int?)null;
                        _processRegistry.Report(procId, pct,
                            $"Downloading {FileUtilities.FormatBytes(overall)} / {FileUtilities.FormatBytes(total)}");
                    }), ct).ConfigureAwait(false);

                await using var input = File.OpenRead(encPath);
                await using var output = File.Create(outPath);
                await MegaCrypto.DecryptCtrAsync(input, output, file.AesKey, file.Nonce, ct).ConfigureAwait(false);
            }
            finally { TryDeleteFile(encPath); }
            done += Math.Max(0, file.Size);
        }
        if (!Directory.Exists(extractDir) || !Directory.EnumerateFileSystemEntries(extractDir).Any())
            throw new OperationException("MEGA_EMPTY_SHARE", "url", shareUrl);
    }

    private static async Task<string> Sha256FileAsync(string path, CancellationToken ct)
    {
        await using var s = File.OpenRead(path);
        using var sha = global::System.Security.Cryptography.SHA256.Create();
        var hash = await sha.ComputeHashAsync(s, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) { _logger.Warn($"[Remote] Failed to clean staging {dir}: {ex.Message}", "RemoteImportService"); }
    }

    private void TryDeleteFile(string file)
    {
        try { if (File.Exists(file)) File.Delete(file); }
        catch (Exception ex) { _logger.Warn($"[Remote] Failed to delete download {file}: {ex.Message}", "RemoteImportService"); }
    }
}
