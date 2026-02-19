using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Steps;
using D3dxSkinManager.Modules.Profiles;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Modules.Migration.Services;

/// <summary>
/// Service for migrating data from Python d3dxSkinManage to React version
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Analyze Python installation and return migration analysis
    /// </summary>
    Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath);

    /// <summary>
    /// Perform migration with specified options
    /// </summary>
    Task<MigrationResult> MigrateAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate migration by comparing source and destination
    /// </summary>
    Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath);
}

/// <summary>
/// Orchestrates migration workflow using step-based architecture
/// Each step is a separate class with clear responsibility
/// This service is now a THIN ORCHESTRATOR - delegates all work to steps
/// </summary>
public class MigrationService : IMigrationService
{
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    private readonly List<IMigrationStep> _steps;

    public MigrationService(
        IProfilePathService profilePaths,
        ILogHelper logger,
        // Inject all migration steps
        MigrationStep1AnalyzeSource step1,
        MigrationStep2MigrateConfiguration step2,
        MigrationStep3MigrateClassifications step3,
        MigrationStep4MigrateClassificationThumbnails step4,
        MigrationStep5MigrateModArchives step5,
        MigrationStep6MigrateModPreviews step6)
    {
        _profilePaths = profilePaths;
        _logger = logger;

        // Steps are automatically ordered by StepNumber
        _steps = new List<IMigrationStep> { step1, step2, step3, step4, step5, step6 }
            .OrderBy(s => s.StepNumber)
            .ToList();
    }

    /// <summary>
    /// Analyze Python installation (calls Step 1 only)
    /// </summary>
    public async Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath)
    {
        var step1 = _steps.First(s => s.StepNumber == 1) as MigrationStep1AnalyzeSource;
        if (step1 == null)
            throw new InvalidOperationException("Step 1 (Analyze Source) not found");

        var logPath = Path.Combine(_profilePaths.LogsDirectory, $"analysis_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var context = new MigrationContext
        {
            Options = new MigrationOptions { SourcePath = pythonPath },
            LogPath = logPath
        };

        await step1.ExecuteAsync(context);

        return context.Analysis ?? new MigrationAnalysis { IsValid = false };
    }

    /// <summary>
    /// Execute full migration workflow
    /// Clear step-by-step process: 1→2→3→4→5→6
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var logPath = Path.Combine(_profilePaths.LogsDirectory, $"migration_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        var context = new MigrationContext
        {
            Options = options,
            LogPath = logPath
        };

        try
        {
            await LogAsync(logPath, "=== MIGRATION WORKFLOW STARTED ===");
            await LogAsync(logPath, $"Source: {options.SourcePath}");
            await LogAsync(logPath, $"Time: {DateTime.Now}");
            await LogAsync(logPath, "");

            // Execute each step in order
            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await LogAsync(logPath, $"--- Executing: Step {step.StepNumber} - {step.StepName} ---");
                _logger.Info($"Executing Step {step.StepNumber}: {step.StepName}", "Migration");

                await step.ExecuteAsync(context, progress, cancellationToken);

                await LogAsync(logPath, $"--- Step {step.StepNumber} Complete ---");
                await LogAsync(logPath, "");
            }

            // Finalize
            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Finalizing,
                CurrentTask = "Finalizing migration...",
                PercentComplete = 95
            });

            context.Result.Success = true;
            context.Result.Duration = DateTime.Now - startTime;
            await LogAsync(logPath, "");
            await LogAsync(logPath, "=== MIGRATION COMPLETE ===");
            await LogAsync(logPath, $"Duration: {context.Result.Duration.TotalSeconds:F1}s");
            await LogAsync(logPath, $"Mods Migrated: {context.Result.ModsMigrated}");
            await LogAsync(logPath, $"Archives Copied: {context.Result.ArchivesCopied}");
            await LogAsync(logPath, $"Previews Copied: {context.Result.PreviewsCopied}");
            await LogAsync(logPath, $"Classification Rules: {context.Result.ClassificationRulesCreated}");

            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Complete,
                CurrentTask = "Migration complete!",
                PercentComplete = 100
            });

            _logger.Info($"Migration complete in {context.Result.Duration.TotalSeconds:F1}s", "Migration");
        }
        catch (Exception ex)
        {
            context.Result.Success = false;
            context.Result.Errors.Add(ex.Message);
            await LogAsync(logPath, "");
            await LogAsync(logPath, "=== MIGRATION FAILED ===");
            await LogAsync(logPath, $"ERROR: {ex.Message}");
            _logger.Error($"Migration failed: {ex.Message}", "Migration", ex);

            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Error,
                ErrorMessage = ex.Message,
                PercentComplete = 0
            });
        }

        context.Result.LogFilePath = logPath;
        return context.Result;
    }

    /// <summary>
    /// Validate migration by comparing source and destination
    /// </summary>
    public async Task<bool> ValidateMigrationAsync(string pythonPath, string reactDataPath)
    {
        // TODO: Implement validation logic
        await Task.CompletedTask;
        return true;
    }

    private async Task LogAsync(string logPath, string message)
    {
        try
        {
            var logMessage = string.IsNullOrEmpty(message)
                ? ""
                : $"[{DateTime.Now:HH:mm:ss}] {message}";
            await File.AppendAllTextAsync(logPath, logMessage + Environment.NewLine);
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
