using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Plugins.HighlightLoadingMod;

/// <summary>
/// Highlights the currently loaded mod in the UI with a visual indicator.
/// Port of highlight_loading_mod Python plugin.
///
/// Features:
/// - Visual highlighting of loaded mod (orange color)
/// - Frontend integration via custom messages
/// - Responds to mod load/unload events
/// </summary>
public class HighlightLoadingModPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;
    private Dictionary<string, string> _loadedModsByObject = new();

    public string Id => "com.d3dxskinmanager.highlightloadingmod";
    public string Name => "Highlight Loading Mod";
    public string Version => "1.0.0";
    public string Description => "Highlights the currently loaded mod in the UI";
    public string Author => "D3dxSkinManager";

    public async Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // Register event handlers
        context.RegisterEventHandler(PluginEventType.ModLoaded, OnModLoaded);
        context.RegisterEventHandler(PluginEventType.ModUnloaded, OnModUnloaded);
        context.RegisterEventHandler(PluginEventType.ModsRefreshed, OnModsRefreshed);

        // Load current state
        await RefreshLoadedModsAsync();

        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
    }

    public async Task ShutdownAsync()
    {
        _loadedModsByObject.Clear();
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
    }

    // ============= Event Handlers =============

    private async Task OnModLoaded(PluginEventArgs args)
    {
        var data = args.Data as dynamic;
        var sha = data?.Sha?.ToString();

        if (string.IsNullOrEmpty(sha) || _context == null)
            return;

        // Get mod info to find its object
        var mod = await _context.ModRepository.GetByIdAsync(sha);
        if (mod != null)
        {
            _loadedModsByObject[mod.ObjectName] = sha;
            _context.Log(LogLevel.Info, $"[{Name}] Highlighted loaded mod: {mod.Name} ({mod.ObjectName})");

            // Notify frontend
            await _context.EmitEventAsync("HIGHLIGHT_MOD_LOADED", new { sha, objectName = mod.ObjectName });
        }
    }

    private async Task OnModUnloaded(PluginEventArgs args)
    {
        var data = args.Data as dynamic;
        var sha = data?.Sha?.ToString();

        if (string.IsNullOrEmpty(sha) || _context == null)
            return;

        // Get mod info to find its object
        var mod = await _context.ModRepository.GetByIdAsync(sha);
        if (mod != null)
        {
            _loadedModsByObject.Remove(mod.ObjectName);
            _context.Log(LogLevel.Info, $"[{Name}] Unhighlighted mod: {mod.Name} ({mod.ObjectName})");

            // Notify frontend
            await _context.EmitEventAsync("HIGHLIGHT_MOD_UNLOADED", new { sha, objectName = mod.ObjectName });
        }
    }

    private async Task OnModsRefreshed(PluginEventArgs args)
    {
        await RefreshLoadedModsAsync();
    }

    // ============= Message Handlers =============

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "GET_LOADED_MODS_MAP", "GET_LOADED_MOD_FOR_OBJECT" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "GET_LOADED_MODS_MAP" => GetLoadedModsMapResponse(request),
                "GET_LOADED_MOD_FOR_OBJECT" => await GetLoadedModForObjectResponse(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    /// <summary>
    /// Returns a map of object names to loaded mod SHAs
    /// </summary>
    private MessageResponse GetLoadedModsMapResponse(MessageRequest request)
    {
        return MessageResponse.CreateSuccess(request.Id, new
        {
            loadedMods = _loadedModsByObject
        });
    }

    /// <summary>
    /// Returns the loaded mod SHA for a specific object
    /// </summary>
    private async Task<MessageResponse> GetLoadedModForObjectResponse(MessageRequest request)
    {
        var payload = request.Payload as System.Text.Json.JsonElement?;
        var objectName = payload?.GetProperty("objectName").GetString();

        if (string.IsNullOrEmpty(objectName))
        {
            return MessageResponse.CreateError(request.Id, "Missing objectName parameter");
        }

        var sha = _loadedModsByObject.TryGetValue(objectName, out var loadedSha) ? loadedSha : null;

        return MessageResponse.CreateSuccess(request.Id, new
        {
            objectName,
            loadedModSha = sha
        });
    }

    // ============= Helper Methods =============

    /// <summary>
    /// Refreshes the loaded mods map from the database
    /// </summary>
    private async Task RefreshLoadedModsAsync()
    {
        if (_context == null) return;

        try
        {
            _loadedModsByObject.Clear();

            // Get all loaded mods
            var loadedShas = await _context.ModRepository.GetLoadedIdsAsync();

            foreach (var sha in loadedShas)
            {
                var mod = await _context.ModRepository.GetByIdAsync(sha);
                if (mod != null)
                {
                    _loadedModsByObject[mod.ObjectName] = sha;
                }
            }

            _context.Log(LogLevel.Info, $"[{Name}] Refreshed loaded mods: {_loadedModsByObject.Count} objects");
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error refreshing loaded mods: {ex.Message}", ex);
        }
    }
}
