using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Resolves a Cloudreve v4 share link (https://host/s/&lt;key&gt;) to a direct presigned download URL.
/// Fully anonymous 3-step API flow, verified live against cloudreve.huihui123.org 2026-07-05
/// (see .claude/knowledge/remote-library.md):
///   1. GET  /api/v4/share/info/{key}                      → gate on unlocked + !expired
///   2. GET  /api/v4/file?uri=cloudreve://{key}@share      → file list (file AND folder shares)
///   3. POST /api/v4/file/url {uris:[path], download:true} → presigned URL
/// The share key rides as the URI userinfo (cloudreve://{key}@share/...), host = literal fs "share".
/// </summary>
public interface ICloudreveShareResolver
{
    Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default);
}

public class CloudreveShareResolver : ICloudreveShareResolver
{
    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar", ".zzz"];

    private readonly IRemotePageFetcher _fetcher;

    public CloudreveShareResolver(IRemotePageFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    public async Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default)
    {
        var (origin, key) = ParseShareUrl(shareUrl);

        // 1. Share metadata — reject passworded/expired shares early with a specific error.
        var info = await GetJsonAsync($"{origin}/api/v4/share/info/{key}", ct).ConfigureAwait(false);
        var infoData = info.GetProperty("data");
        if (infoData.TryGetProperty("expired", out var expired) && expired.GetBoolean())
            throw new OperationException("REMOTE_SHARE_EXPIRED", "url", shareUrl);
        if (infoData.TryGetProperty("unlocked", out var unlocked) && !unlocked.GetBoolean())
            throw new OperationException("REMOTE_SHARE_LOCKED", "url", shareUrl);

        // 2. List the share's files (works for single-file and folder shares alike).
        var listUri = Uri.EscapeDataString($"cloudreve://{key}@share");
        var listing = await GetJsonAsync($"{origin}/api/v4/file?uri={listUri}", ct).ConfigureAwait(false);
        var files = listing.GetProperty("data").GetProperty("files");

        string? bestPath = null, bestName = null;
        long bestSize = -1;
        bool bestIsArchive = false;
        foreach (var file in files.EnumerateArray())
        {
            if (file.TryGetProperty("type", out var type) && type.GetInt32() != 0) continue; // dirs
            var name = file.GetProperty("name").GetString() ?? string.Empty;
            var size = file.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
            var path = file.TryGetProperty("path", out var p) ? p.GetString() : null;
            if (string.IsNullOrEmpty(path)) continue;

            // Prefer archives; among equals, the largest wins (mods ship as one big archive).
            var isArchive = ArchiveExtensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
            if (bestPath == null || (isArchive && !bestIsArchive) || (isArchive == bestIsArchive && size > bestSize))
            {
                bestPath = path;
                bestName = name;
                bestSize = size;
                bestIsArchive = isArchive;
            }
        }

        if (bestPath == null)
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "share has no downloadable file");

        // 3. Presigned URL.
        var body = JsonSerializer.Serialize(new { uris = new[] { bestPath }, download = true });
        var urlResponse = await PostJsonAsync($"{origin}/api/v4/file/url", body, ct).ConfigureAwait(false);
        var urls = urlResponse.GetProperty("data").GetProperty("urls");
        if (urls.GetArrayLength() == 0)
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "no download url returned");
        var downloadUrl = urls[0].GetProperty("url").GetString();
        if (string.IsNullOrEmpty(downloadUrl))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "empty download url");

        return new RemoteResolveResult
        {
            FileName = bestName ?? "download",
            Size = Math.Max(0, bestSize),
            DownloadUrl = downloadUrl,
        };
    }

    /// <summary>Extract the origin + share key from https://host/s/&lt;key&gt;[/…].</summary>
    public static (string Origin, string Key) ParseShareUrl(string shareUrl)
    {
        if (!Uri.TryCreate(shareUrl, UriKind.Absolute, out var uri))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"invalid share url: {shareUrl}");
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2 || !string.Equals(segments[0], "s", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(segments[1]))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"not a share url: {shareUrl}");
        return ($"{uri.Scheme}://{uri.Authority}", segments[1]);
    }

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct) =>
        EnsureOk(await _fetcher.GetStringAsync(url, ct).ConfigureAwait(false));

    private async Task<JsonElement> PostJsonAsync(string url, string body, CancellationToken ct) =>
        EnsureOk(await _fetcher.PostJsonAsync(url, body, ct).ConfigureAwait(false));

    /// <summary>Cloudreve returns 200 with a `code` in the body — non-zero means error (`msg`).</summary>
    private static JsonElement EnsureOk(string json)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException)
        {
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "non-JSON response from download host");
        }
        var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
        if (code != 0)
        {
            var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : null;
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason",
                string.IsNullOrWhiteSpace(msg) ? $"api error {code}" : msg!);
        }
        return root;
    }
}
