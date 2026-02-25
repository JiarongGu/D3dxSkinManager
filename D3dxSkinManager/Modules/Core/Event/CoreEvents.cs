namespace D3dxSkinManager.Modules.Core.Event;

/// <summary>
/// Core system event type constants.
/// Only contains events that are truly core to the application lifecycle.
/// Module-specific events should be defined in their respective modules.
/// </summary>
public static class CoreEvents
{
    // Application lifecycle
    public const string APPLICATION_STARTED = "APPLICATION_STARTED";
    public const string APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN";

    // Core system events
    public const string LOG_LEVEL_CHANGED = "LOG_LEVEL_CHANGED";
}
