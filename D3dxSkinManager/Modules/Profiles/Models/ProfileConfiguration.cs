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
    /// Archive handling mode (Copy/Move/Link)
    /// </summary>
    public string ArchiveHandlingMode { get; set; } = "Copy";

    /// <summary>
    /// Default grading for new mods
    /// </summary>
    public string DefaultGrading { get; set; } = "G";

    /// <summary>
    /// Whether to generate thumbnails automatically
    /// </summary>
    public bool AutoGenerateThumbnails { get; set; } = true;

    /// <summary>
    /// Whether to auto-classify mods based on patterns
    /// </summary>
    public bool AutoClassifyMods { get; set; } = true;

    /// <summary>
    /// Classification patterns (JSON string)
    /// </summary>
    public string? ClassificationPatterns { get; set; }

    /// <summary>
    /// Thumbnail matching algorithm (key-in-only, similarity-only, similarity-threshold, similarity-keyin)
    /// </summary>
    public string ThumbnailAlgorithm { get; set; } = "similarity-threshold";

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
    /// Custom settings (JSON string for extensibility)
    /// </summary>
    public string? CustomSettings { get; set; }
}
