namespace D3dxSkinManager.Modules.Mod;

/// <summary>
/// Mod module event type constants.
/// Used with ModuleNames.MOD as the module identifier.
/// Example: EmitAsync(ModuleNames.MOD, ModEvents.LOADED, payload)
/// </summary>
public static class ModEvents
{
    public const string LOADING = "LOADING";
    public const string LOADED = "LOADED";
    public const string UNLOADED = "UNLOADED";
    public const string DELETED = "DELETED";
    public const string IMPORTED = "IMPORTED";
    public const string REFRESHED = "REFRESHED";
    public const string METADATA_UPDATED = "METADATA_UPDATED";
    public const string CATEGORY_UPDATED = "CATEGORY_UPDATED";
    public const string PREVIEW_IMPORTED = "PREVIEW_IMPORTED";
    public const string THUMBNAIL_UPDATED = "THUMBNAIL_UPDATED";
    public const string PREVIEW_DELETED = "PREVIEW_DELETED";
    public const string CACHE_CHANGED = "CACHE_CHANGED";
    public const string MOD_LIST_UPDATED = "MOD_LIST_UPDATED"; // Emitted when mod list state changes (for frontend refresh)

    // Preset events
    public const string PRESET_SAVED = "PRESET_SAVED";
    public const string PRESET_DELETED = "PRESET_DELETED";
    public const string PRESET_APPLIED = "PRESET_APPLIED";
}
