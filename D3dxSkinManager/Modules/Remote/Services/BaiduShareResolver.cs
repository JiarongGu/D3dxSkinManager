using System.Text.Json;
using System.Text.RegularExpressions;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Resolves a Baidu Netdisk (百度网盘) share link (https://pan.baidu.com/s/1{surl}?pwd={code}) to a
/// download, using the saved account cookie (BDUSS, captured by the in-app login window — see
/// ExternalLoginService). Like Quark, Baidu has no anonymous OR direct share-download endpoint — the
/// working flow is SAVE(转存)-then-download-then-delete, all authed:
///   1. GET  /api/gettemplatevariable?fields=["bdstoken"]           → bdstoken (needed to sign POSTs)
///   2. POST /share/verify?surl={surl}   pwd={code}                 → randsk  (→ BDCLND cookie)
///   3. GET  /share/list?shorturl={surl}&root=1                     → shareid + uk + file list (recurse dirs)
///   4. POST /share/transfer?shareid&from(uk)&sekey(randsk)         fsidlist,path=/D3dxSkinManager → save
///   5. POST /api/filemetas?target=[savedPath]&dlink=1             → dlink
///   6. GET  dlink  (UA "pan.baidu.com" — the app UA bypasses the browser web-download cap)
///   7. POST /api/filemanager?opera=delete  filelist=[savedPath]   → cleanup
/// The two resolve calls (confirm dialog + background download) split so the SAVE happens ONCE, in the
/// background: <see cref="ResolveAsync"/> is metadata-only; <see cref="PrepareDownloadAsync"/> does the
/// save+dlink; <see cref="CleanupAsync"/> deletes the saved copy (called in the import's finally).
/// NOTE: verified in stages against a live account 2026-07-14 — see remote-library.md.
/// </summary>
public interface IBaiduShareResolver
{
    /// <summary>Metadata only (verify + list, NO save) — for the confirm dialog's file name/size.</summary>
    Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default);

    /// <summary>Save the share file into the user's drive and return the dlink + headers + the saved
    /// path to clean up afterward. Background download path only.</summary>
    Task<BaiduDownload> PrepareDownloadAsync(string shareUrl, CancellationToken ct = default);

    /// <summary>Delete the saved copies from the user's drive (best-effort — a failure is logged,
    /// never thrown; the download already succeeded).</summary>
    Task CleanupAsync(IReadOnlyList<string> savedPaths, CancellationToken ct = default);
}

/// <summary>A prepared Baidu download: the dlink + headers to fetch it, plus the drive paths to delete.</summary>
public sealed class BaiduDownload
{
    public string FileName { get; init; } = string.Empty;
    public long Size { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = new();
    public IReadOnlyList<string> SavedPaths { get; init; } = Array.Empty<string>();
}

public class BaiduShareResolver : IBaiduShareResolver
{
    private const string Provider = "baidu";
    private const string ApiBase = "https://pan.baidu.com";

    /// <summary>Dedicated folder in the user's drive for the app's transient 转存 copies.</summary>
    public const string AppDriveFolder = "/D3dxSkinManager";

    // Browser UA for the JSON API calls (verify/list/transfer/filemetas/filemanager).
    private const string WebUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
    // THE DOWNLOAD TRICK: the netdisk app UA on the dlink GET bypasses the browser web-download speed
    // cap (same idea as Quark's desktop-client UA). A plain browser UA gets a throttled/blocked dlink.
    private const string DownloadUserAgent = "pan.baidu.com";

    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar", ".zzz"];

    private readonly IDownloadService _download;
    private readonly IOnlineAccountStore _accounts;
    private readonly ILogHelper _logger;

    public BaiduShareResolver(IDownloadService download, IOnlineAccountStore accounts, ILogHelper logger)
    {
        _download = download;
        _accounts = accounts;
        _logger = logger;
    }

