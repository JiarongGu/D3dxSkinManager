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

    // Step tracking
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int StepProgress { get; set; }  // Progress within current step (0-100)
}

public enum MigrationStage
{
    Analyzing,
    CreatingDatabase,
    MigratingMetadata,
    CopyingArchives,
    CopyingPreviews,
    ConvertingConfiguration,
    ConvertingCategories,
    Verifying,
    Finalizing,
    Complete,
    Error
}
