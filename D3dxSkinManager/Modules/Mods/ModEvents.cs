namespace D3dxSkinManager.Modules.Mods;

/// <summary>
/// Mod module event type constants.
/// Used with ModuleNames.MOD as the module identifier.
/// Example: EmitAsync(ModuleNames.MOD, ModEvents.LOADED, payload)
/// </summary>
public static class ModEvents
{
    public const string LOADED = "LOADED";
    public const string UNLOADED = "UNLOADED";
    public const string DELETED = "DELETED";
    public const string IMPORTED = "IMPORTED";
    public const string REFRESHED = "REFRESHED";
    public const string CLASSIFICATION_TREE_CHANGED = "CLASSIFICATION_TREE_CHANGED";
    public const string METADATA_UPDATED = "METADATA_UPDATED";
    public const string CATEGORY_UPDATED = "CATEGORY_UPDATED";
    public const string PREVIEW_IMPORTED = "PREVIEW_IMPORTED";
    public const string THUMBNAIL_UPDATED = "THUMBNAIL_UPDATED";
    public const string PREVIEW_DELETED = "PREVIEW_DELETED";
}
