namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// Profile configuration settings stored in {profileId}/config.json
/// </summary>
public class ProfileConfiguration
{
    /// <summary>
    /// Profile ID this configuration belongs to
    /// </summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>
    /// 3DMigoto version to use (3dmigoto, 3dmigoto-dev, custom)
    /// </summary>
    public string MigotoVersion { get; set; } = "3dmigoto";

    /// <summary>
    /// Work directory configuration (parent of Mods folder)
    /// </summary>
    public WorkDirectoryConfiguration Work { get; set; } = new WorkDirectoryConfiguration();

    /// <summary>
    /// Window positions and sizes for secondary windows (e.g., "capture", "debug")
    /// Key: window name, Value: window configuration (position and size)
    /// </summary>
    public Dictionary<string, WindowConfiguration> Windows { get; set; } = new Dictionary<string, WindowConfiguration>();
}

/// <summary>
/// Window position and size configuration for any secondary window
/// </summary>
public class WindowConfiguration
{
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}
