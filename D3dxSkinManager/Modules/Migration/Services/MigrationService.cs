using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Migration.Steps;
using D3dxSkinManager.Modules.Mod;

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
    /// Run a full migration for an IPC request: drives <see cref="MigrateAsync"/> while emitting throttled
    /// MIGRATION/PROGRESS events, then invalidates the category cache and emits MIGRATION/COMPLETED +
    /// MOD/REFRESHED. This is the orchestration the facade used to carry inline.
    /// </summary>
    Task<MigrationResult> RunMigrationAsync(MigrationOptions options);

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
    private readonly IProfileEventBus _eventBus;
    private readonly ICategoryService _categoryService;
    private readonly List<IMigrationStep> _steps;

    public MigrationService(
        IProfilePathService profilePaths,
        ILogHelper logger,
        IProfileEventBus eventBus,
        ICategoryService categoryService,
        IEnumerable<IMigrationStep> steps)
    {
        _profilePaths = profilePaths;
        _logger = logger;
        _eventBus = eventBus;
        _categoryService = categoryService;

        // Steps are automatically ordered by StepNumber (DI supplies them in registration order).
        _steps = steps.OrderBy(s => s.StepNumber).ToList();
    }

    /// <summary>
    /// Orchestrate a migration for an IPC request. Extracted from MigrationFacade so the facade stays a
    /// thin router: emit throttled MIGRATION/PROGRESS while running, then invalidate the category cache
    /// and emit MIGRATION/COMPLETED + MOD/REFRESHED so the frontend refreshes.
    /// </summary>
    public async Task<MigrationResult> RunMigrationAsync(MigrationOptions options)
    {
        var progress = new EventEmittingProgress(_eventBus, _logger);

        var result = await MigrateAsync(options, progress, CancellationToken.None).ConfigureAwait(false);

        // Ensure the last PROGRESS emit lands before COMPLETED (progress emits are fire-and-forget).
        await progress.DrainAsync().ConfigureAwait(false);

        // Invalidate category cache so next request gets fresh counts.
        _categoryService.InvalidateTreeCache();

        await _eventBus.EmitAsync(ModuleNames.MIGRATION, MigrationEvents.COMPLETED, result).ConfigureAwait(false);
        // Trigger a mod list reload (migration may have created many mods).
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.REFRESHED).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// <see cref="IProgress{T}"/> that emits throttled MIGRATION/PROGRESS events. Replaces the old
    /// facade-side <c>new Progress&lt;T&gt;(async ...)</c>: that async-void lambda silently swallowed
    /// EmitAsync failures and its throttle state was mutated by concurrent callbacks without a lock.
    /// Here the emit is a real Task whose failures are caught+logged, and the throttle clock is guarded.
    /// </summary>
    private sealed class EventEmittingProgress : IProgress<MigrationProgress>
    {
        private const int ThrottleMs = 200; // emit at most once per 200ms (final progress always emits)
        private readonly IProfileEventBus _eventBus;
        private readonly ILogHelper _logger;
        private readonly object _gate = new();
        private DateTime _lastEmitUtc = DateTime.MinValue;
        private Task _lastEmit = Task.CompletedTask;

        public EventEmittingProgress(IProfileEventBus eventBus, ILogHelper logger)
        {
            _eventBus = eventBus;
            _logger = logger;
        }

        public void Report(MigrationProgress value)
        {
            var isFinal = value.Stage == MigrationStage.Complete
                || value.Stage == MigrationStage.Error
                || value.PercentComplete >= 100;

            lock (_gate)
            {
                var now = DateTime.UtcNow;
                if (!isFinal && (now - _lastEmitUtc).TotalMilliseconds < ThrottleMs) return;
                _lastEmitUtc = now;
                _lastEmit = EmitSafeAsync(value);
            }
        }

        /// <summary>Await the most recent emit so a caller can guarantee the final PROGRESS has landed.</summary>
        public Task DrainAsync()
        {
            lock (_gate) return _lastEmit;
        }

        private async Task EmitSafeAsync(MigrationProgress value)
        {
            try
            {
                await _eventBus.EmitAsync(ModuleNames.MIGRATION, MigrationEvents.PROGRESS, value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to emit migration progress: {ex.Message}", "Migration", ex);
            }
        }
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
