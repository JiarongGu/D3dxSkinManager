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
    /// Capture window position
    /// </summary>
    public CaptureWindowConfiguration? Capture { get; set; }
}

/// <summary>
/// Capture window position configuration
/// </summary>
public class CaptureWindowConfiguration
{
    public int? X { get; set; }
    public int? Y { get; set; }
}
