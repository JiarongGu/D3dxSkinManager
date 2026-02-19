using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Facades;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Tools.Models;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager.Modules.Tools;

/// <summary>
/// Interface for Tools facade
/// Handles: TOOLS_SCAN_CACHE, TOOLS_CLEAN_CACHE, TOOLS_VALIDATE_STARTUP, etc.
/// Prefix: TOOLS_*
/// </summary>
public interface IToolsFacade : IModuleFacade
{

    // Cache Management
    Task<List<CacheItem>> ScanCacheAsync();
    Task<CacheStatistics> GetCacheStatisticsAsync();
    Task<int> CleanCacheAsync(CacheCategory category);
    Task<bool> DeleteCacheItemAsync(string sha);

    // Validation
    Task<StartupValidationReport> ValidateStartupAsync();
}

/// <summary>
/// Facade for tools and utilities
/// Responsibility: Cache management, validation, diagnostics
/// IPC Prefix: TOOLS_*
/// </summary>
public class ToolsFacade : BaseFacade, IToolsFacade
{
    protected override string ModuleName => "ToolsFacade";

    private readonly IModFileService _modFileService;
    private readonly IStartupValidationService _validationService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly IEventEmitterHelper _eventEmitter;

    public ToolsFacade(
        IModFileService modFileService,
        IStartupValidationService validationService,
        IPayloadHelper payloadHelper,
        IEventEmitterHelper eventEmitter,
        ILogHelper logger) : base(logger)
    {
        _modFileService = modFileService ?? throw new ArgumentNullException(nameof(modFileService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _eventEmitter = eventEmitter ?? throw new ArgumentNullException(nameof(eventEmitter));
    }

    protected override async Task<object?> RouteMessageAsync(MessageRequest request)
    {
        return request.Type switch
        {
            "SCAN_CACHE" => await ScanCacheAsync(),
            "TOOLS_GET_CACHE_STATS" or "GET_CACHE_STATISTICS" => await GetCacheStatisticsAsync(),
            "CLEAN_CACHE" => await CleanCacheAsync(request),
            "DELETE_CACHE_ITEM" => await DeleteCacheItemAsync(request),
            "VALIDATE_STARTUP" => await ValidateStartupAsync(),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<List<CacheItem>> ScanCacheAsync()
    {
        return await _modFileService.ScanCacheAsync();
    }

    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        return await _modFileService.GetCacheStatisticsAsync();
    }

    public async Task<int> CleanCacheAsync(CacheCategory category)
    {
        var deletedCount = await _modFileService.CleanCacheAsync(category);

        await _eventEmitter.EmitAsync(
            PluginEventType.CustomEvent,
            "cache.cleaned",
            new { category = category.ToString(), deletedCount });

        return deletedCount;
    }

    public async Task<bool> DeleteCacheItemAsync(string sha)
    {
        var success = await _modFileService.DeleteCacheAsync(sha);

        if (success)
        {
            await _eventEmitter.EmitAsync(
                PluginEventType.CustomEvent,
                "cache.item.deleted",
                new { sha });
        }

        return success;
    }

    public async Task<StartupValidationReport> ValidateStartupAsync()
    {
        return await _validationService.ValidateStartupAsync();
    }

    private async Task<int> CleanCacheAsync(MessageRequest request)
    {
        var categoryString = _payloadHelper.GetRequiredValue<string>(request.Payload, "category");

        if (!Enum.TryParse<CacheCategory>(categoryString, true, out var category))
        {
            throw new ArgumentException($"Invalid cache category: {categoryString}");
        }

        return await CleanCacheAsync(category);
    }

    private async Task<bool> DeleteCacheItemAsync(MessageRequest request)
    {
        var sha = _payloadHelper.GetRequiredValue<string>(request.Payload, "sha");
        return await DeleteCacheItemAsync(sha);
    }
}
