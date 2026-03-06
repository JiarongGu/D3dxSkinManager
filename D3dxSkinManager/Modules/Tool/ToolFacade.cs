using D3dxSkinManager.Modules.Tool.ScreenCapture.Models;
using D3dxSkinManager.Modules.Tool.ScreenCapture.Services;
using D3dxSkinManager.Modules.Tool.Models;
using D3dxSkinManager.Modules.Tool.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mod.Services;

namespace D3dxSkinManager.Modules.Tool;

/// <summary>
/// Interface for Tools facade
/// Module: TOOL
/// Handles: SCAN_CACHE, CLEAN_CACHE, VALIDATE_STARTUP, screen capture, etc.
/// </summary>
public interface IToolFacade : IModuleFacade { }

/// <summary>
/// Facade for tools and utilities
/// Module: TOOL
/// Responsibility: Cache management, validation, diagnostics
/// </summary>
public class ToolFacade : BaseFacade, IToolFacade
{
    protected override string ModuleName => "ToolsFacade";

    private readonly IModCacheService _cacheService;
    private readonly IStartupValidationService _validationService;
    private readonly IScreenCaptureProfileRepository _captureProfileRepository;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IProfileEventBus _eventBus;

    public ToolFacade(
        IModCacheService cacheService,
        IStartupValidationService validationService,
        IScreenCaptureProfileRepository captureProfileRepository,
        IScreenCaptureService screenCaptureService,
        IPayloadHelper payloadHelper,
        IProfileEventBus eventBus,
        ILogHelper logger) : base(logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _captureProfileRepository = captureProfileRepository ?? throw new ArgumentNullException(nameof(captureProfileRepository));
        _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            // Cache Management
            "SCAN_CACHE" => await ScanCacheAsync(),
            "TOOLS_GET_CACHE_STATS" or "GET_CACHE_STATISTICS" => await GetCacheStatisticsAsync(),
            "CLEAN_CACHE" => await CleanCacheAsync(request),

            // Validation
            "VALIDATE_STARTUP" => await ValidateStartupAsync(),

            // Screen Capture - Profile Management
            "SCREEN_CAPTURE_GET_PROFILES" => await GetCaptureProfilesAsync(),
            "SCREEN_CAPTURE_GET_PROFILE" => await GetCaptureProfileAsync(request),
            "SCREEN_CAPTURE_SAVE_PROFILE" => await SaveCaptureProfileAsync(request),
            "SCREEN_CAPTURE_DELETE_PROFILE" => await DeleteCaptureProfileAsync(request),

            // Screen Capture - Capture Operations
            "SCREEN_CAPTURE_SCREEN" => await CaptureScreenAsync(request),

            // Screen Capture - Border Overlay
            "SCREEN_CAPTURE_SHOW_BORDER" => await ShowBorderOverlayAsync(request),
            "SCREEN_CAPTURE_HIDE_BORDER" => await HideBorderOverlayAsync(request),

            // Screen Capture - Control Panel
            "SCREEN_CAPTURE_TOGGLE_CONTROL_PANEL" => Task.FromResult(ToggleCaptureControlPanel(request)),

            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<List<CacheItem>> ScanCacheAsync()
    {
        return await _cacheService.ScanCacheAsync().ConfigureAwait(false);
    }

    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        return await _cacheService.GetCacheStatisticsAsync().ConfigureAwait(false);
    }

    public async Task<int> CleanCacheAsync(CacheCategory category)
    {
        var deletedCount = await _cacheService.CleanCacheAsync(category).ConfigureAwait(false);

        await _eventBus.EmitAsync(
            ModuleNames.TOOL,
            ToolEvents.CACHE_CLEANED,
            new { category = category.ToString(), deletedCount });

        return deletedCount;
    }

    public async Task<StartupValidationReport> ValidateStartupAsync()
    {
        return await _validationService.ValidateStartupAsync().ConfigureAwait(false);
    }

    private async Task<int> CleanCacheAsync(IpcRequest request)
    {
        var categoryString = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");

        if (!Enum.TryParse<CacheCategory>(categoryString, true, out var category))
        {
            throw new ArgumentException($"Invalid cache category: {categoryString}");
        }

        return await CleanCacheAsync(category).ConfigureAwait(false);
    }

    // ===== Screen Capture - Profile Management =====

    public async Task<List<ScreenCaptureProfile>> GetCaptureProfilesAsync()
    {
        return await _captureProfileRepository.GetAllAsync().ConfigureAwait(false);
    }

    public async Task<ScreenCaptureProfile?> GetCaptureProfileAsync(string id)
    {
        return await _captureProfileRepository.GetByIdAsync(id).ConfigureAwait(false);
    }

