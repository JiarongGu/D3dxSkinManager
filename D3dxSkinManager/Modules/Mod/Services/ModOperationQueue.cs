using System.Collections.Concurrent;

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
    // Per-mod semaphores: Only one operation per mod ID at a time
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _modLocks = new();

    // Per-category semaphores: Only one load operation per category at a time
    // Prevents race: Load B trying to unload A while A is still loading
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _categoryLocks = new();

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
    public async Task<T> EnqueueAsync<T>(string modId, Func<Task<T>> operation)
    {
        // Get or create semaphore for this mod (1 = only one operation at a time for this ID)
        var semaphore = _modLocks.GetOrAdd(modId, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // Execute operation with exclusive access to this mod
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();

            // Cleanup: Remove semaphore if no one is waiting (prevents memory leak)
            if (semaphore.CurrentCount == 1)
            {
                _modLocks.TryRemove(modId, out _);
            }
        }
    }

    /// <summary>
    /// Enqueue category-wide operation - serializes operations per category
    /// Prevents race condition: Load Mod B trying to unload Mod A (same category) while A is loading
    /// </summary>
    public async Task<T> EnqueueCategoryOperationAsync<T>(string? category, Func<Task<T>> operation)
    {
        // Unclassified mods (null/empty category) don't need category lock
        if (string.IsNullOrWhiteSpace(category))
        {
            return await operation().ConfigureAwait(false);
        }

        // Normalize category for consistent locking
        var normalizedCategory = category.Trim().ToLowerInvariant();

        // Get or create semaphore for this category
        var semaphore = _categoryLocks.GetOrAdd(normalizedCategory, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // Execute operation with exclusive access to this category
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();

            // Cleanup: Remove semaphore if no one is waiting (prevents memory leak)
            if (semaphore.CurrentCount == 1)
            {
                _categoryLocks.TryRemove(normalizedCategory, out _);
            }
        }
    }
}
