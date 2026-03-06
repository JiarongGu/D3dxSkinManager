using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Handles mod events and emits MOD_LIST_UPDATED for frontend refresh
/// Subscribes to all mod state change events and consolidates them into a single refresh event
///
/// Subscribed events:
/// - LOADED: When a mod is loaded (affects IsLoaded flag)
/// - UNLOADED: When a mod is unloaded (affects IsLoaded flag)
/// - DELETED: When a mod is deleted (removes from list)
/// - IMPORTED: When a new mod is imported (adds to list)
/// - METADATA_UPDATED: When mod metadata changes (name, author, tags, etc.)
/// - CATEGORY_UPDATED: When mod category changes
/// - REFRESHED: When mod list needs full refresh
/// - CACHE_CHANGED: When cache directory changes externally (affects IsLoaded/IsAvailable flags)
/// </summary>
public interface IModListEventHandler : IDisposable
{
}

public class ModListEventHandler : IModListEventHandler
{
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;
    private string? _loadedHandlerId;
    private string? _unloadedHandlerId;
    private string? _deletedHandlerId;
    private string? _importedHandlerId;
    private string? _metadataUpdatedHandlerId;
    private string? _categoryUpdatedHandlerId;
    private string? _refreshedHandlerId;
    private string? _cacheChangedHandlerId;

    public ModListEventHandler(IProfileEventBus eventBus, ILogHelper logger)
    {
        _eventBus = eventBus;
        _logger = logger;

        _logger.Info("ModListEventHandler: Initializing and subscribing to mod events", "ModListEventHandler");

        // Subscribe to all mod state change events that require frontend refresh
        _loadedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.LOADED,
            async (_) => await EmitModListUpdatedAsync("LOADED").ConfigureAwait(false)
        );

        _unloadedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.UNLOADED,
            async (_) => await EmitModListUpdatedAsync("UNLOADED").ConfigureAwait(false)
        );

        _deletedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.DELETED,
            async (_) => await EmitModListUpdatedAsync("DELETED").ConfigureAwait(false)
        );

        _importedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.IMPORTED,
            async (_) => await EmitModListUpdatedAsync("IMPORTED").ConfigureAwait(false)
        );

        _metadataUpdatedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.METADATA_UPDATED,
            async (_) => await EmitModListUpdatedAsync("METADATA_UPDATED").ConfigureAwait(false)
        );

        _categoryUpdatedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.CATEGORY_UPDATED,
            async (_) => await EmitModListUpdatedAsync("CATEGORY_UPDATED").ConfigureAwait(false)
        );

        _refreshedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.REFRESHED,
            async (_) => await EmitModListUpdatedAsync("REFRESHED").ConfigureAwait(false)
        );

        _cacheChangedHandlerId = _eventBus.Subscribe(
            ModuleNames.MOD,
            ModEvents.CACHE_CHANGED,
            async (_) => await EmitModListUpdatedAsync("CACHE_CHANGED").ConfigureAwait(false)
        );

        _logger.Info($"ModListEventHandler: Successfully registered 8 event handlers", "ModListEventHandler");
    }

    private async Task EmitModListUpdatedAsync(string sourceEvent)
    {
        _logger.Info($"ModListEventHandler: Received MOD.{sourceEvent} event - emitting MOD_LIST_UPDATED", "ModListEventHandler");

        // Emit consolidated refresh event for frontend
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.MOD_LIST_UPDATED).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_loadedHandlerId != null)
            _eventBus.Unsubscribe(_loadedHandlerId);
        if (_unloadedHandlerId != null)
            _eventBus.Unsubscribe(_unloadedHandlerId);
        if (_deletedHandlerId != null)
            _eventBus.Unsubscribe(_deletedHandlerId);
        if (_importedHandlerId != null)
            _eventBus.Unsubscribe(_importedHandlerId);
        if (_metadataUpdatedHandlerId != null)
            _eventBus.Unsubscribe(_metadataUpdatedHandlerId);
        if (_categoryUpdatedHandlerId != null)
            _eventBus.Unsubscribe(_categoryUpdatedHandlerId);
        if (_refreshedHandlerId != null)
            _eventBus.Unsubscribe(_refreshedHandlerId);
        if (_cacheChangedHandlerId != null)
            _eventBus.Unsubscribe(_cacheChangedHandlerId);
    }
}
