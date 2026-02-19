using System;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Migration.Models;

namespace D3dxSkinManager.Modules.Migration.Steps;

/// <summary>
/// Base interface for all migration steps
/// Each step is a separate class with clear responsibility
/// </summary>
public interface IMigrationStep
{
    /// <summary>
    /// Step number for ordering
    /// </summary>
    int StepNumber { get; }

    /// <summary>
    /// Human-readable step name
    /// </summary>
    string StepName { get; }

    /// <summary>
    /// Execute this migration step
    /// </summary>
    Task ExecuteAsync(
        MigrationContext context,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context shared across all migration steps
/// Contains all necessary information and results
/// </summary>
public class MigrationContext
{
    public required MigrationOptions Options { get; init; }
    public required string LogPath { get; init; }
    public MigrationAnalysis? Analysis { get; set; }
    public MigrationResult Result { get; set; } = new();
    public string? EnvironmentName { get; set; }
    public string? EnvironmentPath { get; set; }
}
