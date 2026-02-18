using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Tools.Models;

namespace D3dxSkinManager.Modules.Tools.Services;

/// <summary>
/// Implementation of cache management service
/// Manages disabled mod caches in the work directory
/// </summary>
public class CacheService : ICacheService
{
        private readonly string _workModsPath;
        private readonly IModRepository _repository;
        private const string DISABLED_PREFIX = "DISABLED-";

        public CacheService(string dataPath, IModRepository repository)
        {
            _workModsPath = Path.Combine(dataPath, "work_mods");
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            // Ensure work_mods directory exists
            if (!Directory.Exists(_workModsPath))
            {
                Directory.CreateDirectory(_workModsPath);
            }
        }

        /// <summary>
        /// Scan cache directories and categorize them
        /// </summary>
        public async Task<List<CacheItem>> ScanCacheAsync()
        {
            var cacheItems = new List<CacheItem>();

            if (!Directory.Exists(_workModsPath))
            {
                return cacheItems;
            }

            // Get all mods from database
            var allMods = await _repository.GetAllAsync();
            var allShas = allMods.Select(m => m.SHA).ToHashSet();
            var loadedShas = (await _repository.GetLoadedIdsAsync()).ToHashSet();

            // Scan for disabled cache directories
            var directories = Directory.GetDirectories(_workModsPath);

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
        /// Get cache statistics
        /// </summary>
        public async Task<CacheStatistics> GetStatisticsAsync()
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
        /// Clean cache by category
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
                        Console.WriteLine($"[CacheService] Deleted cache: {item.Path}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CacheService] Error deleting cache {item.Path}: {ex.Message}");
                }
            }

            return deletedCount;
        }

        /// <summary>
        /// Delete specific cache item by SHA
        /// </summary>
        public Task<bool> DeleteCacheItemAsync(string sha)
        {
            var cachePath = GetCachePath(sha);

            if (string.IsNullOrEmpty(cachePath) || !Directory.Exists(cachePath))
            {
                return Task.FromResult(false);
            }

            try
            {
                Directory.Delete(cachePath, recursive: true);
                Console.WriteLine($"[CacheService] Deleted cache for SHA: {sha}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CacheService] Error deleting cache for {sha}: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Check if a SHA has cached files
        /// </summary>
        public bool HasCache(string sha)
        {
            var cachePath = GetCachePath(sha);
            return !string.IsNullOrEmpty(cachePath) && Directory.Exists(cachePath);
        }

        /// <summary>
        /// Get cache path for a specific SHA
        /// </summary>
        public string? GetCachePath(string sha)
        {
            var cachePath = Path.Combine(_workModsPath, $"{DISABLED_PREFIX}{sha}");
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
                Console.WriteLine($"[CacheService] Error calculating size for {path}: {ex.Message}");
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

            // Frequently used: SHA is currently loaded
            if (loadedShas.Contains(sha))
            {
                return CacheCategory.FrequentlyUsed;
            }

            // Rarely used: SHA exists in database but not loaded
            return CacheCategory.RarelyUsed;
        }
    }
