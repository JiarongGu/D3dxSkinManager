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
    /// <summary>Resolve a download option to file name/size (for the confirm UI). Cloudreve only.</summary>
    Task<RemoteResolveResult> ResolveAsync(RemoteDownloadOption option, CancellationToken ct = default);

    /// <summary>Start the background download+import. Returns the process id immediately.</summary>
    string StartDownloadImport(string sourceId, RemoteModDetail detail, RemoteDownloadOption option);

    /// <summary>Detail URLs of every mod in this profile that was imported from a remote source
    /// (from the `remote` block each import records in the mod's Metadata JSON).</summary>
    Task<HashSet<string>> GetImportedDetailUrlsAsync();
}

public class RemoteImportService : IRemoteImportService
{
    /// <summary>How many detail-page images become mod previews (first = thumbnail).</summary>
    private const int MaxPreviewImages = 3;

    private readonly ICloudreveShareResolver _cloudreve;
    private readonly IDownloadService _download;
    private readonly IModImportService _import;
    private readonly IModRepository _repository;
    private readonly IImageService _imageService;
    private readonly IProfilePathService _profilePaths;
    private readonly IProcessRegistry _processRegistry;
    private readonly ILogHelper _logger;

    public RemoteImportService(
        ICloudreveShareResolver cloudreve,
        IDownloadService download,
        IModImportService import,
        IModRepository repository,
        IImageService imageService,
        IProfilePathService profilePaths,
        IProcessRegistry processRegistry,
        ILogHelper logger)
    {
        _cloudreve = cloudreve;
        _download = download;
        _import = import;
        _repository = repository;
        _imageService = imageService;
        _profilePaths = profilePaths;
        _processRegistry = processRegistry;
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
                var name = Path.GetFileName(new Uri(option.Url).LocalPath);
                return Task.FromResult(new RemoteResolveResult
                {
                    FileName = string.IsNullOrWhiteSpace(name) ? "download" : name,
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

    public string StartDownloadImport(string sourceId, RemoteModDetail detail, RemoteDownloadOption option)
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
                    // Map download bytes onto the 5–80% band of the overall process.
                    var pct = p.Percent.HasValue ? 5 + (int)(p.Percent.Value * 0.75) : (int?)null;
                    _processRegistry.Report(procId, pct,
                        $"Downloading {FormatBytes(p.BytesReceived)}{(p.TotalBytes.HasValue ? " / " + FormatBytes(p.TotalBytes.Value) : "")}");
                });
                var downloaded = await _download.DownloadAsync(
                    new DownloadRequest { Url = resolved.DownloadUrl, DestinationPath = archivePath },
                    progress, ct).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();
                _processRegistry.Report(procId, 82, "Importing", detailKey: "process.stage.importing");
                var mod = await _import.ImportAsync(archivePath).ConfigureAwait(false)
                          ?? throw new OperationException("REMOTE_IMPORT_FAILED", "reason", "import returned no mod");

                // The archive file name is a hash-ish blob — use the site title as the mod name, and
                // record the remote identity (source, entry, detail URL, content sha256) in Metadata
                // so the library can flag already-imported entries + dedupe re-downloads.
                var entity = await _repository.GetByIdAsync(mod.Id).ConfigureAwait(false);
                if (entity != null)
                {
                    if (!string.IsNullOrWhiteSpace(detail.Title)) entity.Name = detail.Title.Trim();
                    entity.Metadata = WriteRemoteMetadata(entity.Metadata, sourceId, detail.DetailUrl, downloaded.Sha256);
                    await _repository.UpdateAsync(entity).ConfigureAwait(false);
                }

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

    public async Task<HashSet<string>> GetImportedDetailUrlsAsync()
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in await _repository.GetAllAsync().ConfigureAwait(false))
        {
            var url = ReadRemoteDetailUrl(entity.Metadata);
            if (!string.IsNullOrEmpty(url)) urls.Add(url!);
        }
        return urls;
    }

    /// <summary>Merge the remote-import identity into a Metadata JSON string (other fields preserved).</summary>
    public static string WriteRemoteMetadata(string? metadata, string sourceId, string detailUrl, string sha256)
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
            ["detailUrl"] = detailUrl,
            ["sha256"] = sha256,
            ["importedAtUtc"] = DateTime.UtcNow.ToString("O"),
        };
        return obj.ToJsonString();
    }

    public static string? ReadRemoteDetailUrl(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return null;
        try
        {
            var obj = JsonNode.Parse(metadata) as JsonObject;
            return obj?["remote"]?["detailUrl"]?.GetValue<string>();
        }
        catch { return null; }
    }

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
