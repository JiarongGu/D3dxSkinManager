using Microsoft.Extensions.Caching.Memory;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;
using System.Collections.Concurrent;
using System.Net;
using Encoding = System.Text.Encoding;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for handling custom URL scheme requests
/// </summary>
public interface ICustomSchemeHandler
{
    /// <summary>
    /// Handles custom app:// scheme requests for serving local files
    /// </summary>
    /// <param name="url">The full URL (e.g., app://encoded_path)</param>
    /// <param name="contentType">Output parameter for the content type</param>
    /// <returns>Stream containing the file data</returns>
    Stream HandleRequest(string url, out string contentType);

    /// <summary>
    /// Async variant: resolves the app:// url and reads the file bytes with async I/O (no thread held
    /// during the read). Returns the bytes + content type. Never throws — returns a small error payload
    /// (text/plain) on failure so the caller can serve a 200/placeholder without special-casing.
    /// </summary>
    Task<(byte[] Data, string ContentType)> HandleRequestBytesAsync(string url);

    /// <summary>
    /// Invalidate cache for a specific file path
    /// </summary>
    /// <param name="filePath">Absolute or relative file path to invalidate</param>
    void InvalidatePath(string filePath);

    /// <summary>
    /// Invalidate cache for multiple file paths
    /// </summary>
    /// <param name="filePaths">Collection of file paths to invalidate</param>
    void InvalidatePaths(IEnumerable<string> filePaths);
}

/// <summary>
/// Service for handling custom app:// scheme requests
/// Serves local files (images, etc.) through WebView2's custom scheme handler
///
/// URL Format: app://encoded_file_path
/// Examples:
/// - app://profiles%2F123%2Fthumbnails%2Fabc.png (relative to data folder)
/// - app://C%3A%5CUsers%5Cuser%5CPictures%5Cimage.png (absolute path)
///
/// Responsibilities:
/// - Parse and validate app:// URLs
/// - Decode file paths from URLs
/// - Serve files from any accessible location on the filesystem
/// - Return appropriate content types for different file extensions
///
/// Note: No directory restrictions since this is a desktop app with local-only access
/// </summary>
public class CustomSchemeHandler : ICustomSchemeHandler
{
    private readonly IGlobalPathService _globalPathService;
    private readonly ILogHelper _logger;
    private readonly PathCache _pathCache;

    // Cache for content types - static since extensions don't change
    private static readonly ConcurrentDictionary<string, string> _contentTypeCache = new();

    // Pre-allocated error streams to avoid repeated allocations
    private static readonly Lazy<byte[]> _invalidSchemeError = new(() => Encoding.UTF8.GetBytes("Invalid scheme"));
    private static readonly Lazy<byte[]> _emptyPathError = new(() => Encoding.UTF8.GetBytes("Empty file path"));
    private static readonly Lazy<byte[]> _fileNotFoundError = new(() => Encoding.UTF8.GetBytes("File not found"));

    // Constants
    private const string SchemePrefix = "app://";
    private const int SchemePrefixLength = 6; // Length of "app://"
    private const string CacheKeyPrefix = "NormalizedPath_";
    /// <summary>Dedicated proxy scheme for remote images: proxy://image/?u=&lt;urlencoded&gt; — fetched
    /// on demand into the GLOBAL cache by <see cref="IRemoteImageProxy"/> (no frontend preload).
    /// Its own scheme so the URL states the contract: app:// = local file, proxy:// = remote-via-cache.</summary>
    private const string RemoteImagePrefix = "proxy://image/";

    private readonly IRemoteImageProxy _remoteImageProxy;

    public CustomSchemeHandler(IGlobalPathService globalPathService, ILogHelper logger, PathCache pathCache,
        IRemoteImageProxy remoteImageProxy)
    {
        _globalPathService = globalPathService;
        _logger = logger;
        _pathCache = pathCache;
        _remoteImageProxy = remoteImageProxy;
    }

