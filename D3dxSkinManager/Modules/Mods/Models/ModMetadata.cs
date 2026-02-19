namespace D3dxSkinManager.Modules.Mods.Models
{

    /// <summary>
    /// Metadata extracted from mod files
    /// </summary>
    public class ModMetadata
    {
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string? Grading { get; set; }
        public List<string>? Tags { get; set; }
    }
}
