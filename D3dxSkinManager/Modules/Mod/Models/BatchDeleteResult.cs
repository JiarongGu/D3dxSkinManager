namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Result of a batch delete operation
/// </summary>
public class BatchDeleteResult
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> FailedShas { get; set; } = new();
}
