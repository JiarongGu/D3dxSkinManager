using Microsoft.Extensions.Caching.Memory;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
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

    public CustomSchemeHandler(IGlobalPathService globalPathService, ILogHelper logger, PathCache pathCache)
    {
        _globalPathService = globalPathService;
        _logger = logger;
        _pathCache = pathCache;
    }

    /// <summary>
    /// Handles custom app:// scheme requests for serving local files
    /// </summary>
    public Stream HandleRequest(string url, out string contentType)
    {
        contentType = "application/octet-stream";

        try
        {
            _logger.Verbose($"Request: {url}", "CustomScheme");

            // Fast validation: check scheme prefix
            if (!url.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warn($"Invalid scheme: {url}", "CustomScheme");
                contentType = "text/plain";
                return new MemoryStream(_invalidSchemeError.Value);
            }

            // Extract and decode path efficiently
            var encodedPath = url.AsSpan(SchemePrefixLength);
            if (encodedPath.Length == 0)
            {
                _logger.Warn("Empty file path", "CustomScheme");
                contentType = "text/plain";
                return new MemoryStream(_emptyPathError.Value);
            }

            var filePath = WebUtility.UrlDecode(encodedPath.ToString());

            // Try to get cached normalized path or compute it
            // Use IPathCache with size limit (500 entries) for LRU-like behavior
            var cacheKey = CacheKeyPrefix + filePath;
            var absolutePath = _pathCache.GetOrCreate(cacheKey, entry =>
            {
                entry.Size = 1; // Each entry counts as 1 unit toward the 500 limit
                entry.SlidingExpiration = TimeSpan.FromMinutes(30);

                var resolvedPath = Path.IsPathRooted(filePath)
                    ? filePath
                    : Path.Combine(_globalPathService.BaseDataPath, filePath);
                return Path.GetFullPath(resolvedPath);
            });

            // Check if file exists
            if (!File.Exists(absolutePath))
            {
                _logger.Warn($"File not found: {absolutePath}", "CustomScheme");
                contentType = "text/plain";
                return new MemoryStream(_fileNotFoundError.Value);
            }

            // Get cached content type
            contentType = GetCachedContentType(absolutePath);

            _logger.Verbose($"Serving: {absolutePath} ({contentType})", "CustomScheme");

            // For non-images, read into memory to avoid file handle leaks
            // This is safe for a desktop app with local files
            var fileData = File.ReadAllBytes(absolutePath);
            return new MemoryStream(fileData, writable: false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling request: {ex.Message}", "CustomScheme", ex);
            contentType = "text/plain";
            var errorBytes = Encoding.UTF8.GetBytes($"Error: {ex.Message}");
            return new MemoryStream(errorBytes);
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
    /// Clears the path cache - useful if base data path changes
    /// Note: IPathCache doesn't support prefix-based clear, so we rely on automatic expiration
    /// </summary>
    public void ClearPathCache()
    {
        // IPathCache (MemoryCache) doesn't support clearing by prefix
        // Cache entries will expire naturally based on SlidingExpiration (30 minutes)
        // When SizeLimit (500) is reached, LRU entries are automatically evicted
        // This is acceptable since path changes are rare
    }
}
