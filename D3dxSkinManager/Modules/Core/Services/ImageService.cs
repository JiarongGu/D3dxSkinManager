using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for image operations
/// </summary>
public interface IImageService
{
    Task<string?> GetThumbnailPathAsync(string sha);
    Task<List<string>> GetPreviewPathsAsync(string sha);
    Task<string?> GenerateThumbnailAsync(string modDirectory, string sha);
    Task<int> GeneratePreviewsAsync(string modDirectory, string sha);
    Task<bool> CacheImageAsync(string sourcePath, string targetPath);
    Task<bool> ResizeImageAsync(string sourcePath, string targetPath, int maxWidth, int maxHeight);
    Task<bool> ClearModCacheAsync(string sha);
    string[] GetSupportedImageExtensions();
    Task<string?> GetImageAsDataUriAsync(string filePath);
    Task<string?> GetThumbnailAsDataUriAsync(string sha);
    Task<List<string>> GetPreviewsAsDataUriAsync(string sha);
}

/// <summary>
/// Service for image operations: thumbnails, caching, resizing
/// Responsibility: Image processing and cache management
/// </summary>
public class ImageService : IImageService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IPathHelper _pathHelper;
    private readonly IPathValidator _pathValidator;
    private readonly ILogHelper _logger;

    // Standard thumbnail size (based on Python version)
    private const int ThumbnailWidth = 200;
    private const int ThumbnailHeight = 400;

    // Preview size
    private const int PreviewWidth = 450;
    private const int PreviewHeight = 900;

    // All web-renderable image formats
    private readonly string[] _supportedExtensions = {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",  // Common raster formats
        ".svg",                                             // Vector format
        ".ico",                                             // Icon format
        ".avif", ".jxl",                                    // Modern formats
        ".apng",                                            // Animated PNG
        ".tif", ".tiff"                                     // TIFF format
    };

    public ImageService(IProfilePathService profilePaths, IPathHelper pathHelper, IPathValidator pathValidator, ILogHelper logger)
    {
        _profilePaths = profilePaths ?? throw new ArgumentNullException(nameof(profilePaths));
        _pathHelper = pathHelper ?? throw new ArgumentNullException(nameof(pathHelper));
        _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure directories exist
        _profilePaths.EnsureDirectoriesExist();
    }

    /// <summary>
    /// Get thumbnail path for a mod (returns file path, will be converted to data URI by facade layer)
    /// Returns relative path for portability
    /// </summary>
    public async Task<string?> GetThumbnailPathAsync(string sha)
    {
        // Check cache first
        foreach (var ext in _supportedExtensions)
        {
            var cachedPath = _profilePaths.GetThumbnailPath(sha, ext);
            if (File.Exists(cachedPath))
            {
                // Return relative path for portability
                var relativePath = _pathHelper.ToRelativePath(cachedPath);
                return await Task.FromResult(relativePath ?? cachedPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Get preview image paths for a mod by scanning the preview folder
    /// Allows users to add preview images directly to previews/{sha}/ folder
    /// Returns relative paths for portability
    /// </summary>
    public async Task<List<string>> GetPreviewPathsAsync(string sha)
    {
        var previewPaths = new List<string>();
        var modPreviewFolder = _profilePaths.GetPreviewDirectoryPath(sha);

        if (!Directory.Exists(modPreviewFolder))
            return await Task.FromResult(previewPaths);

        // Find all preview files in the mod's folder (preview1.png, preview2.png, etc.)
        var previewFiles = Directory.GetFiles(modPreviewFolder, "preview*.*")
            .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f) // Natural sort by filename
            .Select(f => _pathHelper.ToRelativePath(f) ?? f) // Convert to relative paths for portability
            .ToList();

        previewPaths.AddRange(previewFiles);
        return await Task.FromResult(previewPaths);
    }

    /// <summary>
    /// Generate thumbnail from mod directory
    /// </summary>
    public async Task<string?> GenerateThumbnailAsync(string modDirectory, string sha)
    {
        if (!Directory.Exists(modDirectory))
            return null;

        // Look for preview images in mod directory
        var previewFiles = new[] { "preview.png", "preview.jpg", "thumbnail.png", "thumbnail.jpg" };

        foreach (var fileName in previewFiles)
        {
            var sourcePath = Path.Combine(modDirectory, fileName);
            if (File.Exists(sourcePath))
            {
                var targetPath = _profilePaths.GetThumbnailPath(sha, ".png");

                try
                {
                    // Resize to thumbnail size
                    await ResizeImageAsync(sourcePath, targetPath, ThumbnailWidth, ThumbnailHeight);
                    _logger.Info($"Generated thumbnail for {sha}", "ImageService");
                    // Return relative path for portability
                    return _pathHelper.ToRelativePath(targetPath);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to generate thumbnail: {ex.Message}", "ImageService", ex);
                }
            }
        }

        // Search for any image file in mod directory
        var allImages = Directory.GetFiles(modDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f.Length) // Prefer smaller files first
            .ToList();

        if (allImages.Any())
        {
            var sourcePath = allImages.First();
            var targetPath = _profilePaths.GetThumbnailPath(sha, ".png");

            try
            {
                await ResizeImageAsync(sourcePath, targetPath, ThumbnailWidth, ThumbnailHeight);
                _logger.Info($"Generated thumbnail from {Path.GetFileName(sourcePath)}", "ImageService");
                // Return relative path for portability
                return _pathHelper.ToRelativePath(targetPath);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to generate thumbnail: {ex.Message}", "ImageService", ex);
            }
        }

        return null;
    }

    /// <summary>
    /// Generate preview images from mod directory
    /// Searches for multiple preview images and stores them in per-mod folders
    /// Returns the count of previews generated
    /// </summary>
    public async Task<int> GeneratePreviewsAsync(string modDirectory, string sha)
    {
        int previewCount = 0;

        if (!Directory.Exists(modDirectory))
            return previewCount;

        // Create mod-specific preview folder
        var modPreviewFolder = _profilePaths.GetPreviewDirectoryPath(sha);
        Directory.CreateDirectory(modPreviewFolder);

        // Look for preview images in mod directory (preview.png, preview1.png, preview2.png, etc.)
        var previewPatterns = new[] { "preview*.png", "preview*.jpg", "preview*.jpeg" };
        var foundPreviews = new List<string>();

        foreach (var pattern in previewPatterns)
        {
            var files = Directory.GetFiles(modDirectory, pattern, SearchOption.TopDirectoryOnly)
                .OrderBy(f => f)
                .ToList();
            foundPreviews.AddRange(files);
        }

        // Process each preview image
        int previewIndex = 1;
        foreach (var sourcePath in foundPreviews.Distinct())
        {
            var targetPath = Path.Combine(modPreviewFolder, $"preview{previewIndex}.png");

            try
            {
                // Resize to preview size
                await ResizeImageAsync(sourcePath, targetPath, PreviewWidth, PreviewHeight);
                _logger.Info($"Generated preview {previewIndex} for {sha}", "ImageService");
                previewCount++;
                previewIndex++;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to generate preview {previewIndex}: {ex.Message}", "ImageService", ex);
            }
        }

        // If no previews found, look for any image file as fallback
        if (previewCount == 0)
        {
            var allImages = Directory.GetFiles(modDirectory, "*.*", SearchOption.AllDirectories)
                .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => new FileInfo(f).Length) // Prefer smaller files first
                .Take(3) // Take up to 3 images as previews
                .ToList();

            previewIndex = 1;
            foreach (var sourcePath in allImages)
            {
                var targetPath = Path.Combine(modPreviewFolder, $"preview{previewIndex}.png");

                try
                {
                    await ResizeImageAsync(sourcePath, targetPath, PreviewWidth, PreviewHeight);
                    _logger.Info($"Generated preview {previewIndex} from {Path.GetFileName(sourcePath)}", "ImageService");
                    previewCount++;
                    previewIndex++;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to generate preview: {ex.Message}", "ImageService", ex);
                }
            }
        }

        return previewCount;
    }

    /// <summary>
    /// Copy image from mod directory to cache
    /// </summary>
    public async Task<bool> CacheImageAsync(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
            return false;

        try
        {
            var targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir != null && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            File.Copy(sourcePath, targetPath, overwrite: true);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to cache image: {ex.Message}", "ImageService", ex);
            return false;
        }
    }

    /// <summary>
    /// Resize image maintaining aspect ratio
    /// </summary>
    public async Task<bool> ResizeImageAsync(string sourcePath, string targetPath, int maxWidth, int maxHeight)
    {
        if (!File.Exists(sourcePath))
            return false;

        try
        {
            using var sourceImage = Image.FromFile(sourcePath);

            // Calculate new dimensions maintaining aspect ratio
            var ratioX = (double)maxWidth / sourceImage.Width;
            var ratioY = (double)maxHeight / sourceImage.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(sourceImage.Width * ratio);
            var newHeight = (int)(sourceImage.Height * ratio);

            // Create resized image
            using var resizedImage = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(resizedImage))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(sourceImage, 0, 0, newWidth, newHeight);
            }

            // Save as PNG
            var targetDir = Path.GetDirectoryName(targetPath);
            if (targetDir != null && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            resizedImage.Save(targetPath, ImageFormat.Png);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to resize image: {ex.Message}", "ImageService", ex);
            return false;
        }
    }

    /// <summary>
    /// Clear image cache for a specific mod
    /// </summary>
    public async Task<bool> ClearModCacheAsync(string sha)
    {
        var cleared = false;

        // Delete thumbnails
        foreach (var ext in _supportedExtensions)
        {
            var thumbnailPath = _profilePaths.GetThumbnailPath(sha, ext);
            if (File.Exists(thumbnailPath))
            {
                try
                {
                    File.Delete(thumbnailPath);
                    cleared = true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to delete thumbnail: {ex.Message}", "ImageService", ex);
                }
            }
        }

        // Delete preview folder for this mod
        var modPreviewFolder = _profilePaths.GetPreviewDirectoryPath(sha);
        if (Directory.Exists(modPreviewFolder))
        {
            try
            {
                Directory.Delete(modPreviewFolder, recursive: true);
                cleared = true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to delete preview folder: {ex.Message}", "ImageService", ex);
            }
        }

        return await Task.FromResult(cleared);
    }

    /// <summary>
    /// Get all supported image extensions
    /// </summary>
    public string[] GetSupportedImageExtensions()
    {
        return _supportedExtensions;
    }

    /// <summary>
    /// Get image as base64 data URI for web rendering
    /// </summary>
    public async Task<string?> GetImageAsDataUriAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var mimeType = ext switch
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
                _ => "image/png"
            };

            var base64 = Convert.ToBase64String(bytes);
            return $"data:{mimeType};base64,{base64}";
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to convert image to data URI: {ex.Message}", "ImageService", ex);
            return null;
        }
    }

    /// <summary>
    /// Get thumbnail as base64 data URI
    /// </summary>
    public async Task<string?> GetThumbnailAsDataUriAsync(string sha)
    {
        // Check cache first
        foreach (var ext in _supportedExtensions)
        {
            var cachedPath = _profilePaths.GetThumbnailPath(sha, ext);
            if (File.Exists(cachedPath))
            {
                return await GetImageAsDataUriAsync(cachedPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Get all previews as base64 data URIs
    /// </summary>
    public async Task<List<string>> GetPreviewsAsDataUriAsync(string sha)
    {
        var dataUris = new List<string>();
        var previewPaths = await GetPreviewPathsAsync(sha);

        foreach (var previewPath in previewPaths)
        {
            var dataUri = await GetImageAsDataUriAsync(previewPath);
            if (dataUri != null)
            {
                dataUris.Add(dataUri);
            }
        }

        return dataUris;
    }
}
