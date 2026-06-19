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
        IEnumerable<IMigrationStep> steps)
    {
        _profilePaths = profilePaths;
        _logger = logger;

        // Steps are automatically ordered by StepNumber (DI supplies them in registration order).
        _steps = steps.OrderBy(s => s.StepNumber).ToList();
    }

    /// <summary>
    /// Analyze Python installation (calls Step 1 only)
    /// </summary>
    public async Task<MigrationAnalysis> AnalyzeSourceAsync(string pythonPath)
    {
        var step1 = _steps.FirstOrDefault(s => s.StepNumber == 1);
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
        // Yield to prevent blocking UI thread
        await Task.Yield();

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

            var totalSteps = _steps.Count;

            // Execute each step in order
            foreach (var step in _steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.Info($"--- Executing: Step {step.StepNumber}/{totalSteps} - {step.StepName} ---", "Migration");

                // Report step start
                progress?.Report(new MigrationProgress
                {
                    CurrentStep = step.StepNumber,
                    TotalSteps = totalSteps,
                    StepName = step.StepName,
                    StepProgress = 0,
                    CurrentTask = $"Starting {step.StepName}...",
                    PercentComplete = GetCumulativeProgressBefore(step.StepNumber)
                });

                try
                {
                    // Create a progress wrapper that injects step information
                    var stepProgress = CreateStepProgressReporter(progress, step.StepNumber, totalSteps, step.StepName);

                    await step.ExecuteAsync(context, stepProgress, cancellationToken).ConfigureAwait(false);

                    _logger.Info($"--- Step {step.StepNumber}/{totalSteps} Complete ---", "Migration");

                    // Report step complete
                    progress?.Report(new MigrationProgress
                    {
                        CurrentStep = step.StepNumber,
                        TotalSteps = totalSteps,
                        StepName = step.StepName,
                        StepProgress = 100,
                        CurrentTask = $"{step.StepName} complete",
                        PercentComplete = GetCumulativeProgressBefore(step.StepNumber) + GetStepWeight(step.StepNumber)
                    });
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
                CurrentStep = totalSteps,
                TotalSteps = totalSteps,
                StepName = "Finalizing",
                StepProgress = 50,
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
                CurrentStep = totalSteps,
                TotalSteps = totalSteps,
                StepName = "Complete",
                StepProgress = 100,
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

    /// <summary>
    /// Get the weight (percentage) for each step based on observed durations
    /// Step 1: 0-1%    (Analyze)
    /// Step 2: 1-2%    (Create Database)
    /// Step 3: 2-15%   (Migrate Categories - 13%)
    /// Step 4: 15-20%  (Migrate Configuration - 5%)
    /// Step 5: 20-95%  (Migrate Mod Archives - 75%)
    /// Step 6: 95-100% (Migrate Previews - 5%)
    /// </summary>
    private static int GetStepWeight(int stepNumber)
    {
        return stepNumber switch
        {
            1 => 1,  // Analyze Source - 0-1%
            2 => 1,  // Create Database - 1-2%
            3 => 13, // Migrate Categories - 2-15%
            4 => 5,  // Migrate Configuration - 15-20%
            5 => 75, // Migrate Mod Archives - 20-95%
            6 => 5,  // Migrate Previews - 95-100%
            _ => 1
        };
    }

    /// <summary>
    /// Get cumulative progress percentage up to (but not including) the given step
    /// </summary>
    private static int GetCumulativeProgressBefore(int stepNumber)
    {
        int cumulative = 0;
        for (int i = 1; i < stepNumber; i++)
        {
            cumulative += GetStepWeight(i);
        }
        return cumulative;
    }

    /// <summary>
    /// Creates a progress reporter that automatically injects step information
    /// </summary>
    private IProgress<MigrationProgress>? CreateStepProgressReporter(
        IProgress<MigrationProgress>? originalProgress,
        int currentStep,
        int totalSteps,
        string stepName)
    {
        if (originalProgress == null)
            return null;

        return new Progress<MigrationProgress>(stepProgress =>
        {
            // Inject step information into progress report
            stepProgress.CurrentStep = currentStep;
            stepProgress.TotalSteps = totalSteps;
            stepProgress.StepName = stepName;

            // Calculate step progress based on processed items
            if (stepProgress.TotalItems > 0)
            {
                stepProgress.StepProgress = (stepProgress.ProcessedItems * 100) / stepProgress.TotalItems;
            }

            // Calculate overall progress using weighted distribution
            var baseProgress = GetCumulativeProgressBefore(currentStep);
            var stepWeight = GetStepWeight(currentStep);
            var stepProgressContribution = (stepProgress.StepProgress * stepWeight) / 100;
            stepProgress.PercentComplete = baseProgress + stepProgressContribution;

            originalProgress.Report(stepProgress);
        });
    }
}
