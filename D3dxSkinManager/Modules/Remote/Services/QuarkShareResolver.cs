using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// Resolves a Quark pan (夸克网盘) share link (https://pan.quark.cn/s/{pwd_id}) to a download, using the
/// saved account cookie (captured by the in-app login window — see ExternalLoginService).
///
/// Quark has no anonymous OR direct share-download endpoint (verified live 2026-07-06). The working
/// flow is SAVE-then-download-then-delete: the share file is copied into the user's own drive (转存),
/// downloaded from there, and the copy deleted. All authed (apiv1 ucpro, host drive-pc.quark.cn):
///   1. POST /share/sharepage/token   {pwd_id, passcode}                       → stoken
///   2. GET  /share/sharepage/detail  ?pwd_id&stoken&pdir_fid                   → file list (recurse dirs)
///   3. POST /share/sharepage/save    {fid_list, fid_token_list, to_pdir_fid:0} → task_id
///      GET  /task ?task_id&retry_index (poll status==2)                        → save_as.save_as_top_fids
///   4. POST /file/download           {fids:[savedFid]}                          → download_url (CDN)
///   5. POST /file/delete             {action_type:2, filelist:[savedFid]}      → task (cleanup)
/// The two resolve calls (confirm dialog + background download) are split so the SAVE happens ONCE, in
/// the background: <see cref="ResolveAsync"/> is metadata-only; <see cref="PrepareDownloadAsync"/> does
/// the save+url; <see cref="CleanupAsync"/> deletes the saved copy (called in the import's finally).
/// </summary>
public interface IQuarkShareResolver
{
    /// <summary>Metadata only (token+detail, NO save) — for the confirm dialog's file name/size.</summary>
    Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default);

    /// <summary>Save the share file to the user's drive and return the own-drive download URL + the
    /// saved fids to clean up afterward. Background download path only.</summary>
    Task<QuarkDownload> PrepareDownloadAsync(string shareUrl, CancellationToken ct = default);

    /// <summary>Delete the saved copies from the user's drive (the "cleanup after download" step).
    /// Best-effort — a failure is logged, never thrown (the download already succeeded).</summary>
    Task CleanupAsync(IReadOnlyList<string> savedFids, CancellationToken ct = default);
}

/// <summary>A prepared Quark download: the CDN url + headers to fetch it, plus the drive fids to delete.</summary>
public sealed class QuarkDownload
{
    public string FileName { get; init; } = string.Empty;
    public long Size { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = new();
    public IReadOnlyList<string> SavedFids { get; init; } = Array.Empty<string>();
}

public class QuarkShareResolver : IQuarkShareResolver
{
    private const string Provider = "quark";
    private const string ApiBase = "https://drive-pc.quark.cn";
    private const string ApiQuery = "pr=ucpro&fr=pc";

    /// <summary>Dedicated folder created in the user's cloud drive for the app's transient 转存 copies
    /// (so saves don't litter the drive root). Reusable name for any future cloud-storage resolver.</summary>
    public const string AppDriveFolder = "D3dxSkinManager";
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
        var (headers, pwdId) = Auth(shareUrl);
        var (stoken, _) = await TokenAsync(pwdId, headers, ct).ConfigureAwait(false);
        var best = await FindBestFileAsync(pwdId, stoken, "0", headers, 0, ct).ConfigureAwait(false)
                   ?? throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: share has no downloadable file");
        return new RemoteResolveResult
        {
            FileName = best.Name,
            Size = Math.Max(0, best.Size),
            DownloadUrl = shareUrl, // placeholder — the real URL is minted in PrepareDownloadAsync
        };
    }

