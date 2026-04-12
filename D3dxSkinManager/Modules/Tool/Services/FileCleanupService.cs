using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Mod.Services;
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
    private readonly ILogHelper _logger;

    public FileCleanupService(
        IProfilePathService profilePaths,
        IModRepository repository,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<OrphanScanResult>> ScanAllOrphansAsync()
    {
        var results = new List<OrphanScanResult>();

        foreach (var category in new[] { OrphanCategory.Thumbnail, OrphanCategory.TempFile, OrphanCategory.ModCache })
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

        _logger.Info($"Cleanup complete: {result.DeletedCount} deleted, {result.FailedCount} failed, {FormatBytes(result.FreedBytes)} freed", "FileCleanupService");
        return result;
    }

    /// <summary>
    /// Scan thumbnails directory for files whose mod ID doesn't exist in the database
    /// </summary>
    private async Task<OrphanScanResult> ScanOrphanedThumbnailsAsync()
    {
        var result = new OrphanScanResult { Category = OrphanCategory.Thumbnail };
        var thumbnailsDir = _profilePaths.ThumbnailsDirectory;

        if (!Directory.Exists(thumbnailsDir))
            return result;

        var entities = await _repository.GetAllAsync().ConfigureAwait(false);
        var knownIds = entities.Select(e => e.Id).ToHashSet();

        var files = Directory.GetFiles(thumbnailsDir);
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!knownIds.Contains(fileName))
            {
                var info = new FileInfo(file);
                result.Items.Add(new OrphanedItem
                {
                    Path = file,
                    Name = Path.GetFileName(file),
                    SizeBytes = info.Length,
                    LastModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Category = OrphanCategory.Thumbnail
                });
            }
        }

        // Also scan previews directory for orphaned mod preview folders
        var previewsDir = _profilePaths.PreviewsDirectory;
        if (Directory.Exists(previewsDir))
        {
            var previewDirs = Directory.GetDirectories(previewsDir);
            foreach (var dir in previewDirs)
            {
                var dirName = Path.GetFileName(dir);
                if (!knownIds.Contains(dirName))
                {
                    result.Items.Add(new OrphanedItem
                    {
                        Path = dir,
                        Name = dirName,
                        SizeBytes = FileUtilities.GetDirectorySize(dir),
                        LastModified = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm:ss"),
                        Category = OrphanCategory.Thumbnail
                    });
                }
            }
        }

        _logger.Info($"Found {result.TotalCount} orphaned thumbnail/preview items ({FormatBytes(result.TotalSizeBytes)})", "FileCleanupService");
        return result;
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

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
