namespace D3dxSkinManager.Modules.Core;

/// <summary>
/// System event type constants.
/// Only contains events that are truly core to the application lifecycle.
/// Module-specific events should be defined in their respective modules.
/// </summary>
public static class SystemEvents
{
    // Application lifecycle
    public const string APPLICATION_STARTED = "APPLICATION_STARTED";
    public const string APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN";

    // Core system events
    public const string LOG_LEVEL_CHANGED = "LOG_LEVEL_CHANGED";

    // Unified long-running process monitoring (ProcessRegistry → status bar / Activity panel).
    // Consolidated snapshot of all tracked processes; emitted on any add/update/complete/fail/cancel.
    public const string PROCESS_LIST_UPDATED = "PROCESS_LIST_UPDATED";

    // Raised when the user asks to resume an interrupted+resumable process. The owning op module
    // subscribes (filtering by process type) and continues from its persisted checkpoint.
    public const string PROCESS_RESUME_REQUESTED = "PROCESS_RESUME_REQUESTED";

    // An online-storage account (Quark, …) was logged in/out or changed. The login window can outlive
    // the IPC bridge timeout, so the Online Storage card refreshes on THIS event, not the IPC result.
    public const string ONLINE_ACCOUNT_CHANGED = "ONLINE_ACCOUNT_CHANGED";
}
