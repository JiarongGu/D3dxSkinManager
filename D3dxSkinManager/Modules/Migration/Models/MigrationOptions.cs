namespace D3dxSkinManager.Modules.Migration.Models;

/// <summary>
/// Migration options
/// </summary>
public class MigrationOptions
{
    public string SourcePath { get; set; } = string.Empty;
    public string? EnvironmentName { get; set; }
    public bool MigrateArchives { get; set; } = true;
    public bool MigrateMetadata { get; set; } = true;
    public bool MigratePreviews { get; set; } = true;
    public bool MigrateConfiguration { get; set; } = true;
    public bool MigrateClassifications { get; set; } = true;
    public bool MigrateCache { get; set; } = false;
    public ArchiveHandling ArchiveMode { get; set; } = ArchiveHandling.Copy;
    public PostMigrationAction PostAction { get; set; } = PostMigrationAction.Keep;
}

public enum ArchiveHandling
{
    Copy,    // Copy files (safe, requires extra space)
    Move,    // Move files (faster, modifies source)
    Link     // Create symbolic links (advanced)
}

public enum PostMigrationAction
{
    Keep,           // Keep Python installation intact
    BackupAndRemove, // Create backup zip, then remove
    Remove          // Remove Python installation
}
