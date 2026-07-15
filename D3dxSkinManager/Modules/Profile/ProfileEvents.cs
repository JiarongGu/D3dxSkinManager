namespace D3dxSkinManager.Modules.Profiles;

/// <summary>
/// Profile module event type constants.
/// Used with ModuleNames.PROFILE as the module identifier.
/// </summary>
public static class ProfileEvents
{
    public const string CREATED = "CREATED";
    public const string UPDATED = "UPDATED";
    public const string DELETED = "DELETED";
    public const string DUPLICATED = "DUPLICATED";
    public const string SWITCHED = "SWITCHED";
    public const string CONFIG_UPDATED = "CONFIG_UPDATED";

    // Profile settings bundle export/import run fire-and-forget (long file/zip ops must not block the
    // IPC — background-task-tracking.md); the result arrives via these COMPLETE events. A failed run
    // still emits so the UI leaves the running state.
    public const string EXPORT_SETTINGS_COMPLETE = "EXPORT_SETTINGS_COMPLETE";
    public const string IMPORT_SETTINGS_COMPLETE = "IMPORT_SETTINGS_COMPLETE";
}
