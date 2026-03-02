namespace D3dxSkinManager.Modules.Workflow.Models;

/// <summary>
/// Context for MOD_IMPORT workflow type
/// Stored as JSON in Workflow.Context
/// </summary>
public class ModImportWorkflowContext
{
    /// <summary>
    /// Current step in the workflow
    /// </summary>
    public string? Step { get; set; }

    /// <summary>
    /// Original folder path to import
    /// </summary>
    public string? FolderPath { get; set; }

    /// <summary>
    /// Temporary archive path (created after compression)
    /// </summary>
    public string? TempArchivePath { get; set; }

    /// <summary>
    /// Whether the import source is an archive file (true) or folder (false)
    /// Used to determine if temp files should be deleted
    /// </summary>
    public bool IsArchiveFile { get; set; }

    /// <summary>
    /// Detected folder name
    /// </summary>
    public string? FolderName { get; set; }

    /// <summary>
    /// Number of files in the folder
    /// </summary>
    public int? FileCount { get; set; }

    /// <summary>
    /// Current progress percentage (0-100)
    /// </summary>
    public int? Progress { get; set; }

    // Metadata fields (user can edit these)
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }  // Category ID
    public string? CategoryName { get; set; }  // Category name (for display)
    public List<string>? Tags { get; set; }
    public string? Grading { get; set; }

    /// <summary>
    /// SHA of the imported mod (after successful import)
    /// </summary>
    public string? ImportedModSha { get; set; }
}

/// <summary>
/// Workflow step constants for MOD_IMPORT
/// </summary>
public static class ModImportWorkflowSteps
{
    public const string ExtractMetadata = "extract_metadata";
    public const string CompressFolder = "compress_folder";
    public const string ImportMod = "import_mod";
}
