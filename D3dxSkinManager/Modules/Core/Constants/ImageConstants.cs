namespace D3dxSkinManager.Modules.Core.Constants;

/// <summary>
/// Centralized constants for image file handling
/// All image-related file extensions and MIME types are defined here
/// </summary>
public static class ImageConstants
{
    /// <summary>
    /// All supported image file extensions (with leading dot)
    /// </summary>
    public static readonly string[] SupportedExtensions = new[]
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".webp",
        ".svg",
        ".ico"
    };

    /// <summary>
    /// Check if a file extension is a supported image format
    /// </summary>
    /// <param name="extension">File extension (with or without leading dot)</param>
    /// <returns>True if supported image format</returns>
    public static bool IsImageExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return false;

        // Ensure extension has leading dot
        var ext = extension.StartsWith(".") ? extension : "." + extension;

        return Array.Exists(SupportedExtensions, e =>
            e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get MIME type for an image file extension
    /// </summary>
    /// <param name="extension">File extension (with or without leading dot)</param>
    /// <returns>MIME type string, or "application/octet-stream" if not recognized</returns>
    public static string GetMimeType(string extension)
    {
        var ext = extension?.ToLowerInvariant() ?? string.Empty;
        if (!ext.StartsWith("."))
            ext = "." + ext;

        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".avif" => "image/avif",
            ".jxl" => "image/jxl",
            ".apng" => "image/apng",
            ".tif" or ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }
}
