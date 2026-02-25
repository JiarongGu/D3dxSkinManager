using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Steps;

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
        MigrationStep3MigrateCategories step3,
        MigrationStep4MigrateCategoryThumbnails step4,
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

        var context = new MigrationContext
        {
            Options = new MigrationOptions { SourcePath = pythonPath },
            LogPath = "" // No longer using separate log file
        };

        await step1.ExecuteAsync(context).ConfigureAwait(false);

        return context.Analysis ?? new MigrationAnalysis { IsValid = false };
    }

    /// <summary>
    /// Execute full migration workflow
    /// Clear step-by-step process
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;

        var context = new MigrationContext
        {
            Options = options,
            LogPath = "" // No longer using separate log file
        };

        try
        {
            _logger.Info("=== MIGRATION WORKFLOW STARTED ===", "Migration");
            _logger.Info($"Source: {options.SourcePath}", "Migration");
            _logger.Info($"Time: {DateTime.Now}", "Migration");

            // Execute each step in order
            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.Info($"--- Executing: Step {step.StepNumber} - {step.StepName} ---", "Migration");

                try
                {
                    await step.ExecuteAsync(context, progress, cancellationToken).ConfigureAwait(false);

                    _logger.Info($"--- Step {step.StepNumber} Complete ---", "Migration");
                }
                catch (Exception stepEx)
                {
                    // Log step-specific error
                    _logger.Error($"ERROR in Step {step.StepNumber} ({step.StepName}): {stepEx.Message}", "Migration", stepEx);

                    // Record which step failed
                    context.Result.FailedAtStep = step.StepNumber;
                    context.Result.FailedStepName = step.StepName;
                    context.Result.Errors.Add($"Step {step.StepNumber} ({step.StepName}): {stepEx.Message}");

                    // Re-throw to stop migration
                    throw;
                }
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
            _logger.Info("=== MIGRATION COMPLETE ===", "Migration");
            _logger.Info($"Duration: {context.Result.Duration.TotalSeconds:F1}s", "Migration");
            _logger.Info($"Mods Migrated: {context.Result.ModsMigrated}", "Migration");
            _logger.Info($"Archives Copied: {context.Result.ArchivesCopied}", "Migration");
            _logger.Info($"Previews Copied: {context.Result.PreviewsCopied}", "Migration");
            _logger.Info($"Category Rules: {context.Result.CategoryRulesCreated}", "Migration");

            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Complete,
                CurrentTask = "Migration complete!",
                PercentComplete = 100
            });
        }
        catch (Exception ex)
        {
            context.Result.Success = false;
            context.Result.Errors.Add(ex.Message);
            _logger.Error("=== MIGRATION FAILED ===", "Migration");
            _logger.Error($"ERROR: {ex.Message}", "Migration", ex);

            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Error,
                ErrorMessage = ex.Message,
                PercentComplete = 0
            });
        }

        context.Result.LogFilePath = ""; // No longer using separate log file
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
}
