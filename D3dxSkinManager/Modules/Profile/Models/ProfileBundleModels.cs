namespace D3dxSkinManager.Modules.Profiles.Models;

/// <summary>
/// Manifest (<c>profile.json</c>) at the root of a profile-settings bundle (.zip). Describes a PORTABLE
/// slice of a profile: metadata + config + category tree + remote libraries/tag-rules/tag-labels +
/// customized source overlays. Deliberately EXCLUDES mod archives/DB rows/previews and login
/// credentials (online-accounts.json is DPAPI-bound + global). Bundle-local DTOs (not the live domain
/// models) so the on-disk format stays stable if a domain model changes. Mirrors ModPackage's manifest.
/// </summary>
public class ProfileBundleManifest
{
    public string Version { get; set; } = "1.0";
    public string AppName { get; set; } = "D3dxSkinManager";
    public DateTime ExportDate { get; set; } = DateTime.UtcNow;

    // ----- Profile metadata -----
    public string ProfileName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? GameName { get; set; }
    /// <summary>True when the profile thumbnail was written to <c>thumbnails/profile.png</c> in the bundle.</summary>
    public bool HasThumbnail { get; set; }

    // ----- Profile configuration (config.json contents) -----
    /// <summary>The profile's configuration. Machine-specific paths (launch command, external/xxmi work
    /// dir, fix-tool interpreter) are stripped on export so the bundle is portable + leaks no local path.</summary>
    public ProfileConfiguration? Configuration { get; set; }

    // ----- Category tree (flat; ParentId links; ThumbnailFile is a bundle-relative name) -----
    public List<ProfileBundleCategory> Categories { get; set; } = new();

    // ----- Remote library data -----
    public List<ProfileBundleLibrary> Libraries { get; set; } = new();
    public List<ProfileBundleTagLabelSet> TagLabels { get; set; } = new();
    public List<ProfileBundleSourceOverlay> SourceOverlays { get; set; } = new();
}

/// <summary>A category node in the bundle. <see cref="ThumbnailFile"/> is the file name under
/// <c>thumbnails/categories/</c> in the bundle (null = no thumbnail).</summary>
public class ProfileBundleCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public int Priority { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailFile { get; set; }
}

/// <summary>A configured remote library (per-profile). Mirrors <c>RemoteLibrary</c> fields.</summary>
public class ProfileBundleLibrary
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ProfileBundleTagRule> TagRules { get; set; } = new();
    public Dictionary<string, string> ParamValues { get; set; } = new();
    public bool PreferCache { get; set; }
    public DateTime AddedAtUtc { get; set; }
}

/// <summary>An ordered tag→category import rule embedded in a library. Mirrors <c>RemoteTagRule</c>.</summary>
public class ProfileBundleTagRule
{
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? TitlePattern { get; set; }
    public string CategoryId { get; set; } = string.Empty;
}

/// <summary>Per-source tag label/alias overrides. <see cref="Labels"/> is lang → rawTag → displayLabel.</summary>
public class ProfileBundleTagLabelSet
{
    public string SourceId { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, string>> Labels { get; set; } = new();
}

/// <summary>A customized remote-source overlay. <see cref="ConfigJson"/> is the effective source config
/// serialized as JSON; applied on import only when the target machine has no local overlay for that
/// source (add-missing-only — never clobber another profile's shared customization).</summary>
public class ProfileBundleSourceOverlay
{
    public string SourceId { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
}

// ===== IPC-facing configs + results =====

/// <summary>Export request: which profile, and the OUTPUT FOLDER the <c>{name}.zip</c> is written into.</summary>
public class ProfileBundleExportConfig
{
    public string ProfileId { get; set; } = string.Empty;
    /// <summary>Folder to write the bundle into; the file is named after the (sanitized) profile name.</summary>
    public string OutputPath { get; set; } = string.Empty;
    public bool IncludeCategories { get; set; } = true;
    public bool IncludeRemote { get; set; } = true;
}

public class ProfileBundleExportResult
{
    public bool Success { get; set; }
    /// <summary>Absolute path of the written <c>.zip</c>.</summary>
    public string OutputPath { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public int CategoryCount { get; set; }
    public int LibraryCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>Read-only preview of a bundle (folder OR .zip) for the import UI. Never throws for an
/// expected bad bundle — <see cref="IsValid"/> false + <see cref="ErrorMessage"/> instead.</summary>
public class ProfileBundleAnalysis
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? GameName { get; set; }
    public DateTime ExportDate { get; set; }
    public bool HasThumbnail { get; set; }
    public int CategoryCount { get; set; }
    public int LibraryCount { get; set; }
    public int TagLabelSourceCount { get; set; }
    public int SourceOverlayCount { get; set; }
}

/// <summary>Import request. A bundle (folder OR .zip) always creates a NEW profile.</summary>
public class ProfileBundleImportConfig
{
    public string BundlePath { get; set; } = string.Empty;
    /// <summary>Optional name override for the new profile (defaults to the manifest's profile name).</summary>
    public string? NewProfileName { get; set; }
    public bool ImportCategories { get; set; } = true;
    public bool ImportRemote { get; set; } = true;
}

public class ProfileBundleImportResult
{
    public bool Success { get; set; }
    public string NewProfileId { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public int ImportedCategoryCount { get; set; }
    public int ImportedLibraryCount { get; set; }
    public int ImportedTagLabelCount { get; set; }
    public int ImportedSourceOverlayCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
