namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Input for import_from_temp task
/// Imports a mod from temp archive with user-provided metadata
/// </summary>
public class ImportFromTempTaskInput
{
    public string TempArchivePath { get; set; } = string.Empty;
    public string? ProfileId { get; set; }

    // User-provided metadata
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Grading { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
}
