using System.Collections.Concurrent;
using System.Security.Cryptography;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using Encoding = System.Text.Encoding;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// GLOBAL on-demand cache for remote images behind the <c>app://remote-image/?u=…</c> proxy URLs
/// (remote-library-redesign.md): the frontend just renders the proxy URL — no preload IPC round-trip —
/// and the scheme handler calls <see cref="GetOrFetchAsync"/>, which serves the cached file or
/// downloads it once. NOT profile-scoped (an image URL is the same image whichever profile views it),
/// so the cache is reusable across profiles: {data}/remote-images/{sha1(url)}{ext}.
/// </summary>
public interface IRemoteImageProxy
{
    /// <summary>Absolute path of the cached image, fetching on miss. Null when the fetch fails.</summary>
    Task<string?> GetOrFetchAsync(string remoteUrl);
}

public class RemoteImageProxy : IRemoteImageProxy
{
    private const int MaxConcurrentDownloads = 4;

    private readonly IGlobalPathService _globalPaths;
    private readonly IDownloadService _download;
    private readonly ILogHelper _logger;
    private readonly SemaphoreSlim _gate = new(MaxConcurrentDownloads);

    // Coalesce concurrent fetches of the SAME target file so N callers (e.g. the <img> serve via
    // CustomSchemeHandler + the content-veil check for the same url) share ONE download instead of racing.
    // Keyed by the destination path.
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _inFlight = new();

    public RemoteImageProxy(IGlobalPathService globalPaths, IDownloadService download, ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _download = download;
        _logger = logger;
    }

    private string ImagesDirectory => Path.Combine(_globalPaths.BaseDataPath, "remote-images");

    public Task<string?> GetOrFetchAsync(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl) ||
            !(remoteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
              remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult<string?>(null);

        string fullPath;
        try
        {
            var ext = Path.GetExtension(new Uri(remoteUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
            var name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(remoteUrl))).ToLowerInvariant() + ext;
            fullPath = Path.Combine(ImagesDirectory, name);
        }
        catch (Exception ex)
        {
            _logger.Warn($"[RemoteImageProxy] Bad url {remoteUrl}: {ex.Message}", "RemoteImageProxy");
            return Task.FromResult<string?>(null);
        }

        // Fast path: a COMPLETE cached file (the temp+rename in FetchAsync guarantees File.Exists ⟺ fully
        // written, so this never returns a half-downloaded file to a concurrent reader/veil).
        if (File.Exists(fullPath)) return Task.FromResult<string?>(fullPath);

        return AwaitCoalescedAsync(remoteUrl, fullPath);
    }

    // Concurrent callers for the same target share ONE download. Lazy guarantees the download runs at most
    // once even if ConcurrentDictionary.GetOrAdd invokes the factory more than once under contention.
    private async Task<string?> AwaitCoalescedAsync(string remoteUrl, string fullPath)
    {
        var lazy = _inFlight.GetOrAdd(fullPath, key => new Lazy<Task<string?>>(() => FetchAsync(remoteUrl, key)));
        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(fullPath, out _);
        }
    }

    private async Task<string?> FetchAsync(string remoteUrl, string fullPath)
    {
        try
        {
            Directory.CreateDirectory(ImagesDirectory);
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (File.Exists(fullPath)) return fullPath; // completed while we were queued

                // Download to a UNIQUE temp, then atomically move into place. A concurrent reader / veil
                // never observes a partial file at fullPath — it is either absent or complete. This is the
                // fix for "image load failed on first paint, fine after a hard reload": the old code
                // streamed straight to fullPath, so a concurrent request served the half-written file.
                var tmp = fullPath + "." + Guid.NewGuid().ToString("n") + ".tmp";
                try
                {
                    await _download.DownloadAsync(
                        new DownloadRequest { Url = remoteUrl, DestinationPath = tmp }, null).ConfigureAwait(false);
                    File.Move(tmp, fullPath, overwrite: true);
                }
                catch
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best-effort temp cleanup */ }
                    throw;
                }
                return fullPath;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"[RemoteImageProxy] Fetch failed for {remoteUrl}: {ex.Message}", "RemoteImageProxy");
            return null;
        }
    }
}
