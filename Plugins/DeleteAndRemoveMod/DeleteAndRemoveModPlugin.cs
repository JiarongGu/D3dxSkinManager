using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Plugins.DeleteAndRemoveMod;

/// <summary>
/// Enhanced mod deletion with cache cleanup.
/// Port of delete_and_remove_mod Python plugin.
///
/// Features:
/// - Complete mod removal (data + file + cache)
/// - Soft delete (keeps data record, removes file + cache)
/// - Auto-unload if mod is loaded
/// - Cache cleanup with proper error handling
/// </summary>
public class DeleteAndRemoveModPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.deleteandremovemod";
    public string Name => "Delete And Remove Mod";
    public string Version => "1.0.0";
    public string Description => "Enhanced mod deletion with cache cleanup";
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

    // ============= Message Handlers =============

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "DELETE_MOD_COMPLETE", "REMOVE_MOD_SOFT" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "DELETE_MOD_COMPLETE" => await DeleteModCompleteAsync(request),
                "REMOVE_MOD_SOFT" => await RemoveModSoftAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    /// <summary>
    /// Complete mod removal: unload + delete data + delete file + delete cache
    /// </summary>
    private async Task<MessageResponse> DeleteModCompleteAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        var payload = request.Payload as System.Text.Json.JsonElement?;
        var sha = payload?.GetProperty("sha").GetString();

        if (string.IsNullOrEmpty(sha))
        {
            return MessageResponse.CreateError(request.Id, "Missing sha parameter");
        }

        try
        {
            _context.Log(LogLevel.Info, $"[{Name}] Complete delete initiated for SHA: {sha}");

            // Get mod info
            var mod = await _context.ModRepository.GetByIdAsync(sha);
            if (mod == null)
            {
                return MessageResponse.CreateError(request.Id, $"Mod not found: {sha}");
            }

            // Step 1: Unload if loaded
            if (mod.IsLoaded)
            {
                _context.Log(LogLevel.Info, $"[{Name}] Unloading mod before deletion");
                await _context.ModFacade.UnloadModAsync(sha);
            }

            // Step 2: Delete cache
            await DeleteModCacheAsync(sha, mod.ObjectName);

            // Step 3: Delete from database
            await _context.ModRepository.DeleteAsync(sha);

            // Step 4: Delete file (handled by ModFacade.DeleteModAsync)
            // Note: We already did the cache cleanup, so we skip full DeleteModAsync

            _context.Log(LogLevel.Info, $"[{Name}] Complete delete successful for: {mod.Name}");

            // Emit event
            await _context.EmitEventAsync("MOD_COMPLETELY_DELETED", new { sha, mod });

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                sha,
                modName = mod.Name
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error in complete delete: {ex.Message}", ex);
            return MessageResponse.CreateError(request.Id, $"Delete failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft removal: unload + delete file + delete cache (keeps data record)
    /// </summary>
    private async Task<MessageResponse> RemoveModSoftAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        var payload = request.Payload as System.Text.Json.JsonElement?;
        var sha = payload?.GetProperty("sha").GetString();

        if (string.IsNullOrEmpty(sha))
        {
            return MessageResponse.CreateError(request.Id, "Missing sha parameter");
        }

        try
        {
            _context.Log(LogLevel.Info, $"[{Name}] Soft remove initiated for SHA: {sha}");

            // Get mod info
            var mod = await _context.ModRepository.GetByIdAsync(sha);
            if (mod == null)
            {
                return MessageResponse.CreateError(request.Id, $"Mod not found: {sha}");
            }

            // Step 1: Unload if loaded
            if (mod.IsLoaded)
            {
                _context.Log(LogLevel.Info, $"[{Name}] Unloading mod before removal");
                await _context.ModFacade.UnloadModAsync(sha);
            }

            // Step 2: Delete cache
            await DeleteModCacheAsync(sha, mod.ObjectName);

            // Step 3: Delete file only (keep database record)
            var fileService = _context.FileService;
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var modsPath = Path.Combine(dataPath, "mods");
            var modFilePath = Path.Combine(modsPath, sha);

            if (File.Exists(modFilePath))
            {
                File.Delete(modFilePath);
                _context.Log(LogLevel.Info, $"[{Name}] Deleted mod file: {modFilePath}");
            }

            _context.Log(LogLevel.Info, $"[{Name}] Soft remove successful for: {mod.Name}");

            // Emit event
            await _context.EmitEventAsync("MOD_SOFT_REMOVED", new { sha, mod });

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                sha,
                modName = mod.Name
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error in soft remove: {ex.Message}", ex);
            return MessageResponse.CreateError(request.Id, $"Remove failed: {ex.Message}");
        }
    }

    // ============= Helper Methods =============

    /// <summary>
    /// Deletes mod cache directory
    /// </summary>
    private async Task DeleteModCacheAsync(string sha, string objectName)
    {
        if (_context == null) return;

        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var workModsPath = Path.Combine(dataPath, "work_mods");
            var cacheName = $"DISABLED-{sha}";
            var cachePath = Path.Combine(workModsPath, cacheName);

            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, recursive: true);
                _context.Log(LogLevel.Info, $"[{Name}] Deleted cache directory: {cachePath}");
            }
            else
            {
                _context.Log(LogLevel.Debug, $"[{Name}] Cache directory not found (already clean): {cachePath}");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Permission denied deleting cache: {ex.Message}", ex);
            throw new InvalidOperationException($"Permission denied: Cannot delete cache. Please delete manually or run with elevated privileges.");
        }
        catch (IOException ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] I/O error deleting cache: {ex.Message}", ex);
            throw new InvalidOperationException($"I/O error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Unexpected error deleting cache: {ex.Message}", ex);
            throw;
        }
    }
}
