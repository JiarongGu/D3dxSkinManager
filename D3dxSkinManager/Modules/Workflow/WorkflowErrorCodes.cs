namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Error codes for workflow failures
/// These codes are used by the frontend for i18n translation
/// </summary>
public static class WorkflowErrorCodes
{
    // Mod Import Workflow Errors (MI_ prefix)
    public const string MI_DUPLICATE_MOD = "WORKFLOW_MI_DUPLICATE_MOD";
    public const string MI_FOLDER_NOT_FOUND = "WORKFLOW_MI_FOLDER_NOT_FOUND";
    public const string MI_UNSUPPORTED_FILE_TYPE = "WORKFLOW_MI_UNSUPPORTED_FILE_TYPE";
    public const string MI_PASSWORD_PROTECTED = "WORKFLOW_MI_PASSWORD_PROTECTED";
    public const string MI_ARCHIVE_COMPRESSION_FAILED = "WORKFLOW_MI_ARCHIVE_COMPRESSION_FAILED";
    public const string MI_ARCHIVE_EXTRACTION_FAILED = "WORKFLOW_MI_ARCHIVE_EXTRACTION_FAILED";
    public const string MI_METADATA_EXTRACTION_FAILED = "WORKFLOW_MI_METADATA_EXTRACTION_FAILED";
    public const string MI_IMPORT_FAILED = "WORKFLOW_MI_IMPORT_FAILED";
    public const string MI_CANCELLED = "WORKFLOW_MI_CANCELLED";

    // Generic Workflow Errors
    public const string UNKNOWN_ERROR = "WORKFLOW_UNKNOWN_ERROR";
}
