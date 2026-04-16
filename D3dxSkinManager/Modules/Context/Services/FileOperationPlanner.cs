using D3dxSkinManager.Modules.Context.Models;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Context.Services;

/// <summary>
/// Interface for atomic file system operation planning and execution
/// Simple buffer for file system operations - NO business logic
/// </summary>
public interface IFileOperationPlanner
{
    /// <summary>
    /// Submit a file system operation to the planner
    /// Returns a task that completes when the operation is executed
    /// Operations are executed sequentially to prevent file system conflicts
    /// </summary>
    Task<FileSystemOperationResult> SubmitOperationAsync(FileSystemOperation operation);

    /// <summary>
    /// Get the number of pending operations in the queue
    /// </summary>
    int GetPendingOperationCount();

    /// <summary>
    /// Check if there's a pending operation that matches the criteria
    /// Useful to avoid queueing duplicate operations
    /// </summary>
    bool HasPendingOperation(FileSystemOperationType operationType, string? sourcePath = null, string? targetPath = null);
}

/// <summary>
/// Atomic file system operation planner with batch processing
///
/// DESIGN - TWO PLAN MODEL:
/// - Processing Plan: Currently executing batch of operations
/// - Queued Plan: Accumulating new operations with intelligent merging
/// - When Processing Plan completes, Queued Plan becomes the new Processing Plan
/// - NO business logic - only low-level file system operations
///
/// BENEFITS:
/// - No deadlocks possible (no locks!)
/// - Operations submitted during slow extractions are batched together
/// - Intelligent merging reduces redundant work
/// - Concurrent API calls can be made safely
/// - All file operations are serialized by plan
/// </summary>
public class FileOperationPlanner : IFileOperationPlanner, IDisposable
{
    private readonly IArchiveHelper _archiveHelper;
    private readonly ILogHelper _logger;

    // Two plans: one processing, one queued
    private List<PendingOperation>? _processingPlan = null;
    private List<PendingOperation> _queuedPlan = new();

    // Lock for plan swapping (very brief, only for swapping references)
    private readonly object _planLock = new();

    // Signal for new plan ready
    private readonly SemaphoreSlim _planSignal = new(0);

    // Background worker task
    private readonly Task _workerTask;

    // Cancellation for shutdown
    private readonly CancellationTokenSource _shutdownCts = new();

    // Constants for retry logic
    private const int MAX_RETRY_ATTEMPTS = 3;
    private const int RETRY_DELAY_MS = 500;

    /// <summary>
    /// Represents a pending operation with its completion source
    /// </summary>
    private class PendingOperation
    {
        public FileSystemOperation Operation { get; set; } = null!;
        public TaskCompletionSource<FileSystemOperationResult> CompletionSource { get; set; } = null!;
    }

    public FileOperationPlanner(
        IArchiveHelper archiveHelper,
        ILogHelper logger)
    {
        _archiveHelper = archiveHelper;
        _logger = logger;

        // Start background worker with exception handling
        _workerTask = Task.Run(async () =>
        {
            try
            {
                await ProcessOperationsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error($"CRITICAL: FileOperationPlanner worker crashed on startup: {ex.Message}", "FileOperationPlanner", ex);
                throw;
            }
        });

        _logger.Info("FileOperationPlanner initialized", "FileOperationPlanner");
    }

