using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.UnloadObjectMods;

/// <summary>
/// Unload Object Mods
/// Port of unloadobjectmods Python plugin.
///
/// Features:
/// - Bulk unload operations by object or category
/// - TODO: Implement full feature parity
/// </summary>
public class UnloadObjectModsPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.unloadobjectmods";
    public string Name => "Unload Object Mods";
    public string Version => "1.0.0";
    public string Description => "Bulk unload operations by object or category";
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
        return new[] { "UNLOAD_OBJECT", "UNLOAD_CATEGORY", "UNLOAD_ALL" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            // TODO: Implement message routing
            _context?.Log(LogLevel.Warning, $"[{Name}] Message handler not implemented: {request.Type}");

            return MessageResponse.CreateError(request.Id, "Not yet implemented");
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }
}

