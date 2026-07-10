using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Tests.Modules.Core.Services;

/// <summary>
/// Tests for the authoritative ProcessRegistry — the source of truth for the status bar + Activity
/// panel. Covers lifecycle transitions, snapshot ordering, history cap, cancellation, throttled
/// report emissions, and the profile-DB-announced interrupted entries (the registry is purely
/// in-memory since 2026-07-10 — no process-state.json).
/// </summary>
public class ProcessRegistryTests
{
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly ProcessRegistry _registry;

    public ProcessRegistryTests()
    {
        _eventBus
            .Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        _registry = new ProcessRegistry(_eventBus.Object, Mock.Of<ILogHelper>());
    }

    [Fact]
    public void Start_AddsRunningProcess_AndEmits()
    {
        var id = _registry.Start(ProcessType.ModLoad, "Loading mod");

        var all = _registry.GetAll();
        all.Should().ContainSingle();
        all[0].Id.Should().Be(id);
        all[0].Status.Should().Be(ProcessStatus.Running);
        all[0].Title.Should().Be("Loading mod");
        _eventBus.Verify(
            x => x.EmitAsync(ModuleNames.SYSTEM, SystemEvents.PROCESS_LIST_UPDATED, It.IsAny<object>(), It.IsAny<string?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void RegisterInterrupted_AddsResumableInterruptedEntry_DedupedByPayload()
    {
        var first = _registry.RegisterInterrupted(ProcessType.Analysis, "Analyzing mods", "session-1",
            titleKey: "process.analysis", profileId: "profile-A");
        // A profile switch re-announces the same checkpoint — must reuse, not stack.
        var second = _registry.RegisterInterrupted(ProcessType.Analysis, "Analyzing mods", "session-1",
            titleKey: "process.analysis", profileId: "profile-A");

        second.Should().Be(first);
        var entry = _registry.GetAll().Single();
        entry.Status.Should().Be(ProcessStatus.Interrupted);
        entry.Resumable.Should().BeTrue();
        entry.ResumePayload.Should().Be("session-1");
        entry.ProfileId.Should().Be("profile-A");
    }

    [Fact]
    public void Report_UpdatesProgressAndDetail_ClampedToRange()
    {
        var id = _registry.Start(ProcessType.Package, "Export");
        _registry.Report(id, 150, "writing manifest");

        var p = _registry.GetAll().Single();
        p.Progress.Should().Be(100); // clamped
        p.Detail.Should().Be("writing manifest");
    }

    [Fact]
    public void Complete_MarksCompleted_SetsProgress100_AndFinishedAt()
    {
        var id = _registry.Start(ProcessType.ModLoad, "Loading");
        _registry.Complete(id);

        var p = _registry.GetAll().Single();
        p.Status.Should().Be(ProcessStatus.Completed);
        p.Progress.Should().Be(100);
        p.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_MarksFailed_WithError()
    {
        var id = _registry.Start(ProcessType.Analysis, "Analyze");
        _registry.Fail(id, "boom");

        var p = _registry.GetAll().Single();
        p.Status.Should().Be(ProcessStatus.Failed);
        p.Error.Should().Be("boom");
    }

    [Fact]
    public void Report_AfterTerminal_IsIgnored()
    {
        var id = _registry.Start(ProcessType.ModLoad, "Loading");
        _registry.Complete(id);
        _registry.Report(id, 10);

        _registry.GetAll().Single().Progress.Should().Be(100); // unchanged by post-complete report
    }

    [Fact]
    public async Task Report_Burst_ThrottlesSnapshotEmissions_ButFinalProgressStillLands()
    {
        var id = _registry.Start(ProcessType.Package, "Export");
        _eventBus.Invocations.Clear();

        // A tight per-item loop: 50 Report calls in well under one throttle window.
        for (var i = 0; i < 50; i++) _registry.Report(id, i);

        // Registry state is exact immediately (only the EMISSION is throttled).
        _registry.GetAll().Single().Progress.Should().Be(49);

        // Give the trailing emit time to fire, then count PROCESS_LIST_UPDATED emissions:
        // without the throttle this was 50; with it, a burst inside one window yields a handful
        // at most (immediate leader + one trailing; generous bound to avoid timing flake).
        await Task.Delay(400);
        var emits = _eventBus.Invocations.Count(i =>
            i.Method.Name == nameof(IEventBus.EmitAsync) &&
            Equals(i.Arguments[1], SystemEvents.PROCESS_LIST_UPDATED));
        emits.Should().BeGreaterThan(0, "the trailing emit must deliver the last progress");
        emits.Should().BeLessThan(10, "a 50-call burst must not ship ~50 snapshots");
    }

    [Fact]
    public void Lifecycle_EmitsImmediately_EvenRightAfterAThrottledReport()
    {
        var id = _registry.Start(ProcessType.Package, "Export");
        _registry.Report(id, 10); // opens a throttle window
        _eventBus.Invocations.Clear();

        _registry.Complete(id); // lifecycle transition must not wait for the window

        _eventBus.Verify(
            x => x.EmitAsync(ModuleNames.SYSTEM, SystemEvents.PROCESS_LIST_UPDATED, It.IsAny<object>(), It.IsAny<string?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Cancel_OfCancellable_SignalsTokenAndMarksCancelled()
    {
        var id = _registry.Start(ProcessType.Migration, "Migrating", cancellable: true);
        var token = _registry.GetToken(id);
        token.CanBeCanceled.Should().BeTrue();

        _registry.Cancel(id);

        token.IsCancellationRequested.Should().BeTrue();
        _registry.GetAll().Single().Status.Should().Be(ProcessStatus.Cancelled);
    }

    [Fact]
    public void GetToken_NonCancellable_ReturnsNone()
    {
        var id = _registry.Start(ProcessType.ModLoad, "Loading");
        _registry.GetToken(id).CanBeCanceled.Should().BeFalse();
    }

    [Fact]
    public void Snapshot_OrdersRunningFirst_ThenFinishedNewestFirst()
    {
        var running = _registry.Start(ProcessType.ModLoad, "running");
        var doneA = _registry.Start(ProcessType.Package, "done A");
        var doneB = _registry.Start(ProcessType.Cleanup, "done B");
        _registry.Complete(doneA);
        _registry.Complete(doneB); // newest finished

        var all = _registry.GetAll();
        all[0].Id.Should().Be(running, "running processes come first");
        all[1].Title.Should().Be("done B", "finished are newest-first");
        all[2].Title.Should().Be("done A");
    }

    [Fact]
    public void ClearCompleted_RemovesFinished_KeepsRunning()
    {
        var running = _registry.Start(ProcessType.ModLoad, "running");
        var done = _registry.Start(ProcessType.Package, "done");
        _registry.Complete(done);

        _registry.ClearCompleted();

        var all = _registry.GetAll();
        all.Should().ContainSingle();
        all[0].Id.Should().Be(running);
    }

    [Fact]
    public void History_IsCappedAt50()
    {
        for (int i = 0; i < 60; i++)
        {
            var id = _registry.Start(ProcessType.Other, $"op {i}");
            _registry.Complete(id);
        }

        _registry.GetAll().Count.Should().Be(50, "finished history is bounded");
    }

    [Fact]
    public void HistoryCap_NeverPrunesAPendingResumeOffer()
    {
        var interrupted = _registry.RegisterInterrupted(ProcessType.Analysis, "Analyzing mods", "session-1",
            profileId: "profile-A");

        // A long session finishes far more ops than the history cap.
        for (int i = 0; i < 60; i++)
        {
            var id = _registry.Start(ProcessType.Other, $"op {i}");
            _registry.Complete(id);
        }

        _registry.GetAll().Should().Contain(p => p.Id == interrupted,
            "an interrupted+resumable entry is a pending resume offer, not prunable history");
    }

    [Fact]
    public void RequestResume_OfAnnouncedInterrupted_EmitsEventWithProfileId_AndDropsEntry()
    {
        var id = _registry.RegisterInterrupted(ProcessType.Analysis, "Analyzing mods", "session-9",
            profileId: "profile-B");

        object? emitted = null;
        _eventBus
            .Setup(x => x.EmitAsync(ModuleNames.SYSTEM, SystemEvents.PROCESS_RESUME_REQUESTED, It.IsAny<object>(), It.IsAny<string?>()))
            .Callback<string, string, object, string?>((_, _, payload, _) => emitted = payload)
            .Returns(Task.CompletedTask);

        _registry.RequestResume(id);

        _registry.GetAll().Any(p => p.Id == id).Should().BeFalse("the resumed op registers a fresh process");
        emitted.Should().NotBeNull();
        var payloadType = emitted!.GetType();
        payloadType.GetProperty("resumePayload")!.GetValue(emitted).Should().Be("session-9");
        payloadType.GetProperty("profileId")!.GetValue(emitted).Should().Be("profile-B",
            "the resume must target the OWNING profile, not the selected one");
    }
}
