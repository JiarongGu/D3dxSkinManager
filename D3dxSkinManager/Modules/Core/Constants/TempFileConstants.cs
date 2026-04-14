namespace D3dxSkinManager.Modules.Core.Constants;

/// <summary>
/// Constants for temporary file naming patterns used across the application.
///
/// Temp file inventory (all scanned by FileCleanupService):
///
/// | Extension/Pattern   | Location                              | Created by                    | Cleanup                       |
/// |---------------------|---------------------------------------|-------------------------------|-------------------------------|
/// | .mic                | {ProfilePath}/temp/{workflowId}.mic   | ModImportWorkflowHandler      | Workflow cancel/complete       |
/// | .auc                | {ProfilePath}/temp/{modId}.auc        | ModArchiveService             | Finally block / cleanup tool  |
/// | _temp_reorder{ext}  | {ProfilePath}/previews/{id}/          | ImageService                  | Reorder complete / cleanup    |
/// | .tmp                | Adjacent to target file               | SettingFileService (atomic)   | File.Move (immediate)         |
///
/// RULE: NEVER use Path.GetTempPath() / system temp folder.
/// Always use {ProfilePath}/temp/ — same drive as data, managed by FileCleanupService.
///
/// When adding a new temp file pattern:
/// 1. Add constant + helper here
/// 2. Add scan pattern in FileCleanupService.ScanOrphanedTempFiles()
/// 3. Update this table
/// </summary>
public static class TempFileConstants
{
    // ==================== Temp File Extensions ====================

    /// <summary>
    /// Extension for mod import folder compression temp files.
    /// Format: "{workflowId}.mic" (7z file with .mic extension)
    /// Location: {ProfilePath}/temp/{workflowId}.mic
    /// Used by: ModImportWorkflowHandler (folder import compression step)
    /// Cleanup: Workflow module handles cleanup on cancellation/completion
    /// </summary>
    public const string MOD_IMPORT_COMPRESS_EXT = ".mic";

    /// <summary>
    /// Prefix for preview image reordering temp files.
    /// Format: "_temp_reorder{original_extension}"
    /// Location: {ProfilePath}/previews/{id}/_temp_reorder{ext}
    /// Used by: ImageService
    /// Cleanup: Automatically after reorder completion or on error recovery
    /// Note: This is in previews dir, not temp dir (legacy pattern)
    /// </summary>
    public const string PREVIEW_REORDER_PREFIX = "_temp_reorder";

    /// <summary>
    /// Extension for archive update (compress cache to archive) temp files.
    /// Format: "{modId}.auc" (7z file with .auc extension)
    /// Location: {ProfilePath}/temp/{modId}.auc
    /// Used by: ModArchiveService → FileOperationPlanner (CompressArchive operation)
    /// Cleanup: Finally block in planner / FileCleanupService
    /// </summary>
    public const string ARCHIVE_UPDATE_COMPRESS_EXT = ".auc";

    /// <summary>
    /// Create an archive update compress temp file name.
    /// Caller combines with profile temp directory to get full path.
    /// </summary>
    public static string GetArchiveUpdateTempName(string modId) =>
        $"{modId}{ARCHIVE_UPDATE_COMPRESS_EXT}";

    /// <summary>
    /// Check if a name matches the archive update compress temp pattern (.auc extension).
    /// </summary>
    public static bool IsArchiveUpdateCompressTemp(string name) =>
        name.EndsWith(ARCHIVE_UPDATE_COMPRESS_EXT, StringComparison.Ordinal);

    // ==================== Atomic Write Suffix ====================

    /// <summary>
    /// Suffix for atomic file write operations (settings files).
    /// Format: "{filename}.tmp"
    /// Location: Various locations (settings, config files)
    /// Used by: SettingFileService
    /// Cleanup: Automatically via File.Move (atomic operation)
    /// </summary>
    public const string ATOMIC_WRITE_SUFFIX = ".tmp";

    // ==================== Helper Methods ====================

    /// <summary>
    /// Create a mod import compress temp file name from workflow ID.
    /// </summary>
    /// <param name="workflowId">Workflow unique identifier</param>
    /// <returns>Filename: "{workflowId}.mic"</returns>
    public static string GetModImportCompressTempName(string workflowId) => $"{workflowId}{MOD_IMPORT_COMPRESS_EXT}";

    /// <summary>
    /// Create a preview reorder temp file name with extension.
    /// </summary>
    /// <param name="extension">File extension (e.g., ".png")</param>
    /// <returns>Filename: "_temp_reorder{extension}"</returns>
    public static string GetPreviewReorderTempName(string extension) => $"{PREVIEW_REORDER_PREFIX}{extension}";

    /// <summary>
    /// Create an atomic write temp file path from original file path.
    /// </summary>
    /// <param name="originalPath">Original file path</param>
    /// <returns>Temp file path: "{originalPath}.tmp"</returns>
    public static string GetAtomicWriteTempPath(string originalPath) => $"{originalPath}{ATOMIC_WRITE_SUFFIX}";

    // ==================== Pattern Matching ====================

    /// <summary>
    /// Check if a name matches the mod import compress temp pattern (.mic extension).
    /// </summary>
    public static bool IsModImportCompressTemp(string name) => name.EndsWith(MOD_IMPORT_COMPRESS_EXT, StringComparison.Ordinal);

    /// <summary>
    /// Check if a name matches the preview reorder temp pattern.
    /// </summary>
    public static bool IsPreviewReorderTemp(string name) => name.StartsWith(PREVIEW_REORDER_PREFIX, StringComparison.Ordinal);
}
