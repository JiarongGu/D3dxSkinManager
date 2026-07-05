using System.Security.Cryptography;
using System.Text;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// PER-PROFILE cache for remote-site images ({profile}/remote-cache/images/{sha1(url)}{ext}).
/// The grid/detail call ResolveAsync with the page's image URLs: cached files come back as
/// data-relative paths the frontend serves via app:// (like previews — no repeat remote loads);
/// misses are downloaded best-effort (failures fall back to the remote URL). Cleanable via the
/// file-cleanup tool (OrphanCategory.RemoteCache).
/// </summary>
public interface IRemoteImageCacheService
{
    /// <summary>Map url → local data-relative path (app://-servable) or the original url on miss.</summary>
    Task<Dictionary<string, string>> ResolveAsync(IReadOnlyList<string> urls, CancellationToken ct = default);
}

public class RemoteImageCacheService : IRemoteImageCacheService
{
    private const int MaxConcurrentDownloads = 4;

    private readonly IProfilePathService _profilePaths;
    private readonly IGlobalPathService _globalPaths;
    private readonly IDownloadService _download;
    private readonly ILogHelper _logger;
    private readonly SemaphoreSlim _gate = new(MaxConcurrentDownloads);

    public RemoteImageCacheService(
        IProfilePathService profilePaths,
        IGlobalPathService globalPaths,
        IDownloadService download,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _globalPaths = globalPaths;
        _download = download;
        _logger = logger;
    }

    private string ImagesDirectory => Path.Combine(_profilePaths.ProfilePath, "remote-cache", "images");

    public async Task<Dictionary<string, string>> ResolveAsync(IReadOnlyList<string> urls, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ImagesDirectory);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var tasks = urls
            .Where(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .Select(async url =>
            {
                var local = await ResolveOneAsync(url, ct).ConfigureAwait(false);
                lock (result) { result[url] = local ?? url; }
            });
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return result;
    }

    private async Task<string?> ResolveOneAsync(string url, CancellationToken ct)
    {
        try
        {
            var ext = Path.GetExtension(new Uri(url).AbsolutePath);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".jpg";
            var name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant() + ext;
            var fullPath = Path.Combine(ImagesDirectory, name);

            if (!File.Exists(fullPath))
            {
                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (!File.Exists(fullPath)) // double-check under the gate
                    {
                        await _download.DownloadAsync(
                            new DownloadRequest { Url = url, DestinationPath = fullPath }, null, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }

            // Data-relative forward-slashed path — the shape toAppUrl()/CustomSchemeHandler serve.
            return Path.GetRelativePath(_globalPaths.BaseDataPath, fullPath).Replace('\\', '/');
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.Warn($"[Remote] Image cache miss for {url}: {ex.Message}", "RemoteImageCacheService");
            return null; // caller falls back to the remote URL
        }
    }
}
