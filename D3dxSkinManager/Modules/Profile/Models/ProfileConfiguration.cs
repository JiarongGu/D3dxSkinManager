namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// Profile configuration settings
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
    /// Game executable path for this profile
    /// </summary>
    public string? GamePath { get; set; }

    /// <summary>
    /// Game launch arguments
    /// </summary>
    public string? GameLaunchArgs { get; set; }

    /// <summary>
    /// Custom program executable path
    /// </summary>
    public string? CustomProgramPath { get; set; }

    /// <summary>
    /// Custom program launch arguments
    /// </summary>
    public string? CustomProgramArgs { get; set; }

    /// <summary>
    /// Mod cache storage configuration
    /// </summary>
    public ModCacheConfiguration ModCache { get; set; } = new ModCacheConfiguration();
}
