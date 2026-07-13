using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Resolves a kodbox (可道云 / KodExplorer) share link to a direct download URL. huihui's IP/VPN "Hui盘"
/// mirror (e.g. http://174.136.207.5/#s/&lt;key&gt;) runs kodbox 1.62 — a DIFFERENT app from the main
/// Cloudreve Hui盘, so a different anonymous API (verified live 2026-07-14, see remote-library.md):
///   1. GET /index.php?explorer/share/get&amp;shareID={key}  → {code:true, data:{title, sourceInfo:{
///      name, path:"{shareItemLink:key}/", type:"file"|"folder", size}}}
///   2. download = GET /index.php?explorer/share/fileDownload&amp;shareID={key}&amp;path={path} — streams
///      the raw bytes (Content-Length = size). A folder share streams as one zip via .../share/zipDownload.
/// The share key rides the SPA hash route (#s/key); public shares need no login/cookie/password. Unlike
/// Cloudreve there is NO presigned URL — the resolved URL IS a plain GET the download service streams.
/// </summary>
public interface IKodboxShareResolver
{
    Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default);
}

public class KodboxShareResolver : IKodboxShareResolver
{
    private readonly IRemotePageFetcher _fetcher;

    public KodboxShareResolver(IRemotePageFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    public async Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default)
    {
        var (origin, key) = ParseShareUrl(shareUrl);

        // 1. Share metadata (anonymous). code:false → data is a message string (分享不存在 / 没有权限 / expired).
        var info = await GetJsonAsync($"{origin}/index.php?explorer/share/get&shareID={Uri.EscapeDataString(key)}", ct)
            .ConfigureAwait(false);
        var data = info.GetProperty("data");
        var source = data.TryGetProperty("sourceInfo", out var si) ? si : default;

        var name = TryString(source, "name")
            ?? (data.TryGetProperty("title", out var t) ? t.GetString() : null)
            ?? "download";
        var type = TryString(source, "type") ?? "file";
        var size = source.ValueKind == JsonValueKind.Object && source.TryGetProperty("size", out var s) ? ReadLong(s) : 0;
        // kodbox's internal path for the shared item, e.g. "{shareItemLink:<key>}/". Deterministic fallback.
        var path = TryString(source, "path");
        if (string.IsNullOrEmpty(path)) path = "{shareItemLink:" + key + "}/";

        // 2. A file share streams via fileDownload; a folder streams as one server-zipped archive.
        var isFolder = string.Equals(type, "folder", StringComparison.OrdinalIgnoreCase);
        var action = isFolder ? "zipDownload" : "fileDownload";
        var downloadUrl =
            $"{origin}/index.php?explorer/share/{action}&shareID={Uri.EscapeDataString(key)}&path={Uri.EscapeDataString(path)}";
        var fileName = isFolder && !name.Contains('.') ? name + ".zip" : name;

        return new RemoteResolveResult
        {
            FileName = fileName,
            Size = isFolder ? 0 : Math.Max(0, size), // folder zip size unknown until the download's Content-Length
            DownloadUrl = downloadUrl,
        };
    }

    /// <summary>Extract (origin, shareKey) from a kodbox share link — the key rides the SPA hash route
    /// (http://host/#s/&lt;key&gt;); also accept a server path form (host/s/&lt;key&gt;) defensively.</summary>
    public static (string Origin, string Key) ParseShareUrl(string shareUrl)
    {
        if (!Uri.TryCreate(shareUrl, UriKind.Absolute, out var uri))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"invalid share url: {shareUrl}");
        var key = ExtractKey(uri.Fragment.TrimStart('#')) ?? ExtractKey(uri.AbsolutePath);
        if (key == null)
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"not a kodbox share url: {shareUrl}");
        return ($"{uri.Scheme}://{uri.Authority}", key);
    }

    /// <summary>Return the &lt;key&gt; from an "s/&lt;key&gt;" path or hash fragment, else null.</summary>
    private static string? ExtractKey(string pathOrFragment)
    {
        var segments = pathOrFragment.Trim('/').Split('/');
        return segments.Length >= 2 && string.Equals(segments[0], "s", StringComparison.OrdinalIgnoreCase)
               && !string.IsNullOrWhiteSpace(segments[1])
            ? segments[1]
            : null;
    }

    private async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct)
    {
        var json = await _fetcher.GetStringAsync(url, ct).ConfigureAwait(false);
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException)
        {
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "non-JSON response from download host");
        }
        // kodbox: {code:true|false, data:...}. false → data carries the reason message.
        var ok = root.TryGetProperty("code", out var c) &&
                 (c.ValueKind == JsonValueKind.True || (c.ValueKind == JsonValueKind.Number && c.GetInt32() != 0));
        if (!ok)
        {
            var msg = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            throw new OperationException("KODBOX_SHARE_UNAVAILABLE", "reason",
                string.IsNullOrWhiteSpace(msg) ? "share unavailable" : msg!);
        }
        return root;
    }

    private static string? TryString(JsonElement obj, string prop) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static long ReadLong(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number ? e.GetInt64()
        : e.ValueKind == JsonValueKind.String && long.TryParse(e.GetString(), out var n) ? n
        : 0;
}
