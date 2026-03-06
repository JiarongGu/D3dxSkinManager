using System.Drawing.Imaging;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Mod;
using Drawing2D = System.Drawing.Drawing2D;

namespace D3dxSkinManager.Modules.Context.Services;

/// <summary>
/// Interface for image operations
/// </summary>
public interface IImageService
{
    Task<List<string>> GetPreviewPathsAsync(string sha);
    Task<int> GeneratePreviewsAsync(string modDirectory, string sha);
    Task<bool> ClearModCacheAsync(string sha);
    string[] GetSupportedImageExtensions();
    Task<int> ScanAndImportFromCacheAsync(string sha, string cacheDirectory);
    Task<bool> CheckClipboardHasImageAsync();
    Task<bool> ImportPreviewFromClipboardAsync(string sha);
    Task<bool> ImportPreviewImageAsync(string sha, string imagePath);
    Task<bool> SetThumbnailAsync(string sha, string previewPath);
    Task<bool> DeletePreviewAsync(string sha, string previewPath);
    Task<int> TryAutoImportPreviewsFromCacheAsync(string sha);
}

/// <summary>
/// Service for image operations: preview management, caching, import
/// Responsibility: Image processing and cache management
/// </summary>
public class ImageService : IImageService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IPathHelper _pathHelper;
    private readonly ILogHelper _logger;
    private readonly IHashHelper _hashHelper;
    private readonly ICustomSchemeHandler _schemeHandler;
    private readonly IProfileEventBus _eventBus;

    public ImageService(IProfilePathService profilePaths, IPathHelper pathHelper, ILogHelper logger, IHashHelper hashHelper, ICustomSchemeHandler schemeHandler, IProfileEventBus eventBus)
    {
        _profilePaths = profilePaths;
        _pathHelper = pathHelper;
        _logger = logger;
        _hashHelper = hashHelper;
        _schemeHandler = schemeHandler;
        _eventBus = eventBus;
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
            return await Task.FromResult(previewPaths).ConfigureAwait(false);

        // Find all preview files in the mod's folder (preview1.png, preview2.png, etc.)
        var previewFiles = Directory.GetFiles(modPreviewFolder, "preview*.*")
            .Where(f => ImageConstants.IsImageExtension(Path.GetExtension(f)))
            .OrderBy(f => f) // Natural sort by filename
            .Select(f => _pathHelper.ToRelativePath(f) ?? f) // Convert to relative paths for portability
            .ToList();

        previewPaths.AddRange(previewFiles);
        return await Task.FromResult(previewPaths).ConfigureAwait(false);
    }

    /// <summary>
    /// Generate preview images from mod directory
    /// Searches for multiple preview images and copies them to per-mod folders
    /// Preserves original image format and quality
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

        // Look for preview images in mod directory (all supported formats)
        var previewPatterns = ImageConstants.SupportedExtensions.Select(ext => $"preview*{ext}").ToArray();
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
            // Preserve original extension to maintain image format
            var sourceExtension = Path.GetExtension(sourcePath);
            var targetPath = Path.Combine(modPreviewFolder, $"preview{previewIndex}{sourceExtension}");

            try
            {
                // Copy image directly to preserve original format and quality
                File.Copy(sourcePath, targetPath, overwrite: true);
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
            var allImages = Directory.GetFiles(modDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => ImageConstants.IsImageExtension(Path.GetExtension(f)))
                .OrderBy(f => new FileInfo(f).Length) // Prefer smaller files first
                .Take(3) // Take up to 3 images as previews
                .ToList();

            previewIndex = 1;
            foreach (var sourcePath in allImages)
            {
                // Preserve original extension to maintain image format
                var sourceExtension = Path.GetExtension(sourcePath);
                var targetPath = Path.Combine(modPreviewFolder, $"preview{previewIndex}{sourceExtension}");

                try
                {
                    // Copy image directly to preserve original format and quality
                    File.Copy(sourcePath, targetPath, overwrite: true);
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
    /// Clear image cache for a specific mod
    /// </summary>
    public async Task<bool> ClearModCacheAsync(string sha)
    {
        var cleared = false;
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

        return await Task.FromResult(cleared).ConfigureAwait(false);
    }

    /// <summary>
    /// Get all supported image extensions
    /// </summary>
    public string[] GetSupportedImageExtensions()
    {
        return ImageConstants.SupportedExtensions;
    }

    /// <summary>
    /// Scan cache directory for images and import them as previews with SHA-based deduplication
    /// This prevents duplicate images from being added to the preview folder
    /// Returns the count of new images imported
    /// </summary>
    public async Task<int> ScanAndImportFromCacheAsync(string sha, string cacheDirectory)
    {
        int importCount = 0;

        if (!Directory.Exists(cacheDirectory))
        {
            _logger.Debug($"Cache directory does not exist: {cacheDirectory}", "ImageService");
            return importCount;
        }

        var modPreviewFolder = _profilePaths.GetPreviewDirectoryPath(sha);

        // Get existing preview images and calculate their SHA hashes for deduplication
        var existingImageHashes = new HashSet<string>();
        if (Directory.Exists(modPreviewFolder))
        {
            var existingPreviews = Directory.GetFiles(modPreviewFolder, "preview*.*")
                .Where(f => ImageConstants.IsImageExtension(Path.GetExtension(f)))
                .ToList();

            foreach (var existingPreview in existingPreviews)
            {
                try
                {
                    var hash = await _hashHelper.CalculateFileSHA256Async(existingPreview).ConfigureAwait(false);
                    existingImageHashes.Add(hash);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to calculate hash for existing preview {existingPreview}: {ex.Message}", "ImageService");
                }
            }
        }
        else
        {
            Directory.CreateDirectory(modPreviewFolder);
        }

        // Find all images in cache directory (root folder only, not subdirectories)
        var cacheImages = Directory.GetFiles(cacheDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => ImageConstants.IsImageExtension(Path.GetExtension(f)))
            .OrderBy(f => new FileInfo(f).Length) // Prefer smaller files first
            .ToList();

        _logger.Debug($"Found {cacheImages.Count} image(s) in cache directory", "ImageService");

        // Calculate the next preview index
        var existingPreviewCount = Directory.Exists(modPreviewFolder)
            ? Directory.GetFiles(modPreviewFolder, "preview*.*")
                .Where(f => ImageConstants.IsImageExtension(Path.GetExtension(f)))
                .Count()
            : 0;

        int nextIndex = existingPreviewCount + 1;

        // Import images that don't already exist (based on SHA hash)
        foreach (var sourcePath in cacheImages)
        {
            try
            {
                // Calculate hash of the source image
                var imageHash = await _hashHelper.CalculateFileSHA256Async(sourcePath).ConfigureAwait(false);

                // Skip if this image already exists in previews
                if (existingImageHashes.Contains(imageHash))
                {
                    _logger.Debug($"Skipping duplicate image: {Path.GetFileName(sourcePath)}", "ImageService");
                    continue;
                }

                // Copy image directly to preserve original format and quality
                var sourceExtension = Path.GetExtension(sourcePath);
                var targetPath = Path.Combine(modPreviewFolder, $"preview{nextIndex}{sourceExtension}");

                // Simple file copy
                File.Copy(sourcePath, targetPath, overwrite: false);
                _logger.Info($"Imported preview {nextIndex} from cache: {Path.GetFileName(sourcePath)}", "ImageService");

                // Add to existing hashes to prevent duplicates within this import session
                existingImageHashes.Add(imageHash);

                importCount++;
                nextIndex++;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to import image from cache {sourcePath}: {ex.Message}", "ImageService", ex);
            }
        }

        return importCount;
    }

    /// <summary>
    /// Check if the clipboard contains an image
    /// Uses STA thread to access Windows clipboard
    /// </summary>
    public async Task<bool> CheckClipboardHasImageAsync()
    {
        bool hasImage = false;
        Exception? clipboardException = null;

        var thread = new Thread(() =>
        {
            try
            {
                hasImage = Clipboard.ContainsImage();
            }
            catch (Exception ex)
            {
                clipboardException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (clipboardException != null)
        {
            _logger.Error($"Failed to check clipboard: {clipboardException.Message}", "ImageService", clipboardException);
            return await Task.FromResult(false).ConfigureAwait(false);
        }

        return await Task.FromResult(hasImage).ConfigureAwait(false);
    }

    /// <summary>
    /// Import an image from the Windows clipboard directly to the preview folder
    /// Uses STA thread for clipboard access
    /// </summary>
    public async Task<bool> ImportPreviewFromClipboardAsync(string sha)
    {
        // Get existing preview count to determine next filename
        var existingPreviews = await GetPreviewPathsAsync(sha).ConfigureAwait(false);
        int nextIndex = existingPreviews.Count + 1;

        // Get preview directory and ensure it exists
        var previewDirectory = _profilePaths.GetPreviewDirectoryPath(sha);
        if (!Directory.Exists(previewDirectory))
        {
            Directory.CreateDirectory(previewDirectory);
        }

        // Target file path for the clipboard image
        var targetPath = Path.Combine(previewDirectory, $"preview{nextIndex}.png");

        // Clipboard access must be done on STA thread
        bool success = false;
        Exception? clipboardException = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    using var image = Clipboard.GetImage();
                    if (image != null)
                    {
                        // Save directly to preview folder
                        image.Save(targetPath, global::System.Drawing.Imaging.ImageFormat.Png);
                        _logger.Info($"Saved clipboard image directly to preview folder: {targetPath}", "ImageService");
                        success = true;
                    }
                    else
                    {
                        _logger.Warn("Clipboard contains image but GetImage returned null", "ImageService");
                    }
                }
                else
                {
                    _logger.Warn("No image found in clipboard", "ImageService");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to get image from clipboard: {ex.Message}", "ImageService", ex);
                clipboardException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (clipboardException != null)
        {
            throw new InvalidOperationException("Failed to access clipboard", clipboardException);
        }

        if (!success || !File.Exists(targetPath))
        {
            throw new InvalidOperationException("No image found in clipboard or failed to save clipboard image");
        }

        _logger.Info($"Successfully imported preview from clipboard for mod {sha}", "ImageService");

        // Get the newly imported preview path for the event
        var previews = await GetPreviewPathsAsync(sha).ConfigureAwait(false);
        var latestPreview = previews.LastOrDefault();

        // Emit PREVIEW_IMPORTED event
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PREVIEW_IMPORTED, new { sha, imagePath = latestPreview }).ConfigureAwait(false);

        return await Task.FromResult(true).ConfigureAwait(false);
    }

    /// <summary>
    /// Import a preview image from a file path
    /// </summary>
    public async Task<bool> ImportPreviewImageAsync(string sha, string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Image file not found: {imagePath}");
        }

        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        if (!ImageConstants.SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Invalid image format: {extension}. Supported: {string.Join(", ", ImageConstants.SupportedExtensions)}");
        }

        // Get existing preview paths and determine next filename
        var existingPreviews = await GetPreviewPathsAsync(sha).ConfigureAwait(false);
        int nextIndex = existingPreviews.Count + 1;
        var targetFileName = $"preview{nextIndex}{extension}";

        // Get preview directory and ensure it exists
        var targetDirectory = _profilePaths.GetPreviewDirectoryPath(sha);
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var targetPath = Path.Combine(targetDirectory, targetFileName);
        File.Copy(imagePath, targetPath, overwrite: true);

        _logger.Info($"Imported preview image: {imagePath} -> {targetPath}", "ImageService");

        // Emit PREVIEW_IMPORTED event
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PREVIEW_IMPORTED, new { sha, imagePath }).ConfigureAwait(false);

        return await Task.FromResult(true).ConfigureAwait(false);
    }

    /// <summary>
    /// Set a preview image as the thumbnail by reordering it to preview1
    /// Optimized to rename in-place without temp folder
    /// </summary>
    public async Task<bool> SetThumbnailAsync(string sha, string previewPath)
    {
        // Convert to absolute path if needed for file existence check
        var absolutePreviewPath = _pathHelper.ToAbsolutePath(previewPath) ?? previewPath;

        if (!File.Exists(absolutePreviewPath))
        {
            throw new FileNotFoundException($"Preview image not found: {previewPath}");
        }

        // Get preview directory
        var previewDirectory = _profilePaths.GetPreviewDirectoryPath(sha);
        if (!Directory.Exists(previewDirectory))
        {
            throw new DirectoryNotFoundException($"Preview directory not found: {previewDirectory}");
        }

        // Get all preview files sorted alphabetically
        var allPreviews = Directory.GetFiles(previewDirectory, "preview*.*")
            .Where(f => ImageConstants.IsImageExtension(Path.GetExtension(f)))
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        if (allPreviews.Count == 0)
        {
            throw new InvalidOperationException("No preview images found");
        }

        // Find the index of the selected preview
        var selectedIndex = allPreviews.FindIndex(p =>
            Path.GetFullPath(p).Equals(Path.GetFullPath(absolutePreviewPath), StringComparison.OrdinalIgnoreCase));

        if (selectedIndex == -1)
        {
            throw new FileNotFoundException($"Selected preview not found in preview directory: {previewPath}");
        }

        // If already the first preview (thumbnail), no need to reorder
        if (selectedIndex == 0)
        {
            _logger.Info($"Preview is already the thumbnail for mod {sha}", "ImageService");
            return true;
        }

        // Optimized reordering: rename selected to temp name first, then reorder others
        var selectedFile = allPreviews[selectedIndex];
        var selectedExtension = Path.GetExtension(selectedFile);
        var tempName = Path.Combine(previewDirectory, TempFileConstants.GetPreviewReorderTempName(selectedExtension));

        try
        {
            // Step 1: Rename selected file to temp name to free up the naming slot
            File.Move(selectedFile, tempName);
            _logger.Verbose($"Renamed selected to temp: {Path.GetFileName(selectedFile)} -> {Path.GetFileName(tempName)}", "ImageService");

            // Step 2: Rename all files that need to shift down
            // Files before selected stay the same, files after selected shift down by one
            for (int i = selectedIndex - 1; i >= 0; i--)
            {
                var currentFile = allPreviews[i];
                var currentExtension = Path.GetExtension(currentFile);
                var newName = Path.Combine(previewDirectory, $"preview{i + 2}{currentExtension}");

                File.Move(currentFile, newName);
                _logger.Verbose($"Shifted: preview{i + 1} -> preview{i + 2}", "ImageService");
            }

            // Step 3: Rename temp file to preview1 (thumbnail position)
            var finalName = Path.Combine(previewDirectory, $"preview1{selectedExtension}");
            File.Move(tempName, finalName);
            _logger.Info($"Set preview as thumbnail: {Path.GetFileName(selectedFile)} -> preview1", "ImageService");

            _logger.Info($"Reordered {selectedIndex + 1} preview images for mod {sha}", "ImageService");

            // Invalidate CustomSchemeHandler cache for all affected preview images
            // Since we renamed multiple files, we need to invalidate all of them
            var affectedPaths = new List<string>();
            for (int i = 0; i <= selectedIndex; i++)
            {
                // Add both old paths (before rename) and new paths (after rename)
                var oldPath = _pathHelper.ToRelativePath(allPreviews[i]);
                if (oldPath != null)
                {
                    affectedPaths.Add(oldPath);
                }

                // The new paths after reordering
                var extension = Path.GetExtension(allPreviews[i < selectedIndex ? i + 1 : 0]);
                var newPreviewPath = Path.Combine(previewDirectory, $"preview{i + 1}{extension}");
                var newRelativePath = _pathHelper.ToRelativePath(newPreviewPath);
                if (newRelativePath != null)
                {
                    affectedPaths.Add(newRelativePath);
                }
            }
            _schemeHandler.InvalidatePaths(affectedPaths.Distinct());

            // Emit THUMBNAIL_UPDATED event
            await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.THUMBNAIL_UPDATED, new { sha, previewPath }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to reorder preview images: {ex.Message}", "ImageService", ex);

            // Attempt recovery: if temp file exists, try to restore it
            if (File.Exists(tempName))
            {
                try
                {
                    File.Move(tempName, selectedFile);
                    _logger.Info("Restored temp file to original name", "ImageService");
                }
                catch (Exception restoreEx)
                {
                    _logger.Error($"Failed to restore temp file: {restoreEx.Message}", "ImageService", restoreEx);
                }
            }

            throw;
        }

        return await Task.FromResult(true).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete a preview image
    /// </summary>
    public async Task<bool> DeletePreviewAsync(string sha, string previewPath)
    {
        // Convert to absolute path for file operations
        var absolutePreviewPath = _pathHelper.ToAbsolutePath(previewPath) ?? previewPath;

        if (!File.Exists(absolutePreviewPath))
        {
            throw new FileNotFoundException($"Preview image not found: {previewPath}");
        }

        File.Delete(absolutePreviewPath);
        _logger.Info($"Deleted preview image: {absolutePreviewPath}", "ImageService");

        // Invalidate CustomSchemeHandler cache for this image
        // Use relative path since that's what the frontend uses in app:// URLs
        _schemeHandler.InvalidatePath(previewPath);

        // Emit PREVIEW_DELETED event
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.PREVIEW_DELETED, new { sha, previewPath }).ConfigureAwait(false);

        return await Task.FromResult(true).ConfigureAwait(false);
    }

    /// <summary>
    /// Automatically import preview images from cache folder if no previews exist
    /// Checks both active and disabled cache directories
    /// Returns the number of images imported (0 if previews already exist)
    /// </summary>
    public async Task<int> TryAutoImportPreviewsFromCacheAsync(string sha)
    {
        try
        {
            var existingPreviews = await GetPreviewPathsAsync(sha).ConfigureAwait(false);
            if (existingPreviews.Count > 0)
            {
                return 0; // Previews already exist
            }

            // Get cache directory path (both active and disabled cache)
            var cacheDirectory = Path.Combine(_profilePaths.CacheModsDirectory, sha);
            var disabledCacheDirectory = Path.Combine(_profilePaths.CacheModsDirectory, $"DISABLED-{sha}");

            // Try active cache first, then disabled cache
            string? targetDirectory = null;
            if (Directory.Exists(cacheDirectory))
            {
                targetDirectory = cacheDirectory;
            }
            else if (Directory.Exists(disabledCacheDirectory))
            {
                targetDirectory = disabledCacheDirectory;
            }

            if (targetDirectory != null)
            {
                var importCount = await ScanAndImportFromCacheAsync(sha, targetDirectory).ConfigureAwait(false);
                if (importCount > 0)
                {
                    _logger.Info($"Auto-imported {importCount} preview image(s) from cache for mod {sha}", "ImageService");
                }
                return importCount;
            }

            return 0;
        }
        catch (Exception ex)
        {
            // Don't fail if preview import fails, just log and continue
            _logger.Warn($"Failed to auto-import previews from cache: {ex.Message}", "ImageService");
            return 0;
        }
    }
}
