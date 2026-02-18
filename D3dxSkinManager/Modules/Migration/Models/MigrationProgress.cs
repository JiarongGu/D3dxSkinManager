namespace D3dxSkinManager.Modules.Migration.Models;

/// <summary>
/// Migration progress update
/// </summary>
public class MigrationProgress
{
    public MigrationStage Stage { get; set; }
    public string CurrentTask { get; set; } = string.Empty;
    public int ProcessedItems { get; set; }
    public int TotalItems { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public int PercentComplete { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum MigrationStage
{
    Analyzing,
    CreatingDatabase,
    MigratingMetadata,
    CopyingArchives,
    CopyingPreviews,
    ConvertingConfiguration,
    ConvertingClassifications,
    Verifying,
    Finalizing,
    Complete,
    Error
}
