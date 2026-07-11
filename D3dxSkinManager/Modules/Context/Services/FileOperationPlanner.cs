using D3dxSkinManager.Modules.Context.Models;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;

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
}

/// <summary>
/// File-system operation planner — PATH-OVERLAP DISPATCHER.
///
/// DESIGN:
/// - Operations whose physical paths OVERLAP (equal, or one is an ancestor of the other, across
///   Source/Target/Temp) are executed strictly one-at-a-time, preserving submission order per
///   resource — the corruption-safety guarantee.
/// - Operations on DISJOINT paths (e.g. two different mods) run in PARALLEL, bounded by a small
///   concurrency cap (disk I/O bound). This is the win: batch fix/delete/preset-apply across many
///   mods no longer serialize every compress/extract.
/// - NO business logic — only low-level file system operations. Logical per-mod multi-step atomicity
///   is Layer 2's job (IModOperationQueue), so this layer only needs PHYSICAL path-overlap safety.
///
/// Dispatch is event-driven (on submit + on completion) — no polling worker. Merge/dedup, retry,
/// and idempotency behavior are unchanged from the previous single-worker model.
/// See .claude/knowledge/filesystem-operation-serialization.md.
/// </summary>
public class FileOperationPlanner : IFileOperationPlanner, IDisposable
{
    private readonly IArchiveHelper _archiveHelper;
    private readonly IFileSystem _fileSystem;
    private readonly ILogHelper _logger;

    // Scheduling state — a path-overlap dispatcher (see the class summary). All mutated under _lock.
    private readonly object _lock = new();
    // FIFO queue of not-yet-started operations.
    private readonly LinkedList<PendingOperation> _pending = new();
    // Operations currently executing (their paths are "claimed" — nothing overlapping may start).
    private readonly List<PendingOperation> _inFlight = new();
    // Max operations that may run at once (disjoint paths only). Disk I/O bound, so a small cap.
    private readonly int _maxConcurrency;
    private bool _disposed;

    // Constants for retry logic
    private const int MAX_RETRY_ATTEMPTS = 3;
    private const int RETRY_DELAY_MS = 500;

    /// <summary>A queued/running operation + its completion source and normalized claimed paths.</summary>
    private class PendingOperation
    {
        public FileSystemOperation Operation { get; set; } = null!;
        public TaskCompletionSource<FileSystemOperationResult> CompletionSource { get; set; } = null!;
        /// <summary>Normalized (lower-cased, '\'-separated, trimmed) Source/Target/Temp paths this op mutates.</summary>
        public string[] Paths { get; set; } = global::System.Array.Empty<string>();
        /// <summary>The run task while in-flight (awaited on Dispose so an in-progress op isn't torn mid-write).</summary>
        public Task? RunTask { get; set; }
    }

    /// <param name="maxConcurrency">Max disjoint-path operations to run at once. 0 = auto
    /// (clamp(cores-1, 1, 4)). Tests pass an explicit value so parallelism is deterministic
    /// regardless of the runner's core count.</param>
    public FileOperationPlanner(
        IArchiveHelper archiveHelper,
        IFileSystem fileSystem,
        ILogHelper logger,
        int maxConcurrency = 0)
    {
        _archiveHelper = archiveHelper;
        _fileSystem = fileSystem;
        _logger = logger;
        _maxConcurrency = maxConcurrency > 0 ? maxConcurrency : Math.Clamp(Environment.ProcessorCount - 1, 1, 4);

        _logger.Info($"FileOperationPlanner initialized (maxConcurrency={_maxConcurrency})", "FileOperationPlanner");
    }

    /// <summary>
    /// Submit a file system operation. Returns a task that completes when the op executes. The op runs
    /// as soon as no in-flight op overlaps its paths and a concurrency slot is free; overlapping ops
    /// stay serialized in submission order.
    /// </summary>
    public Task<FileSystemOperationResult> SubmitOperationAsync(FileSystemOperation operation)
    {
        var tcs = new TaskCompletionSource<FileSystemOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingOp = new PendingOperation
        {
            Operation = operation,
            CompletionSource = tcs,
            Paths = NormalizePaths(operation),
        };

        lock (_lock)
        {
            if (_disposed)
            {
                tcs.SetResult(FileSystemOperationResult.Fail("File operation planner is shutting down"));
                return tcs.Task;
            }

            // Dedup: an identical op already pending or in-flight → return success without re-doing the
            // work (matches the previous model's eager-Ok dedup). The first op is added to _inFlight
            // synchronously here in the submit lock, so a back-to-back identical submit sees it.
            if (IsDuplicate(pendingOp))
            {
                _logger.Info($"Operation {operation.OperationType} (ID: {operation.Id}) deduped (identical operation already pending/in-flight)", "FileOperationPlanner");
                tcs.SetResult(FileSystemOperationResult.Ok());
                return tcs.Task;
            }

            _pending.AddLast(pendingOp);
            DispatchLocked();
        }

        return tcs.Task;
    }

