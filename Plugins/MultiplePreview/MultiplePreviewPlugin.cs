using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.MultiplePreview;

/// <summary>
/// Multiple Preview
/// Port of multiplepreview Python plugin.
///
/// Features:
/// - Support for multiple preview images per mod
/// - TODO: Implement full feature parity
/// </summary>
public class MultiplePreviewPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.multiplepreview";
    public string Name => "Multiple Preview";
    public string Version => "1.0.0";
    public string Description => "Support for multiple preview images per mod";
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
        return new[] { "GET_PREVIEW_IMAGES", "ADD_PREVIEW_IMAGE", "DELETE_PREVIEW_IMAGE" };
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

