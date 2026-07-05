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

    public RemoteImageProxy(IGlobalPathService globalPaths, IDownloadService download, ILogHelper logger)
    {
        _globalPaths = globalPaths;
        _download = download;
        _logger = logger;
    }

    private string ImagesDirectory => Path.Combine(_globalPaths.BaseDataPath, "remote-images");

    public async Task<string?> GetOrFetchAsync(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl) ||
            !(remoteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
              remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            return null;

        try
        {
            var ext = Path.GetExtension(new Uri(remoteUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
            var name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(remoteUrl))).ToLowerInvariant() + ext;
            var fullPath = Path.Combine(ImagesDirectory, name);

            if (File.Exists(fullPath)) return fullPath;

            Directory.CreateDirectory(ImagesDirectory);
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!File.Exists(fullPath)) // double-check under the gate
                {
                    await _download.DownloadAsync(
                        new DownloadRequest { Url = remoteUrl, DestinationPath = fullPath }, null).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.Warn($"[RemoteImageProxy] Fetch failed for {remoteUrl}: {ex.Message}", "RemoteImageProxy");
            return null;
        }
    }
}
