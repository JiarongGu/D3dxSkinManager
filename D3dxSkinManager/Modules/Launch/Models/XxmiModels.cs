namespace D3dxSkinManager.Modules.Launch.Models;

/// <summary>
/// A single XXMI model importer (GIMI/SRMI/WWMI/ZZMI/HIMI/EFMI/...) discovered in an
/// XXMI Launcher install. Each importer is a self-contained 3DMigoto with its own Mods folder.
/// </summary>
public class XxmiImporter
{
    /// <summary>Importer code, e.g. "ZZMI".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Absolute path to the importer folder (parent of Mods), e.g.
    /// {launcher}\ZZMI. This is what our work directory should point at.
    /// </summary>
    public string ImporterDir { get; set; } = string.Empty;

    /// <summary>Absolute path to the importer's Mods folder (= ImporterDir\Mods).</summary>
    public string ModsDir { get; set; } = string.Empty;

    /// <summary>Configured game folder for this importer (may be empty if not set up).</summary>
    public string? GameFolder { get; set; }

    /// <summary>True if this is the launcher's currently active importer.</summary>
    public bool IsActive { get; set; }

    /// <summary>True if the importer folder actually exists on disk (installed).</summary>
    public bool IsInstalled { get; set; }
}

/// <summary>
/// Result of probing a folder for an XXMI Launcher install.
/// </summary>
public class XxmiDetectResult
{
    /// <summary>True if the folder is a valid XXMI Launcher install.</summary>
    public bool Found { get; set; }

    /// <summary>Absolute path to "XXMI Launcher.exe" (Resources\Bin), if present.</summary>
    public string? LauncherExe { get; set; }

    /// <summary>Absolute path to "XXMI Launcher Config.json".</summary>
    public string? ConfigPath { get; set; }

    /// <summary>Enabled importers parsed from the launcher config, with resolved paths.</summary>
    public List<XxmiImporter> Importers { get; set; } = new();
}
