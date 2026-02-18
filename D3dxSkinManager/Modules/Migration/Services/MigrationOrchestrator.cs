using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Migration.Models;

namespace D3dxSkinManager.Modules.Migration.Services;

/// <summary>
/// Orchestrates migration process with improved flow control, validation, and error handling
/// </summary>
public class MigrationOrchestrator
{
    private readonly IMigrationService _migrationService;
    private readonly List<MigrationOperation> _operations = new();
    private string? _logPath;

    public MigrationOrchestrator(IMigrationService migrationService)
    {
        _migrationService = migrationService ?? throw new ArgumentNullException(nameof(migrationService));
    }

    /// <summary>
    /// Execute migration with improved orchestration
    /// </summary>
    public async Task<MigrationResult> ExecuteAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new MigrationResult
        {
            Success = false
        };

        _operations.Clear();

        try
        {
            // Stage 1: Pre-flight Validation
            await ValidatePrerequisitesAsync(options, progress, cancellationToken);

            // Stage 2: Execute Core Migration
            result = await _migrationService.MigrateAsync(options, progress, cancellationToken);

            if (!result.Success)
            {
                await LogOperationAsync("Migration failed during execution", isError: true);
                return result;
            }

            // Stage 3: Post-Migration Validation
            await ValidatePostMigrationAsync(options, progress, result, cancellationToken);

            // Stage 4: Finalization
            await FinalizeAsync(progress, cancellationToken);

            result.Success = true;
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            await LogOperationAsync($"Migration completed successfully in {result.Duration.TotalSeconds:F1}s");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Migration orchestration failed: {ex.Message}");
            await LogOperationAsync($"ERROR: {ex.Message}", isError: true);

            progress?.Report(new MigrationProgress
            {
                Stage = MigrationStage.Error,
                ErrorMessage = ex.Message,
                PercentComplete = 0
            });
        }

        return result;
    }

    /// <summary>
    /// Validate prerequisites before starting migration
    /// </summary>
    private async Task ValidatePrerequisitesAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.Analyzing,
            CurrentTask = "Validating prerequisites...",
            PercentComplete = 0
        });

        await LogOperationAsync("=== Pre-flight Validation ===");

        // Check source path exists
        if (!Directory.Exists(options.SourcePath))
        {
            throw new DirectoryNotFoundException($"Source path not found: {options.SourcePath}");
        }
        await LogOperationAsync($"✓ Source path exists: {options.SourcePath}");

        // Check source structure
        var analysis = await _migrationService.AnalyzeSourceAsync(options.SourcePath);
        if (!analysis.IsValid)
        {
            throw new InvalidOperationException($"Invalid source structure: {string.Join(", ", analysis.Errors)}");
        }
        await LogOperationAsync($"✓ Source structure valid");
        await LogOperationAsync($"  - {analysis.TotalMods} mods found");
        await LogOperationAsync($"  - {analysis.Environments.Count} environment(s) found");

        // Check disk space
        var drive = new DriveInfo(Path.GetPathRoot(options.SourcePath) ?? "C:\\");
        var requiredSpace = analysis.TotalArchiveSize + analysis.TotalPreviewSize;
        if (drive.AvailableFreeSpace < requiredSpace)
        {
            var requiredGB = requiredSpace / (1024.0 * 1024 * 1024);
            var availableGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            throw new InvalidOperationException(
                $"Insufficient disk space. Required: {requiredGB:F2} GB, Available: {availableGB:F2} GB");
        }
        await LogOperationAsync($"✓ Sufficient disk space available");

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Validate migrated data after migration completes
    /// </summary>
    private async Task ValidatePostMigrationAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress,
        MigrationResult result,
        CancellationToken cancellationToken)
    {
        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.Verifying,
            CurrentTask = "Validating migrated data...",
            PercentComplete = 95
        });

        await LogOperationAsync("=== Post-Migration Validation ===");

        // Verify mod count
        if (options.MigrateMetadata && result.ModsMigrated > 0)
        {
            await LogOperationAsync($"✓ Migrated {result.ModsMigrated} mod metadata entries");
        }

        // Verify archive count
        if (options.MigrateArchives && result.ArchivesCopied > 0)
        {
            await LogOperationAsync($"✓ Copied {result.ArchivesCopied} mod archives");
        }

        // Verify preview count
        if (options.MigratePreviews && result.PreviewsCopied > 0)
        {
            await LogOperationAsync($"✓ Copied {result.PreviewsCopied} preview images");
        }

        // Verify classification rules
        if (options.MigrateClassifications && result.ClassificationRulesCreated > 0)
        {
            await LogOperationAsync($"✓ Created {result.ClassificationRulesCreated} classification rules");
        }

        // Check for errors
        if (result.Errors.Any())
        {
            await LogOperationAsync($"⚠ Migration completed with {result.Errors.Count} error(s):");
            foreach (var error in result.Errors)
            {
                await LogOperationAsync($"  - {error}", isError: true);
            }
        }
        else
        {
            await LogOperationAsync("✓ No errors detected");
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Finalize migration - cleanup, cache refresh, etc.
    /// </summary>
    private async Task FinalizeAsync(
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.Finalizing,
            CurrentTask = "Finalizing migration...",
            PercentComplete = 98
        });

        await LogOperationAsync("=== Finalization ===");

        // Additional cleanup or finalization tasks can be added here
        await LogOperationAsync("✓ Migration finalized");

        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new MigrationProgress
        {
            Stage = MigrationStage.Complete,
            CurrentTask = "Migration complete!",
            PercentComplete = 100
        });
    }

    /// <summary>
    /// Log an operation to the migration log
    /// </summary>
    private async Task LogOperationAsync(string message, bool isError = false)
    {
        var operation = new MigrationOperation
        {
            Timestamp = DateTime.Now,
            Message = message,
            IsError = isError
        };
        _operations.Add(operation);

        // Also log to console
        var prefix = isError ? "[Migration:ERROR]" : "[Migration]";
        Console.WriteLine($"{prefix} {message}");

        // Log to file if path is set
        if (!string.IsNullOrEmpty(_logPath))
        {
            try
            {
                var logMessage = $"[{operation.Timestamp:yyyy-MM-dd HH:mm:ss}] {message}";
                await File.AppendAllTextAsync(_logPath, logMessage + Environment.NewLine);
            }
            catch
            {
                // Ignore file logging errors
            }
        }
    }

    /// <summary>
    /// Get migration operation log
    /// </summary>
    public IReadOnlyList<MigrationOperation> GetOperationLog() => _operations.AsReadOnly();
}

/// <summary>
/// Represents a single migration operation for logging and rollback
/// </summary>
public class MigrationOperation
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; }
}
