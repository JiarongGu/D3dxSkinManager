using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SystemDrawingImage = System.Drawing.Image;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// Helper for image operations and conversions
/// </summary>
public interface IImageHelper
{
    /// <summary>
    /// Convert an image to PNG format and save it to the target directory
    /// Ensures compatibility with Windows icon conversion
    /// </summary>
    /// <param name="sourcePath">Source image file path</param>
    /// <param name="targetDirectory">Target directory for the converted image</param>
    /// <param name="targetFileName">Target file name (without extension, .png will be added)</param>
    /// <returns>Path to the converted PNG file, or null if conversion failed</returns>
    Task<string?> ConvertToPngAsync(string sourcePath, string targetDirectory, string targetFileName);

    /// <summary>
    /// Return a downscaled copy of <paramref name="sourcePath"/> (≤ maxDimension on the longest side),
    /// cached in <paramref name="cacheDir"/> keyed by source path + mtime + maxDimension. If the source
    /// is already within the bound it is returned unchanged (no work). Synchronous — safe to call from
    /// the app:// scheme handler. Returns null on failure so the caller can fall back to the source.
    /// </summary>
    string? GetOrCreateDownscaled(string sourcePath, int maxDimension, string cacheDir);
}

/// <summary>
/// Helper class for image operations and conversions
/// </summary>
public class ImageHelper : IImageHelper
{
    private readonly ILogHelper _logger;

    public ImageHelper(ILogHelper logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Convert an image to PNG format and save it to the target directory
    /// Uses ImageSharp for loading (supports WEBP, AVIF, etc.) and saves as standard PNG
    /// This ensures compatibility with Windows icon conversion
    /// </summary>
    public async Task<string?> ConvertToPngAsync(string sourcePath, string targetDirectory, string targetFileName)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                _logger.Error($"Source image file not found: {sourcePath}", "ImageHelper");
                return null;
            }

            // Log file details for debugging
            var fileInfo = new FileInfo(sourcePath);
            var extension = fileInfo.Extension.ToLowerInvariant();
            _logger.Info($"Converting image: {Path.GetFileName(sourcePath)} ({fileInfo.Length} bytes, {extension})", "ImageHelper");

            // Ensure target directory exists
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Build target path
            var targetPath = Path.Combine(targetDirectory, $"{targetFileName}.png");

            // Use ImageSharp to load the image (supports WEBP, AVIF, and many other formats)
            using var image = await ImageSharpImage.LoadAsync<Rgba32>(sourcePath);
            _logger.Info($"Image loaded successfully with ImageSharp: {image.Width}x{image.Height}, Format: {extension}", "ImageHelper");

            // Save as PNG using ImageSharp
            var encoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder
            {
                CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
                ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
            };

            await image.SaveAsPngAsync(targetPath, encoder);
            _logger.Info($"Successfully converted to PNG: {targetFileName}.png ({image.Width}x{image.Height})", "ImageHelper");

            return targetPath;
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.Error($"Unsupported or corrupted image format: {ex.Message}", "ImageHelper", ex);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to convert image to PNG from '{sourcePath}': {ex.GetType().Name}: {ex.Message}", "ImageHelper", ex);
            return null;
        }
    }

    public string? GetOrCreateDownscaled(string sourcePath, int maxDimension, string cacheDir)
    {
        try
        {
            if (!File.Exists(sourcePath)) return null;

            // Cache key: source path + mtime + size bound. mtime invalidates when the thumbnail changes.
            var mtime = File.GetLastWriteTimeUtc(sourcePath).Ticks;
            var keyBytes = global::System.Security.Cryptography.SHA1.HashData(
                Encoding.UTF8.GetBytes($"{sourcePath}|{mtime}|{maxDimension}"));
            var key = Convert.ToHexString(keyBytes);
            var cachePath = Path.Combine(cacheDir, $"{key}.png");

            if (File.Exists(cachePath)) return cachePath; // already downscaled this exact source

            using var image = ImageSharpImage.Load<Rgba32>(sourcePath);
            // Already small enough → serve the source as-is (avoid a pointless re-encode).
            if (image.Width <= maxDimension && image.Height <= maxDimension) return sourcePath;

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(maxDimension, maxDimension),
                Mode = ResizeMode.Max, // fit within the box, preserve aspect ratio
            }));

            Directory.CreateDirectory(cacheDir);
            // Encode to a temp file then move, so a concurrent reader never sees a half-written PNG.
            var tempPath = cachePath + ".tmp";
            image.SaveAsPng(tempPath);
            try { File.Move(tempPath, cachePath, overwrite: true); }
            catch { /* another thread won the race */ try { File.Delete(tempPath); } catch { } }

            return File.Exists(cachePath) ? cachePath : sourcePath;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Downscale failed for '{sourcePath}': {ex.Message}", "ImageHelper");
            return null;
        }
    }
}
