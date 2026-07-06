using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Resolves a Quark pan (夸克网盘) share link (https://pan.quark.cn/s/{pwd_id}) to a direct download.
///
/// Quark shares CANNOT be downloaded anonymously (verified 2026-07-06): the token + file-list steps
/// work with no login, but the download endpoint needs a logged-in session cookie. That cookie is
/// captured by the in-app login window (never typed) and stored per-provider in IOnlineAccountStore.
/// Flow (apiv1, ucpro):
///   1. POST /1/clouddrive/share/sharepage/token   {pwd_id, passcode}         → stoken   (anon)
///   2. GET  /1/clouddrive/share/sharepage/detail   ?pwd_id&stoken&pdir_fid    → file list (anon; recurse dirs)
///   3. POST /1/clouddrive/share/sharepage/download {fids, fid_tokens, pwd_id, stoken}
///                                                   WITH the account Cookie   → data[].download_url
/// The returned CDN url ALSO needs the cookie + UA on GET, so they ride back in DownloadHeaders.
///
/// NOTE: this dev machine is geo-blocked from drive-pc.quark.cn (only the drive-h host was reachable,
/// which lacks the download endpoint), so the cookie'd download leg is UNVERIFIED here — confirm live.
/// </summary>
public interface IQuarkShareResolver
{
    Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default);
}

public class QuarkShareResolver : IQuarkShareResolver
{
    private const string Provider = "quark";
    private const string ApiBase = "https://drive-pc.quark.cn";
    private const string ApiQuery = "pr=ucpro&fr=pc";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar", ".zzz"];

    private readonly IDownloadService _download;
    private readonly IOnlineAccountStore _accounts;
    private readonly ILogHelper _logger;

    public QuarkShareResolver(IDownloadService download, IOnlineAccountStore accounts, ILogHelper logger)
    {
        _download = download;
        _accounts = accounts;
        _logger = logger;
    }

