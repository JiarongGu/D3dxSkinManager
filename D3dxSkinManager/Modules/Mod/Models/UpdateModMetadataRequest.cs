namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Request model for updating mod metadata
/// Nullable fields allow partial updates - only non-null values will be applied
/// </summary>
public class UpdateModMetadataRequest
{
    public string? Name { get; set; }
    public string? Author { get; set; }
    public List<string>? Tags { get; set; }
    public string? Grading { get; set; }
    public string? Description { get; set; }
    public bool? DisablePreview { get; set; }
}
