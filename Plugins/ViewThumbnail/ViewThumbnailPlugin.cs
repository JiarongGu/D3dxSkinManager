using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.ViewThumbnail;

/// <summary>
/// View thumbnail configuration file plugin.
/// Port of view_thumbnail Python plugin.
///
/// Features:
/// - Open thumbnail/redirection configuration file in default viewer
/// - Context menu integration via frontend
/// </summary>
public class ViewThumbnailPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.viewthumbnail";
    public string Name => "View Thumbnail";
    public string Version => "1.0.0";
    public string Description => "View thumbnail configuration files";
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
        return new[] { "VIEW_THUMBNAIL_CONFIG" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "VIEW_THUMBNAIL_CONFIG" => await ViewThumbnailConfigAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> ViewThumbnailConfigAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            var resourcesPath = Path.Combine(dataPath, "resources");
            var redirectionPath = Path.Combine(resourcesPath, "redirection.json");

            if (!File.Exists(redirectionPath))
            {
                return MessageResponse.CreateError(request.Id, "Thumbnail configuration file not found");
            }

            // Open file with default application
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = redirectionPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(processInfo);

            _context.Log(LogLevel.Info, $"[{Name}] Opened thumbnail config: {redirectionPath}");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                path = redirectionPath
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error viewing thumbnail config: {ex.Message}", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to open file: {ex.Message}");
        }
    }
}
