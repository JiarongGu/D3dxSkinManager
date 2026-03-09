using D3dxSkinManager.Modules.Mod.Services;
using Xunit;

namespace D3dxSkinManager.Tests.Modules.Mod.Services;

/// <summary>
/// Tests for ModOperationQueue concurrency control
/// Ensures proper locking behavior to prevent deadlocks and race conditions
/// Uses moderate delays (10-30ms) that are long enough to test concurrency properly
/// but shorter than typical I/O operations
///
/// IMPORTANT: These are pure unit tests with NO external dependencies:
/// - No file system operations
/// - No database access
/// - No network calls
/// - Only in-memory operations with delays sufficient to test concurrency behavior
/// </summary>
public class ModOperationQueueTests
{
    private readonly IModOperationQueue _queue;

    public ModOperationQueueTests()
    {
        _queue = new ModOperationQueue();
    }

    #region Per-Mod Lock Tests

    [Fact]
    public async Task EnqueueAsync_SingleMod_ExecutesInOrder()
    {
        // Arrange
        var sha = "test-sha-001";
        var executionOrder = new List<int>();

        // Act - Only in-memory operations, no file system access
        var task1 = _queue.EnqueueAsync(sha, async () =>
        {
            executionOrder.Add(1);
            await Task.Delay(20); // Real delay needed - FakeTimeProvider doesn't work with SemaphoreSlim
            return true;
        });

        var task2 = _queue.EnqueueAsync(sha, async () =>
        {
            executionOrder.Add(2);
            await Task.Delay(20);
            return true;
        });

        var task3 = _queue.EnqueueAsync(sha, async () =>
        {
            executionOrder.Add(3);
            return true;
        });

        await Task.WhenAll(task1, task2, task3);

        // Assert - Verify serialization by order
        Assert.Equal(new[] { 1, 2, 3 }, executionOrder);
    }

    [Fact]
    public async Task EnqueueAsync_DifferentMods_ExecutesInParallel()
    {
        // Arrange
        var sha1 = "test-sha-001";
        var sha2 = "test-sha-002";
        var sha3 = "test-sha-003";
        var startTimes = new Dictionary<string, DateTime>();
        var lockObj = new object();

        // Act - Only in-memory operations, no file system access
        var task1 = _queue.EnqueueAsync(sha1, async () =>
        {
            lock (lockObj) startTimes[sha1] = DateTime.UtcNow;
            await Task.Delay(30);
            return true;
        });

        var task2 = _queue.EnqueueAsync(sha2, async () =>
        {
            lock (lockObj) startTimes[sha2] = DateTime.UtcNow;
            await Task.Delay(30);
            return true;
        });

        var task3 = _queue.EnqueueAsync(sha3, async () =>
        {
            lock (lockObj) startTimes[sha3] = DateTime.UtcNow;
            await Task.Delay(30);
            return true;
        });

        await Task.WhenAll(task1, task2, task3);

        // Assert - all three should start around the same time (parallel execution)
        var startTimeSpan = startTimes.Values.Max() - startTimes.Values.Min();
        Assert.True(startTimeSpan.TotalMilliseconds < 50, $"Operations should start in parallel, but span was {startTimeSpan.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task EnqueueAsync_SameMod_PreventsRaceCondition()
    {
        // Arrange
        var sha = "test-sha-001";
        var counter = 0;
        var tasks = new List<Task<int>>();

        // Act - 10 concurrent operations incrementing an in-memory counter
        // This tests serialization without any file system operations
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_queue.EnqueueAsync<int>(sha, async () =>
            {
                var current = counter;
                await Task.Delay(10);
                counter = current + 1;
                return counter;
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - counter should be 10 (no race condition due to proper serialization)
        Assert.Equal(10, counter);
        Assert.Equal(10, results.Last());
    }

    [Fact]
    public async Task EnqueueAsync_ExceptionInOperation_DoesNotBlockQueue()
    {
        // Arrange
        var sha = "test-sha-001";
        var executedAfterException = false;

        // Act - Test exception handling without any file system operations
        var task1 = _queue.EnqueueAsync<bool>(sha, async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("Test exception");
        });

        var task2 = _queue.EnqueueAsync(sha, async () =>
        {
            await Task.Delay(10);
            executedAfterException = true;
            return true;
        });

        // Assert - Queue should continue processing after exception
        await Assert.ThrowsAsync<InvalidOperationException>(() => task1);
        await task2;
        Assert.True(executedAfterException, "Second operation should execute even after first one throws");
    }

    #endregion

    #region Category Lock Tests

    [Fact]
    public async Task EnqueueCategoryOperationAsync_SameCategory_ExecutesInOrder()
    {
        // Arrange
        var category = "CharacterSkins";
        var executionOrder = new List<int>();

        // Act - Only in-memory operations, no file system access
        var task1 = _queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            executionOrder.Add(1);
            await Task.Delay(20);
            return true;
        });

        var task2 = _queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            executionOrder.Add(2);
            await Task.Delay(20);
            return true;
        });