    /// <summary>Number of operations not yet finished (queued + in-flight).</summary>
    public int GetPendingOperationCount()
    {
        lock (_lock) { return _pending.Count + _inFlight.Count; }
    }

    /// <summary>
    /// Start every pending op whose paths don't overlap an in-flight op OR an earlier still-pending op
    /// (the latter preserves per-resource FIFO), up to the concurrency cap. Must be called under _lock.
    /// </summary>
    private void DispatchLocked()
    {
        for (var node = _pending.First; node != null && _inFlight.Count < _maxConcurrency;)
        {
            var next = node.Next;
            var op = node.Value;
            if (!OverlapsInFlight(op) && !OverlapsEarlierPending(op, node))
            {
                _pending.Remove(node);
                _inFlight.Add(op);
                // Offload to the thread pool so no file work runs while _lock is held.
                op.RunTask = Task.Run(() => RunOperationAsync(op));
            }
            node = next;
        }
    }

    /// <summary>Execute one operation, complete its task, then release its slot and re-dispatch.</summary>
    private async Task RunOperationAsync(PendingOperation op)
    {
        FileSystemOperationResult result;
        try
        {
            _logger.Verbose($"Executing {op.Operation.OperationType} (ID: {op.Operation.Id})", "FileOperationPlanner");
            result = await ExecuteOperationAsync(op.Operation).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing {op.Operation.OperationType}: {ex.Message}", "FileOperationPlanner", ex);
            result = FileSystemOperationResult.Fail(ex.Message, ex);
        }

        op.CompletionSource.TrySetResult(result);

        lock (_lock)
        {
            _inFlight.Remove(op);
            if (!_disposed) DispatchLocked();
        }
    }

    // ---- overlap detection --------------------------------------------------------------------

    private bool OverlapsInFlight(PendingOperation op)
    {
        foreach (var f in _inFlight)
            if (PathsOverlap(op, f)) return true;
        return false;
    }

    private bool OverlapsEarlierPending(PendingOperation op, LinkedListNode<PendingOperation> self)
    {
        for (var n = _pending.First; n != null && n != self; n = n.Next)
            if (PathsOverlap(op, n.Value)) return true;
        return false;
    }

    private static bool PathsOverlap(PendingOperation a, PendingOperation b)
    {
        foreach (var pa in a.Paths)
            foreach (var pb in b.Paths)
                if (PathOverlap(pa, pb)) return true;
        return false;
    }

    /// <summary>Two normalized paths conflict if equal, or one is an ancestor of the other.</summary>
    private static bool PathOverlap(string a, string b)
        => a == b
        || a.StartsWith(b + '\\', StringComparison.Ordinal)
        || b.StartsWith(a + '\\', StringComparison.Ordinal);

