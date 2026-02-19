using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Tools.Models;

using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for mod file operations
/// Orchestrates all file-level operations for mods including:
/// - Load/Unload (extract to work directory, disable/enable)
/// - Import/Export (copy archives, compress/decompress)
/// - Cache management (disabled mod directories)
/// - Batch operations
/// </summary>
public interface IModFileService
{
    // Load/Unload operations
    Task<bool> LoadAsync(string sha);
    Task<bool> UnloadAsync(string sha);
    Task<bool> DeleteAsync(string sha, string? thumbnailPath, string? previewPath);

    // Archive operations
    bool ArchiveExists(string sha);
    string GetArchivePath(string sha);
    Task<string> CopyArchiveAsync(string sourcePath, string sha);

    // Cache management operations
    Task<List<CacheItem>> ScanCacheAsync();
    Task<CacheStatistics> GetCacheStatisticsAsync();
    Task<int> CleanCacheAsync(CacheCategory category);
    Task<bool> DeleteCacheAsync(string sha);
    bool HasCache(string sha);
    string? GetCachePath(string sha);
    long GetDirectorySize(string path);

    // Future: Batch operations
    // Task<BatchResult> LoadBatchAsync(IEnumerable<string> shas);
    // Task<BatchResult> UnloadBatchAsync(IEnumerable<string> shas);
    // Task<string> ExportModAsync(string sha, string targetPath);
    // Task<BatchResult> ExportBatchAsync(IEnumerable<string> shas, string targetDirectory);
}

/// <summary>
/// Service for mod file orchestration
/// Responsibility: High-level mod file operations - import, export, load, unload, cache management
/// Dependencies: FileService for low-level file/archive operations
///
/// Directory structure:
/// - Archives: {DataDirectory}/mods/{SHA}.7z
/// - Active mods: {WorkDirectory}/Mods/{SHA}/
/// - Disabled mods (cache): {WorkDirectory}/Mods/DISABLED-{SHA}/
/// </summary>
public class ModFileService : IModFileService
{
    private readonly string _modsDirectory;
    private readonly string _workModsDirectory;
    private readonly IFileService _fileService;
    private readonly IModRepository _repository;
    private const string DISABLED_PREFIX = "DISABLED-";

    public ModFileService(
        IProfileContext profileContext,
        IFileService fileService,
        IModRepository repository)
    {
        _modsDirectory = Path.Combine(profileContext.ProfilePath, "mods");
        _workModsDirectory = Path.Combine(profileContext.ProfilePath, "work", "Mods");
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

        // Ensure directories exist
        Directory.CreateDirectory(_modsDirectory);
        Directory.CreateDirectory(_workModsDirectory);
    }

    #region Load/Unload Operations

    /// <summary>
    /// Load a mod by extracting its archive to work directory
    /// If mod is cached (disabled), just rename it to enable
    /// </summary>
    public async Task<bool> LoadAsync(string sha)
    {
        try
        {
            var archivePath = GetArchivePath(sha);
            if (!File.Exists(archivePath))
            {
                Console.WriteLine($"[ModFileService] Archive not found: {archivePath}");
                return false;
            }

            var targetDirectory = Path.Combine(_workModsDirectory, sha);
            var disabledDirectory = Path.Combine(_workModsDirectory, $"{DISABLED_PREFIX}{sha}");

            // If already extracted as disabled cache, just rename it (remove DISABLED- prefix)
            if (Directory.Exists(disabledDirectory))
            {
                Directory.Move(disabledDirectory, targetDirectory);
                Console.WriteLine($"[ModFileService] Enabled mod from cache: {sha}");
                return true;
            }

            // Extract archive
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, true);
            }

