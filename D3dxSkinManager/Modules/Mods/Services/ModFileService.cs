using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Tools.Models;

using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Services;

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
    Task<bool> LoadAsync(string sha, IProgressReporter? progressReporter = null);
    Task<bool> UnloadAsync(string sha);
    Task<bool> DeleteAsync(string sha, string? previewPath);

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
/// - Archives: {DataDirectory}/mods/{SHA} (no extension - auto-detected by SharpCompress)
/// - Active mods: {WorkDirectory}/Mods/{SHA}/
/// - Disabled mods (cache): {WorkDirectory}/Mods/DISABLED-{SHA}/
/// </summary>
public class ModFileService : IModFileService
{
    private readonly IProfilePathService _profilePaths;
    private readonly IFileService _fileService;
    private readonly Core.Services.IArchiveService _archiveService;
    private readonly IModRepository _repository;
    private readonly ILogHelper _logger;
    private const string DISABLED_PREFIX = "DISABLED-";

    public ModFileService(
        IProfilePathService profilePaths,
        IFileService fileService,
        Core.Services.IArchiveService archiveService,
        IModRepository repository,
        ILogHelper logger)
    {
        _profilePaths = profilePaths ?? throw new ArgumentNullException(nameof(profilePaths));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure directories exist
        _profilePaths.EnsureDirectoriesExist();
    }

    #region Load/Unload Operations

    /// <summary>
    /// Load a mod by extracting its archive to work directory
    /// If mod is cached (disabled), just rename it to enable
    /// Detects and updates archive type if needed
    /// </summary>
    public async Task<bool> LoadAsync(string sha, IProgressReporter? progressReporter = null)
    {
        try
        {
            progressReporter ??= NullProgressReporter.Instance;

            await progressReporter.ReportProgressAsync(0, "Checking archive...");

            var archivePath = GetArchivePath(sha);
            if (!File.Exists(archivePath))
            {
                _logger.Warning($"Archive not found: {archivePath}", "ModFileService");
                await progressReporter.ReportFailureAsync("Archive not found");
                throw new Core.Models.ModException(
                    Core.Models.ErrorCodes.MOD_ARCHIVE_NOT_FOUND,
                    $"Mod archive file not found: {archivePath}",
                    new { sha, archivePath });
            }

            var targetDirectory = Path.Combine(_profilePaths.WorkModsDirectory, sha);
            var disabledDirectory = Path.Combine(_profilePaths.WorkModsDirectory, $"{DISABLED_PREFIX}{sha}");

            // If already extracted as disabled cache, just rename it (remove DISABLED- prefix)
            if (Directory.Exists(disabledDirectory))
            {
                await progressReporter.ReportProgressAsync(50, "Enabling from cache...");

                try
                {
                    Directory.Move(disabledDirectory, targetDirectory);
                    _logger.Info($"Enabled mod from cache: {sha}", "ModFileService");
                    await progressReporter.ReportProgressAsync(100, "Enabled from cache");
                    await progressReporter.ReportCompletionAsync();
                    return true;
                }
                catch (IOException ioEx)
                {
                    // Folder is in use by another process
                    _logger.Error($"Cannot enable mod {sha} - folder is in use: {ioEx.Message}", "ModFileService", ioEx);
                    await progressReporter.ReportFailureAsync("Folder is in use by another process");
                    throw new Core.Models.ModException(
                        Core.Models.ErrorCodes.MOD_FOLDER_IN_USE,
                        $"Cannot enable mod - the folder is currently in use by another process. Please close any programs accessing: {disabledDirectory}",
                        ioEx,
                        new { sha, path = disabledDirectory });
                }
                catch (UnauthorizedAccessException authEx)
                {
                    // Access denied
                    _logger.Error($"Access denied when enabling mod {sha}: {authEx.Message}", "ModFileService", authEx);
                    await progressReporter.ReportFailureAsync("Access denied");
                    throw new Core.Models.ModException(
                        Core.Models.ErrorCodes.FILE_ACCESS_DENIED,
                        $"Access denied when enabling mod. Please run with appropriate permissions.",
                        authEx,
                        new { sha, path = disabledDirectory });
                }
            }

            await progressReporter.ReportProgressAsync(10, "Detecting archive type...");

            // Extract archive using ArchiveService (with type detection)
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, true);
            }

