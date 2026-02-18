using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.ExportModFile;

/// <summary>
/// Export Mod File
/// Port of exportmodfile Python plugin.
///
/// Features:
/// - Export mods to zip/7z packages for distribution
/// - TODO: Implement full feature parity
/// </summary>
public class ExportModFilePlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;

    public string Id => "com.d3dxskinmanager.exportmodfile";
    public string Name => "Export Mod File";
    public string Version => "1.0.0";
    public string Description => "Export mods to zip/7z packages for distribution";
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
        return new[] { "EXPORT_MOD_ZIP", "EXPORT_MOD_7Z", "EXPORT_WITH_PREVIEW" };
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

