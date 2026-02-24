namespace D3dxSkinManager.Modules.Mods;

/// <summary>
/// Mod module event type constants for IPC notifications.
/// </summary>
public static class ModEvents
{
    public const string MOD_LOADED = "MOD_LOADED";
    public const string MOD_UNLOADED = "MOD_UNLOADED";
    public const string MOD_DELETED = "MOD_DELETED";
    public const string MOD_IMPORTED = "MOD_IMPORTED";
    public const string MODS_REFRESHED = "MODS_REFRESHED";
    public const string CLASSIFICATION_TREE_CHANGED = "CLASSIFICATION_TREE_CHANGED";

    // Custom events
    public const string METADATA_UPDATED = "CUSTOM_EVENT";  // With eventName: "mod.metadata.updated"
    public const string CATEGORY_UPDATED = "CUSTOM_EVENT";   // With eventName: "mod.category.updated"
    public const string PREVIEW_IMPORTED = "CUSTOM_EVENT";   // With eventName: "mod.preview.imported"
    public const string THUMBNAIL_UPDATED = "CUSTOM_EVENT";  // With eventName: "mod.thumbnail.updated"
    public const string PREVIEW_DELETED = "CUSTOM_EVENT";    // With eventName: "mod.preview.deleted"
}