            await progressReporter.ReportProgressAsync(20, "Extracting archive...");

            var extractionResult = await _archiveService.ExtractArchiveAsync(archivePath, targetDirectory);

            if (extractionResult.Success)
            {
                await progressReporter.ReportProgressAsync(80, $"Extracted {extractionResult.FileCount} files");
                _logger.Info($"Loaded mod: {sha} ({extractionResult.FileCount} files)", "ModFileService");

                // Update mod Type in database if detected and different from stored
                if (!string.IsNullOrEmpty(extractionResult.DetectedType))
                {
                    await progressReporter.ReportProgressAsync(90, "Updating metadata...");
                    await UpdateModTypeIfNeededAsync(sha, extractionResult.DetectedType);
                }

                await progressReporter.ReportProgressAsync(100, "Load complete");
                await progressReporter.ReportCompletionAsync();
                return true;
            }
            else
            {
                await progressReporter.ReportFailureAsync("Archive extraction failed");
                throw new Core.Models.ModException(
                    Core.Models.ErrorCodes.MOD_EXTRACTION_FAILED,
                    "Failed to extract mod archive. The file may be corrupted or in an unsupported format.",
                    new { sha, archivePath });
            }
        }
        catch (Core.Models.ModException)
        {
            // Re-throw ModException as-is for proper error handling
            throw;
        }
        catch (Exception ex)
        {
            // Wrap unknown exceptions in ModException
            _logger.Error($"Unexpected error loading mod {sha}: {ex.Message}", "ModFileService", ex);
            await (progressReporter?.ReportFailureAsync(ex.Message) ?? Task.CompletedTask);
            throw new Core.Models.ModException(
                Core.Models.ErrorCodes.UNKNOWN_ERROR,
                $"An unexpected error occurred while loading the mod: {ex.Message}",
                ex,
                new { sha, exceptionType = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Update mod Type field if it's empty or different from detected type
    /// </summary>
    private async Task UpdateModTypeIfNeededAsync(string sha, string detectedType)
    {
        try
        {
            var mod = await _repository.GetByIdAsync(sha);
            if (mod == null)
            {
                return;
            }

            // Normalize both types for comparison (remove dots, lowercase)
            var storedType = (mod.Type ?? "").TrimStart('.').ToLowerInvariant();
            var normalizedDetectedType = detectedType.TrimStart('.').ToLowerInvariant();

            // Update if type is missing or different
            if (string.IsNullOrEmpty(storedType) || storedType != normalizedDetectedType)
            {
                var oldType = mod.Type;
                mod.Type = normalizedDetectedType;
                await _repository.UpdateAsync(mod);

                _logger.Info($"Updated mod type: {sha} ({oldType ?? "empty"} → {normalizedDetectedType})", "ModFileService");
            }
        }
        catch (Exception ex)
        {
            // Don't fail the load operation if type update fails
            _logger.Warning($"Failed to update mod type for {sha}: {ex.Message}", "ModFileService");
        }
    }

    /// <summary>
    /// Unload a mod by renaming its work directory to DISABLED-{SHA} (creates cache)
    /// </summary>
    public async Task<bool> UnloadAsync(string sha)
    {
        try
        {
            var workDirectory = Path.Combine(_profilePaths.WorkModsDirectory, sha);
            if (!Directory.Exists(workDirectory))
            {
                _logger.Warning($"Mod not loaded: {sha}", "ModFileService");
                return false;
            }

            // Rename to DISABLED-{SHA} instead of deleting (creates cache for fast re-enable)
            var disabledDirectory = Path.Combine(_profilePaths.WorkModsDirectory, $"{DISABLED_PREFIX}{sha}");
            if (Directory.Exists(disabledDirectory))
            {
                Directory.Delete(disabledDirectory, true);
            }

            try
            {
                Directory.Move(workDirectory, disabledDirectory);
                _logger.Info($"Unloaded mod (cached): {sha}", "ModFileService");
                return await Task.FromResult(true);
            }
            catch (IOException ioEx)
            {
                // Folder is in use by another process
                _logger.Error($"Cannot unload mod {sha} - folder is in use: {ioEx.Message}", "ModFileService", ioEx);
                throw new Core.Models.ModException(
                    Core.Models.ErrorCodes.MOD_FOLDER_IN_USE,
                    $"Cannot unload mod - the folder is currently in use by another process. Please close any programs accessing: {workDirectory}",
                    ioEx,
                    new { sha, path = workDirectory });
            }
            catch (UnauthorizedAccessException authEx)
            {
                // Access denied
                _logger.Error($"Access denied when unloading mod {sha}: {authEx.Message}", "ModFileService", authEx);
                throw new Core.Models.ModException(
                    Core.Models.ErrorCodes.FILE_ACCESS_DENIED,
                    $"Access denied when unloading mod. Please run with appropriate permissions.",
                    authEx,
                    new { sha, path = workDirectory });
            }
        }
        catch (Core.Models.ModException)
        {
            // Re-throw ModException for proper error handling
            throw;
        }
        catch (Exception ex)
        {
            // Wrap unknown exceptions in ModException
            _logger.Error($"Unexpected error unloading mod {sha}: {ex.Message}", "ModFileService", ex);
            throw new Core.Models.ModException(
                Core.Models.ErrorCodes.UNKNOWN_ERROR,
                $"An unexpected error occurred while unloading the mod: {ex.Message}",
                ex,
                new { sha, exceptionType = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Delete a mod permanently (archive + work directory + cache + images)
    /// </summary>
    public async Task<bool> DeleteAsync(string sha, string? previewPath)
    {
        try
        {
            var deleted = false;

            // Delete archive
            var archivePath = GetArchivePath(sha);
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
                _logger.Info($"Deleted archive: {archivePath}", "ModFileService");
                deleted = true;
            }

            // Delete active work directory
            var workDirectory = Path.Combine(_profilePaths.WorkModsDirectory, sha);
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, true);
                _logger.Info($"Deleted work directory: {workDirectory}", "ModFileService");
                deleted = true;
            }

            // Delete disabled cache directory
            var disabledDirectory = Path.Combine(_profilePaths.WorkModsDirectory, $"{DISABLED_PREFIX}{sha}");
            if (Directory.Exists(disabledDirectory))
            {
                Directory.Delete(disabledDirectory, true);
                _logger.Info($"Deleted cache directory: {disabledDirectory}", "ModFileService");
                deleted = true;
            }

            // Delete preview folder
            if (!string.IsNullOrEmpty(previewPath) && Directory.Exists(previewPath))
            {
                Directory.Delete(previewPath, true);
                _logger.Info($"Deleted preview folder: {previewPath}", "ModFileService");
            }

            return await Task.FromResult(deleted);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deleting mod {sha}: {ex.Message}", "ModFileService", ex);
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
    /// Archives are stored without extensions (like Python version)
    /// </summary>
    public string GetArchivePath(string sha)
    {
        return _profilePaths.GetModArchivePath(sha, "");
    }

    /// <summary>
    /// Copy archive file to mods directory
    /// Stores without extension (like Python version) - SharpCompress auto-detects format
    /// </summary>
    public async Task<string> CopyArchiveAsync(string sourcePath, string sha)
    {
        var targetPath = _profilePaths.GetModArchivePath(sha, "");

        await Task.Run(() => File.Copy(sourcePath, targetPath, overwrite: true));
        _logger.Info($"Copied archive to: {targetPath}", "ModFileService");

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

        if (!Directory.Exists(_profilePaths.WorkModsDirectory))
        {
            return cacheItems;
        }

        // Get all mods from database
        var allMods = await _repository.GetAllAsync();
        var allShas = allMods.Select(m => m.SHA).ToHashSet();
        var loadedShas = (await _repository.GetLoadedIdsAsync()).ToHashSet();

        // Scan for disabled cache directories
        var directories = Directory.GetDirectories(_profilePaths.WorkModsDirectory);

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
            long sizeBytes = FileUtilities.GetDirectorySize(dir);

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
                    _logger.Info($"Deleted cache: {item.Path}", "ModFileService");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting cache {item.Path}: {ex.Message}", "ModFileService", ex);
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
            _logger.Info($"Deleted cache for SHA: {sha}", "ModFileService");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deleting cache for {sha}: {ex.Message}", "ModFileService", ex);
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
        var cachePath = Path.Combine(_profilePaths.WorkModsDirectory, $"{DISABLED_PREFIX}{sha}");
        return Directory.Exists(cachePath) ? cachePath : null;
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
