using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Workflow.Services;

namespace D3dxSkinManager.Tests.Modules.Workflow.Services;

/// <summary>
/// Tests for WorkflowConcurrencyManager (priority admission gate).
///
/// Two guarantees:
///  - PRIORITY: when a slot frees, the highest-priority queued waiter is admitted first —
///    confirmed-before-unconfirmed, then higher-progress, then earlier-created (NOT arbitrary/FIFO).
///  - CANCELLATION: a queued waiter whose token fires throws and never leaks a slot.
///
/// The manager defaults to 5 concurrent slots; tests fill all 5 to force the queued path.
/// </summary>
public class WorkflowConcurrencyManagerTests
{
    private const int DefaultMaxSlots = 5;
    private readonly Mock<ILogHelper> _mockLogger = new();

    private WorkflowConcurrencyManager CreateManager() => new(_mockLogger.Object);

    private static WorkflowPriority P(bool confirmed = false, int progress = 0, DateTime? created = null)
        => new(confirmed, progress, created ?? DateTime.UtcNow);

    private static async Task<string[]> FillAllSlotsAsync(WorkflowConcurrencyManager manager)
    {
        var ids = new string[DefaultMaxSlots];
        for (var i = 0; i < DefaultMaxSlots; i++)
        {
            ids[i] = $"slot-filler-{i}";
            await manager.TryAcquireSlotAsync(ids[i], P());
        }
        return ids;
    }

    #region Normal acquisition

    [Fact]
    public async Task Acquire_WhenSlotAvailable_RunsImmediately()
    {
        var manager = CreateManager();
        await manager.TryAcquireSlotAsync("wf-1", P());
        manager.CurrentRunningCount.Should().Be(1);
    }

    [Fact]
    public async Task Acquire_UpToMaxConcurrent_AllSucceedImmediately()
    {
        var manager = CreateManager();
        var ids = await FillAllSlotsAsync(manager);

        manager.CurrentRunningCount.Should().Be(DefaultMaxSlots);
        manager.CanStartWorkflow().Should().BeFalse("all slots are occupied");

        foreach (var id in ids) manager.ReleaseSlot(id);
    }

    #endregion

    #region Priority admission (the feature)

    [Fact]
    public async Task WhenSlotsFree_WaitersAdmittedByPriority_ConfirmedThenProgressThenAge()
    {
        // Fill all slots, then queue 4 waiters in a deliberately "wrong" submit order. Priority — not
        // submit order — must decide who runs when slots free.
        var manager = CreateManager();
        var holders = await FillAllSlotsAsync(manager);

        var t1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddMinutes(1);
        var t3 = t1.AddMinutes(2);
        var t4 = t1.AddMinutes(3);

        // Expected admission order: D (confirmed, 90, earliest) → C (confirmed, 90, later) →
        //                          B (confirmed, 50) → A (unconfirmed).
        var waiters = new Dictionary<string, Task>
        {
            ["A"] = manager.TryAcquireSlotAsync("A", P(confirmed: false, progress: 0, created: t2)),
            ["B"] = manager.TryAcquireSlotAsync("B", P(confirmed: true, progress: 50, created: t3)),
            ["C"] = manager.TryAcquireSlotAsync("C", P(confirmed: true, progress: 90, created: t4)),
            ["D"] = manager.TryAcquireSlotAsync("D", P(confirmed: true, progress: 90, created: t1)),
        };

        var admitted = new List<string>();
        var remaining = new Dictionary<string, Task>(waiters);
        foreach (var holder in holders.Take(4))
        {
            manager.ReleaseSlot(holder);
            var done = await Task.WhenAny(Task.WhenAny(remaining.Values), Task.Delay(2000));
            (done is Task<Task>).Should().BeTrue("a waiter should have been admitted, not timed out");
            var finished = remaining.First(kv => kv.Value.IsCompletedSuccessfully);
            admitted.Add(finished.Key);
            remaining.Remove(finished.Key);
        }

        admitted.Should().Equal("D", "C", "B", "A");

        manager.ReleaseSlot(holders[4]);
    }

    #endregion

    #region Cancellation while queued

    [Fact]
    public async Task WhenCancelledWhileQueued_ThrowsOperationCanceledException()
    {
        var manager = CreateManager();
        var holders = await FillAllSlotsAsync(manager);

        using var cts = new CancellationTokenSource();
        var queuedTask = manager.TryAcquireSlotAsync("queued-workflow", P(), cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await FluentActions.Awaiting(() => queuedTask)
            .Should().ThrowAsync<OperationCanceledException>(
                "a queued workflow must abort immediately when its cancellation token fires");

        foreach (var id in holders) manager.ReleaseSlot(id);
    }

    [Fact]
    public async Task WhenCancelledWhileQueued_DoesNotLeakSlot()
    {
        var manager = CreateManager();
        var holders = await FillAllSlotsAsync(manager);

        using var cts = new CancellationTokenSource();
        var queuedTask = manager.TryAcquireSlotAsync("queued-workflow", P(), cts.Token);

        await Task.Delay(50);
        cts.Cancel();
        try { await queuedTask; } catch (OperationCanceledException) { }

        // Release one holder — a cancelled waiter must not have consumed the freed slot.
        manager.ReleaseSlot(holders[0]);
        manager.CanStartWorkflow().Should().BeTrue(
            "the freed slot must not be consumed by a waiter that was cancelled before acquiring it");

        await manager.TryAcquireSlotAsync("next-workflow", P());
        manager.CurrentRunningCount.Should().Be(DefaultMaxSlots);
    }

    [Fact]
    public async Task WhenQueuedWaiterCancelled_NextPriorityWaiterStillAdmitted()
    {
        var manager = CreateManager();
        var holders = await FillAllSlotsAsync(manager);

        using var cts1 = new CancellationTokenSource();
        var cancelled = manager.TryAcquireSlotAsync("to-cancel", P(confirmed: true, progress: 90), cts1.Token);
        var survivor = manager.TryAcquireSlotAsync("survivor", P(confirmed: false, progress: 0));

        await Task.Delay(50);
        cts1.Cancel();
        try { await cancelled; } catch (OperationCanceledException) { }

        // Even though the cancelled waiter had higher priority, releasing a slot must admit the survivor.
        manager.ReleaseSlot(holders[0]);
        await survivor.WaitAsync(TimeSpan.FromSeconds(2));
        survivor.IsCompletedSuccessfully.Should().BeTrue();

        foreach (var id in holders[1..]) manager.ReleaseSlot(id);
    }

    #endregion

    #region ReleaseSlot

    [Fact]
    public async Task ReleaseSlot_AfterAcquisition_DecrementsRunningCount()
    {
        var manager = CreateManager();
        await manager.TryAcquireSlotAsync("wf-1", P());
        await manager.TryAcquireSlotAsync("wf-2", P());

        manager.ReleaseSlot("wf-1");

        manager.CurrentRunningCount.Should().Be(1);
        manager.CanStartWorkflow().Should().BeTrue();
    }

    [Fact]
    public void ReleaseSlot_ForUnknownWorkflow_IsNoOp()
    {
        var manager = CreateManager();
        FluentActions.Invoking(() => manager.ReleaseSlot("nonexistent-workflow"))
            .Should().NotThrow("releasing an unknown slot must be a no-op");
    }

    #endregion
}
