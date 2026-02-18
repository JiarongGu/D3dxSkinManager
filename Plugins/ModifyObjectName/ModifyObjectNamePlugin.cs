using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.ModifyObjectName;

/// <summary>
/// Modify Object Name
/// Port of modifyobjectname Python plugin.
///
/// Features:
/// - Rename object categories and update associated mods
/// - TODO: Implement full feature parity
/// </summary>
public class ModifyObjectNamePlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.modifyobjectname";
    public string Name => "Modify Object Name";
    public string Version => "1.0.0";
    public string Description => "Rename object categories and update associated mods";
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
        return new[] { "RENAME_OBJECT", "VALIDATE_OBJECT_NAME" };
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

