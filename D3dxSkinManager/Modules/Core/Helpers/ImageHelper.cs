using System.Drawing;
using System.Drawing.Imaging;
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
}
