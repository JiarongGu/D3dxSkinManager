using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;


namespace D3dxSkinManager.Plugins.DropfilesMultiple;

/// <summary>
/// Plugin to handle drag-and-drop of multiple files (mods and previews).
/// Port of dropfiles_multiple Python plugin.
///
/// Features:
/// - Handles drag-and-drop of multiple files
/// - Separates .7z/.zip/.rar (mods) from .png/.jpg (preview images)
/// - Processes mods and previews separately
/// - Supports both files and directories
///
/// TODO: Full implementation requires:
/// - File drag-and-drop handling
/// - Mod import logic
/// - Preview image import logic
/// - UI integration for drop zone
/// </summary>
public class DropfilesMultiplePlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;
    private static readonly string[] ModSuffixes = { ".7z", ".zip", ".rar" };
    private static readonly string[] PreviewSuffixes = { ".png", ".jpg" };

    public string Id => "com.d3dxskinmanager.dropfilesmultiple";
    public string Name => "Dropfiles Multiple";
    public string Version => "1.0.0";
    public string Description => "Handles drag-and-drop of multiple files (mods and previews)";
    public string Author => "D3dxSkinManager";

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "HANDLE_DROP_FILES" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "HANDLE_DROP_FILES" => await HandleDropFilesAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            _context?.Log(LogLevel.Error, $"[{Name}] Error handling message", ex);
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private async Task<MessageResponse> HandleDropFilesAsync(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            // TODO: Extract file paths from request data
            // Expected format: { "files": ["path1", "path2", ...] }

            var modFiles = new List<string>();
            var previewFiles = new List<string>();
            var modDirectories = new List<string>();

            // TODO: Parse request.Data to get file list
            _context.Log(LogLevel.Info, $"[{Name}] Processing dropped files");

            // TODO: Categorize files
            // - Check if path is file or directory
            // - For files, check extension against ModSuffixes and PreviewSuffixes
            // - Collect paths into appropriate lists

            // TODO: Process mod files
            // - Import each mod file (.7z, .zip, .rar)
            // - Extract to appropriate location
            // - Update mod index

            // TODO: Process mod directories
            // - Scan directory for mod files
            // - Import recursively

            // TODO: Process preview files
            // - Copy preview images to preview directory
            // - Link to corresponding mods by SHA

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                message = "File drop handling not yet implemented",
                modFilesProcessed = 0,
                previewFilesProcessed = 0,
                // TODO: Return actual counts
                todo = "Full implementation required"
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error handling dropped files", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to handle drop: {ex.Message}");
        }
    }

    // TODO: Add helper methods:
    // - private bool IsModFile(string path)
    // - private bool IsPreviewFile(string path)
    // - private async Task ProcessModFileAsync(string path)
    // - private async Task ProcessPreviewFileAsync(string path)
    // - private async Task ProcessDirectoryAsync(string path)
}