    public async Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default)
    {
        var ctx = await OpenShareAsync(shareUrl, ct).ConfigureAwait(false);
        var best = FindBestFile(ctx.Files)
                   ?? throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "baidu: share has no downloadable file");
        return new RemoteResolveResult { FileName = best.Name, Size = Math.Max(0, best.Size), DownloadUrl = shareUrl };
    }

    public async Task<BaiduDownload> PrepareDownloadAsync(string shareUrl, CancellationToken ct = default)
    {
        var ctx = await OpenShareAsync(shareUrl, ct).ConfigureAwait(false);
        var best = FindBestFile(ctx.Files)
                   ?? throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "baidu: share has no downloadable file");

        // 4. 转存 the file into the app folder (create it first if missing).
        await EnsureAppFolderAsync(ctx, ct).ConfigureAwait(false);
        var transferBody = new Dictionary<string, string>
        {
            ["fsidlist"] = "[" + best.FsId + "]",
            ["path"] = AppDriveFolder,
        };
        var transferUrl = $"{ApiBase}/share/transfer?shareid={ctx.ShareId}&from={ctx.Uk}&sekey={ctx.Randsk}"
                          + $"&bdstoken={ctx.Bdstoken}&channel=chunlei&web=1&app_id=250528&clienttype=0";
        var transfer = await PostFormAsync(transferUrl, transferBody, ctx.Cookie, ct, ShareReferer(ctx.Surl)).ConfigureAwait(false);
        // errno 4 = the file is ALREADY in the target folder (a prior run saved it but didn't clean up) —
        // that's fine, we reuse the existing copy (it's deleted after download either way).
        var terrno = transfer.TryGetProperty("errno", out var te) && te.ValueKind == JsonValueKind.Number ? te.GetInt32() : 0;
        if (terrno != 0 && terrno != 4) EnsureErrno(transfer, "transfer");
        var savedPath = $"{AppDriveFolder}/{best.Name}";

        // 5. dlink for the saved copy.
        var dlink = await GetDlinkAsync(savedPath, ctx, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(dlink))
        {
            await CleanupAsync(new[] { savedPath }, ct).ConfigureAwait(false);
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "baidu: no download link for the saved file");
        }

        return new BaiduDownload
        {
            FileName = best.Name,
            Size = Math.Max(0, best.Size),
            DownloadUrl = dlink,
            Headers = new Dictionary<string, string> { ["User-Agent"] = DownloadUserAgent, ["Cookie"] = ctx.Cookie },
            SavedPaths = new[] { savedPath },
        };
    }

    public async Task CleanupAsync(IReadOnlyList<string> savedPaths, CancellationToken ct = default)
    {
        if (savedPaths.Count == 0) return;
        var account = _accounts.Get(Provider);
        if (account == null || string.IsNullOrWhiteSpace(account.Cookie)) return;
        try
        {
            var bdstoken = await GetBdstokenAsync(account.Cookie, ct).ConfigureAwait(false);
            var body = new Dictionary<string, string>
            {
                ["filelist"] = JsonSerializer.Serialize(savedPaths),
            };
            var url = $"{ApiBase}/api/filemanager?opera=delete&async=2&onnest=fail&bdstoken={bdstoken}&channel=chunlei&web=1&app_id=250528&clienttype=0";
            await PostFormAsync(url, body, account.Cookie, ct).ConfigureAwait(false);
            _logger.Info($"[Baidu] cleaned up {savedPaths.Count} saved file(s) after download", nameof(BaiduShareResolver));
        }
        catch (Exception ex)
        {
            _logger.Warn($"[Baidu] cleanup failed (saved copy remains in drive): {ex.Message}", nameof(BaiduShareResolver));
        }
    }

    // ---- steps ---------------------------------------------------------------------------------

    /// <summary>Auth + verify + list — everything needed to identify the share's files (no save yet).</summary>
    private async Task<ShareContext> OpenShareAsync(string shareUrl, CancellationToken ct)
    {
        var account = _accounts.Get(Provider);
        if (account == null || string.IsNullOrWhiteSpace(account.Cookie))
            throw new OperationException("BAIDU_NOT_LOGGED_IN");
        var (surl, pwd) = ParseShareUrl(shareUrl);
        var bdstoken = await GetBdstokenAsync(account.Cookie, ct).ConfigureAwait(false);

        // Verify the extract code → randsk (the share session key, appended as the BDCLND cookie).
        var verifyUrl = $"{ApiBase}/share/verify?surl={surl}&bdstoken={bdstoken}&t={Now()}&channel=chunlei&web=1&app_id=250528&clienttype=0";
        var verify = await PostFormAsync(verifyUrl, new Dictionary<string, string> { ["pwd"] = pwd, ["vcode"] = "", ["vcode_str"] = "" }, account.Cookie, ct, ShareReferer(surl)).ConfigureAwait(false);
        EnsureErrno(verify, "verify");
        var randsk = verify.TryGetProperty("randsk", out var rs) ? rs.GetString() : null;
        if (string.IsNullOrEmpty(randsk))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "baidu: share password rejected (no randsk)");
        // randsk from verify is ALREADY URL-encoded (e.g. contains %2B) — use it VERBATIM for the BDCLND
        // cookie and the sekey query param; re-encoding double-escapes it and the share ops fail (errno -9).
        var cookie = $"{account.Cookie}; BDCLND={randsk}";

        // List the share root (shareid + uk + file list). Recurse into a single wrapping folder.
        var (shareId, uk, files) = await ListShareAsync(surl, cookie, bdstoken, randsk!, ct).ConfigureAwait(false);
        return new ShareContext(cookie, bdstoken, surl, randsk!, shareId, uk, files);
    }

    private async Task<string> GetBdstokenAsync(string cookie, CancellationToken ct)
    {
        var url = $"{ApiBase}/api/gettemplatevariable?fields=%5B%22bdstoken%22%5D&channel=chunlei&web=1&app_id=250528&clienttype=0";
        var r = await GetAsync(url, cookie, ct).ConfigureAwait(false);
        EnsureErrno(r, "gettemplatevariable");
        var token = r.TryGetProperty("result", out var res) && res.TryGetProperty("bdstoken", out var t) ? t.GetString() : null;
        if (string.IsNullOrEmpty(token))
            throw new OperationException("BAIDU_NOT_LOGGED_IN");
        return token!;
    }

    private async Task<(string ShareId, string Uk, List<BaiduFile> Files)> ListShareAsync(string surl, string cookie, string bdstoken, string randsk, CancellationToken ct)
    {
        // The logged-in share page embeds shareid/uk + the root file list (`yunData`). The
        // /share/list?shorturl= form returns errno -9, so scrape the ids from the page then, if the
        // embedded list didn't parse, call /share/list?uk&shareid (the reliable JSON list).
        var html = await _download.GetStringAsync($"{ApiBase}/s/1{surl}", Headers(cookie, ShareReferer(surl)), ct).ConfigureAwait(false);
        // yunData embeds BOTH the viewer's own `uk` and the share OWNER's `share_uk` — /share/list needs
        // the OWNER (share_uk), so match that specifically (both quoted + unquoted-key forms).
        var shareId = FirstGroup(html, @"shareid[""']?\s*:\s*[""']?(?<v>\d+)") ?? string.Empty;
        var uk = FirstGroup(html, @"share_uk[""']?\s*:\s*[""']?(?<v>\d+)") ?? string.Empty;

        var files = new List<BaiduFile>();
        if (!string.IsNullOrEmpty(uk) && !string.IsNullOrEmpty(shareId))
        {
            var url = $"{ApiBase}/share/list?uk={uk}&shareid={shareId}&order=other&desc=1&showempty=0&web=1&page=1&num=1000&root=1"
                      + $"&t={Now()}&sekey={randsk}&bdstoken={bdstoken}&channel=chunlei&app_id=250528&clienttype=0";
            var r = await GetAsync(url, cookie, ct, ShareReferer(surl)).ConfigureAwait(false);
            EnsureErrno(r, "share/list");
            if (r.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var f in list.EnumerateArray())
                    files.Add(ParseFile(f));
        }
        return (shareId, uk, files);
    }

    private static string? FirstGroup(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
        return m.Success ? m.Groups["v"].Value : null;
    }

    private async Task<string?> GetDlinkAsync(string path, ShareContext ctx, CancellationToken ct)
    {
        var url = $"{ApiBase}/api/filemetas?dlink=1&bdstoken={ctx.Bdstoken}&target={Uri.EscapeDataString(JsonSerializer.Serialize(new[] { path }))}"
                  + "&channel=chunlei&web=1&app_id=250528&clienttype=0";
        var r = await GetAsync(url, ctx.Cookie, ct).ConfigureAwait(false);
        EnsureErrno(r, "filemetas");
        return r.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Array && info.GetArrayLength() > 0
            && info[0].TryGetProperty("dlink", out var d) ? d.GetString() : null;
    }

    /// <summary>Create the app folder in the drive root (ignore an "already exists" errno).</summary>
    private async Task EnsureAppFolderAsync(ShareContext ctx, CancellationToken ct)
    {
        var url = $"{ApiBase}/api/create?a=commit&bdstoken={ctx.Bdstoken}&channel=chunlei&web=1&app_id=250528&clienttype=0";
        var body = new Dictionary<string, string> { ["path"] = AppDriveFolder, ["isdir"] = "1", ["block_list"] = "[]" };
        try { await PostFormAsync(url, body, ctx.Cookie, ct).ConfigureAwait(false); }
        catch (OperationException ex) { _logger.Verbose($"[Baidu] app folder create: {ex.Message}", nameof(BaiduShareResolver)); }
    }

    // ---- helpers -------------------------------------------------------------------------------

    private BaiduFile? FindBestFile(List<BaiduFile> files)
    {
        BaiduFile? best = null;
        var bestArchive = false;
        foreach (var f in files)
        {
            if (f.IsDir) continue; // v1: single-level; nested-folder recursion is a follow-up
            var isArchive = ArchiveExtensions.Any(e => f.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
            if (best == null || (isArchive && !bestArchive) || (isArchive == bestArchive && f.Size > best.Size))
            {
                best = f; bestArchive = isArchive;
            }
        }
        return best;
    }

    /// <summary>surl (base62 after "/s/1") + pwd (from ?pwd= query) of a Baidu share URL.</summary>
    public static (string Surl, string Pwd) ParseShareUrl(string shareUrl)
    {
        if (!Uri.TryCreate(shareUrl, UriKind.Absolute, out var uri))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"invalid baidu url: {shareUrl}");
        var m = Regex.Match(uri.AbsolutePath, @"/s/1?(?<surl>[^/?#]+)", RegexOptions.CultureInvariant);
        if (!m.Success || string.IsNullOrWhiteSpace(m.Groups["surl"].Value))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"not a baidu share url: {shareUrl}");
        // pwd from the ?pwd= query (manual parse — no System.Web dependency).
        var pwd = Regex.Match(uri.Query, @"[?&]pwd=(?<pwd>[^&]+)", RegexOptions.CultureInvariant) is { Success: true } pm
            ? Uri.UnescapeDataString(pm.Groups["pwd"].Value) : string.Empty;
        return (m.Groups["surl"].Value, pwd);
    }

    private static BaiduFile ParseFile(JsonElement f) => new(
        FsId: ReadIdString(f, "fs_id"),
        Name: f.TryGetProperty("server_filename", out var n) ? n.GetString() ?? string.Empty : string.Empty,
        Path: f.TryGetProperty("path", out var p) ? p.GetString() ?? string.Empty : string.Empty,
        Size: f.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0,
        IsDir: f.TryGetProperty("isdir", out var d) && (d.ValueKind == JsonValueKind.Number ? d.GetInt32() == 1 : d.ValueKind == JsonValueKind.String && d.GetString() == "1"));

    /// <summary>Baidu returns ids as either a JSON number or string depending on the endpoint.</summary>
    private static string ReadIdString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return string.Empty;
        return v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString() ?? string.Empty;
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private const string DiskReferer = "https://pan.baidu.com/disk/home";

    private async Task<JsonElement> GetAsync(string url, string cookie, CancellationToken ct, string? referer = null) =>
        EnsureJson(await _download.GetStringAsync(url, Headers(cookie, referer), ct).ConfigureAwait(false));

    private async Task<JsonElement> PostFormAsync(string url, IReadOnlyDictionary<string, string> form, string cookie, CancellationToken ct, string? referer = null) =>
        EnsureJson(await _download.PostFormAsync(url, form, Headers(cookie, referer), ct).ConfigureAwait(false));

    // Baidu SHARE APIs (verify/list/transfer) check the Referer is the share page; DISK APIs
    // (gettemplatevariable/filemetas/filemanager) want the netdisk home.
    private static string ShareReferer(string surl) => $"https://pan.baidu.com/s/1{surl}";

    private static Dictionary<string, string> Headers(string cookie, string? referer) => new()
    {
        ["User-Agent"] = WebUserAgent,
        ["Cookie"] = cookie,
        ["Referer"] = referer ?? DiskReferer,
        ["Origin"] = "https://pan.baidu.com",
    };

    private JsonElement EnsureJson(string json)
    {
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "baidu: non-JSON response"); }
    }

    /// <summary>Baidu returns 200 with an `errno` (0 = ok). errno -6 = BDUSS invalid → re-login.</summary>
    private void EnsureErrno(JsonElement root, string step)
    {
        var errno = root.TryGetProperty("errno", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 0;
        if (errno == 0) return;
        _logger.Warn($"[Baidu] {step} errno={errno}: {root.GetRawText()[..Math.Min(400, root.GetRawText().Length)]}", nameof(BaiduShareResolver));
        // NOTE: do NOT auto-remove the account on an errno — Baidu returns -6 for a malformed/underspecified
        // request too (not only a dead session), so removing here would log the user out on a bug. Surface
        // the errno; a genuinely expired login shows the same and the user re-logs-in manually.
        throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"baidu {step} error {errno}", $"baidu {step} error {errno}");
    }

    private sealed record BaiduFile(string FsId, string Name, string Path, long Size, bool IsDir);

    private sealed record ShareContext(string Cookie, string Bdstoken, string Surl, string Randsk, string ShareId, string Uk, List<BaiduFile> Files);
}
