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
/// Handles: SCAN_CACHE, CLEAN_CACHE, VALIDATE_STARTUP, etc.
/// </summary>
public interface IToolFacade : IModuleFacade
{

    // Cache Management
    Task<List<CacheItem>> ScanCacheAsync();
    Task<CacheStatistics> GetCacheStatisticsAsync();
    Task<int> CleanCacheAsync(CacheCategory category);

    // Validation
    Task<StartupValidationReport> ValidateStartupAsync();
}

/// <summary>
/// Facade for tools and utilities
/// Module: TOOL
/// Responsibility: Cache management, validation, diagnostics
/// </summary>
public class ToolFacade : BaseFacade, IToolFacade
{
    protected override string ModuleName => "ToolsFacade";

    private readonly IModFileService _modFileService;
    private readonly IStartupValidationService _validationService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IProfileEventBus _eventBus;

    public ToolFacade(
        IModFileService modFileService,
        IStartupValidationService validationService,
        IPayloadHelper payloadHelper,
        IProfileEventBus eventBus,
        ILogHelper logger) : base(logger)
    {
        _modFileService = modFileService ?? throw new ArgumentNullException(nameof(modFileService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "SCAN_CACHE" => await ScanCacheAsync(),
            "TOOLS_GET_CACHE_STATS" or "GET_CACHE_STATISTICS" => await GetCacheStatisticsAsync(),
            "CLEAN_CACHE" => await CleanCacheAsync(request),
            "VALIDATE_STARTUP" => await ValidateStartupAsync(),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<List<CacheItem>> ScanCacheAsync()
    {
        return await _modFileService.ScanCacheAsync().ConfigureAwait(false);
    }

    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        return await _modFileService.GetCacheStatisticsAsync().ConfigureAwait(false);
    }

    public async Task<int> CleanCacheAsync(CacheCategory category)
    {
        var deletedCount = await _modFileService.CleanCacheAsync(category).ConfigureAwait(false);

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
}
