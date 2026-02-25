namespace D3dxSkinManager.Modules.TaskQueue.Models;

/// <summary>
/// Input for compress_folder task
/// Compresses a folder to temp directory and pauses for user metadata input
/// </summary>
public class CompressFolderTaskInput
{
    public string FolderPath { get; set; } = string.Empty;
    public string? ProfileId { get; set; }
}
