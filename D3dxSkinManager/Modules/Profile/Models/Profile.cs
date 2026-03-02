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
    /// Color tag for UI differentiation (hex color code)
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Game name this profile is associated with
    /// </summary>
    public string? GameName { get; set; }

    /// <summary>
    /// Path to thumbnail image (relative path, stored in profile data directory)
    /// </summary>
    public string? Thumbnail { get; set; }
}
