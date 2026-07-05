using System.Linq;
using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Site engine for the GameBanana apiv11 JSON API (adapter <c>engine = "gamebanana"</c>). GameBanana
/// is a JSON API, not scrapeable HTML, so the regex "http" engine can't read it — this is a HARDCODED
/// engine (remote-library-redesign.md) whose only config is the base URL + game ids; it normalizes the
/// API responses into the shared DTOs. Parsing lives in public statics (unit-tested without HTTP).
///
/// Verified live 2026-07-06 (see remote-library.md):
/// - list:     {base}/apiv11/Game/{gameId}/Subfeed?_nPage={n}&amp;_sSort=new  → _aRecords[] + _aMetadata
/// - search:   {base}/apiv11/Util/Search/Results?_sModelName=Mod&amp;_sSearchString={q}&amp;_idGameRow={gameId}&amp;_nPage={n}
/// - detail:   {base}/apiv11/Mod/{id}/ProfilePage                            → _aFiles[]._sDownloadUrl (DIRECT)
/// - NSFW:     content-rated mods are ALREADY in the subfeed (_sInitialVisibility warn/hide) — no auth
///             or extra param needed; we index every record.
/// </summary>
public class GameBananaEngine : RemoteSiteEngineBase
{
    public const string EngineName = "gamebanana";

    public GameBananaEngine(IRemotePageFetcher fetcher, ILogHelper logger) : base(fetcher, logger) { }

    public override string EngineId => EngineName;

    public override bool SupportsSearch(RemoteSourceConfig config) => true;

    public override async Task<RemoteBrowseResult> BrowseAsync(RemoteSourceConfig config, string listId, int page, CancellationToken ct)
    {
        var json = await FetchAsync(BuildSubfeedUrl(config.BaseUrl, listId, page), ct).ConfigureAwait(false);
        return ParseSubfeed(json, config.BaseUrl, page);
    }

    public override async Task<RemoteBrowseResult> SearchAsync(RemoteSourceConfig config, string query, string? listId, CancellationToken ct)
    {
        var json = await FetchAsync(BuildSearchUrl(config.BaseUrl, query, listId, 1), ct).ConfigureAwait(false);
        // The search response carries the same _aRecords shape as the Subfeed.
        return ParseSubfeed(json, config.BaseUrl, 1);
    }

    public override async Task<RemoteModDetail> GetDetailAsync(RemoteSourceConfig config, string detailUrl, CancellationToken ct)
    {
        var modId = ExtractModId(detailUrl)
            ?? throw new OperationException("REMOTE_FETCH_FAILED", "url", detailUrl);
        var json = await FetchAsync(BuildProfilePageUrl(config.BaseUrl, modId), ct).ConfigureAwait(false);
        return ParseProfilePage(json, detailUrl);
    }

    // ---- URL builders + parsers (static — unit-tested without HTTP) -----------------------------

    // NO _sSort param — the DEFAULT Subfeed order IS the site's game-page order (verified against
    // gamebanana.com/games/19567 in Chrome, 2026-07-06: first 10 mods matched exactly). It's the
    // site's own recently-updated/featured mix — NOT reconstructible client-side (an earlier
    // _sSort=new + re-sort by _tsDateAdded buried freshly-UPDATED old mods, dropping the page's
    // first mod entirely). Preserve the API's returned order verbatim.
    public static string BuildSubfeedUrl(string baseUrl, string gameId, int page) =>
        $"{baseUrl.TrimEnd('/')}/apiv11/Game/{gameId}/Subfeed?_nPage={Math.Max(1, page)}";

    /// <summary>Game-scoped mod search (listId = the GameBanana game id; null searches site-wide).</summary>
    public static string BuildSearchUrl(string baseUrl, string query, string? gameId, int page) =>
        $"{baseUrl.TrimEnd('/')}/apiv11/Util/Search/Results?_sModelName=Mod&_sSearchString={Uri.EscapeDataString(query.Trim())}"
        + (string.IsNullOrWhiteSpace(gameId) ? string.Empty : $"&_idGameRow={gameId}")
        + $"&_nPage={Math.Max(1, page)}";

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
                    continue; // subfeeds carry non-mod submissions (Questions, WiPs, Sounds) — mods only
                var url = GetString(rec, "_sProfileUrl");
                var name = GetString(rec, "_sName");
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name)) continue;
                // Recency = last UPDATE (what the site surfaces); falls back to the added date.
                var ts = GetLong(rec, "_tsDateModified");
                if (ts <= 0) ts = GetLong(rec, "_tsDateAdded");
                var card = new RemoteModCard
                {
                    Title = name,
                    DetailUrl = url,
                    ImageUrl = FirstImageUrl(rec) ?? string.Empty,
                    DateHint = ts > 0 ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.ToString("yyyy-MM-dd") : null,
                };
                // Super category ("Skins", "UI") — the subfeed's only taxonomy level; the sub category
                // lives on the ProfilePage and joins via GetDetail. Both are TAGS (redesign).
                if (CategoryName(rec, "_aRootCategory") is { } super_) card.Tags.Add(super_);
                // API order preserved verbatim — it IS the site's page order (see BuildSubfeedUrl).
                result.Cards.Add(card);
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

        // Tags: the ProfilePage carries the SUB (leaf) category; the super comes from the subfeed card.
        if (CategoryName(root, "_aCategory") is { } sub) detail.Tags.Add(sub);

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

    /// <summary>A category object's name (_aRootCategory = super, _aCategory = sub/leaf).</summary>
    private static string? CategoryName(JsonElement record, string prop)
    {
        if (record.TryGetProperty(prop, out var cat)) return GetString(cat, "_sName");
        return null;
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

    private static long GetLong(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
}
