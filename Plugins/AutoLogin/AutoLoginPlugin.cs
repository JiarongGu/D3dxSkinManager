using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.AutoLogin;

/// <summary>
/// Auto Login
/// Port of autologin Python plugin.
///
/// Features:
/// - Automated user login and program launch system
/// - TODO: Implement full feature parity
/// </summary>
public class AutoLoginPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.autologin";
    public string Name => "Auto Login";
    public string Version => "1.0.0";
    public string Description => "Automated user login and program launch system";
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
        return new[] { "ENABLE_AUTO_LOGIN", "DISABLE_AUTO_LOGIN", "LAUNCH_PROGRAM" };
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