    /// <summary>
    /// Submit a file system operation to the planner
    /// Adds to queued plan with intelligent merging
    /// Returns immediately with a task that completes when the operation is executed
    /// </summary>
    public Task<FileSystemOperationResult> SubmitOperationAsync(FileSystemOperation operation)
    {
        var tcs = new TaskCompletionSource<FileSystemOperationResult>();

        var pendingOp = new PendingOperation
        {
            Operation = operation,
            CompletionSource = tcs
        };

        bool shouldSignal = false;

        lock (_planLock)
        {
            // Try to merge with existing operations in queued plan
            var mergeResult = TryMergeWithQueuedPlan(pendingOp);

            if (mergeResult.WasMerged)
            {
                _logger.Info($"Operation {operation.OperationType} (ID: {operation.Id}) was merged: {mergeResult.Reason}", "FileOperationPlanner");

                // If completely deduplicated, return success immediately
                if (mergeResult.CancelledOperation)
                {
                    tcs.SetResult(FileSystemOperationResult.Ok());
                    return tcs.Task;
                }
            }

            // Add to queued plan
            _queuedPlan.Add(pendingOp);

            // If no processing plan, signal worker to start processing
            if (_processingPlan == null)
            {
                shouldSignal = true;
            }

            _logger.Info($"Added to queued plan: {operation.OperationType} (ID: {operation.Id}), Queued plan size: {_queuedPlan.Count}, Processing plan: {(_processingPlan == null ? "idle" : "active")}", "FileOperationPlanner");
        }

        // Signal outside of lock
        if (shouldSignal)
        {
            _planSignal.Release();
        }

        return tcs.Task;
    }

    /// <summary>
    /// Get the number of pending operations across both plans
    /// </summary>
    public int GetPendingOperationCount()
    {
        lock (_planLock)
        {
            int count = _queuedPlan.Count;
            if (_processingPlan != null)
            {
                count += _processingPlan.Count;
            }
            return count;
        }
    }

