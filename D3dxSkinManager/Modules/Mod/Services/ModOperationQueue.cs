using System.Collections.Concurrent;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Manages per-mod and per-category operation queues to prevent concurrent operations
///
/// Two levels of locking:
/// 1. Per-mod locks (ID-based) - prevents concurrent load/unload on same mod
/// 2. Per-category locks - prevents concurrent load operations in same category
///    (needed because loading Mod B unloads Mod A if they share a category)
/// </summary>
public interface IModOperationQueue
{
    /// <summary>
    /// Enqueue an operation for a specific mod
    /// Ensures only one operation at a time per mod ID
    /// </summary>
    Task<T> EnqueueAsync<T>(string modId, Func<Task<T>> operation);

    /// <summary>
    /// Enqueue a category-wide operation (e.g., load with category-based unload)
    /// Ensures only one load operation at a time per category
    /// Use null/empty category for unclassified mods (no category lock)
    /// </summary>
    Task<T> EnqueueCategoryOperationAsync<T>(string? category, Func<Task<T>> operation);

    /// <summary>
    /// Gets the number of active per-mod locks (for testing memory leak prevention)
    /// </summary>
    int ActiveModLockCount { get; }

    /// <summary>
    /// Gets the number of active per-category locks (for testing memory leak prevention)
    /// </summary>
    int ActiveCategoryLockCount { get; }
}

public class ModOperationQueue : IModOperationQueue
{
    private readonly ILogHelper _logger;

    // Per-mod locks: Only one operation per mod ID at a time
    private readonly ConcurrentDictionary<string, LockHandle> _modLocks = new();

    // Per-category locks: Only one load operation per category at a time
    // Prevents race: Load B trying to unload A while A is still loading
    private readonly ConcurrentDictionary<string, LockHandle> _categoryLocks = new();

    // Serializes the acquire/release bookkeeping (refcount + dictionary membership) so a handle
    // can never be removed from the dictionary while another thread still holds a reference to it.
    private readonly object _bookkeepingLock = new();

    /// <summary>
    /// A reference-counted lock entry. RefCount is guarded by <see cref="_bookkeepingLock"/>.
    /// </summary>
    private sealed class LockHandle
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    public ModOperationQueue(ILogHelper logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the number of active per-mod locks (for testing memory leak prevention)
    /// </summary>
    public int ActiveModLockCount => _modLocks.Count;

    /// <summary>
    /// Gets the number of active per-category locks (for testing memory leak prevention)
    /// </summary>
    public int ActiveCategoryLockCount => _categoryLocks.Count;

    /// <summary>
    /// Enqueue operation for mod - serializes operations per ID, allows parallel across IDs
    /// </summary>
    public Task<T> EnqueueAsync<T>(string modId, Func<Task<T>> operation)
        => RunWithLockAsync(_modLocks, modId, operation, "mod");

    /// <summary>
    /// Enqueue category-wide operation - serializes operations per category
    /// Prevents race condition: Load Mod B trying to unload Mod A (same category) while A is loading
    /// </summary>
    public Task<T> EnqueueCategoryOperationAsync<T>(string? category, Func<Task<T>> operation)
    {
        // Unclassified mods (null/empty category) don't need category lock
        if (string.IsNullOrWhiteSpace(category))
        {
            return operation();
        }

        // Normalize category for consistent locking
        var normalizedCategory = category.Trim().ToLowerInvariant();
        return RunWithLockAsync(_categoryLocks, normalizedCategory, operation, "category");
    }

    /// <summary>
    /// Acquire a reference-counted lock for <paramref name="key"/>, run the operation, then release.
    ///
    /// The refcount is incremented under <see cref="_bookkeepingLock"/> BEFORE the handle can be
    /// observed by anyone else, and the entry is only removed from the dictionary when the refcount
    /// hits zero under the same lock. This closes the classic check-then-remove race where two
    /// threads could otherwise end up with different semaphore instances for the same key and run
    /// concurrently.
    /// </summary>
    private async Task<T> RunWithLockAsync<T>(
        ConcurrentDictionary<string, LockHandle> locks,
        string key,
        Func<Task<T>> operation,
        string scope)
    {
        LockHandle handle;
        lock (_bookkeepingLock)
        {
            handle = locks.GetOrAdd(key, _ => new LockHandle());
            handle.RefCount++;
        }

        if (handle.Semaphore.CurrentCount == 0)
        {
            _logger.Info($"Operation queued for {scope} '{key}' (waiting for lock)", "ModOperationQueue");
        }

        await handle.Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.Verbose($"Executing operation for {scope} '{key}'", "ModOperationQueue");
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            handle.Semaphore.Release();

            lock (_bookkeepingLock)
            {
                handle.RefCount--;
                if (handle.RefCount == 0)
                {
                    // Safe to remove: no other thread holds a reference (they would have
                    // incremented RefCount under this same lock before obtaining the handle).
                    locks.TryRemove(key, out _);
                    handle.Semaphore.Dispose();
                }
            }
        }
    }
}
