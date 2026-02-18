using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Plugins.CacheClearup;

/// <summary>
/// Intelligent cache management system plugin.
/// Port of cache_clearup Python plugin.
///
/// Features:
/// - Categorize caches (invalid, rarely used, frequently used)
/// - Separate cleanup operations for each category
/// - File size calculations
/// - Safe cleanup with validation
/// </summary>
public class CacheClearupPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.cacheclearup";
    public string Name => "Cache Clearup";
    public string Version => "1.0.0";
    public string Description => "Intelligent cache management system";
    public string Author => "D3dxSkinManager";

    public async Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
    }

    public async Task ShutdownAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
    }

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "SCAN_CACHE", "CLEAR_INVALID_CACHE", "CLEAR_RARELY_USED_CACHE", "CLEAR_ALL_CACHE" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "SCAN_CACHE" => await ScanCacheAsync(request),
                "CLEAR_INVALID_CACHE" => await ClearInvalidCacheAsync(request),
                "CLEAR_RARELY_USED_CACHE" => await ClearRarelyUsedCacheAsync(request),
                "CLEAR_ALL_CACHE" => await ClearAllCacheAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> ScanCacheAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var workModsPath = Path.Combine(dataPath, "work_mods");

            if (!Directory.Exists(workModsPath))
            {
                return MessageResponse.CreateSuccess(request.Id, new
                {
                    invalidCaches = new string[0],
                    rarelyUsedCaches = new string[0],
                    frequentlyUsedCaches = new string[0],
                    totalSize = 0L
                });
            }

            var allMods = await _context.ModRepository.GetAllAsync();
            var validShas = new HashSet<string>(allMods.Select(m => m.SHA));

            var cacheDirectories = Directory.GetDirectories(workModsPath);
            var invalidCaches = new List<string>();
            var rarelyUsedCaches = new List<string>();
            var frequentlyUsedCaches = new List<string>();
            long totalSize = 0;

            foreach (var cacheDir in cacheDirectories)
            {
                var dirName = Path.GetFileName(cacheDir);
                var sha = dirName.Replace("DISABLED-", "");

                var dirInfo = new DirectoryInfo(cacheDir);
                var size = GetDirectorySize(dirInfo);
                totalSize += size;

                // Categorize
                if (!validShas.Contains(sha))
                {
                    invalidCaches.Add(dirName);
                }
                else
                {
                    var lastAccess = dirInfo.LastAccessTime;
                    var daysSinceAccess = (DateTime.Now - lastAccess).TotalDays;

                    if (daysSinceAccess > 30)
                        rarelyUsedCaches.Add(dirName);
                    else
                        frequentlyUsedCaches.Add(dirName);
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Scanned cache: {invalidCaches.Count} invalid, {rarelyUsedCaches.Count} rarely used, {frequentlyUsedCaches.Count} frequently used");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                invalidCaches,
                rarelyUsedCaches,
                frequentlyUsedCaches,
                totalSize,
                totalSizeMB = totalSize / (1024 * 1024)
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error scanning cache: {ex.Message}", ex);
            return MessageResponse.CreateError(request.Id, $"Scan failed: {ex.Message}");
        }
    }

    private async Task<MessageResponse> ClearInvalidCacheAsync(MessageRequest request)
    {
        return await ClearCacheByCategoryAsync(request, "invalid");
    }

    private async Task<MessageResponse> ClearRarelyUsedCacheAsync(MessageRequest request)
    {
        return await ClearCacheByCategoryAsync(request, "rarely_used");
    }

    private async Task<MessageResponse> ClearAllCacheAsync(MessageRequest request)
    {
        return await ClearCacheByCategoryAsync(request, "all");
    }

    private async Task<MessageResponse> ClearCacheByCategoryAsync(MessageRequest request, string category)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // First scan to get categories
            var scanResponse = await ScanCacheAsync(request);
            if (!scanResponse.Success)
                return scanResponse;

            var scanData = scanResponse.Data as dynamic;
            List<string> cachesToDelete = new();

            if (category == "invalid")
                cachesToDelete = (scanData?.invalidCaches as IEnumerable<string>)?.ToList() ?? new();
            else if (category == "rarely_used")
                cachesToDelete = (scanData?.rarelyUsedCaches as IEnumerable<string>)?.ToList() ?? new();
            else if (category == "all")
            {
                cachesToDelete = new List<string>();
                cachesToDelete.AddRange((scanData?.invalidCaches as IEnumerable<string>) ?? Array.Empty<string>());
                cachesToDelete.AddRange((scanData?.rarelyUsedCaches as IEnumerable<string>) ?? Array.Empty<string>());
                cachesToDelete.AddRange((scanData?.frequentlyUsedCaches as IEnumerable<string>) ?? Array.Empty<string>());
            }

            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var workModsPath = Path.Combine(dataPath, "work_mods");
            int deletedCount = 0;
            long freedSpace = 0;

            foreach (var cacheName in cachesToDelete)
            {
                var cachePath = Path.Combine(workModsPath, cacheName);
                if (Directory.Exists(cachePath))
                {
                    var size = GetDirectorySize(new DirectoryInfo(cachePath));
                    Directory.Delete(cachePath, true);
                    deletedCount++;
                    freedSpace += size;
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Cleared {deletedCount} {category} caches, freed {freedSpace / (1024 * 1024)}MB");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                deletedCount,
                freedSpace,
                freedSpaceMB = freedSpace / (1024 * 1024),
                category
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error clearing cache: {ex.Message}", ex);
            return MessageResponse.CreateError(request.Id, $"Clear failed: {ex.Message}");
        }
    }

    private long GetDirectorySize(DirectoryInfo directory)
    {
        long size = 0;
        try
        {
            foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }
        }
        catch
        {
            // Ignore access errors
        }
        return size;
    }
}

