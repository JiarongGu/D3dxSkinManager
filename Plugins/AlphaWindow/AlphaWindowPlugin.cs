using D3dxSkinManager.Modules.Plugins.Services;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Plugins.AlphaWindow;

/// <summary>
/// Window transparency control plugin.
/// Port of alpha_window Python plugin.
///
/// Features:
/// - Adjustable window transparency (20-100%)
/// - Persistent alpha value in configuration
/// - Real-time adjustment via frontend slider
/// </summary>
public class AlphaWindowPlugin : IMessageHandlerPlugin
{
    private IPluginContext? _context;
    private int _currentAlpha = 100; // 20-100
    private const int MIN_ALPHA = 20;
    private const int MAX_ALPHA = 100;

    public string Id => "com.d3dxskinmanager.alphawindow";
    public string Name => "Alpha Window";
    public string Version => "1.0.0";
    public string Description => "Window transparency control";
    public string Author => "D3dxSkinManager Team (ported from numlinka's plugin)";

    public async Task InitializeAsync(IPluginContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        // Load saved alpha value (would be from config in full implementation)
        _currentAlpha = MAX_ALPHA;

        _context.Log(LogLevel.Info, $"[{Name}] Initialized with alpha: {_currentAlpha}");
    }

    public async Task ShutdownAsync()
    {
        _context?.Log(LogLevel.Info, $"[{Name}] Shut down");
    }

    public IEnumerable<string> GetHandledMessageTypes()
    {
        return new[] { "GET_WINDOW_ALPHA", "SET_WINDOW_ALPHA" };
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            return request.Type switch
            {
                "GET_WINDOW_ALPHA" => GetWindowAlphaResponse(request),
                "SET_WINDOW_ALPHA" => SetWindowAlphaResponse(request),
                _ => MessageResponse.CreateError(request.Id, $"Unknown message type: {request.Type}")
            };
        }
        catch (Exception ex)
        {
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
    }

    private MessageResponse GetWindowAlphaResponse(MessageRequest request)
    {
        return MessageResponse.CreateSuccess(request.Id, new
        {
            alpha = _currentAlpha,
            minAlpha = MIN_ALPHA,
            maxAlpha = MAX_ALPHA,
            alphaPercent = _currentAlpha / 100.0
        });
    }

    private MessageResponse SetWindowAlphaResponse(MessageRequest request)
    {
        if (_context == null)
            return MessageResponse.CreateError(request.Id, "Plugin context not initialized");

        try
        {
            var payload = request.Payload as System.Text.Json.JsonElement?;
            var alpha = payload?.GetProperty("alpha").GetInt32() ?? _currentAlpha;

            // Validate range
            if (alpha < MIN_ALPHA) alpha = MIN_ALPHA;
            if (alpha > MAX_ALPHA) alpha = MAX_ALPHA;

            _currentAlpha = alpha;

            // TODO: In full implementation, persist to config
            // TODO: In Photino/desktop app, this would call native window API

            _context.Log(LogLevel.Info, $"[{Name}] Set window alpha to: {alpha}%");

            // Emit event for frontend
            _context.EmitEventAsync("WINDOW_ALPHA_CHANGED", new { alpha });

            return MessageResponse.CreateSuccess(request.Id, new
            {
                success = true,
                alpha = _currentAlpha,
                alphaPercent = _currentAlpha / 100.0
            });
        }
        catch (Exception ex)
        {
            _context.Log(LogLevel.Error, $"[{Name}] Error setting alpha: {ex.Message}", ex);
            return MessageResponse.CreateError(request.Id, $"Failed to set alpha: {ex.Message}");
        }
    }
}

