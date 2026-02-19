using System;

namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// Represents a mod management profile with independent settings and configuration
/// Each profile has its own work directory, database, and configuration
/// </summary>
public class Profile
{
    /// <summary>
    /// Unique identifier for the profile
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name of the profile
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the profile
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Game directory path (where the game executable is located)
    /// Used for 3DMigoto deployment
    /// </summary>
    public string? GameDirectory { get; set; }

    /// <summary>
    /// Work directory path (base directory for mod deployment)
    /// Mods are extracted to: {WorkDirectory}/Mods/{SHA}/
    /// Can be external or internal. Typically set to game directory for direct deployment.
    /// Defaults to: {DataDirectory}/work/
    /// </summary>
    public string WorkDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Data directory path (where mod archives, thumbnails, previews, and config are stored)
    /// Defaults to: {AppDataPath}/profiles/{ProfileId}/
    /// </summary>
    public string DataDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Whether this profile is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When this profile was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this profile was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Color tag for UI differentiation (hex color code)
    /// </summary>
    public string? ColorTag { get; set; }

    /// <summary>
    /// Icon name for UI display
    /// </summary>
    public string? IconName { get; set; }

    /// <summary>
    /// Game name this profile is associated with
    /// </summary>
    public string? GameName { get; set; }

    /// <summary>
    /// Custom metadata (JSON string for extensibility)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Number of mods in this profile
    /// </summary>
    public int ModCount { get; set; }

    /// <summary>
    /// Total size of mods in bytes
    /// </summary>
    public long TotalSizeBytes { get; set; }
}
