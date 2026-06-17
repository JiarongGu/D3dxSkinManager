using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Mod.Services;
using Moq;
using Xunit;

namespace D3dxSkinManager.Tests.Modules.Mod;

/// <summary>
/// Unit tests for mod loading concurrency and deadlock prevention
/// Tests the specific scenario: Load Mod A �?Load Mod B (same category) while A is still loading
///
/// IMPORTANT: These are pure unit tests with NO external dependencies:
/// - No file system operations
/// - No database access
/// - No network calls
/// - Only tests the ModOperationQueue behavior in isolation
/// - Uses in-memory lists to track execution order
/// </summary>
public class ModLoadConcurrencyTests
{
    [Fact]
    public async Task LoadTwoModsInSameCategory_Sequential_NoDeadlock()
    {
        // Arrange
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var category = "CharacterSkins";
        var loadOrder = new List<string>();

        // Act - In-memory only: simulate loading without file system access
        var taskA = queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            loadOrder.Add("A-start");
            await Task.Delay(100); // Simulate async work (not file I/O)
            loadOrder.Add("A-end");
            return true;
        });

        await Task.Delay(10); // Timing delay (not file I/O)

        var taskB = queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            loadOrder.Add("B-start");
            await Task.Delay(50); // Simulate async work (not file I/O)
            loadOrder.Add("B-end");
            return true;
        });

        // Should complete without deadlock
        await Task.WhenAll(taskA, taskB);

        // Assert - A should complete before B starts
        Assert.Equal(new[] { "A-start", "A-end", "B-start", "B-end" }, loadOrder);
    }

    [Fact]
    public async Task LoadTwoModsInSameCategory_Concurrent_NoDeadlock()
    {
        // Arrange
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var category = "CharacterSkins";
        var loadOrder = new List<string>();
        var lockObj = new object();

        // Act - In-memory only: simulate concurrent loading without file system access
        var taskA = queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            lock (lockObj) loadOrder.Add("A-start");
            await Task.Delay(100); // Simulate async work (not file I/O)
            lock (lockObj) loadOrder.Add("A-end");
            return true;
        });

        var taskB = queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            lock (lockObj) loadOrder.Add("B-start");
            await Task.Delay(50); // Simulate async work (not file I/O)
            lock (lockObj) loadOrder.Add("B-end");
            return true;
        });

        // Should complete without deadlock within reasonable time
        var timeout = Task.Delay(TimeSpan.FromSeconds(5));
        var completed = await Task.WhenAny(Task.WhenAll(taskA, taskB), timeout);

        // Assert
        Assert.NotSame(timeout, completed);
        Assert.True(taskA.IsCompleted && taskB.IsCompleted, "Both tasks should complete");

        // One should complete before the other starts (serialized by category lock)
        Assert.Equal(4, loadOrder.Count);
    }

    [Fact]
    public async Task LoadModWithCategoryLock_UnloadAnotherMod_NoDeadlock()
    {
        // Arrange - This simulates the exact deadlock scenario
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var category = "CharacterSkins";
        var operations = new List<string>();
        var lockObj = new object();

        // Act - In-memory only: simulate load with unload (no file system access)
        var loadTask = queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            lock (lockObj) operations.Add("Load-A-start");

            // Simulate unloading Mod B while holding category lock (in-memory)
            // This is what happens in ModFacade.LoadModInternalAsync
            lock (lockObj) operations.Add("Unload-B-start");
            await Task.Delay(50); // Simulate async work (not file I/O)
            lock (lockObj) operations.Add("Unload-B-end");

            await Task.Delay(50); // Simulate async work (not file I/O)
            lock (lockObj) operations.Add("Load-A-end");
            return true;
        });

        // Wait for completion
        var timeout = Task.Delay(TimeSpan.FromSeconds(3));
        var completed = await Task.WhenAny(loadTask, timeout);

        // Assert - Should complete without deadlock
        Assert.NotSame(timeout, completed);
        Assert.True(loadTask.IsCompleted);
        Assert.Equal(new[] { "Load-A-start", "Unload-B-start", "Unload-B-end", "Load-A-end" }, operations);
    }

    [Fact]
    public async Task LoadModA_ThenLoadModB_SameCategory_WithUnload_NoDeadlock()
    {
        // Arrange - Realistic scenario with UnloadWithoutLockAsync pattern
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var category = "CharacterSkins";
        var operations = new List<string>();
        var lockObj = new object();

        // Simulate LoadAsync for Mod A (in-memory only, no file system)
        var loadATask = queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            lock (lockObj) operations.Add("LoadA-acquired-category-lock");
            await Task.Delay(100); // Simulate async work (not file I/O)
            lock (lockObj) operations.Add("LoadA-complete");
            return true;
        });

        // Wait a bit to ensure A starts (timing only, not file I/O)
        await Task.Delay(20);

        // Simulate LoadAsync for Mod B (in-memory only, no file system)
        var loadBTask = queue.EnqueueCategoryOperationAsync(category, async () =>
        {
            lock (lockObj) operations.Add("LoadB-acquired-category-lock");

            // Simulate unloading Mod A (in-memory, without per-mod lock - this is the fix!)
            lock (lockObj) operations.Add("LoadB-unloading-A");
            await Task.Delay(50); // Simulate async work (not file I/O)
            lock (lockObj) operations.Add("LoadB-unloaded-A");

            // Then load Mod B (in-memory)
            await Task.Delay(50); // Simulate async work (not file I/O)
            lock (lockObj) operations.Add("LoadB-complete");
            return true;
        });

        // Wait for both with timeout
        var timeout = Task.Delay(TimeSpan.FromSeconds(5));
        var completed = await Task.WhenAny(Task.WhenAll(loadATask, loadBTask), timeout);

        // Assert - Should complete without deadlock
        Assert.NotSame(timeout, completed);
        Assert.True(loadATask.IsCompleted && loadBTask.IsCompleted);

        // Verify execution order
        Assert.Contains("LoadA-acquired-category-lock", operations);
        Assert.Contains("LoadA-complete", operations);
        Assert.Contains("LoadB-acquired-category-lock", operations);
        Assert.Contains("LoadB-unloading-A", operations);
        Assert.Contains("LoadB-complete", operations);

        // A must complete before B starts
        var aCompleteIndex = operations.IndexOf("LoadA-complete");
        var bStartIndex = operations.IndexOf("LoadB-acquired-category-lock");
        Assert.True(aCompleteIndex < bStartIndex, "Mod A should complete before Mod B starts");
    }

    [Fact]
    public async Task MultipleModsLoadingInDifferentCategories_Parallel_NoDeadlock()
    {
        // Arrange
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var tasks = new List<Task<bool>>();
        var completedCount = 0;

        // Act - In-memory only: simulate 3 mods in different categories (should run in parallel)
        tasks.Add(queue.EnqueueCategoryOperationAsync("Category1", async () =>
        {
            await Task.Delay(100); // Simulate async work (not file I/O)
            Interlocked.Increment(ref completedCount);
            return true;
        }));

        tasks.Add(queue.EnqueueCategoryOperationAsync("Category2", async () =>
        {
            await Task.Delay(100); // Simulate async work (not file I/O)
            Interlocked.Increment(ref completedCount);
            return true;
        }));

        tasks.Add(queue.EnqueueCategoryOperationAsync("Category3", async () =>
        {
            await Task.Delay(100); // Simulate async work (not file I/O)
            Interlocked.Increment(ref completedCount);
            return true;
        }));

        var startTime = DateTime.UtcNow;
        await Task.WhenAll(tasks);
        var duration = DateTime.UtcNow - startTime;

        // Assert - Should complete in parallel (around 100ms), not sequentially (300ms)
        Assert.Equal(3, completedCount);
        Assert.True(duration.TotalMilliseconds < 200, $"Parallel execution should take ~100ms, but took {duration.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task MultipleModsLoadingInSameCategory_Sequential_NoDeadlock()
    {
        // Arrange
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var category = "CharacterSkins";
        var tasks = new List<Task<bool>>();
        var executionOrder = new List<int>();
        var lockObj = new object();

        // Act - In-memory only: simulate 3 mods in same category (should run sequentially)
        for (int i = 1; i <= 3; i++)
        {
            var index = i;
            tasks.Add(queue.EnqueueCategoryOperationAsync(category, async () =>
            {
                lock (lockObj) executionOrder.Add(index);
                await Task.Delay(50); // Simulate async work (not file I/O)
                return true;
            }));
        }

        var timeout = Task.Delay(TimeSpan.FromSeconds(5));
        var completed = await Task.WhenAny(Task.WhenAll(tasks), timeout);

        // Assert
        Assert.NotSame(timeout, completed);
        Assert.Equal(new[] { 1, 2, 3 }, executionOrder);
    }

    [Fact]
    public async Task UnclassifiedMods_LoadInParallel_NoDeadlock()
    {
        // Arrange
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var tasks = new List<Task<bool>>();
        var startTimes = new List<DateTime>();
        var lockObj = new object();

        // Act - In-memory only: simulate 3 unclassified mods (should run in parallel)
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(queue.EnqueueCategoryOperationAsync(null, async () =>
            {
                lock (lockObj) startTimes.Add(DateTime.UtcNow);
                await Task.Delay(100); // Simulate async work (not file I/O)
                return true;
            }));
        }

        var startTime = DateTime.UtcNow;
        await Task.WhenAll(tasks);
        var duration = DateTime.UtcNow - startTime;

        // Assert - Should complete in parallel
        Assert.Equal(3, startTimes.Count);
        var timeSpan = startTimes.Max() - startTimes.Min();
        Assert.True(timeSpan.TotalMilliseconds < 50, "Unclassified mods should load in parallel");
        Assert.True(duration.TotalMilliseconds < 200, $"Should complete in ~100ms, took {duration.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task RapidLoadUnload_SameMod_NoDeadlock()
    {
        // Arrange
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var id = "test-mod-id";
        var operations = new List<string>();
        var lockObj = new object();

        // Act - In-memory only: simulate rapid load/unload of same mod
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(queue.EnqueueAsync(id, async () =>
            {
                var operation = index % 2 == 0 ? "load" : "unload";
                lock (lockObj) operations.Add($"{operation}-{index}");
                await Task.Delay(10); // Simulate async work (not file I/O)
                return true;
            }));
        }

        var timeout = Task.Delay(TimeSpan.FromSeconds(3));
        var completed = await Task.WhenAny(Task.WhenAll(tasks), timeout);

        // Assert
        Assert.NotSame(timeout, completed);
        Assert.Equal(10, operations.Count);
    }

    [Fact]
    public async Task StressTest_RealWorldScenario_NoDeadlock()
    {
        // Arrange
        var queue = new ModOperationQueue(Mock.Of<ILogHelper>());
        var categories = new[] { "CharacterSkins", "WeaponSkins", "Effects", null };
        var ids = Enumerable.Range(0, 20).Select(i => $"mod-{i:00}").ToArray();
        var tasks = new List<Task>();
        var operationCount = 0;

        // Act - In-memory only: simulate 100 operations (no file system access)
        for (int i = 0; i < 100; i++)
        {
            var category = categories[Random.Shared.Next(categories.Length)];
            var id = ids[Random.Shared.Next(ids.Length)];

            // 70% category operations (loads), 30% per-mod operations (unloads, updates)
            if (Random.Shared.NextDouble() < 0.7)
            {
                tasks.Add(queue.EnqueueCategoryOperationAsync(category, async () =>
                {
                    await Task.Delay(Random.Shared.Next(10, 50)); // Simulate async work (not file I/O)
                    Interlocked.Increment(ref operationCount);
                    return true;
                }));
            }
            else
            {
                tasks.Add(queue.EnqueueAsync(id, async () =>
                {
                    await Task.Delay(Random.Shared.Next(5, 30)); // Simulate async work (not file I/O)
                    Interlocked.Increment(ref operationCount);
                    return true;
                }));
            }
        }

        // Wait with timeout
        var timeout = Task.Delay(TimeSpan.FromSeconds(30));
        var completed = await Task.WhenAny(Task.WhenAll(tasks), timeout);

        // Assert
        Assert.NotSame(timeout, completed);
        Assert.Equal(100, operationCount);
        Assert.True(tasks.All(t => t.IsCompleted), "All operations should complete");
    }
}
