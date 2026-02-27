namespace D3dxSkinManager.Modules.TaskQueue;

/// <summary>
/// Constants for task types used throughout the TaskQueue system
/// </summary>
public static class TaskTypes
{
    /// <summary>
    /// Mod import task - imports a mod from file or folder
    /// </summary>
    public const string MOD_IMPORT = "MOD_IMPORT";

    /// <summary>
    /// Compress folder task - compresses a folder to temporary archive
    /// </summary>
    public const string COMPRESS_FOLDER = "COMPRESS_FOLDER";

    /// <summary>
    /// Import from temp task - imports mod from temporary archive with metadata
    /// </summary>
    public const string IMPORT_FROM_TEMP = "IMPORT_FROM_TEMP";

    // Future task types can be added here
    // public const string MOD_EXPORT = "MOD_EXPORT";
    // public const string BATCH_PROCESS = "BATCH_PROCESS";
    // public const string VALIDATE_MOD = "VALIDATE_MOD";
}