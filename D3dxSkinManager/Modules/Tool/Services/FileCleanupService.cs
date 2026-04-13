using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Tool.Models;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Service for scanning and cleaning orphaned files (thumbnails, temp, mod caches)
/// </summary>
public interface IFileCleanupService
{
    /// <summary>
    /// Scan for orphaned items in the specified category
    /// </summary>
    Task<OrphanScanResult> ScanOrphansAsync(OrphanCategory category);

    /// <summary>
    /// Delete specified orphaned items
    /// </summary>
    Task<CleanupResult> CleanOrphansAsync(OrphanCategory category, List<string> paths);

    /// <summary>
    /// Scan all categories at once
    /// </summary>
    Task<List<OrphanScanResult>> ScanAllOrphansAsync();
}

/// <summary>
/// Scans profile directories for files/folders not tracked in the database
/// </summary>
public class FileCleanupService : IFileCleanupService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IModRepository _repository;
    private readonly ICategoryService _categoryService;
    private readonly ILogHelper _logger;

    public FileCleanupService(
        IProfilePathService profilePaths,
        IModRepository repository,
        ICategoryService categoryService,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _repository = repository;
        _categoryService = categoryService;
        _logger = logger;
    }

    public async Task<List<OrphanScanResult>> ScanAllOrphansAsync()
    {
        var results = new List<OrphanScanResult>();

        foreach (var category in new[] { OrphanCategory.Thumbnail, OrphanCategory.Preview, OrphanCategory.TempFile, OrphanCategory.ModCache })
        {
            var result = await ScanOrphansAsync(category).ConfigureAwait(false);
            results.Add(result);
        }

        return results;
    }

    public async Task<OrphanScanResult> ScanOrphansAsync(OrphanCategory category)
    {
        return category switch
        {
            OrphanCategory.Thumbnail => await ScanOrphanedThumbnailsAsync().ConfigureAwait(false),
            OrphanCategory.Preview => await ScanOrphanedPreviewsAsync().ConfigureAwait(false),
            OrphanCategory.TempFile => ScanOrphanedTempFiles(),
            OrphanCategory.ModCache => await ScanOrphanedModCachesAsync().ConfigureAwait(false),
            _ => new OrphanScanResult { Category = category }
        };
    }

    public async Task<CleanupResult> CleanOrphansAsync(OrphanCategory category, List<string> paths)
    {
        var result = new CleanupResult { Category = category };

        if (paths == null || paths.Count == 0)
            return result;

        _logger.Info($"Cleaning {paths.Count} orphaned {category} items", "FileCleanupService");

        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var size = FileUtilities.GetDirectorySize(path);
                    Directory.Delete(path, recursive: true);
                    result.DeletedCount++;
                    result.FreedBytes += size;
                }
                else if (File.Exists(path))
                {
                    var size = new FileInfo(path).Length;
                    File.Delete(path);
                    result.DeletedCount++;
                    result.FreedBytes += size;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to delete orphaned item {path}: {ex.Message}", "FileCleanupService");
                result.FailedCount++;
                result.Errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        // Clean up empty sub-directories left after file deletion
        if (category == OrphanCategory.Thumbnail)
        {
            CleanEmptyDirectories(_profilePaths.ThumbnailsDirectory);
        }
        else if (category == OrphanCategory.Preview)
        {
            CleanEmptyDirectories(_profilePaths.PreviewsDirectory);
        }

        _logger.Info($"Cleanup complete: {result.DeletedCount} deleted, {result.FailedCount} failed, {FormatBytes(result.FreedBytes)} freed", "FileCleanupService");
        return result;
    }

    /// <summary>
    /// Scan thumbnails directory for files not referenced by any category.
    /// Thumbnails are used by categories (filename is content hash, not mod ID).
    /// </summary>
    private async Task<OrphanScanResult> ScanOrphanedThumbnailsAsync()
    {
        var result = new OrphanScanResult { Category = OrphanCategory.Thumbnail };
        var thumbnailsDir = _profilePaths.ThumbnailsDirectory;

        if (!Directory.Exists(thumbnailsDir))
            return result;

        var categories = await _categoryService.GetCategoryTreeAsync().ConfigureAwait(false);
        var referencedFiles = CollectReferencedThumbnailFiles(categories);

        var files = Directory.GetFiles(thumbnailsDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (!referencedFiles.Contains(fileName))
            {
                var info = new FileInfo(file);
                result.Items.Add(new OrphanedItem
                {
                    Path = file,
                    Name = fileName,
                    SizeBytes = info.Length,
                    LastModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Category = OrphanCategory.Thumbnail
                });
            }
        }

        _logger.Info($"Found {result.TotalCount} orphaned thumbnail items ({FormatBytes(result.TotalSizeBytes)})", "FileCleanupService");
        return result;
    }

    /// <summary>
    /// Scan previews directory for folders whose mod ID doesn't exist in the database.
    /// Preview folders are named by mod ID.
    /// </summary>
    private async Task<OrphanScanResult> ScanOrphanedPreviewsAsync()
    {
        var result = new OrphanScanResult { Category = OrphanCategory.Preview };
        var previewsDir = _profilePaths.PreviewsDirectory;

        if (!Directory.Exists(previewsDir))
            return result;

        var entities = await _repository.GetAllAsync().ConfigureAwait(false);
        var knownModIds = entities.Select(e => e.Id).ToHashSet();

        var previewDirs = Directory.GetDirectories(previewsDir);
        foreach (var dir in previewDirs)
        {
            var dirName = Path.GetFileName(dir);
            if (!knownModIds.Contains(dirName))
            {
                result.Items.Add(new OrphanedItem
                {
                    Path = dir,
                    Name = dirName,
                    SizeBytes = FileUtilities.GetDirectorySize(dir),
                    LastModified = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm:ss"),
                    Category = OrphanCategory.Preview
                });
            }
        }

        _logger.Info($"Found {result.TotalCount} orphaned preview items ({FormatBytes(result.TotalSizeBytes)})", "FileCleanupService");
        return result;
    }

    /// <summary>
    /// Flatten category tree and collect all referenced thumbnail filenames.
    /// Category.Thumbnail is a relative path like "thumbnails/abc123.png" — extract just the filename.
    /// </summary>
    private static HashSet<string> CollectReferencedThumbnailFiles(List<CategoryInfo> categories)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectThumbnailsRecursive(categories, files);
        return files;
    }

    private static void CollectThumbnailsRecursive(List<CategoryInfo> categories, HashSet<string> files)
    {
        foreach (var category in categories)
        {
            if (!string.IsNullOrEmpty(category.Thumbnail))
            {
                var fileName = Path.GetFileName(category.Thumbnail);
                if (!string.IsNullOrEmpty(fileName))
                    files.Add(fileName);
            }
            if (category.Children.Count > 0)
                CollectThumbnailsRecursive(category.Children, files);
        }
    }

    /// <summary>
    /// Scan temp directory for leftover files from workflows/imports
    /// </summary>
    private OrphanScanResult ScanOrphanedTempFiles()
    {
        var result = new OrphanScanResult { Category = OrphanCategory.TempFile };
        var tempDir = _profilePaths.TempDirectory;

        if (!Directory.Exists(tempDir))
            return result;

        // Scan files
        foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(file);
            result.Items.Add(new OrphanedItem
            {
                Path = file,
                Name = info.Name,
                SizeBytes = info.Length,
                LastModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Category = OrphanCategory.TempFile
            });
        }

        // Scan subdirectories
        foreach (var dir in Directory.GetDirectories(tempDir))
        {
            result.Items.Add(new OrphanedItem
            {
                Path = dir,
                Name = Path.GetFileName(dir),
                SizeBytes = FileUtilities.GetDirectorySize(dir),
                LastModified = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm:ss"),
                Category = OrphanCategory.TempFile
            });
        }

        _logger.Info($"Found {result.TotalCount} temp items ({FormatBytes(result.TotalSizeBytes)})", "FileCleanupService");
        return result;
    }

    /// <summary>
    /// Scan mod cache directories for folders whose ID doesn't exist in the database
    /// Includes both active ({Id}) and disabled (DISABLED-{Id}) caches
    /// </summary>
    private async Task<OrphanScanResult> ScanOrphanedModCachesAsync()
    {
        var result = new OrphanScanResult { Category = OrphanCategory.ModCache };
        var cacheDir = _profilePaths.CacheModsDirectory;

        if (!Directory.Exists(cacheDir))
            return result;

        var entities = await _repository.GetAllAsync().ConfigureAwait(false);
        var knownIds = entities.Select(e => e.Id).ToHashSet();

        const string DISABLED_PREFIX = "DISABLED-";

        foreach (var dir in Directory.GetDirectories(cacheDir))
        {
            var dirName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(dirName))
                continue;

            // Extract the mod ID from directory name
            var modId = dirName.StartsWith(DISABLED_PREFIX)
                ? dirName.Substring(DISABLED_PREFIX.Length)
                : dirName;

            if (!knownIds.Contains(modId))
            {
                result.Items.Add(new OrphanedItem
                {
                    Path = dir,
                    Name = dirName,
                    SizeBytes = FileUtilities.GetDirectorySize(dir),
                    LastModified = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm:ss"),
                    Category = OrphanCategory.ModCache
                });
            }
        }

        _logger.Info($"Found {result.TotalCount} orphaned mod cache items ({FormatBytes(result.TotalSizeBytes)})", "FileCleanupService");
        return result;
    }

    /// <summary>
    /// Recursively delete empty sub-directories (bottom-up). Skips the root directory itself.
    /// </summary>
    private void CleanEmptyDirectories(string rootDir)
    {
        if (!Directory.Exists(rootDir))
            return;

        foreach (var dir in Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(d => d.Length)) // deepest first
        {
            try
            {
                if (Directory.Exists(dir) &&
                    !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                    _logger.Verbose($"Deleted empty directory: {dir}", "FileCleanupService");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to delete empty directory {dir}: {ex.Message}", "FileCleanupService");
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
