using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for image operations
/// </summary>
public interface IImageService
{
    Task<string?> GetThumbnailPathAsync(string sha);
    Task<string?> GetPreviewPathAsync(string sha);
    Task<string?> GenerateThumbnailAsync(string modDirectory, string sha);
    Task<bool> CacheImageAsync(string sourcePath, string targetPath);
    Task<bool> ResizeImageAsync(string sourcePath, string targetPath, int maxWidth, int maxHeight);
    Task<bool> ClearModCacheAsync(string sha);
    string[] GetSupportedImageExtensions();
    Task<string?> GetImageAsDataUriAsync(string filePath);
    Task<string?> GetThumbnailAsDataUriAsync(string sha);
    Task<string?> GetPreviewAsDataUriAsync(string sha);
}

/// <summary>
/// Service for image operations: thumbnails, caching, resizing
/// Responsibility: Image processing and cache management
/// </summary>
public class ImageService : IImageService
{
    private readonly string _dataPath;
    private readonly string _thumbnailsPath;
    private readonly string _previewsPath;
        private readonly string _previewScreenPath;

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

        public ImageService(string dataPath)
        {
            _dataPath = dataPath;
            _thumbnailsPath = Path.Combine(dataPath, "thumbnails");
            _previewsPath = Path.Combine(dataPath, "previews");
            _previewScreenPath = Path.Combine(dataPath, "preview_screen");

            // Create directories
            Directory.CreateDirectory(_thumbnailsPath);
            Directory.CreateDirectory(_previewsPath);
            Directory.CreateDirectory(_previewScreenPath);
        }

        /// <summary>
        /// Get thumbnail path for a mod (returns file path, will be converted to data URI by facade layer)
        /// </summary>
        public async Task<string?> GetThumbnailPathAsync(string sha)
        {
            // Check cache first
            foreach (var ext in _supportedExtensions)
            {
                var cachedPath = Path.Combine(_thumbnailsPath, $"{sha}{ext}");
                if (File.Exists(cachedPath))
                    return await Task.FromResult(cachedPath);
            }

            return null;
        }

        /// <summary>
        /// Get preview image path for a mod (returns file path, will be converted to data URI by facade layer)
        /// </summary>
        public async Task<string?> GetPreviewPathAsync(string sha)
        {
            // Check preview_screen first (full size)
            foreach (var ext in _supportedExtensions)
            {
                var screenPath = Path.Combine(_previewScreenPath, $"{sha}{ext}");
                if (File.Exists(screenPath))
                    return await Task.FromResult(screenPath);
            }

            // Check regular previews
            foreach (var ext in _supportedExtensions)
            {
                var previewPath = Path.Combine(_previewsPath, $"{sha}{ext}");
                if (File.Exists(previewPath))
                    return await Task.FromResult(previewPath);
            }

            return null;
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
                    var targetPath = Path.Combine(_thumbnailsPath, $"{sha}.png");

                    try
                    {
                        // Resize to thumbnail size
                        await ResizeImageAsync(sourcePath, targetPath, ThumbnailWidth, ThumbnailHeight);
                        Console.WriteLine($"[Image] Generated thumbnail for {sha}");
                        // Return file path (will be converted to data URI by facade layer)
                        return targetPath;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Image] Failed to generate thumbnail: {ex.Message}");
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
                var targetPath = Path.Combine(_thumbnailsPath, $"{sha}.png");

                try
                {
                    await ResizeImageAsync(sourcePath, targetPath, ThumbnailWidth, ThumbnailHeight);
                    Console.WriteLine($"[Image] Generated thumbnail from {Path.GetFileName(sourcePath)}");
                    // Return file path (will be converted to data URI by facade layer)
                    return targetPath;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Image] Failed to generate thumbnail: {ex.Message}");
                }
            }

            return null;
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
                Console.WriteLine($"[Image] Failed to cache image: {ex.Message}");
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
                Console.WriteLine($"[Image] Failed to resize image: {ex.Message}");
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
                var thumbnailPath = Path.Combine(_thumbnailsPath, $"{sha}{ext}");
                if (File.Exists(thumbnailPath))
                {
                    try
                    {
                        File.Delete(thumbnailPath);
                        cleared = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Image] Failed to delete thumbnail: {ex.Message}");
                    }
                }

                var previewPath = Path.Combine(_previewsPath, $"{sha}{ext}");
                if (File.Exists(previewPath))
                {
                    try
                    {
                        File.Delete(previewPath);
                        cleared = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Image] Failed to delete preview: {ex.Message}");
                    }
                }

                var screenPath = Path.Combine(_previewScreenPath, $"{sha}{ext}");
                if (File.Exists(screenPath))
                {
                    try
                    {
                        File.Delete(screenPath);
                        cleared = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Image] Failed to delete screen preview: {ex.Message}");
                    }
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
            Console.WriteLine($"[Image] Failed to convert image to data URI: {ex.Message}");
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
            var cachedPath = Path.Combine(_thumbnailsPath, $"{sha}{ext}");
            if (File.Exists(cachedPath))
            {
                return await GetImageAsDataUriAsync(cachedPath);
            }
        }

        return null;
    }

    /// <summary>
    /// Get preview as base64 data URI
    /// </summary>
    public async Task<string?> GetPreviewAsDataUriAsync(string sha)
    {
        // Check preview_screen first (full size)
        foreach (var ext in _supportedExtensions)
        {
            var screenPath = Path.Combine(_previewScreenPath, $"{sha}{ext}");
            if (File.Exists(screenPath))
            {
                return await GetImageAsDataUriAsync(screenPath);
            }
        }

        // Check regular previews
        foreach (var ext in _supportedExtensions)
        {
            var previewPath = Path.Combine(_previewsPath, $"{sha}{ext}");
            if (File.Exists(previewPath))
            {
                return await GetImageAsDataUriAsync(previewPath);
            }
        }

        return null;
    }
}
