using System.Text.Json;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Models;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>One file to download + AES-CTR decrypt from a MEGA share (folder tree flattened to a relative path).</summary>
public sealed class MegaFile
{
    public required string RelativePath { get; init; }
    public required string Handle { get; init; }
    public required byte[] AesKey { get; init; }
    public required byte[] Nonce { get; init; }
    public long Size { get; init; }
    /// <summary>Filled by <see cref="IMegaShareResolver.PrepareDownloadAsync"/> (a batched MEGA <c>g</c> call).</summary>
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// Resolves an ANONYMOUS MEGA (mega.nz) share — a FOLDER (mega.nz/folder/…, a directory TREE) OR a single
/// FILE (mega.nz/file/…). A folder is flattened to relative paths so the import rebuilds the mod folder; a
/// file is one encrypted archive. Either way MEGA serves ENCRYPTED bytes, AES-CTR-decrypted with the link
/// key — all crypto in <see cref="MegaCrypto"/> (validated end-to-end, see devtools/mega-probe.mjs). No login.
/// huihui recommends MEGA over Quark ("夸克经常失效"), and uses BOTH folder + file links.
/// </summary>
public interface IMegaShareResolver
{
    /// <summary>Name + total size for the confirm UI (download URLs not resolved yet). Handles folder AND file links.</summary>
    Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default);

    /// <summary>A FOLDER share's files as relative paths + keys, WITH download URLs resolved (ready to fetch+decrypt).</summary>
    Task<IReadOnlyList<MegaFile>> PrepareDownloadAsync(string shareUrl, CancellationToken ct = default);

    /// <summary>A single FILE share resolved to its name + key + a download URL (the `g` API with a PUBLIC
    /// handle). The result is one encrypted archive to fetch + AES-CTR decrypt.</summary>
    Task<MegaFile> PrepareFileAsync(string shareUrl, CancellationToken ct = default);
}

public class MegaShareResolver : IMegaShareResolver
{
    private const string ApiBase = "https://g.api.mega.co.nz";
    private static long _seq = Environment.TickCount64 & 0xFFFFFFF;

    private readonly IRemotePageFetcher _fetcher;

    public MegaShareResolver(IRemotePageFetcher fetcher) => _fetcher = fetcher;

    public async Task<RemoteResolveResult> ResolveAsync(string shareUrl, CancellationToken ct = default)
    {
        // A FILE link (mega.nz/file/…) is a single archive — resolve it directly.
        if (IsFileLink(shareUrl))
        {
            var file = await PrepareFileAsync(shareUrl, ct).ConfigureAwait(false);
            return new RemoteResolveResult { FileName = file.RelativePath, Size = Math.Max(0, file.Size), DownloadUrl = shareUrl };
        }

        var files = await ListFolderAsync(shareUrl, ct).ConfigureAwait(false);
        if (files.Count == 0) throw new OperationException("MEGA_EMPTY_SHARE", "url", shareUrl);
        var total = files.Sum(f => f.Size);
        // A descriptive name for the normalized archive (the mod NAME itself comes from detail.Title).
        var top = files[0].RelativePath.Split('/')[0];
        var name = files.Count == 1 ? files[0].RelativePath : (string.IsNullOrWhiteSpace(top) ? "mega-download" : top);
        return new RemoteResolveResult { FileName = name, Size = Math.Max(0, total), DownloadUrl = shareUrl };
    }

