using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Workflow.Services;

namespace D3dxSkinManager.Tests.Modules.Workflow;

/// <summary>
/// Behavior lock for the internal-actor import queue (ImportQueueActor): bounded concurrency, priority
/// admission, pull-next-on-completion, yield/re-enqueue, cancel (queued + running), no slot leak on a
/// throwing handler, and unknown-type not hanging. These encode the guarantees the old
/// WorkflowConcurrencyManager gave, now on the mailbox+single-loop model (no locks). The actor is async +
/// message-driven, so tests drive a FakeHandler whose per-job gate the test releases, and poll with
/// WaitUntil rather than sleeping.
/// </summary>
public class ImportQueueActorTests
{
    private static WorkflowPriority Prio(bool confirmed = false, int progress = 0, int createdOffsetMs = 0) =>
        new(confirmed, progress, new DateTime(2026, 1, 1).AddMilliseconds(createdOffsetMs));

    private static ImportQueueActor NewActor(FakeHandler handler, int max) =>
        new(() => new IImportJobHandler[] { handler }, Mock.Of<ILogHelper>(), maxImportConcurrency: max);

    /// <summary>Two-lane actor: the "download" lane (types in <paramref name="downloadTypes"/>) and the
    /// "import" lane (everything else) have INDEPENDENT caps. Registers both handlers so the actor can
    /// dispatch either lane's job type.</summary>
    private static ImportQueueActor NewLaneActor(
        FakeHandler importHandler, FakeHandler downloadHandler, int maxImport, int maxDownload, params string[] downloadTypes) =>
        new(() => new IImportJobHandler[] { importHandler, downloadHandler }, Mock.Of<ILogHelper>(),
            maxImportConcurrency: maxImport, maxDownloadConcurrency: maxDownload,
            downloadJobTypes: new HashSet<string>(downloadTypes, StringComparer.OrdinalIgnoreCase));

