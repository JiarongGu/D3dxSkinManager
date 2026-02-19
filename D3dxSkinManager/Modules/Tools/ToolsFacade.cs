using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Tools.Models;
using D3dxSkinManager.Modules.Tools.Services;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager.Modules.Tools;

/// <summary>
/// Facade for tools and utilities
/// Responsibility: Cache management, validation, diagnostics
/// IPC Prefix: TOOLS_*
/// </summary>
public class ToolsFacade : IToolsFacade
{
    private readonly IModFileService _modFileService;
    private readonly IStartupValidationService _validationService;
    private readonly IPayloadHelper _payloadHelper;
    private readonly PluginEventBus? _eventBus;

    public ToolsFacade(
        IModFileService modFileService,
        IStartupValidationService validationService,
        IPayloadHelper payloadHelper,
        PluginEventBus? eventBus = null)
    {
        _modFileService = modFileService ?? throw new ArgumentNullException(nameof(modFileService));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _payloadHelper = payloadHelper ?? throw new ArgumentNullException(nameof(payloadHelper));
        _eventBus = eventBus;
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            Console.WriteLine($"[ToolsFacade] Handling message: {request.Type}");

            object? responseData = request.Type switch
            {
                "SCAN_CACHE" or "SCAN_CACHE" => await ScanCacheAsync(),
                "TOOLS_GET_CACHE_STATS" or "GET_CACHE_STATISTICS" => await GetCacheStatisticsAsync(),
                "CLEAN_CACHE" or "CLEAN_CACHE" => await CleanCacheAsync(request),
                "DELETE_CACHE_ITEM" or "DELETE_CACHE_ITEM" => await DeleteCacheItemAsync(request),
                "VALIDATE_STARTUP" or "VALIDATE_STARTUP" => await ValidateStartupAsync(),
                _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
            };

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ToolsFacade] Error handling message: {ex.Message}");
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
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

        if (_eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "cache.cleaned",
                Data = new { category = category.ToString(), deletedCount }
            });
        }

        return deletedCount;
    }

    public async Task<bool> DeleteCacheItemAsync(string sha)
    {
        var success = await _modFileService.DeleteCacheAsync(sha);

        if (success && _eventBus != null)
        {
            await _eventBus.EmitAsync(new PluginEventArgs
            {
                EventType = PluginEventType.CustomEvent,
                EventName = "cache.item.deleted",
                Data = new { sha }
            });
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
