using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Handles mod events and emits MOD_LIST_UPDATED for frontend refresh
/// Subscribes to mod state change events that require full list refresh
///
/// Subscribed events:
/// - DELETED: When a mod is deleted (removes from list)
/// - IMPORTED: When a new mod is imported (adds to list)
/// - METADATA_UPDATED: When mod metadata changes (name, author, tags, etc.)
/// - CATEGORY_UPDATED: When mod category changes
/// - REFRESHED: When mod list needs full refresh
/// - CACHE_CHANGED: When cache directory changes externally (affects IsLoaded/IsAvailable flags)
///
/// NOT subscribed (frontend handles directly for optimistic updates):
/// - LOADED: Frontend updates single mod's IsLoaded flag + refreshes statistics
/// - UNLOADED: Frontend updates single mod's IsLoaded flag + refreshes statistics
/// </summary>
public interface IModListEventHandler : IDisposable
{
}

public class ModListEventHandler : IModListEventHandler
{
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;
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

        // Subscribe to mod state change events that require full list refresh
        // NOTE: LOADED/UNLOADED are NOT subscribed here - frontend handles them directly
        // for optimistic single-mod updates + statistics refresh

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

        _logger.Info($"ModListEventHandler: Successfully registered 6 event handlers (LOADED/UNLOADED handled by frontend)", "ModListEventHandler");
    }

    private async Task EmitModListUpdatedAsync(string sourceEvent)
    {
        _logger.Info($"ModListEventHandler: Received MOD.{sourceEvent} event - emitting MOD_LIST_UPDATED", "ModListEventHandler");

        // Emit consolidated refresh event for frontend
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.MOD_LIST_UPDATED).ConfigureAwait(false);
    }

    public void Dispose()
    {
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
