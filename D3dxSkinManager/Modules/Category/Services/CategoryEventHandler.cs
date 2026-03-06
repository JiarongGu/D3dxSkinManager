using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod;

namespace D3dxSkinManager.Modules.Category.Services;

/// <summary>
/// Handles events from other modules that affect the category tree
/// Subscribes to mod events (CATEGORY_UPDATED, IMPORTED, DELETED) to invalidate cache
/// </summary>
public interface ICategoryEventHandler : IDisposable
{
}

public class CategoryEventHandler : ICategoryEventHandler
{
    private readonly ICategoryService _categoryService;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;
    private string? _categoryUpdatedHandlerId;
    private string? _modImportedHandlerId;
    private string? _modDeletedHandlerId;

    public CategoryEventHandler(ICategoryService categoryService, IProfileEventBus eventBus, ILogHelper logger)
    {
        _categoryService = categoryService;
        _eventBus = eventBus;
        _logger = logger;

        _logger.Info("CategoryEventHandler: Initializing and subscribing to mod events", "CategoryEventHandler");

        // Subscribe to CATEGORY_UPDATED from Mod module
        // When a mod's category changes, invalidate the tree cache and emit CATEGORY_TREE_UPDATED
        _categoryUpdatedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.CATEGORY_UPDATED,
            async (_) => await HandleModEventAsync("CATEGORY_UPDATED").ConfigureAwait(false)
        );

        // Subscribe to IMPORTED from Mod module
        // When a new mod is imported, invalidate the tree cache to update mod counts
        _modImportedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.IMPORTED,
            async (_) => await HandleModEventAsync("IMPORTED").ConfigureAwait(false)
        );

        // Subscribe to DELETED from Mod module
        // When a mod is deleted, invalidate the tree cache to update mod counts
        _modDeletedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.DELETED,
            async (_) => await HandleModEventAsync("DELETED").ConfigureAwait(false)
        );

        _logger.Info($"CategoryEventHandler: Successfully registered handlers - CategoryUpdated: {_categoryUpdatedHandlerId}, ModImported: {_modImportedHandlerId}, ModDeleted: {_modDeletedHandlerId}", "CategoryEventHandler");
    }

    private Task HandleModEventAsync(string eventType)
    {
        _logger.Info($"CategoryEventHandler: Received MOD.{eventType} event - invalidating cache", "CategoryEventHandler");

        // Invalidate the tree cache (this will also emit CATEGORY_TREE_UPDATED event)
        _categoryService.InvalidateTreeCache();

        _logger.Info("CategoryEventHandler: Cache invalidated and event emitted", "CategoryEventHandler");

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_categoryUpdatedHandlerId != null)
        {
            _eventBus.Unsubscribe(_categoryUpdatedHandlerId);
        }
        if (_modImportedHandlerId != null)
        {
            _eventBus.Unsubscribe(_modImportedHandlerId);
        }
        if (_modDeletedHandlerId != null)
        {
            _eventBus.Unsubscribe(_modDeletedHandlerId);
        }
    }
}


