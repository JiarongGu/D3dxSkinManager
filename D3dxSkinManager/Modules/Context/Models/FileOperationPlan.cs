namespace D3dxSkinManager.Modules.Context.Models;

/// <summary>
/// Represents a low-level file system operation
/// The planner only handles file operations, not business logic
/// </summary>
public enum FileSystemOperationType
{
    /// <summary>
    /// Move a directory from source to target
    /// </summary>
    MoveDirectory,

    /// <summary>
    /// Copy a file from source to target
    /// </summary>
    CopyFile,

    /// <summary>
    /// Delete a directory (recursive)
    /// </summary>
    DeleteDirectory,

    /// <summary>
    /// Delete a file
    /// </summary>
    DeleteFile,

    /// <summary>
    /// Extract an archive to a directory
    /// </summary>
    ExtractArchive,

    /// <summary>
    /// Compress a directory into an archive file
    /// </summary>
    CompressArchive,

    /// <summary>
    /// Update (replace/add) a single file entry inside an existing archive without recompressing
    /// the whole archive. Fast for small edits (e.g. a keybinding .ini change).
    /// </summary>
    UpdateFileInArchive
}

/// <summary>
/// Represents a planned file system operation
/// Simple data structure - no business logic
/// </summary>
public class FileSystemOperation
{
    /// <summary>
    /// Unique identifier for this operation
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of file system operation
    /// </summary>
    public FileSystemOperationType OperationType { get; set; }

    /// <summary>
    /// Source path (for move, copy, extract operations)
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// Target path (for move, copy, extract operations)
    /// </summary>
    public string? TargetPath { get; set; }

    /// <summary>
    /// Intermediate temp path (for compress operations that need atomic replace)
    /// </summary>
    public string? TempPath { get; set; }

    /// <summary>
    /// Whether to overwrite existing files/directories
    /// </summary>
    public bool Overwrite { get; set; } = true;

    /// <summary>
    /// In-archive entry path for <see cref="FileSystemOperationType.UpdateFileInArchive"/>
    /// (relative, forward-slash, e.g. "sub/mod.ini"). SourcePath = the new file on disk;
    /// TargetPath = the archive.
    /// </summary>
    public string? ArchiveEntryPath { get; set; }

    /// <summary>
    /// Timestamp when this operation was submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of a file system operation execution
/// </summary>
public class FileSystemOperationResult
{
    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception that occurred (if any)
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Additional result data (e.g., file count for extraction)
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static FileSystemOperationResult Ok(Dictionary<string, object>? data = null)
    {
        return new FileSystemOperationResult
        {
            Success = true,
            Data = data ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static FileSystemOperationResult Fail(string errorMessage, Exception? exception = null)
    {
        return new FileSystemOperationResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}
