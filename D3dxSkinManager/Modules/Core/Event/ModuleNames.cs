namespace D3dxSkinManager.Modules.Core.Event;

/// <summary>
/// Module name constants for event system.
/// Used as the "Module" property in EventMessage to identify the source module.
/// Follows the same pattern as IpcRequest module names.
/// </summary>
public static class ModuleNames
{
    // Core system modules
    public const string SYSTEM = "SYSTEM";
    public const string DROP_ZONE = "DROP_ZONE";

    // Feature modules
    public const string MOD = "MOD";
    public const string CATEGORY = "CATEGORY";
    public const string PROFILE = "PROFILE";
    public const string TASK_QUEUE = "TASK_QUEUE";
    public const string SETTING = "SETTING";
    public const string MIGRATION = "MIGRATION";
    public const string TOOL = "TOOL";
    public const string PLUGIN = "PLUGIN";
}