    private async Task<ScreenCaptureProfile?> GetCaptureProfileAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        return await GetCaptureProfileAsync(id).ConfigureAwait(false);
    }

    public async Task<string> SaveCaptureProfileAsync(SaveScreenCaptureProfileRequest request)
    {
        var profile = new ScreenCaptureProfile
        {
            Id = request.Id ?? Guid.NewGuid().ToString(),
            Name = request.Name,
            X = request.X,
            Y = request.Y,
            Width = request.Width,
            Height = request.Height,
        };

        if (string.IsNullOrEmpty(request.Id))
        {
            // Insert new profile
            var id = await _captureProfileRepository.InsertAsync(profile).ConfigureAwait(false);
            await _eventBus.EmitAsync(
                ModuleNames.TOOL,
                ToolEvents.CAPTURE_PROFILE_CREATED,
                new { id, name = profile.Name });
            return id;
        }
        else
        {
            // Update existing profile
            await _captureProfileRepository.UpdateAsync(profile).ConfigureAwait(false);
            await _eventBus.EmitAsync(
                ModuleNames.TOOL,
                ToolEvents.CAPTURE_PROFILE_UPDATED,
                new { id = profile.Id, name = profile.Name });
            return profile.Id;
        }
    }

    private async Task<string> SaveCaptureProfileAsync(IpcRequest request)
    {
        var saveRequest = new SaveScreenCaptureProfileRequest
        {
            Id = _payloadHelper.GetOptionalValue<string>(request.Payload, "id"),
            Name = _payloadHelper.GetRequiredValue<string>(request.Payload, "name"),
            X = _payloadHelper.GetRequiredValue<int>(request.Payload, "x"),
            Y = _payloadHelper.GetRequiredValue<int>(request.Payload, "y"),
            Width = _payloadHelper.GetRequiredValue<int>(request.Payload, "width"),
            Height = _payloadHelper.GetRequiredValue<int>(request.Payload, "height"),
        };

        return await SaveCaptureProfileAsync(saveRequest).ConfigureAwait(false);
    }

    public async Task DeleteCaptureProfileAsync(string id)
    {
        await _captureProfileRepository.DeleteAsync(id).ConfigureAwait(false);
        await _eventBus.EmitAsync(
            ModuleNames.TOOL,
            ToolEvents.CAPTURE_PROFILE_DELETED,
            new { id });
    }

    private async Task<object?> DeleteCaptureProfileAsync(IpcRequest request)
    {
        var id = _payloadHelper.GetRequiredValue<string>(request.Payload, "id");
        await DeleteCaptureProfileAsync(id).ConfigureAwait(false);
        return null;
    }

    // ===== Screen Capture - Capture Operations =====

    public async Task<ScreenCaptureResult> CaptureScreenAsync(ScreenCaptureConfig config)
    {
        return await _screenCaptureService.CaptureAsync(config).ConfigureAwait(false);
    }

    private async Task<ScreenCaptureResult> CaptureScreenAsync(IpcRequest request)
    {
        _logger.Info("[ToolFacade] CaptureScreenAsync called");
        var payloadJson = global::System.Text.Json.JsonSerializer.Serialize(request.Payload);
        _logger.Info($"[ToolFacade] Request payload: {payloadJson}");

        var config = new ScreenCaptureConfig
        {
            ProfileId = _payloadHelper.GetOptionalValue<string>(request.Payload, "profileId"),
            X = _payloadHelper.GetOptionalValue<int>(request.Payload, "x"),
            Y = _payloadHelper.GetOptionalValue<int>(request.Payload, "y"),
            Width = _payloadHelper.GetOptionalValue<int>(request.Payload, "width"),
            Height = _payloadHelper.GetOptionalValue<int>(request.Payload, "height"),
            ShowSelectionUI = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "showSelectionUI") ?? false,
            CopyToClipboard = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "copyToClipboard") ?? true,
            SaveToFile = _payloadHelper.GetOptionalValue<bool?>(request.Payload, "saveToFile") ?? false,
            OutputPath = _payloadHelper.GetOptionalValue<string>(request.Payload, "outputPath")
        };

        _logger.Info($"[ToolFacade] Capture config: X={config.X}, Y={config.Y}, W={config.Width}, H={config.Height}, Clipboard={config.CopyToClipboard}");
        _logger.Info("[ToolFacade] Calling ScreenCaptureService.CaptureAsync...");

        var result = await CaptureScreenAsync(config).ConfigureAwait(false);

        _logger.Info($"[ToolFacade] Capture result: Success={result.Success}, Error={result.ErrorMessage}");
        return result;
    }

    // ===== Screen Capture - Border Overlay =====

    public async Task ShowBorderOverlayAsync(int x, int y, int width, int height)
    {
        await _screenCaptureService.ShowBorderOverlayAsync(x, y, width, height, _eventBus).ConfigureAwait(false);
    }

    private async Task<object?> ShowBorderOverlayAsync(IpcRequest request)
    {
        _logger.Debug("[ToolFacade] ShowBorderOverlayAsync handler called");
        var x = _payloadHelper.GetRequiredValue<int>(request.Payload, "x");
        var y = _payloadHelper.GetRequiredValue<int>(request.Payload, "y");
        var width = _payloadHelper.GetRequiredValue<int>(request.Payload, "width");
        var height = _payloadHelper.GetRequiredValue<int>(request.Payload, "height");
        _logger.Debug($"[ToolFacade] Parsed values: x={x}, y={y}, width={width}, height={height}");
        _logger.Debug("[ToolFacade] Calling ScreenCaptureService.ShowBorderOverlayAsync...");
        await ShowBorderOverlayAsync(x, y, width, height).ConfigureAwait(false);
        _logger.Debug("[ToolFacade] ShowBorderOverlayAsync completed");
        return null;
    }

    public async Task HideBorderOverlayAsync()
    {
        await _screenCaptureService.HideBorderOverlayAsync().ConfigureAwait(false);
    }

    private async Task<object?> HideBorderOverlayAsync(IpcRequest request)
    {
        await HideBorderOverlayAsync().ConfigureAwait(false);
        return null;
    }

    public void ToggleCaptureControlPanel(string profileId)
    {
        _screenCaptureService.ToggleCaptureControlPanel(profileId);
    }

    private object? ToggleCaptureControlPanel(IpcRequest request)
    {
        var profileId = request.ProfileId ?? throw new InvalidOperationException("ProfileId is required");
        ToggleCaptureControlPanel(profileId);
        return null;
    }
}