    public async Task<QuarkDownload> PrepareDownloadAsync(string shareUrl, CancellationToken ct = default)
    {
        var (headers, pwdId) = Auth(shareUrl);
        var (stoken, _) = await TokenAsync(pwdId, headers, ct).ConfigureAwait(false);
        var best = await FindBestFileAsync(pwdId, stoken, "0", headers, 0, ct).ConfigureAwait(false)
                   ?? throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: share has no downloadable file");

        // 3. SAVE (转存) the share file into the app's dedicated folder in the user's drive (not the
        // root), then poll the async task.
        var appFolder = await EnsureAppFolderAsync(headers, ct).ConfigureAwait(false);
        var saveBody = JsonSerializer.Serialize(new
        {
            fid_list = new[] { best.Fid },
            fid_token_list = new[] { best.FidToken },
            to_pdir_fid = appFolder,
            pwd_id = pwdId,
            stoken,
            pdir_fid = best.PdirFid,
            pdir_save_all = false,
            exclude_fids = Array.Empty<string>(),
            scene = "link",
        });
        var save = await PostAsync($"{ApiBase}/1/clouddrive/share/sharepage/save?{ApiQuery}", saveBody, headers, ct).ConfigureAwait(false);
        var taskId = save.GetProperty("data").TryGetProperty("task_id", out var tid) ? tid.GetString() : null;
        if (string.IsNullOrEmpty(taskId))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: save-to-drive returned no task (cookie may be expired — log in again)");

        var taskData = await PollTaskAsync(taskId!, headers, ct).ConfigureAwait(false);
        var savedFid = taskData.TryGetProperty("save_as", out var sa) && sa.TryGetProperty("save_as_top_fids", out var fids)
            && fids.ValueKind == JsonValueKind.Array && fids.GetArrayLength() > 0
                ? fids[0].GetString()
                : null;
        if (string.IsNullOrEmpty(savedFid))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: save-to-drive produced no file");

        // 4. Own-drive download URL.
        var dlBody = JsonSerializer.Serialize(new { fids = new[] { savedFid } });
        JsonElement dl;
        try
        {
            dl = await PostAsync($"{ApiBase}/1/clouddrive/file/download?{ApiQuery}", dlBody, headers, ct).ConfigureAwait(false);
        }
        catch (OperationException ex) when (
            ex.Message.Contains("size limit", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("23018"))
        {
            // Quark caps download size/traffic for FREE accounts (apiv1 code 23018 "download file size
            // limit"). The 转存 save succeeded but downloading from the drive is quota-blocked — not a bug
            // and not bypassable in-app. Clean up the saved copy and surface a clear message.
            await CleanupAsync(new[] { savedFid! }, ct).ConfigureAwait(false);
            throw new OperationException("REMOTE_QUARK_SIZE_LIMIT");
        }
        var data = dl.GetProperty("data");
        var url = data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
            && data[0].TryGetProperty("download_url", out var u) ? u.GetString() : null;
        if (string.IsNullOrEmpty(url))
        {
            // Don't leak the saved copy if the URL step failed.
            await CleanupAsync(new[] { savedFid! }, ct).ConfigureAwait(false);
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: no download url from own drive");
        }

        return new QuarkDownload
        {
            FileName = best.Name,
            Size = Math.Max(0, best.Size),
            DownloadUrl = url!,
            Headers = BuildHeaders(_accounts.Get(Provider)?.Cookie ?? string.Empty), // CDN GET needs cookie + UA
            SavedFids = new[] { savedFid! },
        };
    }

    public async Task CleanupAsync(IReadOnlyList<string> savedFids, CancellationToken ct = default)
    {
        if (savedFids.Count == 0) return;
        var account = _accounts.Get(Provider);
        if (account == null || string.IsNullOrWhiteSpace(account.Cookie)) return;
        var headers = BuildHeaders(account.Cookie);
        try
        {
            var body = JsonSerializer.Serialize(new { action_type = 2, filelist = savedFids, exclude_fids = Array.Empty<string>() });
            var del = await PostAsync($"{ApiBase}/1/clouddrive/file/delete?{ApiQuery}", body, headers, ct).ConfigureAwait(false);
            var taskId = del.GetProperty("data").TryGetProperty("task_id", out var tid) ? tid.GetString() : null;
            if (!string.IsNullOrEmpty(taskId)) await PollTaskAsync(taskId!, headers, ct).ConfigureAwait(false);
            _logger.Info($"[Quark] cleaned up {savedFids.Count} saved file(s) after download", "QuarkShareResolver");
        }
        catch (Exception ex)
        {
            // The download already succeeded — a failed cleanup just leaves a copy in the user's drive.
            _logger.Warn($"[Quark] cleanup failed (saved copy remains in drive): {ex.Message}", "QuarkShareResolver");
        }
    }

    // ---- steps ---------------------------------------------------------------------------------

    private (Dictionary<string, string> Headers, string PwdId) Auth(string shareUrl)
    {
        var account = _accounts.Get(Provider);
        if (account == null || string.IsNullOrWhiteSpace(account.Cookie))
            throw new OperationException("QUARK_NOT_LOGGED_IN");
        return (BuildHeaders(account.Cookie), ParsePwdId(shareUrl));
    }

    private async Task<(string Stoken, JsonElement Data)> TokenAsync(string pwdId, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { pwd_id = pwdId, passcode = "" });
        var token = await PostAsync($"{ApiBase}/1/clouddrive/share/sharepage/token?{ApiQuery}", body, headers, ct).ConfigureAwait(false);
        var data = token.GetProperty("data");
        var stoken = data.TryGetProperty("stoken", out var st) ? st.GetString() : null;
        if (string.IsNullOrEmpty(stoken))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: no share token (share may be private/expired)");
        return (stoken!, data);
    }

