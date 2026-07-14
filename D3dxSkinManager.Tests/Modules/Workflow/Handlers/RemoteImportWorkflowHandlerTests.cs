using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Handlers;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Services;

namespace D3dxSkinManager.Tests.Modules.Workflow.Handlers;

/// <summary>
/// RemoteImportWorkflowHandler = the IMPORT leg of a two-stage REMOTE_IMPORT on the shared import queue
/// actor. StartRemoteImportAsync creates a Pending row + enqueues the DOWNLOAD leg (fail-fast on an
/// unsupported host); ProcessAsync runs the import leg via IRemoteImportService.ImportStageAsync and maps
/// the outcome (Completed → delete the queue row, Failed → mark Failed). A row with no download result yet
/// is bounced back to the download stage. The download leg itself is RemoteDownloadHandler.
/// </summary>
public class RemoteImportWorkflowHandlerTests
{
    private readonly Mock<IWorkflowRepository> _repo = new();
    private readonly Mock<IEventBus> _bus = new();
    private readonly Mock<IImportQueueActor> _queue = new();
    private readonly Mock<IRemoteImportService> _remote = new();
    private readonly RemoteImportWorkflowHandler _handler;

    public RemoteImportWorkflowHandlerTests()
    {
        _bus.Setup(b => b.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AddAsync(It.IsAny<WorkflowInfo>())).ReturnsAsync((WorkflowInfo w) => w);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<WorkflowInfo>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _handler = new RemoteImportWorkflowHandler(_repo.Object, _bus.Object, _queue.Object, _remote.Object, Mock.Of<ILogHelper>());
    }

    private static RemoteImportJob Job(string type = "direct") => new()
    {
        SourceId = "src",
        Option = new RemoteDownloadOption { Type = type, Name = "Host", Url = "http://example/x" },
        Detail = new RemoteModDetail { Title = "My Mod" },
    };

    /// <summary>A context already past the DOWNLOAD stage (a download result present), ready for the import leg.</summary>
    private static string DownloadedContext() => JsonHelper.Serialize(new RemoteImportWorkflowContext
    {
        Job = Job(),
        Stage = RemoteImportStage.Import,
        Download = new RemoteDownloadResult { StagingDir = "staging", ArchivePath = "a.7z", FileName = "x.7z" },
    });

    [Fact]
    public async Task StartRemoteImportAsync_CreatesPendingRow_AndEnqueuesTheDownloadStage()
    {
        var wf = await _handler.StartRemoteImportAsync(Job());

        wf.Type.Should().Be("REMOTE_IMPORT");
        wf.Status.Should().Be(WorkflowStatus.Pending, "queued, not run inline");
        _repo.Verify(r => r.AddAsync(It.Is<WorkflowInfo>(w => w.Type == "REMOTE_IMPORT")), Times.Once);
        _queue.Verify(q => q.Enqueue(wf.Id, RemoteDownloadHandler.TypeId, It.IsAny<WorkflowPriority>()), Times.Once,
            "start enqueues the DOWNLOAD leg (download lane), not the import leg");
    }

