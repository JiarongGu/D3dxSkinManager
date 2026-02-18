using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Migration.Models;

/// <summary>
/// Python mod index entry
/// </summary>
public class PythonModEntry
{
    public string Sha { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public string Type { get; set; } = "7z";
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Grading { get; set; } = "G";
    public string Explain { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