    /// <summary>Normalize the op's Source/Target/Temp paths for overlap comparison (lower-case,
    /// '\'-separated, trailing-separator trimmed). These are the paths the op mutates.</summary>
    private static string[] NormalizePaths(FileSystemOperation op)
        => new[] { op.SourcePath, op.TargetPath, op.TempPath }
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant())
            .Distinct()
            .ToArray();

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
            FileSystemOperationType.UpdateFileInArchive => await ExecuteUpdateFileInArchiveAsync(operation).ConfigureAwait(false),
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
            if (!_fileSystem.DirectoryExists(operation.SourcePath))
            {
                if (_fileSystem.DirectoryExists(operation.TargetPath))
                {
                    _logger.Verbose($"Move already completed (target exists): {operation.TargetPath}", "FileOperationPlanner");
                    return FileSystemOperationResult.Ok();
                }
                return FileSystemOperationResult.Fail($"Source directory does not exist: {operation.SourcePath}");
            }

            // If overwrite is enabled and target exists, delete it first
            if (operation.Overwrite && _fileSystem.DirectoryExists(operation.TargetPath))
            {
                _fileSystem.DeleteDirectory(operation.TargetPath, true);
                await Task.Delay(100).ConfigureAwait(false); // Brief delay after delete
            }

            _fileSystem.MoveDirectory(operation.SourcePath, operation.TargetPath);
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
            if (!_fileSystem.FileExists(operation.SourcePath))
            {
                return FileSystemOperationResult.Fail($"Source file does not exist: {operation.SourcePath}");
            }

            await Task.Run(() => _fileSystem.CopyFile(operation.SourcePath, operation.TargetPath, operation.Overwrite)).ConfigureAwait(false);
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
            if (!_fileSystem.DirectoryExists(operation.SourcePath))
            {
                _logger.Verbose($"Directory already deleted or does not exist: {operation.SourcePath}", "FileOperationPlanner");
                return FileSystemOperationResult.Ok(); // Not an error if already deleted
            }

            await Task.Run(() => _fileSystem.DeleteDirectory(operation.SourcePath, true)).ConfigureAwait(false);
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
            if (!_fileSystem.FileExists(operation.SourcePath))
            {
                _logger.Verbose($"File already deleted or does not exist: {operation.SourcePath}", "FileOperationPlanner");
                return FileSystemOperationResult.Ok(); // Not an error if already deleted
            }

            await Task.Run(() => _fileSystem.DeleteFile(operation.SourcePath)).ConfigureAwait(false);
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
            if (!_fileSystem.FileExists(operation.SourcePath))
            {
                return FileSystemOperationResult.Fail($"Archive file does not exist: {operation.SourcePath}");
            }

            // Clear target directory if it exists and overwrite is enabled
            if (operation.Overwrite && _fileSystem.DirectoryExists(operation.TargetPath))
            {
                _fileSystem.DeleteDirectory(operation.TargetPath, true);
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

        if (!_fileSystem.DirectoryExists(operation.SourcePath))
        {
            return FileSystemOperationResult.Fail($"Source directory does not exist: {operation.SourcePath}");
        }

        var tempPath = operation.TempPath
            ?? Path.Combine(Path.GetDirectoryName(operation.TargetPath)!, $"{Guid.NewGuid():N}.tmp");

        try
        {
            // Phase 1: Compress cache to temp file
            // Clean up stale temp from a previous failed attempt
            if (_fileSystem.FileExists(tempPath))
            {
                _logger.Warn($"Stale temp file found, deleting: {tempPath}", "FileOperationPlanner");
                _fileSystem.DeleteFile(tempPath);
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
            if (!_fileSystem.FileExists(tempPath))
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
            if (_fileSystem.FileExists(tempPath))
            {
                try { _fileSystem.DeleteFile(tempPath); } catch { /* best effort */ }
            }

            _logger.Error($"IO error during compress archive: {ioEx.Message}", "FileOperationPlanner", ioEx);
            return FileSystemOperationResult.Fail(
                $"File operation failed after {MAX_RETRY_ATTEMPTS} attempts. The archive may be in use by another process.",
                ioEx);
        }
        catch (Exception ex)
        {
            if (_fileSystem.FileExists(tempPath))
            {
                try { _fileSystem.DeleteFile(tempPath); } catch { /* best effort */ }
            }

            _logger.Error($"Unexpected error during compress archive: {ex.Message}", "FileOperationPlanner", ex);
            return FileSystemOperationResult.Fail($"An unexpected error occurred: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Update a single entry inside an existing archive (append mode) — fast path for small edits.
    /// </summary>
    private async Task<FileSystemOperationResult> ExecuteUpdateFileInArchiveAsync(FileSystemOperation operation)
    {
        if (string.IsNullOrEmpty(operation.SourcePath) || string.IsNullOrEmpty(operation.TargetPath) || string.IsNullOrEmpty(operation.ArchiveEntryPath))
        {
            return FileSystemOperationResult.Fail("Source file, archive path, and entry path are required for update-file-in-archive");
        }
        if (!_fileSystem.FileExists(operation.SourcePath))
        {
            return FileSystemOperationResult.Fail($"Source file does not exist: {operation.SourcePath}");
        }
        if (!_fileSystem.FileExists(operation.TargetPath))
        {
            return FileSystemOperationResult.Fail($"Archive does not exist: {operation.TargetPath}");
        }
        try
        {
            await _archiveHelper.UpdateFileInArchiveAsync(operation.TargetPath, operation.SourcePath, operation.ArchiveEntryPath).ConfigureAwait(false);
            _logger.Info($"Updated entry '{operation.ArchiveEntryPath}' in archive {operation.TargetPath}", "FileOperationPlanner");
            return FileSystemOperationResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update file in archive: {ex.Message}", "FileOperationPlanner", ex);
            return FileSystemOperationResult.Fail($"Failed to update file in archive: {ex.Message}", ex);
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
                if (_fileSystem.FileExists(targetPath))
                {
                    _fileSystem.DeleteFile(targetPath);
                }
                _fileSystem.MoveFile(sourcePath, targetPath);
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

    /// <summary>Is an identical op (same type + source + target) already pending OR in-flight?
    /// Called under _lock.</summary>
    private bool IsDuplicate(PendingOperation newOp)
    {
        bool Same(PendingOperation p) =>
            p.Operation.OperationType == newOp.Operation.OperationType &&
            string.Equals(p.Operation.SourcePath, newOp.Operation.SourcePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Operation.TargetPath, newOp.Operation.TargetPath, StringComparison.OrdinalIgnoreCase);
        return _pending.Any(Same) || _inFlight.Any(Same);
    }

    public void Dispose()
    {
        Task[] running;
        lock (_lock)
        {
            _disposed = true;
            // Fail anything that never started; snapshot in-flight run tasks to await.
            foreach (var p in _pending)
                p.CompletionSource.TrySetResult(FileSystemOperationResult.Fail("File operation planner is shutting down"));
            _pending.Clear();
            running = _inFlight.Where(o => o.RunTask != null).Select(o => o.RunTask!).ToArray();
        }

        // Let in-flight file ops finish so nothing is torn mid-write (app shutdown). Bounded wait.
        try { Task.WaitAll(running, TimeSpan.FromSeconds(30)); }
        catch { /* best-effort on shutdown */ }
    }
}
