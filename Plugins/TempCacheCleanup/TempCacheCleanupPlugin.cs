using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;


namespace D3dxSkinManager.Plugins.TempCacheCleanup;

/// <summary>
/// Plugin to clean temporary cache files (mod zips and preview images).
/// Port of temp_cache_clearup Python plugin.
///
/// Features:
/// - Scans cache directory for temporary mod files (.7z, .zip, .rar)
/// - Scans cache directory for temporary preview images (.png, .jpg)
/// - Separate cleanup operations for mods and previews
/// - File size calculations
/// </summary>
public class TempCacheCleanupPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;
    private static readonly string[] ModSuffixes = { ".7z", ".zip", ".rar" };
    private static readonly string[] PreviewSuffixes = { ".png", ".jpg" };

    public string Id => "com.d3dxskinmanager.tempcachecleanup";
    public string Name => "Temp Cache Cleanup";
    public string Version => "1.0.0";
    public string Description => "Cleans temporary cache files (mod zips and preview images)";
    public string Author => "D3dxSkinManager";

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "SCAN_TEMP_CACHE", "CLEAN_TEMP_MODS", "CLEAN_TEMP_PREVIEWS" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "SCAN_TEMP_CACHE" => await ScanTempCacheAsync(request),
                "CLEAN_TEMP_MODS" => await CleanTempModsAsync(request),
                "CLEAN_TEMP_PREVIEWS" => await CleanTempPreviewsAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            _context?.Log(LogLevel.Error, $"[{Name}] Error handling message", ex);
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> ScanTempCacheAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var cachePath = Path.Combine(dataPath, "resources", "cache");

            if (!Directory.Exists(cachePath))
            {
                return MessageResponse.CreateSuccess(request.Id, new
                {
                    modFiles = new string[0],
                    modCount = 0,
                    modSize = 0L,
                    previewFiles = new string[0],
                    previewCount = 0,
                    previewSize = 0L
                });
            }

            var modFiles = new List<string>();
            var previewFiles = new List<string>();
            long modSize = 0;
            long previewSize = 0;

            foreach (var filePath in Directory.GetFiles(cachePath))
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var fileInfo = new FileInfo(filePath);

                if (ModSuffixes.Contains(extension))
                {
                    modFiles.Add(filePath);
                    modSize += fileInfo.Length;
                }
                else if (PreviewSuffixes.Contains(extension))
                {
                    previewFiles.Add(filePath);
                    previewSize += fileInfo.Length;
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Scanned cache: {modFiles.Count} mod files ({modSize / (1024 * 1024)}MB), {previewFiles.Count} preview files ({previewSize / (1024 * 1024)}MB)");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                modFiles = modFiles.ToArray(),
                modCount = modFiles.Count,
                modSize,
                modSizeMB = modSize / (1024.0 * 1024.0),
                previewFiles = previewFiles.ToArray(),
                previewCount = previewFiles.Count,
                previewSize,
                previewSizeMB = previewSize / (1024.0 * 1024.0)
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error scanning cache", ex);
            return MessageResponse.CreateError(request.Id, $"Scan failed: {ex.Message}");
        }
    }

    private async Task<MessageResponse> CleanTempModsAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // First scan to get mod files
            var scanResponse = await ScanTempCacheAsync(request);
            if (!scanResponse.Success)
                return scanResponse;

            var scanData = scanResponse.Data as dynamic;
            var modFiles = (scanData?.modFiles as IEnumerable<object>)?.Select(f => f.ToString() ?? "").ToList() ?? new List<string>();

            int deletedCount = 0;
            long freedSpace = 0;
            var errors = new List<string>();

            foreach (var filePath in modFiles)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        var size = fileInfo.Length;
                        File.Delete(filePath);
                        deletedCount++;
                        freedSpace += size;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    errors.Add($"Permission denied: {filePath}");
                }
                catch (IOException ex)
                {
                    errors.Add($"IO error on {filePath}: {ex.Message}");
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Cleaned {deletedCount} temp mod files, freed {freedSpace / (1024 * 1024)}MB");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                deletedCount,
                freedSpace,
                freedSpaceMB = freedSpace / (1024.0 * 1024.0),
                errors = errors.ToArray()
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error cleaning temp mods", ex);
            return MessageResponse.CreateError(request.Id, $"Clean failed: {ex.Message}");
        }
    }

    private async Task<MessageResponse> CleanTempPreviewsAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // First scan to get preview files
            var scanResponse = await ScanTempCacheAsync(request);
            if (!scanResponse.Success)
                return scanResponse;

            var scanData = scanResponse.Data as dynamic;
            var previewFiles = (scanData?.previewFiles as IEnumerable<object>)?.Select(f => f.ToString() ?? "").ToList() ?? new List<string>();

            int deletedCount = 0;
            long freedSpace = 0;
            var errors = new List<string>();

            foreach (var filePath in previewFiles)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        var size = fileInfo.Length;
                        File.Delete(filePath);
                        deletedCount++;
                        freedSpace += size;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    errors.Add($"Permission denied: {filePath}");
                }
                catch (IOException ex)
                {
                    errors.Add($"IO error on {filePath}: {ex.Message}");
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Cleaned {deletedCount} temp preview files, freed {freedSpace / (1024 * 1024)}MB");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                deletedCount,
                freedSpace,
                freedSpaceMB = freedSpace / (1024.0 * 1024.0),
                errors = errors.ToArray()
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error cleaning temp previews", ex);
            return MessageResponse.CreateError(request.Id, $"Clean failed: {ex.Message}");
        }
    }
}
