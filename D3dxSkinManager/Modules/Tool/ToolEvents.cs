namespace D3dxSkinManager.Modules.Tool;

/// <summary>
/// Tools module event type constants.
/// Used with ModuleNames.TOOL as the module identifier.
/// </summary>
public static class ToolEvents
{
    // Cache events
    public const string CACHE_CLEANED = "CACHE_CLEANED";
    public const string CACHE_ITEM_DELETED = "CACHE_ITEM_DELETED";

    // Screen capture profile events
    public const string CAPTURE_PROFILE_CREATED = "CAPTURE_PROFILE_CREATED";
    public const string CAPTURE_PROFILE_UPDATED = "CAPTURE_PROFILE_UPDATED";
    public const string CAPTURE_PROFILE_DELETED = "CAPTURE_PROFILE_DELETED";
}
