using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod;

namespace D3dxSkinManager.Modules.Category.Services;

/// <summary>
/// Handles events from other modules that affect the category tree
/// Subscribes to CATEGORY_UPDATED event from Mod module to invalidate cache
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

    public CategoryEventHandler(ICategoryService categoryService, IProfileEventBus eventBus, ILogHelper logger)
    {
        _categoryService = categoryService;
        _eventBus = eventBus;
        _logger = logger;

        _logger.Info("CategoryEventHandler: Initializing and subscribing to MOD.CATEGORY_UPDATED event", "CategoryEventHandler");

        // Subscribe to CATEGORY_UPDATED from Mod module
        // When a mod's category changes, invalidate the tree cache and emit CATEGORY_TREE_UPDATED
        _categoryUpdatedHandlerId = _eventBus.RegisterHandler(
            ModuleNames.MOD,
            ModEvents.CATEGORY_UPDATED,
            async (_) => await HandleCategoryUpdatedAsync().ConfigureAwait(false)
        );

        _logger.Info($"CategoryEventHandler: Successfully registered handler (ID: {_categoryUpdatedHandlerId})", "CategoryEventHandler");
    }

    private Task HandleCategoryUpdatedAsync()
    {
        _logger.Info("CategoryEventHandler: Received MOD.CATEGORY_UPDATED event - invalidating cache", "CategoryEventHandler");

        // Invalidate the tree cache (this will also emit CATEGORY_TREE_UPDATED event)
        _categoryService.InvalidateTreeCache();

        _logger.Info("CategoryEventHandler: Cache invalidated and event emitted", "CategoryEventHandler");

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_categoryUpdatedHandlerId != null)
        {
            _eventBus.UnregisterHandler(_categoryUpdatedHandlerId);
        }
    }
}
