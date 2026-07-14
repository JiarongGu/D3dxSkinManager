using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Event;
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
/// RemoteDownloadHandler = the DOWNLOAD leg (download lane) of a two-stage REMOTE_IMPORT. It downloads the
/// bytes via IRemoteImportService.DownloadStageAsync, persists the result on the row context, then
/// re-enqueues the IMPORT leg (import lane) — so a finished download waits for a compress slot. A download
/// failure marks the row Failed and does NOT hand off.
/// </summary>
public class RemoteDownloadHandlerTests
{
    private readonly Mock<IWorkflowRepository> _repo = new();
    private readonly Mock<IEventBus> _bus = new();
    private readonly Mock<IImportQueueActor> _queue = new();
    private readonly Mock<IRemoteImportService> _remote = new();
    private readonly RemoteDownloadHandler _handler;

    public RemoteDownloadHandlerTests()
    {
        _bus.Setup(b => b.EmitAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<WorkflowInfo>())).Returns(Task.CompletedTask);
        _handler = new RemoteDownloadHandler(_repo.Object, _bus.Object, _queue.Object, _remote.Object, Mock.Of<ILogHelper>());
    }

    private static string DownloadStageContext() => JsonHelper.Serialize(new RemoteImportWorkflowContext
    {
        Job = new RemoteImportJob
        {
            SourceId = "src",
            Option = new RemoteDownloadOption { Type = "direct", Name = "Host", Url = "http://example/x" },
            Detail = new RemoteModDetail { Title = "My Mod" },
        },
        Stage = RemoteImportStage.Download,
    });

    [Fact]
    public async Task ProcessAsync_Download_PersistsResult_AndEnqueuesTheImportStage()
    {
        var wf = new WorkflowInfo { Id = "d1", Type = "REMOTE_IMPORT", Status = WorkflowStatus.Pending, Context = DownloadStageContext() };
        _repo.Setup(r => r.GetByIdAsync("d1")).ReturnsAsync(wf);
        var dl = new RemoteDownloadResult { StagingDir = "staging", ArchivePath = "raw.7z", FileName = "x.7z" };
        _remote.Setup(s => s.DownloadStageAsync(It.IsAny<RemoteImportJob>(), It.IsAny<CancellationToken>())).ReturnsAsync(dl);
        string? savedContext = null;
        _repo.Setup(r => r.UpdateAsync(It.IsAny<WorkflowInfo>())).Callback<WorkflowInfo>(w => savedContext = w.Context).Returns(Task.CompletedTask);

        var outcome = await _handler.ProcessAsync("d1", CancellationToken.None);

        outcome.Should().Be(JobOutcome.Completed, "the download leg completes and frees its slot");
        _queue.Verify(q => q.Enqueue("d1", RemoteImportWorkflowHandler.TypeId, It.IsAny<WorkflowPriority>()), Times.Once,
            "the finished download hands off to the IMPORT lane");
        var ctx = RemoteImportWorkflowHandler.DeserializeContext(savedContext);
        ctx.Stage.Should().Be(RemoteImportStage.Import, "the stage advanced");
        ctx.Download!.StagingDir.Should().Be("staging", "the download result is persisted for the import leg");
    }

    [Fact]
    public async Task ProcessAsync_DownloadThrows_MarksFailed_NoHandoff()
    {
        var wf = new WorkflowInfo { Id = "d2", Type = "REMOTE_IMPORT", Status = WorkflowStatus.Pending, Context = DownloadStageContext() };
        _repo.Setup(r => r.GetByIdAsync("d2")).ReturnsAsync(wf);
        _remote.Setup(s => s.DownloadStageAsync(It.IsAny<RemoteImportJob>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.InvalidOperationException("network down"));
        var statuses = new System.Collections.Generic.List<WorkflowStatus>();
        _repo.Setup(r => r.UpdateAsync(It.IsAny<WorkflowInfo>())).Callback<WorkflowInfo>(w => statuses.Add(w.Status)).Returns(Task.CompletedTask);

        var outcome = await _handler.ProcessAsync("d2", CancellationToken.None);

        outcome.Should().Be(JobOutcome.Failed);
        statuses.Should().Contain(WorkflowStatus.Failed, "a download failure fails the row");
        _queue.Verify(q => q.Enqueue(It.IsAny<string>(), RemoteImportWorkflowHandler.TypeId, It.IsAny<WorkflowPriority>()), Times.Never,
            "a failed download never hands off to import");
    }

    [Fact]
    public async Task ProcessAsync_CancelledDuringDownload_ReturnsCancelled()
    {
        var wf = new WorkflowInfo { Id = "d3", Type = "REMOTE_IMPORT", Status = WorkflowStatus.Pending, Context = DownloadStageContext() };
        _repo.Setup(r => r.GetByIdAsync("d3")).ReturnsAsync(wf);
        _remote.Setup(s => s.DownloadStageAsync(It.IsAny<RemoteImportJob>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.OperationCanceledException());

        var outcome = await _handler.ProcessAsync("d3", CancellationToken.None);

        outcome.Should().Be(JobOutcome.Cancelled);
        _queue.Verify(q => q.Enqueue(It.IsAny<string>(), RemoteImportWorkflowHandler.TypeId, It.IsAny<WorkflowPriority>()), Times.Never);
    }
}