    /// <summary>
    /// Check if there's a pending operation that matches the criteria
    /// Useful to avoid queueing duplicate operations
    /// </summary>
    public bool HasPendingOperation(FileSystemOperationType operationType, string? sourcePath = null, string? targetPath = null)
    {
        lock (_planLock)
        {
            // Check both queued and processing plans
            var allPendingOps = new List<PendingOperation>();
            allPendingOps.AddRange(_queuedPlan);
            if (_processingPlan != null)
            {
                allPendingOps.AddRange(_processingPlan);
            }

            return allPendingOps.Any(p =>
                p.Operation.OperationType == operationType &&
                (sourcePath == null || string.Equals(p.Operation.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)) &&
                (targetPath == null || string.Equals(p.Operation.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>
    /// Background worker that processes plans atomically
    /// Two-plan model: Process one plan, then swap to queued plan
    /// </summary>
    private async Task ProcessOperationsAsync()
    {
        try
        {
            _logger.Info("FileOperationPlanner worker started (two-plan batch model)", "FileOperationPlanner");

            while (!_shutdownCts.Token.IsCancellationRequested)
            {
                List<PendingOperation>? planToProcess = null;

                try
                {
                    // Wait for a signal that there's a plan to process
                    await _planSignal.WaitAsync(_shutdownCts.Token).ConfigureAwait(false);

                    // Swap queued plan to processing plan
                    lock (_planLock)
                    {
                        if (_queuedPlan.Count > 0)
                        {
                            planToProcess = _queuedPlan;
                            _queuedPlan = new List<PendingOperation>();
                            _processingPlan = planToProcess;

                            _logger.Info($"Plan activated: {planToProcess.Count} operation(s) to process", "FileOperationPlanner");
                        }
                        else
                        {
                            _logger.Warn("Worker woke up but queued plan was empty!", "FileOperationPlanner");
                            continue;
                        }
                    }

                    // Execute all operations in the plan sequentially (outside of lock)
                    foreach (var pendingOp in planToProcess)
                    {
                        try
                        {
                            _logger.Info($"Executing {pendingOp.Operation.OperationType} (ID: {pendingOp.Operation.Id})", "FileOperationPlanner");

                            var result = await ExecuteOperationAsync(pendingOp.Operation).ConfigureAwait(false);

                            if (!pendingOp.CompletionSource.Task.IsCompleted)
                            {
                                pendingOp.CompletionSource.SetResult(result);
                                _logger.Info($"Completed {pendingOp.Operation.OperationType} (ID: {pendingOp.Operation.Id})", "FileOperationPlanner");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error executing {pendingOp.Operation.OperationType}: {ex.Message}", "FileOperationPlanner", ex);

                            if (!pendingOp.CompletionSource.Task.IsCompleted)
                            {
                                pendingOp.CompletionSource.SetResult(FileSystemOperationResult.Fail(ex.Message, ex));
                            }
                        }
                    }

                    _logger.Info($"Plan completed: {planToProcess.Count} operation(s) finished", "FileOperationPlanner");

                    // Mark processing plan as complete and check if there's a new queued plan
                    lock (_planLock)
                    {
                        _processingPlan = null;

                        // If new operations were queued while processing, signal to process them
                        if (_queuedPlan.Count > 0)
                        {
                            _logger.Info($"New operations queued during processing, triggering next plan", "FileOperationPlanner");
                            _planSignal.Release();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Shutdown requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error in plan processor: {ex.Message}", "FileOperationPlanner", ex);

                    // Fail all operations in the plan
                    if (planToProcess != null)
                    {
                        foreach (var op in planToProcess)
                        {
                            if (!op.CompletionSource.Task.IsCompleted)
                            {
                                op.CompletionSource.SetResult(FileSystemOperationResult.Fail($"Plan processor error: {ex.Message}", ex));
                            }
                        }
                    }

                    // Clear processing plan
                    lock (_planLock)
                    {
                        _processingPlan = null;
                    }
                }
            }

            _logger.Info("FileOperationPlanner worker stopped", "FileOperationPlanner");
        }
        catch (Exception ex)
        {
            _logger.Error($"FATAL: FileOperationPlanner worker thread crashed: {ex.Message}", "FileOperationPlanner", ex);
            throw;
        }
    }

    /// <summary>
    /// Execute a single file system operation with retry logic
    /// </summary>
    private async Task<FileSystemOperationResult> ExecuteOperationAsync(FileSystemOperation operation)
    {
        _logger.Verbose($"Executing {operation.OperationType} (ID: {operation.Id})", "FileOperationPlanner");

        return operation.OperationType switch
        {
            FileSystemOperationType.MoveDirectory => await ExecuteMoveDirectoryAsync(operation).ConfigureAwait(false),
            FileSystemOperationType.CopyFile => await ExecuteCopyFileAsync(operation).ConfigureAwait(false),
            FileSystemOperationType.DeleteDirectory => await ExecuteDeleteDirectoryAsync(operation).ConfigureAwait(false),
            FileSystemOperationType.DeleteFile => await ExecuteDeleteFileAsync(operation).ConfigureAwait(false),
            FileSystemOperationType.ExtractArchive => await ExecuteExtractArchiveAsync(operation).ConfigureAwait(false),
            FileSystemOperationType.CompressArchive => await ExecuteCompressArchiveAsync(operation).ConfigureAwait(false),
            _ => FileSystemOperationResult.Fail($"Unknown operation type: {operation.OperationType}")
        };
    }

    /// <summary>
    /// Execute a directory move operation
    /// </summary>
    private async Task<FileSystemOperationResult> ExecuteMoveDirectoryAsync(FileSystemOperation operation)
    {
        if (string.IsNullOrEmpty(operation.SourcePath) || string.IsNullOrEmpty(operation.TargetPath))
        {
            return FileSystemOperationResult.Fail("Source and target paths are required for move operation");
        }

        return await RetryOperationAsync(async () =>
        {
            // Idempotent check: if source doesn't exist but target does, operation already completed
            if (!Directory.Exists(operation.SourcePath))
            {
                if (Directory.Exists(operation.TargetPath))
                {
                    _logger.Verbose($"Move already completed (target exists): {operation.TargetPath}", "FileOperationPlanner");
                    return FileSystemOperationResult.Ok();
                }
                return FileSystemOperationResult.Fail($"Source directory does not exist: {operation.SourcePath}");
            }

            // If overwrite is enabled and target exists, delete it first
            if (operation.Overwrite && Directory.Exists(operation.TargetPath))
            {
                Directory.Delete(operation.TargetPath, true);
                await Task.Delay(100).ConfigureAwait(false); // Brief delay after delete
            }

            Directory.Move(operation.SourcePath, operation.TargetPath);
            _logger.Verbose($"Moved directory from {operation.SourcePath} to {operation.TargetPath}", "FileOperationPlanner");
            return FileSystemOperationResult.Ok();
        }, operation).ConfigureAwait(false);
    }

    /// <summary>
    /// Execute a file copy operation
    /// </summary>
    private async Task<FileSystemOperationResult> ExecuteCopyFileAsync(FileSystemOperation operation)
    {
        if (string.IsNullOrEmpty(operation.SourcePath) || string.IsNullOrEmpty(operation.TargetPath))
        {
            return FileSystemOperationResult.Fail("Source and target paths are required for copy operation");
        }

        return await RetryOperationAsync(async () =>
        {
            if (!File.Exists(operation.SourcePath))
            {
                return FileSystemOperationResult.Fail($"Source file does not exist: {operation.SourcePath}");
            }

            await Task.Run(() => File.Copy(operation.SourcePath, operation.TargetPath, operation.Overwrite)).ConfigureAwait(false);
            _logger.Verbose($"Copied file from {operation.SourcePath} to {operation.TargetPath}", "FileOperationPlanner");
            return FileSystemOperationResult.Ok();
        }, operation).ConfigureAwait(false);
    }

    /// <summary>
    /// Execute a directory delete operation
    /// </summary>
    private async Task<FileSystemOperationResult> ExecuteDeleteDirectoryAsync(FileSystemOperation operation)
    {
        if (string.IsNullOrEmpty(operation.SourcePath))
        {
            return FileSystemOperationResult.Fail("Source path is required for delete operation");
        }

        return await RetryOperationAsync(async () =>
        {
            if (!Directory.Exists(operation.SourcePath))
            {
                _logger.Verbose($"Directory already deleted or does not exist: {operation.SourcePath}", "FileOperationPlanner");
                return FileSystemOperationResult.Ok(); // Not an error if already deleted
            }

            await Task.Run(() => Directory.Delete(operation.SourcePath, true)).ConfigureAwait(false);
            _logger.Verbose($"Deleted directory: {operation.SourcePath}", "FileOperationPlanner");
            return FileSystemOperationResult.Ok();
        }, operation).ConfigureAwait(false);
    }

    /// <summary>
    /// Execute a file delete operation
    /// </summary>
    private async Task<FileSystemOperationResult> ExecuteDeleteFileAsync(FileSystemOperation operation)
    {
        if (string.IsNullOrEmpty(operation.SourcePath))
        {
            return FileSystemOperationResult.Fail("Source path is required for delete operation");
        }

        return await RetryOperationAsync(async () =>
        {
            if (!File.Exists(operation.SourcePath))
            {
                _logger.Verbose($"File already deleted or does not exist: {operation.SourcePath}", "FileOperationPlanner");
                return FileSystemOperationResult.Ok(); // Not an error if already deleted
            }

            await Task.Run(() => File.Delete(operation.SourcePath)).ConfigureAwait(false);
            _logger.Verbose($"Deleted file: {operation.SourcePath}", "FileOperationPlanner");
            return FileSystemOperationResult.Ok();
        }, operation).ConfigureAwait(false);
    }

    /// <summary>
    /// Execute an archive extraction operation
    /// </summary>
    private async Task<FileSystemOperationResult> ExecuteExtractArchiveAsync(FileSystemOperation operation)
    {
        if (string.IsNullOrEmpty(operation.SourcePath) || string.IsNullOrEmpty(operation.TargetPath))
        {
            return FileSystemOperationResult.Fail("Source and target paths are required for extract operation");
        }

        return await RetryOperationAsync(async () =>
        {
            if (!File.Exists(operation.SourcePath))
            {
                return FileSystemOperationResult.Fail($"Archive file does not exist: {operation.SourcePath}");
            }

            // Clear target directory if it exists and overwrite is enabled
            if (operation.Overwrite && Directory.Exists(operation.TargetPath))
            {
                Directory.Delete(operation.TargetPath, true);
                await Task.Delay(100).ConfigureAwait(false); // Brief delay after delete
            }

            // Call sync version directly since we're already on background worker thread
            _logger.Info($"[FileOperationPlanner] Calling _archiveHelper.ExtractArchive...", "FileOperationPlanner");
            var extractionResult = _archiveHelper.ExtractArchive(operation.SourcePath, operation.TargetPath);
            _logger.Info($"[FileOperationPlanner] _archiveHelper.ExtractArchive returned. Success={extractionResult.Success}", "FileOperationPlanner");

            if (extractionResult.Success)
            {
                _logger.Info($"Extracted archive from {operation.SourcePath} to {operation.TargetPath} ({extractionResult.FileCount} files)", "FileOperationPlanner");
                return FileSystemOperationResult.Ok(new Dictionary<string, object>
                {
                    ["fileCount"] = extractionResult.FileCount,
                    ["detectedType"] = extractionResult.DetectedType ?? ""
                });
            }
            else
            {
                _logger.Error($"Archive extraction failed for {operation.SourcePath}", "FileOperationPlanner");
                return FileSystemOperationResult.Fail("Failed to extract archive");
            }
        }, operation).ConfigureAwait(false);
    }

    /// <summary>
    /// Execute a directory compression to archive operation.
    /// Uses a two-phase approach: compress once, then retry only the file replacement.
    /// This avoids re-running expensive compression when only the final move/delete fails
    /// due to transient file locks.
    /// </summary>
    private async Task<FileSystemOperationResult> ExecuteCompressArchiveAsync(FileSystemOperation operation)
    {
        if (string.IsNullOrEmpty(operation.SourcePath) || string.IsNullOrEmpty(operation.TargetPath))
        {
            return FileSystemOperationResult.Fail("Source directory and target archive path are required for compress operation");
        }

        if (!Directory.Exists(operation.SourcePath))
        {
            return FileSystemOperationResult.Fail($"Source directory does not exist: {operation.SourcePath}");
        }

        var tempPath = operation.TempPath
            ?? Path.Combine(Path.GetDirectoryName(operation.TargetPath)!, $"{Guid.NewGuid():N}.tmp");

        try
        {
            // Phase 1: Compress cache to temp file
            // Clean up stale temp from a previous failed attempt
            if (File.Exists(tempPath))
            {
                _logger.Warn($"Stale temp file found, deleting: {tempPath}", "FileOperationPlanner");
                File.Delete(tempPath);
            }

            try
            {
                await _archiveHelper.CompressFolderAsync(operation.SourcePath, tempPath).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (ex.InnerException is IOException ioEx)
            {
                // CompressFolderAsync wraps IOException in InvalidOperationException —
                // unwrap so we can report the actual IO error
                _logger.Error($"IO error during compression: {ioEx.Message}", "FileOperationPlanner", ioEx);
                return FileSystemOperationResult.Fail(
                    $"Compression failed due to file access error: {ioEx.Message}",
                    ioEx);
            }

            // Verify compressed file was created
            if (!File.Exists(tempPath))
            {
                return FileSystemOperationResult.Fail("Compression completed but output file was not created");
            }

            // Phase 2: Replace old archive — retry only this step for transient file locks
            await ReplaceFileWithRetryAsync(tempPath, operation.TargetPath).ConfigureAwait(false);

            _logger.Info($"Compressed directory {operation.SourcePath} to {operation.TargetPath}", "FileOperationPlanner");
            return FileSystemOperationResult.Ok();
        }
        catch (IOException ioEx)
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }

            _logger.Error($"IO error during compress archive: {ioEx.Message}", "FileOperationPlanner", ioEx);
            return FileSystemOperationResult.Fail(
                $"File operation failed after {MAX_RETRY_ATTEMPTS} attempts. The archive may be in use by another process.",
                ioEx);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }

            _logger.Error($"Unexpected error during compress archive: {ex.Message}", "FileOperationPlanner", ex);
            return FileSystemOperationResult.Fail($"An unexpected error occurred: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Replace target file with source file, retrying on transient IOException.
    /// Keeps compression result intact — only retries the delete+move step.
    /// </summary>
    private async Task ReplaceFileWithRetryAsync(string sourcePath, string targetPath)
    {
        for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                File.Move(sourcePath, targetPath);
                return;
            }
            catch (IOException) when (attempt < MAX_RETRY_ATTEMPTS)
            {
                var delayMs = RETRY_DELAY_MS * attempt;
                _logger.Warn($"Retry {attempt}/{MAX_RETRY_ATTEMPTS} for file replacement after {delayMs}ms", "FileOperationPlanner");
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
            // Last attempt IOException propagates to caller
        }
    }

    /// <summary>
    /// Retry an operation with exponential backoff for transient IOException
    /// This handles file system locks and temporary access issues
    /// </summary>
    private async Task<FileSystemOperationResult> RetryOperationAsync(
        Func<Task<FileSystemOperationResult>> operation,
        FileSystemOperation fileOp)
    {
        for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (IOException ioEx) when (attempt < MAX_RETRY_ATTEMPTS)
            {
                // Retry on IOException (file/folder lock) with exponential backoff
                var delayMs = RETRY_DELAY_MS * attempt;
                _logger.Warn($"Retry {attempt}/{MAX_RETRY_ATTEMPTS} for {fileOp.OperationType} after {delayMs}ms (error: {ioEx.Message})", "FileOperationPlanner");
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
            catch (IOException ioEx) when (attempt == MAX_RETRY_ATTEMPTS)
            {
                // Final attempt failed
                _logger.Error($"Failed {fileOp.OperationType} after {MAX_RETRY_ATTEMPTS} attempts: {ioEx.Message}", "FileOperationPlanner", ioEx);
                return FileSystemOperationResult.Fail(
                    $"File system operation failed after {MAX_RETRY_ATTEMPTS} retry attempts. The folder may be in use by another process.",
                    ioEx);
            }
            catch (UnauthorizedAccessException authEx)
            {
                // Don't retry permission errors
                _logger.Error($"Access denied during {fileOp.OperationType}: {authEx.Message}", "FileOperationPlanner", authEx);
                return FileSystemOperationResult.Fail("Access denied. Please run with appropriate permissions.", authEx);
            }
            catch (Exception ex)
            {
                // Unexpected error - don't retry
                _logger.Error($"Unexpected error during {fileOp.OperationType}: {ex.Message}", "FileOperationPlanner", ex);
                return FileSystemOperationResult.Fail($"An unexpected error occurred: {ex.Message}", ex);
            }
        }

        // Should never reach here
        return FileSystemOperationResult.Fail($"Retry loop completed without returning for {fileOp.OperationType}");
    }

    /// <summary>
    /// Check if the new operation is a duplicate within the queued plan
    /// Called inside _planLock, so queued plan is stable
    /// </summary>
    private MergeResult TryMergeWithQueuedPlan(PendingOperation newOp)
    {
        // Check for identical duplicate operations (dedupe)
        var duplicate = _queuedPlan.FirstOrDefault(p =>
            p.Operation.OperationType == newOp.Operation.OperationType &&
            string.Equals(p.Operation.SourcePath, newOp.Operation.SourcePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Operation.TargetPath, newOp.Operation.TargetPath, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            return new MergeResult
            {
                WasMerged = true,
                CancelledOperation = true,
                Reason = "Identical operation already in queued plan"
            };
        }

        return new MergeResult { WasMerged = false };
    }

    public void Dispose()
    {
        _shutdownCts.Cancel();
        _planSignal.Release(); // Wake up worker
        _workerTask.Wait(TimeSpan.FromSeconds(5));
        _planSignal.Dispose();
        _shutdownCts.Dispose();
    }
}

/// <summary>
/// Result of trying to merge an operation with pending operations
/// </summary>
internal class MergeResult
{
    public bool WasMerged { get; set; }
    public bool CancelledOperation { get; set; }
    public string Reason { get; set; } = "";
}