            var success = await _fileService.ExtractArchiveAsync(archivePath, targetDirectory);
            if (success)
            {
                Console.WriteLine($"[ModFileService] Loaded mod: {sha}");
            }

            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModFileService] Error loading mod {sha}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unload a mod by renaming its work directory to DISABLED-{SHA} (creates cache)
    /// </summary>
    public async Task<bool> UnloadAsync(string sha)
    {
        try
        {
            var workDirectory = Path.Combine(_workModsDirectory, sha);
            if (!Directory.Exists(workDirectory))
            {
                Console.WriteLine($"[ModFileService] Mod not loaded: {sha}");
                return false;
            }

            // Rename to DISABLED-{SHA} instead of deleting (creates cache for fast re-enable)
            var disabledDirectory = Path.Combine(_workModsDirectory, $"{DISABLED_PREFIX}{sha}");
            if (Directory.Exists(disabledDirectory))
            {
                Directory.Delete(disabledDirectory, true);
            }

            Directory.Move(workDirectory, disabledDirectory);
            Console.WriteLine($"[ModFileService] Unloaded mod (cached): {sha}");

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModFileService] Error unloading mod {sha}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete a mod permanently (archive + work directory + cache + images)
    /// </summary>
    public async Task<bool> DeleteAsync(string sha, string? thumbnailPath, string? previewPath)
    {
        try
        {
            var deleted = false;

            // Delete archive
            var archivePath = GetArchivePath(sha);
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
                Console.WriteLine($"[ModFileService] Deleted archive: {archivePath}");
                deleted = true;
            }

            // Delete active work directory
            var workDirectory = Path.Combine(_workModsDirectory, sha);
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, true);
                Console.WriteLine($"[ModFileService] Deleted work directory: {workDirectory}");
                deleted = true;
            }

            // Delete disabled cache directory
            var disabledDirectory = Path.Combine(_workModsDirectory, $"{DISABLED_PREFIX}{sha}");
            if (Directory.Exists(disabledDirectory))
            {
                Directory.Delete(disabledDirectory, true);
                Console.WriteLine($"[ModFileService] Deleted cache directory: {disabledDirectory}");
                deleted = true;
            }

            // Delete thumbnail
            if (!string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
            {
                File.Delete(thumbnailPath);
                Console.WriteLine($"[ModFileService] Deleted thumbnail: {thumbnailPath}");
            }

            // Delete preview folder
            if (!string.IsNullOrEmpty(previewPath) && Directory.Exists(previewPath))
            {
                Directory.Delete(previewPath, true);
                Console.WriteLine($"[ModFileService] Deleted preview folder: {previewPath}");
            }

            return await Task.FromResult(deleted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModFileService] Error deleting mod {sha}: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Archive Operations

    /// <summary>
    /// Check if mod archive exists
    /// </summary>
    public bool ArchiveExists(string sha)
    {
        return File.Exists(GetArchivePath(sha));
    }

    /// <summary>
    /// Get the path to a mod's archive file
    /// Checks for .7z first, then .zip
    /// </summary>
    public string GetArchivePath(string sha)
    {
        var sevenZipPath = Path.Combine(_modsDirectory, $"{sha}.7z");
        if (File.Exists(sevenZipPath))
        {
            return sevenZipPath;
        }

        var zipPath = Path.Combine(_modsDirectory, $"{sha}.zip");
        return zipPath;
    }

    /// <summary>
    /// Copy archive file to mods directory
    /// </summary>
    public async Task<string> CopyArchiveAsync(string sourcePath, string sha)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var targetPath = Path.Combine(_modsDirectory, $"{sha}{extension}");

        await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true));
        Console.WriteLine($"[ModFileService] Copied archive to: {targetPath}");

        return targetPath;
    }

    #endregion

    #region Cache Management Operations

    /// <summary>
    /// Scan for disabled mod caches (DISABLED-{SHA} directories) and categorize them
    /// </summary>
    public async Task<List<CacheItem>> ScanCacheAsync()
    {
        var cacheItems = new List<CacheItem>();

        if (!Directory.Exists(_workModsDirectory))
        {
            return cacheItems;
        }

        // Get all mods from database
        var allMods = await _repository.GetAllAsync();
        var allShas = allMods.Select(m => m.SHA).ToHashSet();
        var loadedShas = (await _repository.GetLoadedIdsAsync()).ToHashSet();

        // Scan for disabled cache directories
        var directories = Directory.GetDirectories(_workModsDirectory);

        foreach (var dir in directories)
        {
            var dirName = Path.GetFileName(dir);

            // Check if directory is a disabled cache
            if (!dirName.StartsWith(DISABLED_PREFIX))
            {
                continue;
            }

            // Extract SHA from directory name
            var sha = dirName.Substring(DISABLED_PREFIX.Length);

            // Calculate directory size
            long sizeBytes = GetDirectorySize(dir);

            // Get last modified time
            var lastModified = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm:ss");

            // Categorize cache
            var category = CategorizCache(sha, allShas, loadedShas);

            cacheItems.Add(new CacheItem
            {
                Path = dir,
                Sha = sha,
                SizeBytes = sizeBytes,
                Category = category,
                LastModified = lastModified
            });
        }

        return cacheItems;
    }

    /// <summary>
    /// Get cache statistics (counts and sizes by category)
    /// </summary>
    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        var cacheItems = await ScanCacheAsync();

        var stats = new CacheStatistics();

        foreach (var item in cacheItems)
        {
            switch (item.Category)
            {
                case CacheCategory.Invalid:
                    stats.InvalidCount++;
                    stats.InvalidSizeBytes += item.SizeBytes;
                    break;
                case CacheCategory.RarelyUsed:
                    stats.RarelyUsedCount++;
                    stats.RarelyUsedSizeBytes += item.SizeBytes;
                    break;
                case CacheCategory.FrequentlyUsed:
                    stats.FrequentlyUsedCount++;
                    stats.FrequentlyUsedSizeBytes += item.SizeBytes;
                    break;
            }
        }

        stats.TotalCount = cacheItems.Count;
        stats.TotalSizeBytes = stats.InvalidSizeBytes + stats.RarelyUsedSizeBytes + stats.FrequentlyUsedSizeBytes;

        return stats;
    }

    /// <summary>
    /// Clean cache by category (delete all caches in the specified category)
    /// </summary>
    public async Task<int> CleanCacheAsync(CacheCategory category)
    {
        var cacheItems = await ScanCacheAsync();
        var itemsToDelete = cacheItems.Where(item => item.Category == category).ToList();

        int deletedCount = 0;

        foreach (var item in itemsToDelete)
        {
            try
            {
                if (Directory.Exists(item.Path))
                {
                    Directory.Delete(item.Path, recursive: true);
                    deletedCount++;
                    Console.WriteLine($"[ModFileService] Deleted cache: {item.Path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ModFileService] Error deleting cache {item.Path}: {ex.Message}");
            }
        }

        return deletedCount;
    }

    /// <summary>
    /// Delete specific cache by SHA
    /// </summary>
    public Task<bool> DeleteCacheAsync(string sha)
    {
        var cachePath = GetCachePath(sha);

        if (string.IsNullOrEmpty(cachePath) || !Directory.Exists(cachePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            Directory.Delete(cachePath, recursive: true);
            Console.WriteLine($"[ModFileService] Deleted cache for SHA: {sha}");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModFileService] Error deleting cache for {sha}: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Check if a SHA has cached files (DISABLED-{SHA} directory exists)
    /// </summary>
    public bool HasCache(string sha)
    {
        var cachePath = GetCachePath(sha);
        return !string.IsNullOrEmpty(cachePath) && Directory.Exists(cachePath);
    }

    /// <summary>
    /// Get cache path for a specific SHA
    /// Returns null if cache doesn't exist
    /// </summary>
    public string? GetCachePath(string sha)
    {
        var cachePath = Path.Combine(_workModsDirectory, $"{DISABLED_PREFIX}{sha}");
        return Directory.Exists(cachePath) ? cachePath : null;
    }

    /// <summary>
    /// Calculate directory size in bytes (recursive)
    /// </summary>
    public long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        long totalSize = 0;

        try
        {
            // Get all files in directory and subdirectories
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                }
                catch
                {
                    // Skip files that can't be accessed
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModFileService] Error calculating size for {path}: {ex.Message}");
        }

        return totalSize;
    }

    /// <summary>
    /// Categorize cache based on SHA presence in database and loaded state
    /// </summary>
    private CacheCategory CategorizCache(string sha, HashSet<string> allShas, HashSet<string> loadedShas)
    {
        // Invalid: SHA not found in database at all
        if (!allShas.Contains(sha))
        {
            return CacheCategory.Invalid;
        }

        // Frequently used: SHA is currently loaded (shouldn't happen, but check anyway)
        if (loadedShas.Contains(sha))
        {
            return CacheCategory.FrequentlyUsed;
        }

        // Rarely used: SHA exists in database but not loaded
        return CacheCategory.RarelyUsed;
    }

    #endregion
}
