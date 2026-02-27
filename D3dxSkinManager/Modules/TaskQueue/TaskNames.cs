namespace D3dxSkinManager.Modules.TaskQueue;

/// <summary>
/// Centralized task type constants for the TaskQueue system
/// </summary>
public static class TaskNames
{
    // Import tasks
    public const string MOD_IMPORT = "mod_import";
    public const string COMPRESS_FOLDER = "compress_folder";
    public const string IMPORT_FROM_TEMP = "import_from_temp";

    // Future task types can be added here
    // public const string MOD_EXPORT = "mod_export";
    // public const string BATCH_PROCESS = "batch_process";
    // public const string VALIDATE_MOD = "validate_mod";
}