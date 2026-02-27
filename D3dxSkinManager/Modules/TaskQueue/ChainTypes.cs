namespace D3dxSkinManager.Modules.TaskQueue;

/// <summary>
/// Constants for chain types used throughout the TaskQueue system
/// </summary>
public static class ChainTypes
{
    /// <summary>
    /// Interactive folder import chain with user metadata input
    /// Workflow: COMPRESS_FOLDER → [AwaitingConfirmation] → IMPORT_FROM_TEMP
    /// </summary>
    public const string FOLDER_IMPORT = "FOLDER_IMPORT";

    /// <summary>
    /// Quick folder import chain without user interaction
    /// Workflow: COMPRESS_FOLDER → [Auto] → IMPORT_FROM_TEMP
    /// </summary>
    public const string QUICK_FOLDER_IMPORT = "QUICK_FOLDER_IMPORT";

    /// <summary>
    /// Import with validation step before final import
    /// Workflow: COMPRESS_FOLDER → validate → [UserReview] → IMPORT_FROM_TEMP
    /// </summary>
    public const string VALIDATED_IMPORT = "VALIDATED_IMPORT";

    /// <summary>
    /// Batch processing chain for multiple items
    /// Workflow: configure → process_item_1 → process_item_2 → ... → complete
    /// </summary>
    public const string BATCH_PROCESSING = "BATCH_PROCESSING";
}