    /// <summary>
    /// Handles custom app:// scheme requests for serving local files
    /// </summary>
    public Stream HandleRequest(string url, out string contentType)
    {
        if (url.StartsWith(RemoteImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Rare sync path — the deferred async route below is the normal server.
            var (data, proxyCt) = HandleRemoteImageAsync(url).GetAwaiter().GetResult();
            contentType = proxyCt;
            return new MemoryStream(data, writable: false);
        }

        var (absolutePath, ct, errorBytes) = ResolveRequest(url);
        contentType = ct;
        if (errorBytes != null) return new MemoryStream(errorBytes);
        try
        {
            // Read into memory to avoid file handle leaks. Safe for a desktop app with local files.
            return new MemoryStream(File.ReadAllBytes(absolutePath!), writable: false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error reading request: {ex.Message}", "CustomScheme", ex);
            contentType = "text/plain";
            return new MemoryStream(Encoding.UTF8.GetBytes($"Error: {ex.Message}"));
        }
    }

    public async Task<(byte[] Data, string ContentType)> HandleRequestBytesAsync(string url)
    {
        if (url.StartsWith(RemoteImagePrefix, StringComparison.OrdinalIgnoreCase))
            return await HandleRemoteImageAsync(url).ConfigureAwait(false);

        var (absolutePath, ct, errorBytes) = ResolveRequest(url);
        if (errorBytes != null) return (errorBytes, ct);
        try
        {
            // Async I/O: no thread-pool thread is held during the read, so a burst of thumbnail
            // requests doesn't stall on thread-pool ramp-up.
            var data = await File.ReadAllBytesAsync(absolutePath!).ConfigureAwait(false);
            return (data, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error reading request: {ex.Message}", "CustomScheme", ex);
            return (Encoding.UTF8.GetBytes($"Error: {ex.Message}"), "text/plain");
        }
    }

    /// <summary>Serve a remote image through the global on-demand cache (fetches on miss).</summary>
    private async Task<(byte[] Data, string ContentType)> HandleRemoteImageAsync(string url)
    {
        try
        {
            var queryIndex = url.IndexOf("u=", StringComparison.OrdinalIgnoreCase);
            var remoteUrl = queryIndex < 0 ? null : WebUtility.UrlDecode(url[(queryIndex + 2)..]);
            var path = string.IsNullOrWhiteSpace(remoteUrl)
                ? null
                : await _remoteImageProxy.GetOrFetchAsync(remoteUrl!).ConfigureAwait(false);
            if (path == null) return (_fileNotFoundError.Value, "text/plain");
            var data = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            return (data, GetCachedContentType(path));
        }
        catch (Exception ex)
        {
            _logger.Error($"Remote image proxy error: {ex.Message}", "CustomScheme", ex);
            return (Encoding.UTF8.GetBytes($"Error: {ex.Message}"), "text/plain");
        }
    }

    /// <summary>
    /// Resolve an app:// url to an absolute file path + content type. Returns <c>errorBytes</c> (non-null)
    /// instead of a path when the url is invalid / empty / not found, so callers serve that payload.
    /// </summary>
    private (string? AbsolutePath, string ContentType, byte[]? ErrorBytes) ResolveRequest(string url)
    {
        try
        {
            _logger.Verbose($"Request: {url}", "CustomScheme");

            if (!url.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warn($"Invalid scheme: {url}", "CustomScheme");
                return (null, "text/plain", _invalidSchemeError.Value);
            }

            var encodedPath = url.AsSpan(SchemePrefixLength);
            if (encodedPath.Length == 0)
            {
                _logger.Warn("Empty file path", "CustomScheme");
                return (null, "text/plain", _emptyPathError.Value);
            }

            // Strip query parameters (e.g., ?t=1234567890) used for cache busting
            var queryIndex = encodedPath.IndexOf('?');
            if (queryIndex >= 0) encodedPath = encodedPath.Slice(0, queryIndex);

            var filePath = WebUtility.UrlDecode(encodedPath.ToString());

            // Cached normalized path (IPathCache, 500-entry LRU-like)
            var cacheKey = CacheKeyPrefix + filePath;
            var absolutePath = _pathCache.GetOrCreate(cacheKey, entry =>
            {
                entry.Size = 1;
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);
                var resolvedPath = Path.IsPathRooted(filePath)
                    ? filePath
                    : Path.Combine(_globalPathService.BaseDataPath, filePath);
                return Path.GetFullPath(resolvedPath);
            });

            if (!File.Exists(absolutePath))
            {
                _logger.Warn($"File not found: {absolutePath}", "CustomScheme");
                return (null, "text/plain", _fileNotFoundError.Value);
            }

            var contentType = GetCachedContentType(absolutePath);
            _logger.Verbose($"Serving: {absolutePath} ({contentType})", "CustomScheme");
            return (absolutePath, contentType, null);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling request: {ex.Message}", "CustomScheme", ex);
            return (null, "text/plain", Encoding.UTF8.GetBytes($"Error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets content type with caching to avoid repeated lookups
    /// </summary>
    private string GetCachedContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
        return _contentTypeCache.GetOrAdd(extension, ImageConstants.GetMimeType);
    }

    /// <summary>
    /// Invalidate cache for a specific file path
    /// Used when files are modified, deleted, or renamed to ensure fresh content
    /// </summary>
    public void InvalidatePath(string filePath)
    {
        var cacheKey = CacheKeyPrefix + filePath;
        _pathCache.Remove(cacheKey);
        _logger.Verbose($"Invalidated cache for: {filePath}", "CustomScheme");
    }

    /// <summary>
    /// Invalidate cache for multiple file paths
    /// More efficient than calling InvalidatePath multiple times
    /// </summary>
    public void InvalidatePaths(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths)
        {
            var cacheKey = CacheKeyPrefix + filePath;
            _pathCache.Remove(cacheKey);
        }
        _logger.Verbose($"Invalidated cache for {filePaths.Count()} paths", "CustomScheme");
    }
}