    private static async Task WaitUntil(Func<bool> cond, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!cond())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"WaitUntil timed out: {because}");
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task RespectsMaxConcurrency_AndNeverExceedsIt()
    {
        var h = new FakeHandler();
        await using var actor = NewActor(h, max: 2);

        for (var i = 0; i < 5; i++) actor.Enqueue($"j{i}", "TEST", Prio(createdOffsetMs: i));

        await WaitUntil(() => h.Started.Count >= 2, "two jobs admitted");
        await Task.Delay(80); // give any (bug) extra admissions a chance to appear
        h.Running.Should().Be(2, "max concurrency is 2");
        h.Started.Count.Should().Be(2);

        h.Release("j0");
        await WaitUntil(() => h.Started.Count >= 3, "next job admitted on completion");
        h.Peak.Should().BeLessThanOrEqualTo(2, "peak concurrency must never exceed the cap");

        foreach (var id in new[] { "j1", "j2", "j3", "j4" }) h.Release(id);
        await WaitUntil(() => h.Completed.Count == 5, "all five complete");
    }

    [Fact]
    public async Task PullsNextOnCompletion_InOrder_AtMaxOne()
    {
        var h = new FakeHandler();
        await using var actor = NewActor(h, max: 1);

        actor.Enqueue("a", "TEST", Prio(createdOffsetMs: 0));
        actor.Enqueue("b", "TEST", Prio(createdOffsetMs: 1));
        actor.Enqueue("c", "TEST", Prio(createdOffsetMs: 2));

        await WaitUntil(() => h.Started.Count == 1, "only one runs at a time");
        h.Started.Single().Should().Be("a");

        h.Release("a");
        await WaitUntil(() => h.Started.Contains("b"), "b runs after a");
        h.Release("b");
        await WaitUntil(() => h.Started.Contains("c"), "c runs after b");
        h.Release("c");
        await WaitUntil(() => h.Completed.Count == 3, "all done");

        h.StartOrder.Should().Equal("a", "b", "c");
    }

    [Fact]
    public async Task AdmitsHighestPriorityFirst_WhenSlotFrees()
    {
        var h = new FakeHandler();
        await using var actor = NewActor(h, max: 1);

        actor.Enqueue("block", "TEST", Prio(createdOffsetMs: 0));
        await WaitUntil(() => h.Started.Contains("block"), "the slot is occupied");

        // Queue a LOW (unconfirmed) then a HIGH (confirmed) — confirmed must jump ahead.
        actor.Enqueue("low", "TEST", Prio(confirmed: false, createdOffsetMs: 10));
        actor.Enqueue("high", "TEST", Prio(confirmed: true, createdOffsetMs: 20));
        await Task.Delay(50); // let both queue

        h.Release("block");
        await WaitUntil(() => h.Started.Contains("high"), "the confirmed job is admitted first");
        h.Started.Should().NotContain("low", "the lower-priority job waits");

        h.Release("high");
        await WaitUntil(() => h.Started.Contains("low"), "then the low-priority job");
        h.Release("low");
        await WaitUntil(() => h.Completed.Count == 3, "all done");
    }

    [Fact]
    public async Task CancelWhileQueued_DropsBeforeItRuns()
    {
        var h = new FakeHandler();
        await using var actor = NewActor(h, max: 1);

        actor.Enqueue("block", "TEST", Prio(createdOffsetMs: 0));
        await WaitUntil(() => h.Started.Contains("block"), "slot occupied");
        actor.Enqueue("x", "TEST", Prio(createdOffsetMs: 10));
        actor.Enqueue("y", "TEST", Prio(createdOffsetMs: 20));
        await Task.Delay(50);

        actor.Cancel("x"); // still queued
        h.Release("block");

        await WaitUntil(() => h.Started.Contains("y"), "y runs");
        h.Started.Should().NotContain("x", "the cancelled-while-queued job never starts");
        h.Release("y");
    }

    [Fact]
    public async Task CancelWhileRunning_SignalsTheToken_AndFreesTheSlot()
    {
        var h = new FakeHandler();
        await using var actor = NewActor(h, max: 1);

        actor.Enqueue("run", "TEST", Prio());
        await WaitUntil(() => h.Started.Contains("run"), "job started");

        actor.Cancel("run"); // running → token signalled → handler observes cancellation
        await WaitUntil(() => h.Cancelled.Contains("run"), "the running job's token was cancelled");

        // Slot must be freed → a newly enqueued job runs.
        actor.Enqueue("next", "TEST", Prio());
        await WaitUntil(() => h.Started.Contains("next"), "the freed slot admits the next job");
        h.Release("next");
    }

    [Fact]
    public async Task ThrowingHandler_FreesSlot_NoLeak()
    {
        var h = new FakeHandler { ThrowFor = { "boom" } };
        await using var actor = NewActor(h, max: 1);

        actor.Enqueue("boom", "TEST", Prio(createdOffsetMs: 0));
        actor.Enqueue("after", "TEST", Prio(createdOffsetMs: 10));

        await WaitUntil(() => h.Started.Contains("after"), "the slot freed by the throw admits the next job");
        h.Release("after");
        await WaitUntil(() => h.Completed.Contains("after"), "after completes");
    }

    [Fact]
    public async Task ReEnqueueAfterFinish_RunsAgain()
    {
        var h = new FakeHandler { AutoComplete = { "yield" } };
        await using var actor = NewActor(h, max: 1);

        actor.Enqueue("yield", "TEST", Prio());
        await WaitUntil(() => h.StartOrder.Count(s => s == "yield") == 1, "ran once");

        actor.Enqueue("yield", "TEST", Prio(confirmed: true)); // e.g. preview confirmed
        await WaitUntil(() => h.StartOrder.Count(s => s == "yield") == 2, "runs again after re-enqueue");
    }

    [Fact]
    public async Task UnknownJobType_IsDropped_WithoutHanging()
    {
        var h = new FakeHandler(); // only handles "TEST"
        await using var actor = NewActor(h, max: 1);

        actor.Enqueue("ghost", "NOPE", Prio(createdOffsetMs: 0)); // no handler for "NOPE"
        actor.Enqueue("real", "TEST", Prio(createdOffsetMs: 10));

        await WaitUntil(() => h.Started.Contains("real"), "the known job still runs (queue didn't hang on the unknown type)");
        h.Release("real");
    }

    // ---- Two-lane concurrency (download vs import don't share a slot pool) ----

    [Fact]
    public async Task DownloadAndImportLanes_HaveIndependentCaps()
    {
        var imp = new FakeHandler { JobType = "IMP" };
        var dl = new FakeHandler { JobType = "DL" };
        await using var actor = NewLaneActor(imp, dl, maxImport: 2, maxDownload: 1, "DL");

        // Fill both lanes past their caps.
        for (var i = 0; i < 3; i++) actor.Enqueue($"dl{i}", "DL", Prio(createdOffsetMs: i));
        for (var i = 0; i < 3; i++) actor.Enqueue($"imp{i}", "IMP", Prio(createdOffsetMs: i));

        // The DOWNLOAD lane holds 1 while the IMPORT lane holds 2 — concurrently, from separate pools.
        await WaitUntil(() => dl.Running >= 1 && imp.Running >= 2, "each lane fills its own cap");
        await Task.Delay(80); // let any (bug) cross-lane admission surface
        dl.Running.Should().Be(1, "the download lane cap is 1");
        imp.Running.Should().Be(2, "the import lane cap is 2 — a full download lane does NOT steal import slots");

        // Drain and confirm neither lane ever exceeded its own cap.
        foreach (var id in new[] { "dl0", "dl1", "dl2" }) dl.Release(id);
        foreach (var id in new[] { "imp0", "imp1", "imp2" }) imp.Release(id);
        await WaitUntil(() => dl.Completed.Count == 3 && imp.Completed.Count == 3, "all drain");
        dl.Peak.Should().BeLessThanOrEqualTo(1, "download peak never exceeds its cap");
        imp.Peak.Should().BeLessThanOrEqualTo(2, "import peak never exceeds its cap");
    }

    [Fact]
    public async Task ReEnqueueAcrossLanes_DownloadThenImport_Runs()
    {
        // The two-stage handoff: a REMOTE_DOWNLOAD job re-enqueues itself as REMOTE_IMPORT (a different
        // lane) on finish. Mirrors RemoteDownloadHandler → RemoteImportWorkflowHandler.
        var imp = new FakeHandler { JobType = "IMP" };
        var dl = new FakeHandler { JobType = "DL" };
        await using var actor = NewLaneActor(imp, dl, maxImport: 1, maxDownload: 1, "DL");

        actor.Enqueue("x", "DL", Prio());
        await WaitUntil(() => dl.Started.Contains("x"), "x runs in the download lane");

        // Simulate the handler re-enqueuing the SAME id into the import lane while it's still running in
        // the download lane (the confirm-races-yield path, now across lanes).
        actor.Enqueue("x", "IMP", Prio(confirmed: true));
        dl.Release("x");

        await WaitUntil(() => imp.Started.Contains("x"), "x transitions to the import lane and runs there");
        imp.Release("x");
        await WaitUntil(() => imp.Completed.Contains("x"), "the import leg completes");
    }

    [Fact]
    public async Task SetDownloadConcurrency_AppliesLive_AdmitsMore()
    {
        var imp = new FakeHandler { JobType = "IMP" };
        var dl = new FakeHandler { JobType = "DL" };
        await using var actor = NewLaneActor(imp, dl, maxImport: 1, maxDownload: 1, "DL");

        for (var i = 0; i < 3; i++) actor.Enqueue($"dl{i}", "DL", Prio(createdOffsetMs: i));
        await WaitUntil(() => dl.Running == 1, "only one download runs at cap 1");
        await Task.Delay(60);
        dl.Running.Should().Be(1);

        actor.MaxDownloadConcurrency = 3; // raise the download lane live
        await WaitUntil(() => dl.Running == 3, "raising the cap admits the queued downloads");
        imp.Running.Should().Be(0, "raising the download cap did not touch the import lane");

        foreach (var id in new[] { "dl0", "dl1", "dl2" }) dl.Release(id);
        await WaitUntil(() => dl.Completed.Count == 3, "all drain");
    }

    /// <summary>Test handler: each job blocks in ProcessAsync until the test Releases it (or is cancelled /
    /// auto-completes / throws), while recording start order + peak concurrency.</summary>
    private sealed class FakeHandler : IImportJobHandler
    {
        public string JobType { get; init; } = "TEST";
        public readonly HashSet<string> ThrowFor = new();
        public readonly HashSet<string> AutoComplete = new(); // return immediately, don't block

        private readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new();
        public readonly ConcurrentQueue<string> StartedQ = new();
        public readonly ConcurrentQueue<string> CompletedQ = new();
        public readonly ConcurrentBag<string> Cancelled = new();
        private int _running;
        private int _peak;

        public IReadOnlyList<string> Started => StartedQ.ToArray();
        public IReadOnlyList<string> StartOrder => StartedQ.ToArray();
        public IReadOnlyList<string> Completed => CompletedQ.ToArray();
        public int Running => Volatile.Read(ref _running);
        public int Peak => Volatile.Read(ref _peak);

        private TaskCompletionSource Gate(string id) =>
            _gates.GetOrAdd(id, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        public void Release(string id) => Gate(id).TrySetResult();

        public async Task<JobOutcome> ProcessAsync(string jobId, CancellationToken ct)
        {
            StartedQ.Enqueue(jobId);
            var now = Interlocked.Increment(ref _running);
            int prev;
            while (now > (prev = Volatile.Read(ref _peak))) Interlocked.CompareExchange(ref _peak, now, prev);
            try
            {
                if (ThrowFor.Contains(jobId)) throw new InvalidOperationException("boom");
                if (AutoComplete.Contains(jobId)) { CompletedQ.Enqueue(jobId); return JobOutcome.Completed; }

                using (ct.Register(() => Gate(jobId).TrySetCanceled()))
                    await Gate(jobId).Task.ConfigureAwait(false);

                CompletedQ.Enqueue(jobId);
                return JobOutcome.Completed;
            }
            catch (OperationCanceledException)
            {
                Cancelled.Add(jobId);
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }
    }
}
