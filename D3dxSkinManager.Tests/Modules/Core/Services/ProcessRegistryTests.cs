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
/// panel. Covers lifecycle transitions, snapshot ordering, history cap, cancellation, and that a
/// consolidated PROCESS_LIST_UPDATED event is emitted on every mutation.
/// </summary>
public class ProcessRegistryTests
{
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly ProcessRegistry _registry;
    private readonly string _dataDir;

    public ProcessRegistryTests()
    {
        _eventBus
            .Setup(x => x.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        // Isolate persistence to a fresh temp dir so tests don't load/clobber real state.
        _dataDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d3dx-proc-test-" + System.Guid.NewGuid().ToString("N"));
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.BaseDataPath).Returns(_dataDir);
        _registry = new ProcessRegistry(_eventBus.Object, Mock.Of<ILogHelper>(), paths.Object);
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
    public void InterruptedProcess_IsRestoredAsInterrupted_OnRestart()
    {
        // A process left Running (app "crashes" before Complete) is persisted as Running...
        var id = _registry.Start(ProcessType.Migration, "long op");

        // ...and a fresh registry reading the same state dir (app restart) marks it Interrupted.
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.BaseDataPath).Returns(_dataDir);
        var restarted = new ProcessRegistry(_eventBus.Object, Mock.Of<ILogHelper>(), paths.Object);

        var restored = restarted.GetAll().FirstOrDefault(p => p.Id == id);
        restored.Should().NotBeNull();
        restored!.Status.Should().Be(ProcessStatus.Interrupted);
        restored.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public void RequestResume_OfInterruptedResumable_EmitsEvent_AndDropsEntry()
    {
        var id = _registry.Start(ProcessType.Migration, "migrating", resumable: true);

        // Restart → the running+resumable process becomes Interrupted.
        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.BaseDataPath).Returns(_dataDir);
        var restarted = new ProcessRegistry(_eventBus.Object, Mock.Of<ILogHelper>(), paths.Object);

        restarted.RequestResume(id);

        restarted.GetAll().Any(p => p.Id == id).Should().BeFalse("the resumed op registers a fresh process");
        _eventBus.Verify(
            x => x.EmitAsync(ModuleNames.SYSTEM, SystemEvents.PROCESS_RESUME_REQUESTED, It.IsAny<object>(), It.IsAny<string?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void CompletedProcess_StaysCompleted_OnRestart()
    {
        var id = _registry.Start(ProcessType.Package, "export");
        _registry.Complete(id);

        var paths = new Mock<IGlobalPathService>();
        paths.Setup(p => p.BaseDataPath).Returns(_dataDir);
        var restarted = new ProcessRegistry(_eventBus.Object, Mock.Of<ILogHelper>(), paths.Object);

        restarted.GetAll().First(p => p.Id == id).Status.Should().Be(ProcessStatus.Completed);
    }
}
