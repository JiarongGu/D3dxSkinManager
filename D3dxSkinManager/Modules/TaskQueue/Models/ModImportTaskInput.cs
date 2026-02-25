namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Input data for mod import task
/// </summary>
public class ModImportTaskInput
{
    /// <summary>
    /// Path to archive file or folder
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the path is a folder (true) or archive file (false)
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    /// Profile context for import
    /// </summary>
    public string? ProfileId { get; set; }

    // Optional metadata overrides
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Grading { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
}
