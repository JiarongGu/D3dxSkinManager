using System;
using System.IO;
using System.Net;

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
/// Serves local files (images, etc.) through Photino's custom scheme handler
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
    private readonly string _dataPath;
    private readonly ILogHelper _logger;

    public CustomSchemeHandler(string dataPath, ILogHelper logger)
    {
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles custom app:// scheme requests for serving local files
    /// </summary>
    public Stream HandleRequest(string url, out string contentType)
    {
        contentType = "application/octet-stream";

        try
        {
            _logger.Info($"Request: {url}", "CustomScheme");

            // Extract the file path from the URL
            // URL format: app://encoded_file_path
            if (!url.StartsWith("app://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning($"Invalid scheme: {url}", "CustomScheme");
                contentType = "text/plain";
                return CreateErrorStream("Invalid scheme");
            }

            var encodedPath = url.Substring(6); // Remove "app://"
            var filePath = WebUtility.UrlDecode(encodedPath);

            if (string.IsNullOrEmpty(filePath))
            {
                _logger.Warning("Empty file path", "CustomScheme");
                contentType = "text/plain";
                return CreateErrorStream("Empty file path");
            }

            // Convert relative path to absolute path
            var absolutePath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(_dataPath, filePath);

            // Normalize path
            absolutePath = Path.GetFullPath(absolutePath);

            // Check if file exists
            if (!File.Exists(absolutePath))
            {
                _logger.Warning($"File not found: {absolutePath}", "CustomScheme");
                contentType = "text/plain";
                return CreateErrorStream("File not found");
            }

            // Determine content type from file extension
            contentType = GetContentType(absolutePath);

            _logger.Info($"Serving: {absolutePath} ({contentType})", "CustomScheme");

            // Read and return file stream
            return new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling request: {ex.Message}", "CustomScheme", ex);
            contentType = "text/plain";
            return CreateErrorStream($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines the MIME content type from file extension
    /// </summary>
    private static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
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
        };
    }

    /// <summary>
    /// Creates a memory stream with an error message
    /// </summary>
    private static Stream CreateErrorStream(string message)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);
        return new MemoryStream(bytes);
    }
}
