using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Workflow.Services;

namespace D3dxSkinManager.Tests.Modules.Workflow.Services;

/// <summary>
/// Tests for WorkflowConcurrencyManager, focused on the CancellationToken fix.
///
/// Before the fix: TryAcquireSlotAsync called _semaphore.WaitAsync() with no
/// cancellation token, so a queued workflow kept waiting even after CancelAsync
/// fired — then acquired a slot, overwrote the DB status to Processing, and
/// emitted a spurious STATUS_CHANGED event.
///
/// After the fix: TryAcquireSlotAsync accepts a CancellationToken and passes it
/// to _semaphore.WaitAsync(cancellationToken), so the wait is interrupted when
/// the token is cancelled and the slot is never acquired.
///
/// NOTE: WorkflowConcurrencyManager is constructed with a hardcoded default of
/// 5 concurrent slots (SemaphoreSlim is initialised in the constructor).  The
/// MaxConcurrentWorkflows setter only updates the backing field — it does NOT
/// resize the semaphore.  Tests therefore fill all 5 default slots when they
/// need to force a caller into the blocking-wait path.
/// </summary>
public class WorkflowConcurrencyManagerTests
{
    private const int DefaultMaxSlots = 5; // must match WorkflowConcurrencyManager constructor

    private readonly Mock<ILogHelper> _mockLogger = new();

    private WorkflowConcurrencyManager CreateManager() =>
        new(_mockLogger.Object);

    /// <summary>
    /// Occupies all <see cref="DefaultMaxSlots"/> slots so the next caller must
    /// block on the semaphore.  Returns the slot-holder IDs for later release.
    /// </summary>
    private static async Task<string[]> FillAllSlotsAsync(WorkflowConcurrencyManager manager)
    {
        var ids = new string[DefaultMaxSlots];
        for (var i = 0; i < DefaultMaxSlots; i++)
        {
            ids[i] = $"slot-filler-{i}";
            await manager.TryAcquireSlotAsync(ids[i]);
        }
        return ids;
    }

    #region Normal acquisition

    [Fact]
    public async Task TryAcquireSlotAsync_WhenSlotAvailable_ReturnsTrue()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var result = await manager.TryAcquireSlotAsync("wf-1");