    public async Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default)
    {
        var account = _accounts.Get(Provider);
        if (account == null || string.IsNullOrWhiteSpace(account.Cookie))
            throw new OperationException("QUARK_NOT_LOGGED_IN");

        var pwdId = ParsePwdId(shareUrl);
        var headers = BuildHeaders(account.Cookie);

        // 1. Share token (anonymous).
        var tokenBody = JsonSerializer.Serialize(new { pwd_id = pwdId, passcode = "" });
        var token = await PostAsync($"{ApiBase}/1/clouddrive/share/sharepage/token?{ApiQuery}", tokenBody, headers, ct)
            .ConfigureAwait(false);
        var stoken = token.GetProperty("data").TryGetProperty("stoken", out var st) ? st.GetString() : null;
        if (string.IsNullOrEmpty(stoken))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: no share token (share may be private/expired)");

        // 2. List files, recursing into folders to find the best archive.
        var best = await FindBestFileAsync(pwdId, stoken!, "0", headers, depth: 0, ct).ConfigureAwait(false);
        if (best == null)
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: share has no downloadable file");

        // 3. Authenticated download URL.
        var dlBody = JsonSerializer.Serialize(new
        {
            fids = new[] { best.Value.Fid },
            fid_tokens = new[] { best.Value.FidToken },
            pwd_id = pwdId,
            stoken,
        });
        var dl = await PostAsync($"{ApiBase}/1/clouddrive/share/sharepage/download?{ApiQuery}", dlBody, headers, ct)
            .ConfigureAwait(false);
        var data = dl.GetProperty("data");
        var url = data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
            ? data[0].TryGetProperty("download_url", out var u) ? u.GetString() : null
            : null;
        if (string.IsNullOrEmpty(url))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: no download url (cookie may be expired — log in again)");

        return new RemoteResolveResult
        {
            FileName = best.Value.Name,
            Size = Math.Max(0, best.Value.Size),
            DownloadUrl = url!,
            // Quark's CDN checks the cookie + UA on the GET too.
            DownloadHeaders = BuildHeaders(account.Cookie),
        };
    }

    /// <summary>DFS for the largest archive (mods ship as one big archive); any file if no archive.
    /// Bounded depth so a pathological share can't recurse forever.</summary>
    private async Task<(string Fid, string FidToken, string Name, long Size)?> FindBestFileAsync(
        string pwdId, string stoken, string pdirFid, IReadOnlyDictionary<string, string> headers, int depth, CancellationToken ct)
    {
        if (depth > 5) return null;
        var list = await ListDirAsync(pwdId, stoken, pdirFid, headers, ct).ConfigureAwait(false);

        (string Fid, string FidToken, string Name, long Size)? best = null;
        var bestIsArchive = false;
        foreach (var f in list.EnumerateArray())
        {
            var isDir = f.TryGetProperty("dir", out var d) && d.GetBoolean();
            if (isDir)
            {
                var fid = f.GetProperty("fid").GetString();
                var sub = fid == null ? null
                    : await FindBestFileAsync(pwdId, stoken, fid, headers, depth + 1, ct).ConfigureAwait(false);
                if (sub != null && Prefer(sub.Value, best, bestIsArchive, out var subArchive))
                {
                    best = sub;
                    bestIsArchive = subArchive;
                }
                continue;
            }

            var name = f.TryGetProperty("file_name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            var candidate = (
                Fid: f.TryGetProperty("fid", out var cf) ? cf.GetString() ?? string.Empty : string.Empty,
                FidToken: f.TryGetProperty("share_fid_token", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                Name: name,
                Size: f.TryGetProperty("size", out var s) ? s.GetInt64() : 0);
            if (string.IsNullOrEmpty(candidate.Fid) || string.IsNullOrEmpty(candidate.FidToken)) continue;
            if (Prefer(candidate, best, bestIsArchive, out var isArchive))
            {
                best = candidate;
                bestIsArchive = isArchive;
            }
        }
        return best;
    }

    /// <summary>Prefer archives; among equals, the largest. Returns whether the candidate is chosen
    /// and (out) whether it's an archive.</summary>
    private static bool Prefer((string Fid, string FidToken, string Name, long Size) candidate,
        (string Fid, string FidToken, string Name, long Size)? current, bool currentIsArchive, out bool isArchive)
    {
        isArchive = ArchiveExtensions.Any(e => candidate.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        if (current == null) return true;
        if (isArchive && !currentIsArchive) return true;
        if (isArchive == currentIsArchive) return candidate.Size > current.Value.Size;
        return false;
    }

    private async Task<JsonElement> ListDirAsync(string pwdId, string stoken, string pdirFid,
        IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        var url = $"{ApiBase}/1/clouddrive/share/sharepage/detail?{ApiQuery}&pwd_id={pwdId}" +
                  $"&stoken={Uri.EscapeDataString(stoken)}&pdir_fid={pdirFid}&force=0&_page=1&_size=200" +
                  "&_fetch_banner=0&_fetch_share=0&_fetch_total=1&_sort=file_type:asc,updated_at:desc";
        var root = await GetAsync(url, headers, ct).ConfigureAwait(false);
        return root.GetProperty("data").TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array
            ? list
            : default;
    }

    /// <summary>pwd_id from https://pan.quark.cn/s/{pwd_id}[/…].</summary>
    public static string ParsePwdId(string shareUrl)
    {
        if (!Uri.TryCreate(shareUrl, UriKind.Absolute, out var uri))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"invalid quark url: {shareUrl}");
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2 || !string.Equals(segments[0], "s", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(segments[1]))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"not a quark share url: {shareUrl}");
        return segments[1];
    }

    private static Dictionary<string, string> BuildHeaders(string cookie) => new()
    {
        ["User-Agent"] = UserAgent,
        ["Cookie"] = cookie,
        ["Referer"] = "https://pan.quark.cn/",
        ["Origin"] = "https://pan.quark.cn",
    };

    private async Task<JsonElement> GetAsync(string url, IReadOnlyDictionary<string, string> headers, CancellationToken ct) =>
        EnsureOk(await _download.GetStringAsync(url, headers, ct).ConfigureAwait(false));

    private async Task<JsonElement> PostAsync(string url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken ct) =>
        EnsureOk(await _download.PostJsonAsync(url, body, headers, ct).ConfigureAwait(false));

    /// <summary>Quark returns 200 with a `code`/`status` in the body — non-zero + message = error.</summary>
    private static JsonElement EnsureOk(string json)
    {
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException)
        {
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: non-JSON response");
        }
        var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
        if (code != 0)
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason",
                string.IsNullOrWhiteSpace(msg) ? $"quark api error {code}" : msg!);
        }
        return root;
    }
}
