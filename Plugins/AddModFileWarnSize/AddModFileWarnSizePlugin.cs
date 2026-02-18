using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.AddModFileWarnSize;

/// <summary>
/// File size warning threshold configuration plugin.
/// Port of add_mod_file_warn_size Python plugin.
///
/// Features:
/// - Configurable file size warning thresholds (100MB - 2GB or unlimited)
/// - Warn users when importing large mod files
/// - Persistent configuration
/// </summary>
public class AddModFileWarnSizePlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;
    private long _warningSizeBytes = 100 * 1024 * 1024; // Default: 100MB

    public string Id => "com.d3dxskinmanager.addmodfilewarnsize";
    public string Name => "Add Mod File Warn Size";
    public string Version => "1.0.0";
    public string Description => "File size warning threshold configuration";
    public string Author => "D3dxSkinManager Team";

    public async Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        // TODO: Load configuration from plugin data directory
        _context.Log(LogLevel.Info, $"[{Name}] Initialized with warning size: {_warningSizeBytes / (1024 * 1024)}MB");
    }

    public async Task ShutdownAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
    }

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "GET_FILE_SIZE_WARNING", "SET_FILE_SIZE_WARNING", "CHECK_FILE_SIZE" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "GET_FILE_SIZE_WARNING" => GetFileSizeWarningResponse(request),
                "SET_FILE_SIZE_WARNING" => SetFileSizeWarningResponse(request),
                "CHECK_FILE_SIZE" => CheckFileSizeResponse(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private MessageResponse GetFileSizeWarningResponse(MessageRequest request)
    {
        return MessageResponse.CreateSuccess(request.Id, new
        {
            warningSizeBytes = _warningSizeBytes,
            warningSizeMB = _warningSizeBytes / (1024 * 1024),
            options = new[]
            {
                new { label = "100 MB", value = 100L * 1024 * 1024 },
                new { label = "200 MB", value = 200L * 1024 * 1024 },
                new { label = "500 MB", value = 500L * 1024 * 1024 },
                new { label = "1 GB", value = 1024L * 1024 * 1024 },
                new { label = "2 GB", value = 2048L * 1024 * 1024 },
                new { label = "Unlimited", value = -1L }
            }
        });
    }

    private MessageResponse SetFileSizeWarningResponse(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var payload = request.Payload as System.Text.Json.JsonElement?;
            var sizeBytes = payload?.GetProperty("sizeBytes").GetInt64() ?? _warningSizeBytes;

            _warningSizeBytes = sizeBytes;
            // TODO: Persist to configuration file

            _context.Log(LogLevel.Info, $"[{Name}] Set warning size to: {sizeBytes / (1024 * 1024)}MB");

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                warningSizeBytes = _warningSizeBytes
            });
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private MessageResponse CheckFileSizeResponse(MessageRequest request)
    {
        try
        {
            var payload = request.Payload as System.Text.Json.JsonElement?;
            var fileSize = payload?.GetProperty("fileSize").GetInt64() ?? 0;

            var shouldWarn = _warningSizeBytes > 0 && fileSize > _warningSizeBytes;

            return MessageResponse.CreateSuccess(request.Id, new
            {
                shouldWarn,
                fileSize,
                warningSize = _warningSizeBytes,
                fileSizeMB = fileSize / (1024 * 1024),
                warningSizeMB = _warningSizeBytes / (1024 * 1024)
            });
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }
}