    /// <summary>Find (or create) the app's dedicated folder in the drive root; return its fid. The
    /// transient 转存 copy is saved here so it never litters the drive root; the folder is reused.</summary>
    private async Task<string> EnsureAppFolderAsync(IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        var listUrl = $"{ApiBase}/1/clouddrive/file/sort?{ApiQuery}&pdir_fid=0&_page=1&_size=100&_fetch_total=1&_sort=file_type:asc,updated_at:desc";
        var root = await GetAsync(listUrl, headers, ct).ConfigureAwait(false);
        if (root.GetProperty("data").TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in list.EnumerateArray())
            {
                var isDir = f.TryGetProperty("dir", out var d) && d.GetBoolean();
                var name = f.TryGetProperty("file_name", out var n) ? n.GetString() : null;
                if (isDir && string.Equals(name, AppDriveFolder, StringComparison.Ordinal))
                    return f.GetProperty("fid").GetString()!;
            }
        }
        // Not there — create it (synchronous; returns the new fid).
        var mkBody = JsonSerializer.Serialize(new { pdir_fid = "0", file_name = AppDriveFolder, dir_path = "", dir_init_lock = false });
        var mk = await PostAsync($"{ApiBase}/1/clouddrive/file?{ApiQuery}", mkBody, headers, ct).ConfigureAwait(false);
        var fid = mk.GetProperty("data").TryGetProperty("fid", out var mkFid) ? mkFid.GetString() : null;
        if (string.IsNullOrEmpty(fid))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: could not create the app folder");
        return fid!;
    }

    /// <summary>Poll an async task until status==2 (done); throw on failure/timeout. Returns task data.</summary>
    private async Task<JsonElement> PollTaskAsync(string taskId, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        for (var i = 0; i < 40; i++)
        {
            ct.ThrowIfCancellationRequested();
            var r = await GetAsync($"{ApiBase}/1/clouddrive/task?{ApiQuery}&task_id={Uri.EscapeDataString(taskId)}&retry_index={i}", headers, ct).ConfigureAwait(false);
            var data = r.GetProperty("data");
            var status = data.TryGetProperty("status", out var s) ? s.GetInt32() : -1;
            if (status == 2) return data;
            if (status == 3) throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: task failed");
            await Task.Delay(800, ct).ConfigureAwait(false);
        }
        throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "quark: task did not finish in time");
    }

    /// <summary>DFS for the largest archive (mods ship as one big archive); any file if no archive.
    /// Carries the parent dir fid so the save step can reference the source folder.</summary>
    private async Task<QuarkFile?> FindBestFileAsync(string pwdId, string stoken, string pdirFid,
        IReadOnlyDictionary<string, string> headers, int depth, CancellationToken ct)
    {
        if (depth > 5) return null;
        var list = await ListDirAsync(pwdId, stoken, pdirFid, headers, ct).ConfigureAwait(false);

        QuarkFile? best = null;
        var bestIsArchive = false;
        foreach (var f in list.EnumerateArray())
        {
            var isDir = f.TryGetProperty("dir", out var d) && d.GetBoolean();
            if (isDir)
            {
                var fid = f.TryGetProperty("fid", out var df) ? df.GetString() : null;
                var sub = fid == null ? null : await FindBestFileAsync(pwdId, stoken, fid, headers, depth + 1, ct).ConfigureAwait(false);
                if (sub != null && Prefer(sub, best, bestIsArchive, out var subArchive)) { best = sub; bestIsArchive = subArchive; }
                continue;
            }
            var candidate = new QuarkFile(
                Fid: f.TryGetProperty("fid", out var cf) ? cf.GetString() ?? string.Empty : string.Empty,
                FidToken: f.TryGetProperty("share_fid_token", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                Name: f.TryGetProperty("file_name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                Size: f.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0,
                PdirFid: pdirFid);
            if (string.IsNullOrEmpty(candidate.Fid) || string.IsNullOrEmpty(candidate.FidToken)) continue;
            if (Prefer(candidate, best, bestIsArchive, out var isArchive)) { best = candidate; bestIsArchive = isArchive; }
        }
        return best;
    }

    private static bool Prefer(QuarkFile candidate, QuarkFile? current, bool currentIsArchive, out bool isArchive)
    {
        isArchive = ArchiveExtensions.Any(e => candidate.Name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        if (current == null) return true;
        if (isArchive && !currentIsArchive) return true;
        if (isArchive == currentIsArchive) return candidate.Size > current.Size;
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

    private sealed record QuarkFile(string Fid, string FidToken, string Name, long Size, string PdirFid);

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

    /// <summary>Quark returns 200 with a `code` in the body — non-zero + message = error.</summary>
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
