using System.Text.Json;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Parser for the GameBanana apiv11 JSON API (adapter <c>engine = "gamebanana"</c>). GameBanana is a
/// JSON API, not scrapeable HTML, so the regex-over-HTML path (the "http" engine) can't read it — this
/// turns its Subfeed + ProfilePage responses into the same <see cref="RemoteModCard"/>/
/// <see cref="RemoteModDetail"/> DTOs the rest of the pipeline consumes. All URLs are absolute.
///
/// Verified live 2026-07-06 (see remote-library.md):
/// - list:     {base}/apiv11/Game/{gameId}/Subfeed?_nPage={n}&amp;_sSort=new  → _aRecords[] + _aMetadata
/// - detail:   {base}/apiv11/Mod/{id}/ProfilePage                            → _aFiles[]._sDownloadUrl (DIRECT)
/// - NSFW:     content-rated mods are ALREADY in the subfeed (_sInitialVisibility warn/hide) — no auth
///             or extra param needed; we index every record.
/// </summary>
public static class GameBananaEngine
{
    public const string EngineName = "gamebanana";

    public static string BuildSubfeedUrl(string baseUrl, string gameId, int page) =>
        $"{baseUrl.TrimEnd('/')}/apiv11/Game/{gameId}/Subfeed?_nPage={Math.Max(1, page)}&_sSort=new";

    public static string BuildProfilePageUrl(string baseUrl, string modId) =>
        $"{baseUrl.TrimEnd('/')}/apiv11/Mod/{modId}/ProfilePage";

    /// <summary>The numeric mod id from a `.../mods/{id}` profile URL (used to build the ProfilePage URL).</summary>
    public static string? ExtractModId(string detailUrl)
    {
        var m = global::System.Text.RegularExpressions.Regex.Match(detailUrl, @"/mods/(\d+)");
        return m.Success ? m.Groups[1].Value : null;
    }

    public static RemoteBrowseResult ParseSubfeed(string json, string baseUrl, int page)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var result = new RemoteBrowseResult { Page = Math.Max(1, page) };

        if (root.TryGetProperty("_aRecords", out var records) && records.ValueKind == JsonValueKind.Array)
        {
            foreach (var rec in records.EnumerateArray())
            {
                if (GetString(rec, "_sModelName") is { } model && !model.Equals("Mod", StringComparison.OrdinalIgnoreCase))
                    continue; // subfeeds can carry non-mod submissions (sounds, WiPs) — mods only
                var url = GetString(rec, "_sProfileUrl");
                var name = GetString(rec, "_sName");
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name)) continue;
                result.Cards.Add(new RemoteModCard
                {
                    Title = name,
                    DetailUrl = url,
                    ImageUrl = FirstImageUrl(rec) ?? string.Empty,
                });
            }
        }

        // Total pages from the metadata (record count / per-page). Caller caps the crawl.
        if (root.TryGetProperty("_aMetadata", out var meta))
        {
            var count = GetInt(meta, "_nRecordCount");
            var perPage = GetInt(meta, "_nPerpage");
            if (count > 0 && perPage > 0) result.TotalPages = (int)Math.Ceiling(count / (double)perPage);
        }
        return result;
    }

    public static RemoteModDetail ParseProfilePage(string json, string detailUrl)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var detail = new RemoteModDetail { DetailUrl = detailUrl, Title = GetString(root, "_sName") ?? string.Empty };

        // Gallery: every preview image at full resolution.
        if (root.TryGetProperty("_aPreviewMedia", out var media)
            && media.TryGetProperty("_aImages", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in images.EnumerateArray())
            {
                var url = ImageUrl(img, preferLarge: true);
                if (!string.IsNullOrEmpty(url) && !detail.Images.Contains(url)) detail.Images.Add(url);
            }
        }

        // Current files → direct downloads (gamebanana.com/dl/{fileId}; resolver type "direct").
        if (root.TryGetProperty("_aFiles", out var files) && files.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in files.EnumerateArray())
            {
                var dl = GetString(f, "_sDownloadUrl");
                if (string.IsNullOrWhiteSpace(dl)) continue;
                var fileName = GetString(f, "_sFile");
                if (detail.Downloads.Any(d => d.Url == dl)) continue;
                detail.Downloads.Add(new RemoteDownloadOption
                {
                    Name = string.IsNullOrWhiteSpace(fileName) ? "GameBanana" : fileName!,
                    Url = dl!,
                    Type = "direct",
                });
            }
        }
        return detail;
    }

    private static string? FirstImageUrl(JsonElement record)
    {
        if (record.TryGetProperty("_aPreviewMedia", out var media)
            && media.TryGetProperty("_aImages", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            foreach (var img in images.EnumerateArray())
            {
                var url = ImageUrl(img, preferLarge: false); // card thumbnail: the 530 variant
                if (!string.IsNullOrEmpty(url)) return url;
            }
        }
        return null;
    }

    /// <summary>Compose an image URL from _sBaseUrl + a file field. Cards prefer the 530px variant;
    /// the detail gallery prefers the original (_sFile).</summary>
    private static string? ImageUrl(JsonElement img, bool preferLarge)
    {
        var baseUrl = GetString(img, "_sBaseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        var file = preferLarge
            ? (GetString(img, "_sFile") ?? GetString(img, "_sFile530"))
            : (GetString(img, "_sFile530") ?? GetString(img, "_sFile220") ?? GetString(img, "_sFile"));
        return string.IsNullOrWhiteSpace(file) ? null : $"{baseUrl!.TrimEnd('/')}/{file}";
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}
