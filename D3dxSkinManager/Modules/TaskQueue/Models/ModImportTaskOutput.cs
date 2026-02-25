namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Output data for mod import task
/// </summary>
public class ModImportTaskOutput
{
    /// <summary>
    /// SHA of imported mod
    /// </summary>
    public string Sha { get; set; } = string.Empty;

    /// <summary>
    /// Name of imported mod
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether import was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
