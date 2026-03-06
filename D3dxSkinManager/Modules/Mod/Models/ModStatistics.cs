using System.Collections.Generic;

namespace D3dxSkinManager.Modules.Mod.Models;

/// <summary>
/// Statistics about mods in the database
/// </summary>
public class ModStatistics
{
    public int TotalMods { get; set; }
    public int LoadedMods { get; set; }
    public int AvailableMods { get; set; }
    public int TotalCategories { get; set; }
    public int TotalAuthors { get; set; }
}