        // Assert
        result.Should().BeTrue();
        manager.CurrentRunningCount.Should().Be(1);
    }

    [Fact]
    public async Task TryAcquireSlotAsync_UpToMaxConcurrent_AllSucceedImmediately()
    {
        // Arrange
        var manager = CreateManager();

        // Act — fill all default slots
        var ids = await FillAllSlotsAsync(manager);

        // Assert
        manager.CurrentRunningCount.Should().Be(DefaultMaxSlots);
        manager.CanStartWorkflow().Should().BeFalse("all slots are occupied");

        // Teardown
        foreach (var id in ids)
            manager.ReleaseSlot(id);
    }

    #endregion

    #region Cancellation while queued (the critical bug fix)

    [Fact]
    public async Task TryAcquireSlotAsync_WhenCancelledWhileQueued_ThrowsOperationCanceledException()
    {
        // Arrange: fill all slots so the next caller must wait
        var manager = CreateManager();
        var holders = await FillAllSlotsAsync(manager);

        using var cts = new CancellationTokenSource();

        // Act: start a new acquisition — it will block on the semaphore
        var queuedTask = manager.TryAcquireSlotAsync("queued-workflow", cts.Token);

        // Give the async continuation time to reach _semaphore.WaitAsync(cancellationToken)
        await Task.Delay(50);

        // Cancel while it is waiting
        cts.Cancel();

        // Assert: the queued task must throw, not silently acquire after cancellation
        await FluentActions.Awaiting(() => queuedTask)
            .Should().ThrowAsync<OperationCanceledException>(
                "a queued workflow must abort immediately when its cancellation token fires");

        // Teardown
        foreach (var id in holders)
            manager.ReleaseSlot(id);
    }

    [Fact]
    public async Task TryAcquireSlotAsync_WhenCancelledWhileQueued_DoesNotLeakSemaphoreSlot()
    {
        // Arrange
        var manager = CreateManager();
        var holders = await FillAllSlotsAsync(manager);

        using var cts = new CancellationTokenSource();
        var queuedTask = manager.TryAcquireSlotAsync("queued-workflow", cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        try { await queuedTask; } catch (OperationCanceledException) { /* expected */ }

        // Release one holder — should make exactly one slot available
        manager.ReleaseSlot(holders[0]);

        // Assert: exactly one slot is available, not zero (which would indicate a leak)
        manager.CanStartWorkflow().Should().BeTrue(
            "the semaphore slot must not be consumed by a waiter that was cancelled before acquiring it");

        // Confirm another workflow can acquire the now-available slot
        var nextResult = await manager.TryAcquireSlotAsync("next-workflow");
        nextResult.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireSlotAsync_WithAlreadyCancelledTokenAndNoSlotAvailable_ThrowsImmediately()
    {
        // Arrange: fill all slots so we must queue
        var manager = CreateManager();
        var holders = await FillAllSlotsAsync(manager);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // already cancelled before calling

        // Act & Assert
        await FluentActions.Awaiting(() => manager.TryAcquireSlotAsync("workflow", cts.Token))
            .Should().ThrowAsync<OperationCanceledException>(
                "an already-cancelled token must cause an immediate throw without waiting");

        // No extra slot should have been leaked
        manager.ReleaseSlot(holders[0]);
        manager.CanStartWorkflow().Should().BeTrue("no slot was leaked by the already-cancelled call");

        // Teardown
        foreach (var id in holders[1..])
            manager.ReleaseSlot(id);
    }

    [Fact]
    public async Task TryAcquireSlotAsync_WhenQueuedTaskCancelled_OtherQueuedTasksEventuallyAcquire()
    {
        // Arrange: fill all slots — two workflows queue up; first gets cancelled, second should succeed
        var manager = CreateManager();
        var holders = await FillAllSlotsAsync(manager);

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        var queuedTask1 = manager.TryAcquireSlotAsync("queued-1", cts1.Token);
        var queuedTask2 = manager.TryAcquireSlotAsync("queued-2", cts2.Token);

        await Task.Delay(50); // let both reach the blocking wait

        // Cancel the first queued task only
        cts1.Cancel();
        try { await queuedTask1; } catch (OperationCanceledException) { /* expected */ }

        // Release one holder — should unblock queued-2
        manager.ReleaseSlot(holders[0]);

        // Assert: second task acquires normally
        var result2 = await queuedTask2.WaitAsync(TimeSpan.FromSeconds(2));
        result2.Should().BeTrue("the second queued workflow should acquire the slot once it becomes free");

        // Teardown
        foreach (var id in holders[1..])
            manager.ReleaseSlot(id);
    }

    #endregion

    #region ReleaseSlot

    [Fact]
    public async Task ReleaseSlot_AfterAcquisition_DecrementsRunningCount()
    {
        // Arrange
        var manager = CreateManager();
        await manager.TryAcquireSlotAsync("wf-1");
        await manager.TryAcquireSlotAsync("wf-2");

        // Act
        manager.ReleaseSlot("wf-1");

        // Assert
        manager.CurrentRunningCount.Should().Be(1);
        manager.CanStartWorkflow().Should().BeTrue();
    }

    [Fact]
    public void ReleaseSlot_ForUnknownWorkflow_IsNoOp()
    {
        // Arrange
        var manager = CreateManager();

        // Act & Assert — must not throw
        FluentActions.Invoking(() => manager.ReleaseSlot("nonexistent-workflow"))
            .Should().NotThrow("releasing an unknown slot must be a no-op");
    }

    #endregion
}
