using System.Text.Json.Nodes;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
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

    /// <summary>Identity keys ("sourceId|listId|entryId") + legacy detail URLs of every mod imported
    /// from a remote source — cached (rebuilt on TTL/import) so INDEX_QUERY doesn't rescan all mods.</summary>
    Task<(HashSet<string> Keys, HashSet<string> LegacyUrls)> GetImportedLookupAsync();
}

public class RemoteImportService : IRemoteImportService
{
    /// <summary>How many detail-page images become mod previews (first = thumbnail).</summary>
    private const int MaxPreviewImages = 3;

    /// <summary>Imported-lookup cache TTL — bounds staleness after out-of-band changes (mod deletes).</summary>
    private static readonly TimeSpan ImportedCacheTtl = TimeSpan.FromSeconds(30);

    private readonly ICloudreveShareResolver _cloudreve;
    private readonly IDownloadService _download;
    private readonly IModImportService _import;
    private readonly IModRepository _repository;
    private readonly IImageService _imageService;
    private readonly IRemoteLibraryStore _libraries;
    private readonly IProfilePathService _profilePaths;
    private readonly IProcessRegistry _processRegistry;
    private readonly IArchiveHelper _archiveHelper;
    private readonly ILogHelper _logger;

    private (HashSet<string> Keys, HashSet<string> LegacyUrls)? _importedCache;
    private DateTime _importedCacheAtUtc;
    private readonly object _importedCacheLock = new();

    public RemoteImportService(
        ICloudreveShareResolver cloudreve,
        IDownloadService download,
        IModImportService import,
        IModRepository repository,
        IImageService imageService,
        IRemoteLibraryStore libraries,
        IProfilePathService profilePaths,
        IProcessRegistry processRegistry,
        IArchiveHelper archiveHelper,
        ILogHelper logger)
    {
        _cloudreve = cloudreve;
        _download = download;
        _import = import;
        _repository = repository;
        _imageService = imageService;
        _libraries = libraries;
        _profilePaths = profilePaths;
        _processRegistry = processRegistry;
        _archiveHelper = archiveHelper;
        _logger = logger;
    }

    /// <summary>
    /// Download-method dispatch — each resolver `type` in an adapter maps to a strategy here.
    /// "cloudreve" = share-API resolve; "direct" = the URL IS the file (covers simple sites with
    /// zero code); "external"/unknown = browser-only. New methods (other pan APIs, auth'd hosts)
    /// slot in as new cases + adapter resolver types.
    /// </summary>
    public Task<RemoteResolveResult> ResolveAsync(RemoteDownloadOption option, CancellationToken ct = default)
    {
        switch (option.Type.ToLowerInvariant())
        {
            case "cloudreve":
                return _cloudreve.ResolveAsync(option.Url, ct);
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
            try
            {
                Directory.CreateDirectory(staging);

                _processRegistry.Report(procId, 2, "Resolving download", detailKey: "process.stage.resolving");
                var resolved = await ResolveAsync(option, ct).ConfigureAwait(false);

                var archivePath = Path.Combine(staging, Path.GetFileName(resolved.FileName));
                var progress = new Progress<DownloadProgress>(p =>
                {
                    // Map download bytes onto the 5–60% band of the overall process.
                    var pct = p.Percent.HasValue ? 5 + (int)(p.Percent.Value * 0.55) : (int?)null;
                    _processRegistry.Report(procId, pct,
                        $"Downloading {FormatBytes(p.BytesReceived)}{(p.TotalBytes.HasValue ? " / " + FormatBytes(p.TotalBytes.Value) : "")}");
                });
                var downloaded = await _download.DownloadAsync(
                    new DownloadRequest { Url = resolved.DownloadUrl, DestinationPath = archivePath },
                    progress, ct).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                // ALWAYS normalize: extract + recompress into OUR storage format. A verbatim copy of
                // the download would keep the site's password/odd container in the archive store and
                // fail at load time. Most archives need NO password — extract plain first, and only
                // when THAT fails with a password error retry with the user's password (or the
                // resolver's site default).
                _processRegistry.Report(procId, 62, "Extracting archive", detailKey: "process.stage.extracting");
                var extractDir = Path.Combine(staging, "extract");
                var unzipPassword = string.IsNullOrWhiteSpace(password) ? option.UnzipPassword : password;
                try
                {
                    await _archiveHelper.ExtractArchiveAsync(archivePath, extractDir).ConfigureAwait(false);
                }
                catch (Exception ex) when (ArchiveHelper.IsPasswordError(ex))
                {
                    if (string.IsNullOrWhiteSpace(unzipPassword))
                        throw new OperationException("REMOTE_ARCHIVE_PASSWORD", "name", resolved.FileName);
                    TryDeleteDir(extractDir); // drop any partial output from the plain attempt
                    try
                    {
                        await _archiveHelper.ExtractArchiveAsync(archivePath, extractDir, unzipPassword).ConfigureAwait(false);
                    }
                    catch (Exception ex2) when (ArchiveHelper.IsPasswordError(ex2))
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
                    entity.Metadata = WriteRemoteMetadata(entity.Metadata, sourceId, listId, entryId, detail.DetailUrl, downloaded.Sha256);
                    await _repository.UpdateAsync(entity).ConfigureAwait(false);
                }
                InvalidateImportedCache();

                _processRegistry.Report(procId, 90, "Importing previews", detailKey: "process.stage.previews");
                await ImportPreviewImagesAsync(mod.Id, detail.Images, staging, ct).ConfigureAwait(false);

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

    public async Task<(HashSet<string> Keys, HashSet<string> LegacyUrls)> GetImportedLookupAsync()
    {
        lock (_importedCacheLock)
        {
            if (_importedCache != null && DateTime.UtcNow - _importedCacheAtUtc < ImportedCacheTtl)
                return _importedCache.Value;
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in await _repository.GetAllAsync().ConfigureAwait(false))
        {
            var remote = ReadRemote(entity.Metadata);
            if (remote == null) continue;
            if (!string.IsNullOrEmpty(remote.Value.Key)) keys.Add(remote.Value.Key!);
            if (!string.IsNullOrEmpty(remote.Value.DetailUrl)) urls.Add(remote.Value.DetailUrl!);
        }

        var result = (keys, urls);
        lock (_importedCacheLock)
        {
            _importedCache = result;
            _importedCacheAtUtc = DateTime.UtcNow;
        }
        return result;
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
        JsonObject obj;
        try
        {
            obj = JsonNode.Parse(string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata)
                as JsonObject ?? new JsonObject();
        }
        catch { obj = new JsonObject(); }
        obj["remote"] = new JsonObject
        {
            ["sourceId"] = sourceId,
            ["listId"] = listId,
            ["entryId"] = entryId,
            ["detailUrl"] = detailUrl,
            ["sha256"] = sha256,
            ["importedAtUtc"] = DateTime.UtcNow.ToString("O"),
        };
        return obj.ToJsonString();
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

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.#} {units[unit]}";
    }

    private void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) { _logger.Warn($"[Remote] Failed to clean staging {dir}: {ex.Message}", "RemoteImportService"); }
    }
}
