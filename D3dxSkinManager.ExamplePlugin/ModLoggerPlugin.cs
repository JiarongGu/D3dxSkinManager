using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.ExamplePlugin;

/// <summary>
/// Example plugin that logs all mod operations to console and a log file.
/// Demonstrates:
/// - Event handling (ModLoaded, ModUnloaded, ModImported, ModDeleted)
/// - Plugin context access
/// - Plugin data directory usage
/// - Custom message handling
/// </summary>
public class ModLoggerPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;
    private string? _logFilePath;
    private readonly object _fileLock = new();

    public string Id => "com.d3dxskinmanager.modlogger";
    public string Name => "Mod Logger";
    public string Version => "1.0.0";
    public string Description => "Logs all mod operations to console and file";
    public string Author => "D3dxSkinManager Team";

    public async Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // Get plugin data directory
        var dataPath = _context.GetPluginDataPath(Id);
        _logFilePath = Path.Combine(dataPath, "mod_operations.log");

        _context.Log(LogLevel.Info, $"[{Name}] Initialized");
        _context.Log(LogLevel.Info, $"[{Name}] Log file: {_logFilePath}");

        // Register event handlers
        _context.RegisterEventHandler(PluginEventType.ApplicationStarted, OnApplicationStarted);
        _context.RegisterEventHandler(PluginEventType.ModLoaded, OnModLoaded);
        _context.RegisterEventHandler(PluginEventType.ModUnloaded, OnModUnloaded);
        _context.RegisterEventHandler(PluginEventType.ModImported, OnModImported);
        _context.RegisterEventHandler(PluginEventType.ModDeleted, OnModDeleted);

        // Write startup message to log file
        await WriteLogAsync("=== Mod Logger Plugin Started ===");
    }

    public async Task ShutdownAsync()
    {
        await WriteLogAsync("=== Mod Logger Plugin Shutdown ===");
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
    }

    // ============= Event Handlers =============

    private async Task OnApplicationStarted(PluginEventArgs args)
    {
        await WriteLogAsync($"Application started at {args.Timestamp:yyyy-MM-dd HH:mm:ss}");
    }

    private async Task OnModLoaded(PluginEventArgs args)
    {
        var data = args.Data as dynamic;
        var sha = data?.Sha?.ToString() ?? "unknown";

        var message = $"[MOD_LOADED] SHA: {sha}";
        _context?.Log(LogLevel.Info, $"[{Name}] {message}");
        await WriteLogAsync(message);
    }

    private async Task OnModUnloaded(PluginEventArgs args)
    {
        var data = args.Data as dynamic;
        var sha = data?.Sha?.ToString() ?? "unknown";

        var message = $"[MOD_UNLOADED] SHA: {sha}";
        _context?.Log(LogLevel.Info, $"[{Name}] {message}");
        await WriteLogAsync(message);
    }

    private async Task OnModImported(PluginEventArgs args)
    {
        var mod = args.Data as ModInfo;
        if (mod != null)
        {
            var message = $"[MOD_IMPORTED] Name: {mod.Name}, Object: {mod.Category}, SHA: {mod.SHA}";
            _context?.Log(LogLevel.Info, $"[{Name}] {message}");
            await WriteLogAsync(message);
        }
    }

    private async Task OnModDeleted(PluginEventArgs args)
    {
        var data = args.Data as dynamic;
        var sha = data?.Sha?.ToString() ?? "unknown";

        var message = $"[MOD_DELETED] SHA: {sha}";
        _context?.Log(LogLevel.Info, $"[{Name}] {message}");
        await WriteLogAsync(message);
    }

    // ============= Message Handler =============

    public IEnumerable<string> GetHandledMessageTypes()
    {
        // This plugin handles custom messages from frontend
        return new[] { "GET_MOD_LOG", "CLEAR_MOD_LOG" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "GET_MOD_LOG" => await GetModLogAsync(request),
                "CLEAR_MOD_LOG" => await ClearModLogAsync(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private Task<MessageResponse> GetModLogAsync(MessageRequest request)
    {
        if (_logFilePath == null || !File.Exists(_logFilePath))
        {
            return Task.FromResult(MessageResponse.CreateSuccess(request.Id, new { log = "" }));
        }

        lock (_fileLock)
        {
            var logContent = File.ReadAllText(_logFilePath);
            return Task.FromResult(MessageResponse.CreateSuccess(request.Id, new { log = logContent }));
        }
    }

    private async Task<MessageResponse> ClearModLogAsync(MessageRequest request)
    {
        if (_logFilePath == null)
        {
            return MessageResponse.CreateError(request.Id, "Log file not initialized");
        }

        lock (_fileLock)
        {
            File.WriteAllText(_logFilePath, "");
        }

        await WriteLogAsync("=== Log Cleared ===");
        return MessageResponse.CreateSuccess(request.Id, new { success = true });
    }

    // ============= Helper Methods =============

    private Task WriteLogAsync(string message)
    {
        if (_logFilePath == null) return Task.CompletedTask;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logLine = $"[{timestamp}] {message}\n";

        lock (_fileLock)
        {
            File.AppendAllText(_logFilePath, logLine);
        }

        return Task.CompletedTask;
    }
}