    public async Task<IReadOnlyList<MegaFile>> PrepareDownloadAsync(string shareUrl, CancellationToken ct = default)
    {
        var files = await ListFolderAsync(shareUrl, ct).ConfigureAwait(false);
        if (files.Count == 0) throw new OperationException("MEGA_EMPTY_SHARE", "url", shareUrl);

        // Batch the download-URL (`g`) requests — one API round-trip for the whole tree, aligned to `files`.
        var folderId = ParseFolderLink(shareUrl).FolderId;
        var body = "[" + string.Join(",", files.Select(f => $"{{\"a\":\"g\",\"g\":1,\"n\":\"{f.Handle}\"}}")) + "]";
        var arr = await ApiAsync(folderId, body, ct).ConfigureAwait(false);
        for (var i = 0; i < files.Count && i < arr.GetArrayLength(); i++)
        {
            if (arr[i].ValueKind == JsonValueKind.Object && arr[i].TryGetProperty("g", out var g))
                files[i].DownloadUrl = g.GetString();
        }
        var ready = files.Where(f => !string.IsNullOrEmpty(f.DownloadUrl)).ToList();
        if (ready.Count == 0)
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "MEGA returned no download URLs");
        return ready;
    }

    public async Task<MegaFile> PrepareFileAsync(string shareUrl, CancellationToken ct = default)
    {
        var (handle, key) = ParseFileLink(shareUrl);
        var (aesKey, nonce) = MegaCrypto.UnpackFileKey(key);
        // Single-file `g`: PUBLIC handle `p` (not `n`), top-level (no &n=folder). Reply: {g:url, s:size, at:attrs}.
        var arr = await ApiAsync(null, $"[{{\"a\":\"g\",\"g\":1,\"p\":\"{handle}\"}}]", ct).ConfigureAwait(false);
        var g0 = arr[0];
        if (g0.ValueKind != JsonValueKind.Object)
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "unexpected MEGA file response");
        var url = g0.TryGetProperty("g", out var gg) ? gg.GetString() : null;
        if (string.IsNullOrEmpty(url))
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "MEGA returned no download url");
        var size = g0.TryGetProperty("s", out var ss) ? ss.GetInt64() : 0;
        var at = g0.TryGetProperty("at", out var aa) ? aa.GetString() : null;
        // Attr decrypt gives the real name (MEGA strips it from the link); fall back to the handle.
        var name = MegaCrypto.DecryptAttrName(aesKey, at) ?? handle;
        return new MegaFile
        {
            RelativePath = Sanitize(name),
            Handle = handle,
            AesKey = aesKey,
            Nonce = nonce,
            Size = size,
            DownloadUrl = url,
        };
    }

    // ---- Folder listing + path reconstruction ---------------------------------------------------

    private async Task<List<MegaFile>> ListFolderAsync(string shareUrl, CancellationToken ct)
    {
        var (folderId, folderKey) = ParseFolderLink(shareUrl);
        var arr = await ApiAsync(folderId, "[{\"a\":\"f\",\"c\":1,\"r\":1,\"ca\":1}]", ct).ConfigureAwait(false);
        if (!arr[0].TryGetProperty("f", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "MEGA folder listing empty");

        // Snapshot the nodes (h, p, t=0 file/1 folder, k, a=attrs, s=size).
        var snap = new List<(string H, string? P, int T, string? K, string? A, long S)>();
        foreach (var node in nodes.EnumerateArray())
        {
            var h = node.TryGetProperty("h", out var hh) ? hh.GetString() : null;
            if (string.IsNullOrEmpty(h)) continue;
            snap.Add((h!,
                node.TryGetProperty("p", out var pp) ? pp.GetString() : null,
                node.TryGetProperty("t", out var tt) ? tt.GetInt32() : -1,
                node.TryGetProperty("k", out var kk) ? kk.GetString() : null,
                node.TryGetProperty("a", out var aa) ? aa.GetString() : null,
                node.TryGetProperty("s", out var ss) ? ss.GetInt64() : 0));
        }

        // A node's `k` is `h1:key1/h2:key2/…` — its key encrypted under EACH sharing ancestor. The LINK key
        // is the SHARE ROOT's key (a folder whose parent isn't in the tree); nested nodes are keyed under a
        // SUBFOLDER, not the share id. So seed the root(s) with the link key, then decrypt the folder-key
        // HIERARCHY top-down to a fixed point (a subfolder's key needs its parent decrypted first).
        var handles = snap.Select(n => n.H).ToHashSet();
        var keys = new Dictionary<string, byte[]>();
        foreach (var n in snap)
            if (string.IsNullOrEmpty(n.P) || !handles.Contains(n.P!)) keys[n.H] = folderKey;
        for (var pass = 0; pass < 32; pass++)
        {
            var progressed = false;
            foreach (var n in snap)
            {
                if (n.T != 1 || keys.ContainsKey(n.H)) continue;
                foreach (var (kh, kb) in ParseK(n.K))
                {
                    if (!keys.TryGetValue(kh, out var parentKey)) continue;
                    var enc = TryB64(kb);
                    if (enc is { Length: >= 16 }) { keys[n.H] = MegaCrypto.DecryptEcb(parentKey, enc[..16]); progressed = true; }
                    break;
                }
            }
            if (!progressed) break;
        }

        // Decrypt names + file keys, each under whichever ancestor key its `k` lists.
        var folders = new Dictionary<string, (string Name, string? Parent)>();
        var rawFiles = new List<(string Handle, string? Parent, byte[] AesKey, byte[] Nonce, string Name, long Size)>();
        foreach (var n in snap)
        {
            var pair = ParseK(n.K).FirstOrDefault(p => keys.ContainsKey(p.Handle));
            if (pair.Handle == null) continue;
            var enc = TryB64(pair.Key);
            if (enc == null) continue;
            var parentKey = keys[pair.Handle];
            if (n.T == 1 && enc.Length >= 16) // folder — 16-byte key IS its AES key
            {
                var key = MegaCrypto.DecryptEcb(parentKey, enc[..16]);
                folders[n.H] = (MegaCrypto.DecryptAttrName(key, n.A) ?? n.H, n.P);
            }
            else if (n.T == 0 && enc.Length >= 32) // file — 32-byte key → unpack
            {
                var nodeKey = MegaCrypto.DecryptEcb(parentKey, enc[..32]);
                var (aesKey, nonce) = MegaCrypto.UnpackFileKey(nodeKey);
                rawFiles.Add((n.H, n.P, aesKey, nonce, MegaCrypto.DecryptAttrName(aesKey, n.A) ?? n.H, n.S));
            }
        }

        var result = new List<MegaFile>();
        foreach (var f in rawFiles)
        {
            var segments = new List<string> { Sanitize(f.Name) };
            var parent = f.Parent;
            var guard = 0;
            while (!string.IsNullOrEmpty(parent) && parent != folderId
                   && folders.TryGetValue(parent!, out var fold) && guard++ < 64)
            {
                segments.Insert(0, Sanitize(fold.Name));
                parent = fold.Parent;
            }
            result.Add(new MegaFile
            {
                RelativePath = string.Join('/', segments),
                Handle = f.Handle,
                AesKey = f.AesKey,
                Nonce = f.Nonce,
                Size = f.Size,
            });
        }
        return result;
    }

    /// <summary>Parse a MEGA <c>k</c> field (<c>h1:key1/h2:key2/…</c>) into (ancestorHandle, encKeyB64) pairs.</summary>
    private static List<(string Handle, string Key)> ParseK(string? k)
    {
        var pairs = new List<(string, string)>();
        if (string.IsNullOrEmpty(k)) return pairs;
        foreach (var part in k.Split('/'))
        {
            var i = part.IndexOf(':');
            if (i > 0 && i < part.Length - 1) pairs.Add((part[..i], part[(i + 1)..]));
        }
        return pairs;
    }

    private static byte[]? TryB64(string b64)
    {
        try { return MegaCrypto.Base64UrlDecode(b64); } catch { return null; }
    }

    /// <summary>Strip a path segment to a safe file/dir name (no traversal, no invalid chars).</summary>
    public static string Sanitize(string name)
    {
        var cleaned = name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars()) cleaned = cleaned.Replace(c, '_');
        cleaned = cleaned.Replace("..", "_").Trim('.', ' ');
        return string.IsNullOrEmpty(cleaned) ? "_" : cleaned;
    }

    // ---- MEGA link parse + API ------------------------------------------------------------------

    /// <summary>Parse <c>mega.nz/folder/&lt;id&gt;#&lt;keyB64&gt;</c> → (folderId, 16-byte folder key).</summary>
    public static (string FolderId, byte[] FolderKey) ParseFolderLink(string shareUrl)
    {
        var m = global::System.Text.RegularExpressions.Regex.Match(shareUrl,
            @"mega\.nz/folder/(?<id>[^#/?]+)#(?<key>[^#/?\s]+)");
        if (!m.Success)
            throw new OperationException("MEGA_LINK_UNSUPPORTED", "url", shareUrl);
        byte[] key;
        try { key = MegaCrypto.Base64UrlDecode(m.Groups["key"].Value); }
        catch { throw new OperationException("MEGA_LINK_UNSUPPORTED", "url", shareUrl); }
        if (key.Length != 16)
            throw new OperationException("MEGA_LINK_UNSUPPORTED", "url", shareUrl);
        return (m.Groups["id"].Value, key);
    }

    /// <summary>Parse <c>mega.nz/file/&lt;handle&gt;#&lt;keyB64&gt;</c> → (public handle, 32-byte file key).</summary>
    public static (string Handle, byte[] Key) ParseFileLink(string shareUrl)
    {
        var m = global::System.Text.RegularExpressions.Regex.Match(shareUrl,
            @"mega\.nz/file/(?<id>[^#/?]+)#(?<key>[^#/?\s]+)");
        if (!m.Success)
            throw new OperationException("MEGA_LINK_UNSUPPORTED", "url", shareUrl);
        byte[] key;
        try { key = MegaCrypto.Base64UrlDecode(m.Groups["key"].Value); }
        catch { throw new OperationException("MEGA_LINK_UNSUPPORTED", "url", shareUrl); }
        if (key.Length != 32) // a file key is 32 bytes (→ 16-byte AES key + 8-byte nonce)
            throw new OperationException("MEGA_LINK_UNSUPPORTED", "url", shareUrl);
        return (m.Groups["id"].Value, key);
    }

    /// <summary>True for a mega.nz/file/… link (single file), vs a folder share.</summary>
    public static bool IsFileLink(string shareUrl) =>
        global::System.Text.RegularExpressions.Regex.IsMatch(shareUrl, @"mega\.nz/file/");

    private static long NextSeq() => global::System.Threading.Interlocked.Increment(ref _seq);

    /// <summary>POST a MEGA <c>cs</c> command array → the response array. MEGA replies with a bare negative
    /// number (or <c>[number]</c>) on failure.</summary>
    private async Task<JsonElement> ApiAsync(string? folderId, string body, CancellationToken ct)
    {
        // A file `g` call is top-level (no &n=folder); a folder call scopes to its id.
        var url = folderId == null
            ? $"{ApiBase}/cs?id={NextSeq()}"
            : $"{ApiBase}/cs?id={NextSeq()}&n={folderId}";
        var json = await _fetcher.PostJsonAsync(url, body, ct).ConfigureAwait(false);
        JsonElement root;
        try { root = JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "non-JSON MEGA response"); }
        if (root.ValueKind == JsonValueKind.Number) throw MegaError(root.GetInt32());
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            throw new OperationException("REMOTE_RESOLVE_FAILED", "reason", "unexpected MEGA response");
        if (root[0].ValueKind == JsonValueKind.Number) throw MegaError(root[0].GetInt32());
        return root;
    }

    private static OperationException MegaError(int code) => code switch
    {
        -9 or -11 => new OperationException("MEGA_SHARE_UNAVAILABLE", "code", code.ToString()), // ENOENT / EACCESS
        _ => new OperationException("REMOTE_RESOLVE_FAILED", "reason", $"MEGA error {code}"),
    };
}
