namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Output from compress_folder task
/// Contains temp archive path for next phase
/// </summary>
public class CompressFolderTaskOutput
{
    public string TempArchivePath { get; set; } = string.Empty;
    public string OriginalFolderPath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
