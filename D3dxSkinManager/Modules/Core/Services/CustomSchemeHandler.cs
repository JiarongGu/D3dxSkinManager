using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
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

    // Cache for content types - static since extensions don't change
    private static readonly ConcurrentDictionary<string, string> _contentTypeCache = new();

    // LRU cache for normalized paths with size limit
    private readonly LruCache<string, string> _normalizedPathCache;

    // Pre-allocated error streams to avoid repeated allocations
    private static readonly Lazy<byte[]> _invalidSchemeError = new(() => Encoding.UTF8.GetBytes("Invalid scheme"));
    private static readonly Lazy<byte[]> _emptyPathError = new(() => Encoding.UTF8.GetBytes("Empty file path"));
    private static readonly Lazy<byte[]> _fileNotFoundError = new(() => Encoding.UTF8.GetBytes("File not found"));

    // Constants
    private const string SchemePrefix = "app://";
    private const int SchemePrefixLength = 6; // Length of "app://"
    private const int FileStreamBufferSize = 4096; // Optimal buffer size for file streaming
    private const int MaxPathCacheSize = 500; // Maximum number of cached paths

    public CustomSchemeHandler(IGlobalPathService globalPathService, ILogHelper logger)
    {
        _globalPathService = globalPathService;
        _logger = logger;
        _normalizedPathCache = new LruCache<string, string>(MaxPathCacheSize);
    }

    /// <summary>
    /// Handles custom app:// scheme requests for serving local files
    /// </summary>
    public Stream HandleRequest(string url, out string contentType)
    {
        contentType = "application/octet-stream";

        try
        {
            // Only log in debug mode or for errors
#if DEBUG
            _logger.Info($"Request: {url}", "CustomScheme");
#endif

            // Fast validation: check scheme prefix
            if (!url.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning($"Invalid scheme: {url}", "CustomScheme");
                contentType = "text/plain";
                return new MemoryStream(_invalidSchemeError.Value);
            }

            // Extract and decode path efficiently
            var encodedPath = url.AsSpan(SchemePrefixLength);
            if (encodedPath.Length == 0)
            {
                _logger.Warning("Empty file path", "CustomScheme");
                contentType = "text/plain";
                return new MemoryStream(_emptyPathError.Value);
            }

            var filePath = WebUtility.UrlDecode(encodedPath.ToString());

            // Try to get cached normalized path or compute it
            var absolutePath = _normalizedPathCache.GetOrAdd(filePath, path =>
            {
                var resolvedPath = Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(_globalPathService.BaseDataPath, path);
                return Path.GetFullPath(resolvedPath);
            });

            // Check if file exists
            if (!File.Exists(absolutePath))
            {
                _logger.Warning($"File not found: {absolutePath}", "CustomScheme");
                contentType = "text/plain";
                return new MemoryStream(_fileNotFoundError.Value);
            }

            // Get cached content type
            contentType = GetCachedContentType(absolutePath);

#if DEBUG
            _logger.Info($"Serving: {absolutePath} ({contentType})", "CustomScheme");
#endif

            // Return optimized file stream with buffer
            return new FileStream(
                absolutePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: FileStreamBufferSize,
                useAsync: false); // Sync is faster for local files
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

        return _contentTypeCache.GetOrAdd(extension, ext => ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".avif" => "image/avif",
            ".tif" or ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        });
    }

    /// <summary>
    /// Clears the path cache - useful if base data path changes
    /// </summary>
    public void ClearPathCache()
    {
        _normalizedPathCache.Clear();
    }
}
