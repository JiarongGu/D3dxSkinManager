namespace D3dxSkinManager.Modules.Core.Event;

/// <summary>
/// Core system event type constants for IPC notifications.
/// </summary>
public static class CoreEvents
{
    public const string APPLICATION_STARTED = "APPLICATION_STARTED";
    public const string APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN";
    public const string MOD_LOADED = "MOD_LOADED";
    public const string MOD_UNLOADED = "MOD_UNLOADED";
    public const string MOD_DELETED = "MOD_DELETED";
    public const string MOD_IMPORTED = "MOD_IMPORTED";
    public const string MODS_REFRESHED = "MODS_REFRESHED";
    public const string CLASSIFICATION_TREE_CHANGED = "CLASSIFICATION_TREE_CHANGED";
    public const string CUSTOM_EVENT = "CUSTOM_EVENT";
    public const string LOG_LEVEL_CHANGED = "LOG_LEVEL_CHANGED";

    /// <summary>
    /// Get all core event types for registration
    /// </summary>
    public static readonly string[] All = new[]
    {
        APPLICATION_STARTED,
        APPLICATION_SHUTDOWN,
        MOD_LOADED,
        MOD_UNLOADED,
        MOD_DELETED,
        MOD_IMPORTED,
        MODS_REFRESHED,
        CLASSIFICATION_TREE_CHANGED,
        CUSTOM_EVENT,
        LOG_LEVEL_CHANGED
    };
}
