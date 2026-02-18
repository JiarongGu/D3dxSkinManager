using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;


namespace D3dxSkinManager.Plugins.UnloadWithDeleteCache;

/// <summary>
/// Plugin to automatically delete cache when unloading mods.
/// Port of unload_with_delete_cache Python plugin.
///
/// Features:
/// - Event-driven: automatically triggered when mods are unloaded
/// - Configurable per-user environment
/// - Deletes DISABLED-{SHA} cache folders automatically
/// - Enable/disable functionality via messages
/// </summary>
public class UnloadWithDeleteCachePlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;
    private HashSet<string> _enabledUsers = new();
    private bool _autoDeleteEnabled = false;

    public string Id => "com.d3dxskinmanager.unloadwithdeletecache";
    public string Name => "Unload with Delete Cache";
    public string Version => "1.0.0";
    public string Description => "Automatically deletes cache when unloading mods (event handling via messages)";
    public string Author => "D3dxSkinManager";

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
        // TODO: Subscribe to MOD_UNLOADED events when event system is available
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "ENABLE_AUTO_DELETE_CACHE", "DISABLE_AUTO_DELETE_CACHE", "MOD_UNLOADED" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "ENABLE_AUTO_DELETE_CACHE" => await EnableAutoDeleteCacheAsync(request),
                "DISABLE_AUTO_DELETE_CACHE" => await DisableAutoDeleteCacheAsync(request),
                "MOD_UNLOADED" => await HandleModUnloadedAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            _context?.Log(LogLevel.Error, $"[{Name}] Error handling message", ex);
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> HandleModUnloadedAsync(MessageRequest request)
    {
        if (!_autoDeleteEnabled)
            return MessageResponse.CreateSuccess(request.Id, new { skipped = true, reason = "Auto-delete disabled" });

        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // Extract SHA from request
            var sha = request.Payload?.ToString();

            if (string.IsNullOrEmpty(sha))
            {
                _context.Log(LogLevel.Warning, $"[{Name}] MOD_UNLOADED message received but no SHA found");
                return MessageResponse.CreateError(request.Id, "SHA is required");
            }

            // Check if cache exists
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var workModsPath = Path.Combine(dataPath, "work_mods");
            var cacheDir = $"DISABLED-{sha}";
            var cachePath = Path.Combine(workModsPath, cacheDir);

            if (!Directory.Exists(cachePath))
            {
                _context.Log(LogLevel.Debug, $"[{Name}] No cache to delete for SHA: {sha}");
                return MessageResponse.CreateSuccess(request.Id, new { deleted = false, reason = "Cache not found" });
            }

            // Delete the cache
            Directory.Delete(cachePath, true);
            _context.Log(LogLevel.Info, $"[{Name}] Auto-deleted cache: {cacheDir}");

            return MessageResponse.CreateSuccess(request.Id, new { deleted = true, sha, cacheDir });
        }
        catch (UnauthorizedAccessException ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Permission denied while auto-deleting cache", ex);
            return MessageResponse.CreateError(request.Id, $"Permission denied: {ex.Message}");
        }
        catch (IOException ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] IO error while auto-deleting cache", ex);
            return MessageResponse.CreateError(request.Id, $"IO error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error auto-deleting cache", ex);
            return MessageResponse.CreateError(request.Id, $"Error: {ex.Message}");
        }
    }

    private async Task<MessageResponse> EnableAutoDeleteCacheAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var userName = request.Payload?.ToString();

            _autoDeleteEnabled = true;

            if (!string.IsNullOrEmpty(userName))
                _enabledUsers.Add(userName);

            _context.Log(LogLevel.Info, $"[{Name}] Auto-delete cache enabled" +
                (string.IsNullOrEmpty(userName) ? "" : $" for user: {userName}"));

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                enabled = true,
                message = "Auto-delete cache enabled"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error enabling auto-delete", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to enable: {ex.Message}");
        }
    }

    private async Task<MessageResponse> DisableAutoDeleteCacheAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var userName = request.Payload?.ToString();

            if (!string.IsNullOrEmpty(userName))
            {
                _enabledUsers.Remove(userName);
                if (_enabledUsers.Count == 0)
                    _autoDeleteEnabled = false;
            }
            else
            {
                _autoDeleteEnabled = false;
                _enabledUsers.Clear();
            }

            _context.Log(LogLevel.Info, $"[{Name}] Auto-delete cache disabled" +
                (string.IsNullOrEmpty(userName) ? "" : $" for user: {userName}"));

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                enabled = false,
                message = "Auto-delete cache disabled"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error disabling auto-delete", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to disable: {ex.Message}");
        }
    }
}