        var task3 = _queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            executionOrder.Add(3);
            return true;
        });

        await Task.WhenAll(task1, task2, task3);

        // Assert - Verify category-level serialization
        Assert.Equal(new[] { 1, 2, 3 }, executionOrder);
    }

    [Fact]
    public async Task EnqueueCategoryOperationAsync_DifferentCategories_ExecutesInParallel()
    {
        // Arrange
        var category1 = "CharacterSkins";
        var category2 = "WeaponSkins";
        var category3 = "Effects";
        var startTimes = new Dictionary<string, DateTime>();
        var lockObj = new object();

        // Act - Only in-memory operations, no file system access
        var task1 = _queue.EnqueueCategoryOperationAsync(category1, async () =>
        {
            lock (lockObj) startTimes[category1] = DateTime.UtcNow;
            await Task.Delay(30);
            return true;
        });

        var task2 = _queue.EnqueueCategoryOperationAsync(category2, async () =>
        {
            lock (lockObj) startTimes[category2] = DateTime.UtcNow;
            await Task.Delay(30);
            return true;
        });

        var task3 = _queue.EnqueueCategoryOperationAsync(category3, async () =>
        {
            lock (lockObj) startTimes[category3] = DateTime.UtcNow;
            await Task.Delay(30);
            return true;
        });

        await Task.WhenAll(task1, task2, task3);

        // Assert - Different categories should execute in parallel
        var startTimeSpan = startTimes.Values.Max() - startTimes.Values.Min();
        Assert.True(startTimeSpan.TotalMilliseconds < 50, $"Operations in different categories should start in parallel");
    }

    [Fact]
    public async Task EnqueueCategoryOperationAsync_NullCategory_ExecutesImmediately()
    {
        // Arrange
        var executed = false;

        // Act - Only in-memory flag toggle, no file system access
        var result = await _queue.EnqueueCategoryOperationAsync(null, async () =>
        {
            executed = true;
            await Task.CompletedTask; // No actual I/O
            return true;
        });

        // Assert - Null category bypasses lock
        Assert.True(executed);
        Assert.True(result);
    }

    [Fact]
    public async Task EnqueueCategoryOperationAsync_EmptyCategory_ExecutesImmediately()
    {
        // Arrange
        var executed = false;

        // Act - Only in-memory flag toggle, no file system access
        var result = await _queue.EnqueueCategoryOperationAsync("", async () =>
        {
            executed = true;
            await Task.CompletedTask; // No actual I/O
            return true;
        });

        // Assert - Empty category bypasses lock
        Assert.True(executed);
        Assert.True(result);
    }

    [Fact]
    public async Task EnqueueCategoryOperationAsync_WhitespaceCategory_ExecutesImmediately()
    {
        // Arrange
        var executed = false;

        // Act - Only in-memory flag toggle, no file system access
        var result = await _queue.EnqueueCategoryOperationAsync("   ", async () =>
        {
            executed = true;
            await Task.CompletedTask; // No actual I/O
            return true;
        });

        // Assert - Whitespace category bypasses lock
        Assert.True(executed);
        Assert.True(result);
    }

    [Fact]
    public async Task EnqueueCategoryOperationAsync_CaseInsensitive_UsesSameLock()
    {
        // Arrange
        var category1 = "CharacterSkins";
        var category2 = "characterskins"; // Different case
        var executionOrder = new List<string>();

        // Act - Only in-memory list operations, no file system access
        var task1 = _queue.EnqueueCategoryOperationAsync(category1, async () =>
        {
            executionOrder.Add("first");
            await Task.Delay(20);
            return true;
        });

        var task2 = _queue.EnqueueCategoryOperationAsync(category2, async () =>
        {
            executionOrder.Add("second");
            return true;
        });

        await Task.WhenAll(task1, task2);

        // Assert - Categories are normalized (case-insensitive)
        Assert.Equal(new[] { "first", "second" }, executionOrder);
    }

    #endregion

    #region Deadlock Prevention Tests

    [Fact]
    public async Task ConcurrentLoadOperations_SameCategory_NoDeadlock()
    {
        // Arrange
        var category = "CharacterSkins";
        var executionCount = 0;

        // Act - Simulate 5 concurrent load operations (in-memory only, no file system)
        var tasks = Enumerable.Range(0, 5).Select(i =>
            _queue.EnqueueCategoryOperationAsync(category, async () =>
            {
                await Task.Delay(10);
                Interlocked.Increment(ref executionCount);
                return true;
            })
        ).ToList();

        // Should complete without deadlock
        await Task.WhenAll(tasks);

        // Assert - All 5 operations complete successfully
        Assert.Equal(5, executionCount);
    }

    [Fact]
    public async Task PerModAndCategoryLocks_DifferentMods_NoDeadlock()
    {
        // Arrange
        var sha1 = "test-sha-001";
        var sha2 = "test-sha-002";
        var category = "CharacterSkins";
        var results = new List<string>();
        var lockObj = new object();

        // Act - Mix per-mod and category operations (in-memory only, no file system)
        var task1 = _queue.EnqueueAsync(sha1, async () =>
        {
            await Task.Delay(10);
            lock (lockObj) results.Add("mod1");
            return true;
        });

        var task2 = _queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            await Task.Delay(10);
            lock (lockObj) results.Add("category");
            return true;
        });

        var task3 = _queue.EnqueueAsync(sha2, async () =>
        {
            await Task.Delay(10);
            lock (lockObj) results.Add("mod2");
            return true;
        });

        // Should complete without deadlock
        await Task.WhenAll(task1, task2, task3);

        // Assert - All three operations complete
        Assert.Equal(3, results.Count);
        Assert.Contains("mod1", results);
        Assert.Contains("mod2", results);
        Assert.Contains("category", results);
    }

    [Fact]
    public async Task StressTest_ManyOperations_NoDeadlock()
    {
        // Arrange
        var categories = new[] { "Cat1", "Cat2", "Cat3" };
        var shas = Enumerable.Range(0, 10).Select(i => $"sha-{i:000}").ToArray();
        var tasks = new List<Task>();

        // Act - Create 50 operations mixing category and per-mod locks (in-memory only)
        for (int i = 0; i < 50; i++)
        {
            var category = categories[i % categories.Length];
            var sha = shas[i % shas.Length];

            if (i % 2 == 0)
            {
                // Category operation (no file system access)
                tasks.Add(_queue.EnqueueCategoryOperationAsync(category, async () =>
                {
                    await Task.Delay(Random.Shared.Next(5, 15)); // Short random delay
                    return true;
                }));
            }
            else
            {
                // Per-mod operation (no file system access)
                tasks.Add(_queue.EnqueueAsync(sha, async () =>
                {
                    await Task.Delay(Random.Shared.Next(5, 15)); // Short random delay
                    return true;
                }));
            }
        }

        // Should complete within reasonable time without deadlock
        var allTasksCompleted = Task.WhenAll(tasks);
        var completedTask = await Task.WhenAny(allTasksCompleted, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert - All operations should complete without deadlock
        Assert.Same(allTasksCompleted, completedTask);
        Assert.True(tasks.All(t => t.IsCompleted), "All operations should complete");
    }

    #endregion

    #region Memory Leak Prevention Tests

    [Fact]
    public async Task EnqueueAsync_CleansUpSemaphoresAfterOperations()
    {
        // Arrange
        var queue = new ModOperationQueue();
        var operations = new List<Task>();

        // Act - Create 50 operations with different SHAs
        for (int i = 0; i < 50; i++)
        {
            var sha = $"test-sha-{i:000}";
            operations.Add(queue.EnqueueAsync(sha, async () =>
            {
                await Task.Delay(10);
                return true;
            }));
        }

        // Wait for all operations to complete
        await Task.WhenAll(operations);

        // Small delay to ensure cleanup completes
        await Task.Delay(50);

        // Assert - All semaphores should be cleaned up
        Assert.Equal(0, queue.ActiveModLockCount);
    }

    [Fact]
    public async Task EnqueueCategoryOperationAsync_CleansUpSemaphoresAfterOperations()
    {
        // Arrange
        var queue = new ModOperationQueue();
        var operations = new List<Task>();

        // Act - Create 50 operations with different categories
        for (int i = 0; i < 50; i++)
        {
            var category = $"category-{i:000}";
            operations.Add(queue.EnqueueCategoryOperationAsync(category, async () =>
            {
                await Task.Delay(10);
                return true;
            }));
        }

        // Wait for all operations to complete
        await Task.WhenAll(operations);

        // Small delay to ensure cleanup completes
        await Task.Delay(50);

        // Assert - All semaphores should be cleaned up
        Assert.Equal(0, queue.ActiveCategoryLockCount);
    }

    [Fact]
    public async Task EnqueueAsync_ReusingSameSHA_MaintainsLockWhileActive()
    {
        // Arrange
        var queue = new ModOperationQueue();
        var sha = "test-sha-001";

        // Act - Start an operation but don't await it yet
        var operation1 = queue.EnqueueAsync(sha, async () =>
        {
            await Task.Delay(100);
            return true;
        });

        // Small delay to ensure first operation has acquired the lock
        await Task.Delay(10);

        // Assert - Lock should exist while operation is active
        Assert.Equal(1, queue.ActiveModLockCount);

        // Wait for operation to complete
        await operation1;

        // Small delay to ensure cleanup completes
        await Task.Delay(50);

        // Assert - Lock should be cleaned up
        Assert.Equal(0, queue.ActiveModLockCount);
    }

    #endregion

    #region Return Value Tests

    [Fact]
    public async Task EnqueueAsync_ReturnsCorrectValue()
    {
        // Arrange
        var sha = "test-sha-001";
        var expectedValue = 42;

        // Act - In-memory computation only, no file system access
        var result = await _queue.EnqueueAsync(sha, async () =>
        {
            await Task.Delay(10);
            return expectedValue;
        });

        // Assert - Return value is propagated correctly
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public async Task EnqueueCategoryOperationAsync_ReturnsCorrectValue()
    {
        // Arrange
        var category = "CharacterSkins";
        var expectedValue = "test-result";

        // Act - In-memory computation only, no file system access
        var result = await _queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            await Task.Delay(10);
            return expectedValue;
        });

        // Assert - Return value is propagated correctly
        Assert.Equal(expectedValue, result);
    }

    #endregion
}
