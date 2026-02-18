using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.DeleteModCache;

/// <summary>
/// Plugin to delete mod cache files (DISABLED-{SHA} folders).
/// Port of delete_mod_cache Python plugin.
///
/// Features:
/// - Deletes cache folders for specific mods
/// - Safe deletion with error handling
/// - Permission and file system error handling
/// </summary>
public class DeleteModCachePlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.deletemodcache";
    public string Name => "Delete Mod Cache";
    public string Version => "1.0.0";
    public string Description => "Deletes mod cache files (DISABLED-{SHA} folders)";
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
        return new[] { "DELETE_MOD_CACHE" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "DELETE_MOD_CACHE" => await DeleteModCacheAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            _context?.Log(LogLevel.Error, $"[{Name}] Error handling message", ex);
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> DeleteModCacheAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // Get SHA from request data
            var sha = request.Payload?.ToString();
            if (string.IsNullOrEmpty(sha))
                return MessageResponse.CreateError(request.Id, "SHA is required");

            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var workModsPath = Path.Combine(dataPath, "work_mods");
            var cacheDirectoryName = $"DISABLED-{sha}";
            var cachePath = Path.Combine(workModsPath, cacheDirectoryName);

            if (!Directory.Exists(cachePath))
            {
                return MessageResponse.CreateError(request.Id, $"Cache directory not found: {cachePath}");
            }

            // Delete the cache directory
            Directory.Delete(cachePath, true);

            _context.Log(LogLevel.Info, $"[{Name}] Deleted cache: {cacheDirectoryName}");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                sha,
                message = $"Cache deleted successfully: {cacheDirectoryName}"
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Permission denied", ex);
            return MessageResponse.CreateError(request.Id, $"Permission denied: {ex.Message}. Please delete the cache manually.");
        }
        catch (IOException ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] IO error", ex);
            return MessageResponse.CreateError(request.Id, $"IO error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error deleting cache", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to delete cache: {ex.Message}");
        }
    }
}