    [Fact]
    public async Task StartRemoteImportAsync_UnsupportedHost_ThrowsAndDoesNotEnqueue()
    {
        await _handler.Invoking(h => h.StartRemoteImportAsync(Job(type: "randomhost")))
            .Should().ThrowAsync<OperationException>();
        _queue.Verify(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<WorkflowPriority>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_Completed_DeletesTheQueueRow()
    {
        var wf = new WorkflowInfo { Id = "w1", Type = "REMOTE_IMPORT", Status = WorkflowStatus.Pending, Context = DownloadedContext() };
        _repo.Setup(r => r.GetByIdAsync("w1")).ReturnsAsync(wf);
        _remote.Setup(s => s.ImportStageAsync(It.IsAny<RemoteImportJob>(), It.IsAny<RemoteDownloadResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JobOutcome.Completed);

        var outcome = await _handler.ProcessAsync("w1", CancellationToken.None);

        outcome.Should().Be(JobOutcome.Completed);
        _remote.Verify(s => s.ImportStageAsync(It.IsAny<RemoteImportJob>(), It.IsAny<RemoteDownloadResult>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.DeleteAsync("w1"), Times.Once, "a completed remote import removes its queue row");
    }

    [Fact]
    public async Task ProcessAsync_Failed_MarksTheRowFailed()
    {
        var wf = new WorkflowInfo { Id = "w2", Type = "REMOTE_IMPORT", Status = WorkflowStatus.Pending, Context = DownloadedContext() };
        _repo.Setup(r => r.GetByIdAsync("w2")).ReturnsAsync(wf);
        _remote.Setup(s => s.ImportStageAsync(It.IsAny<RemoteImportJob>(), It.IsAny<RemoteDownloadResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JobOutcome.Failed);
        // Capture status at call time — WorkflowStatus is a value type, so it isn't affected by the later
        // mutation of the shared workflow object (the Moq re-evaluation gotcha).
        var statuses = new System.Collections.Generic.List<WorkflowStatus>();
        _repo.Setup(r => r.UpdateAsync(It.IsAny<WorkflowInfo>())).Callback<WorkflowInfo>(w => statuses.Add(w.Status)).Returns(Task.CompletedTask);

        var outcome = await _handler.ProcessAsync("w2", CancellationToken.None);

        outcome.Should().Be(JobOutcome.Failed);
        statuses.Should().Contain(WorkflowStatus.Failed, "the leg failure marks the row Failed");
        _repo.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never, "a failed row is kept (not deleted)");
    }

    [Fact]
    public async Task ProcessAsync_NoDownloadResult_BouncesBackToDownloadStage()
    {
        // A crash lost the in-flight staging (Download == null) — the import leg re-queues the download leg.
        var ctx = JsonHelper.Serialize(new RemoteImportWorkflowContext { Job = Job(), Stage = RemoteImportStage.Import });
        var wf = new WorkflowInfo { Id = "w3", Type = "REMOTE_IMPORT", Status = WorkflowStatus.Pending, Context = ctx };
        _repo.Setup(r => r.GetByIdAsync("w3")).ReturnsAsync(wf);

        var outcome = await _handler.ProcessAsync("w3", CancellationToken.None);

        outcome.Should().Be(JobOutcome.Yielded, "no download result → yield and re-download");
        _queue.Verify(q => q.Enqueue("w3", RemoteDownloadHandler.TypeId, It.IsAny<WorkflowPriority>()), Times.Once);
        _remote.Verify(s => s.ImportStageAsync(It.IsAny<RemoteImportJob>(), It.IsAny<RemoteDownloadResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_MissingRow_ReturnsCompleted_NoThrow()
    {
        _repo.Setup(r => r.GetByIdAsync("gone")).ReturnsAsync((WorkflowInfo?)null);
        (await _handler.ProcessAsync("gone", CancellationToken.None)).Should().Be(JobOutcome.Completed);
        _remote.Verify(s => s.ImportStageAsync(It.IsAny<RemoteImportJob>(), It.IsAny<RemoteDownloadResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_BetweenStages_DiscardsTheDownload_AndDeletesRow()
    {
        var wf = new WorkflowInfo { Id = "w4", Type = "REMOTE_IMPORT", Status = WorkflowStatus.Pending, Context = DownloadedContext() };
        _repo.Setup(r => r.GetByIdAsync("w4")).ReturnsAsync(wf);

        await _handler.CancelAsync("w4");

        _queue.Verify(q => q.Cancel("w4"), Times.Once);
        _remote.Verify(s => s.DiscardDownloadAsync(It.Is<RemoteDownloadResult>(d => d.StagingDir == "staging")), Times.Once,
            "a cancel of a downloaded-but-not-imported row cleans its staged files");
        _repo.Verify(r => r.DeleteAsync("w4"), Times.Once);
    }
}
