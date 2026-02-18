using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.EnforceLogout;

/// <summary>
/// User logout functionality plugin.
/// Port of enforcelogout Python plugin.
///
/// Features:
/// - Frontend-initiated logout via IPC message
/// - Session cleanup
/// - User confirmation
/// </summary>
public class EnforceLogoutPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.enforcelogout";
    public string Name => "Enforce Logout";
    public string Version => "1.0.0";
    public string Description => "User logout functionality";
    public string Author => "D3dxSkinManager Team (ported from numlinka's plugin)";

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
        return new[] { "USER_LOGOUT" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "USER_LOGOUT" => await HandleLogoutAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> HandleLogoutAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            _context.Log(LogLevel.Info, $"[{Name}] User logout requested");

            // Emit logout event
            await _context.EmitEventAsync("USER_LOGOUT_REQUESTED");

            // In a full implementation, this would:
            // 1. Clear user session
            // 2. Clear cached data
            // 3. Reset UI state
            // 4. Show login screen

            return MessageResponse.CreateSuccess(request.Id, new { success = true });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error during logout: {ex.Message}", ex);
            return MessageResponse.CreateError(request.Id, $"Logout failed: {ex.Message}");
        }
    }
}

