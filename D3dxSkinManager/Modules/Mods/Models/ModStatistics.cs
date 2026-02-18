using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Mods.Models;

/// <summary>
/// Statistics about mods in the database
/// </summary>
public class ModStatistics
{
    public int TotalMods { get; set; }
    public int LoadedMods { get; set; }
    public int AvailableMods { get; set; }
    public int UniqueObjects { get; set; }
    public int UniqueAuthors { get; set; }
    public Dictionary<string, int> ModsByGrading { get; set; } = new();
}
