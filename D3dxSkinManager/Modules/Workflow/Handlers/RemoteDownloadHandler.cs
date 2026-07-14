using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Services;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Modules.Workflow.Handlers;

/// <summary>
/// Stage 1 of a two-stage REMOTE_IMPORT: the DOWNLOAD leg. Runs in the import queue actor's DOWNLOAD lane
/// (network-bound, bounded by MaxParallelDownloads) — <see cref="JobType"/> is the actor dispatch key
/// only, NOT a WorkflowInfo row type (the row stays <c>REMOTE_IMPORT</c>, owned by
/// <see cref="RemoteImportWorkflowHandler"/>). On finish it re-enqueues the SAME row's IMPORT leg into the
/// import lane, so a downloaded item WAITS for a compress slot rather than holding this download slot.
/// </summary>
public class RemoteDownloadHandler : IImportJobHandler
{
    /// <summary>The DOWNLOAD-stage job type (import queue actor's download lane). Not a workflow row type.</summary>
    public const string TypeId = "REMOTE_DOWNLOAD";
    public string JobType => TypeId;

    private readonly IWorkflowRepository _workflowRepository;
    private readonly IEventBus _eventBus;
    private readonly IImportQueueActor _queue;
    private readonly IRemoteImportService _remoteImportService;
    private readonly ILogHelper _logger;

    public RemoteDownloadHandler(
        IWorkflowRepository workflowRepository,
        IEventBus eventBus,
        IImportQueueActor queue,
        IRemoteImportService remoteImportService,
        ILogHelper logger)
    {
        _workflowRepository = workflowRepository;
        _eventBus = eventBus;
        _queue = queue;
        _remoteImportService = remoteImportService;
        _logger = logger;
    }

    /// <summary>The actor runs this as the DOWNLOAD leg: fetch the raw bytes, persist the result on the
    /// row's context, then re-enqueue the IMPORT leg (import lane). Freeing this download slot on return.</summary>
    public async Task<JobOutcome> ProcessAsync(string jobId, CancellationToken ct)
    {
        var workflow = await _workflowRepository.GetByIdAsync(jobId);
        if (workflow == null)
        {
            _logger.Info($"[RemoteDownload] job {jobId} vanished before it ran — skipping", "RemoteDownloadHandler");
            return JobOutcome.Completed;
        }

        var ctx = RemoteImportWorkflowHandler.DeserializeContext(workflow.Context);
        if (ctx.Job == null || string.IsNullOrEmpty(ctx.Job.Option?.Url))
        {
            await FailAsync(workflow, "Invalid remote import context");
            return JobOutcome.Failed;
        }

        try
        {
            workflow.Status = WorkflowStatus.Processing;
            await _workflowRepository.UpdateAsync(workflow);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

            // Stage 1: download the bytes (throws on cancel/fail — its own ProcessRegistry entry is already
            // Cancelled/Failed and staging cleaned).
            var download = await _remoteImportService.DownloadStageAsync(ctx.Job, ct);

            // Persist the download result + advance the stage, then hand off to the IMPORT lane. Re-read the
            // row so a Cancel that raced the download isn't clobbered (if it's gone, discard the staging).
            var fresh = await _workflowRepository.GetByIdAsync(jobId);
            if (fresh == null)
            {
                await _remoteImportService.DiscardDownloadAsync(download);
                return JobOutcome.Cancelled;
            }
            ctx.Stage = RemoteImportStage.Import;
            ctx.Download = download;
            fresh.Context = JsonHelper.Serialize(ctx);
            fresh.Status = WorkflowStatus.Pending; // downloaded → waiting for an import slot
            await _workflowRepository.UpdateAsync(fresh);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, fresh);

            // Prefer finishing a downloaded item (progress 50) over freshly-confirmed ones in the import lane.
            _queue.Enqueue(jobId, RemoteImportWorkflowHandler.TypeId,
                new WorkflowPriority(Confirmed: true, Progress: 50, fresh.CreatedAt));
            _logger.Info($"[RemoteDownload] {jobId} downloaded → queued for import", "RemoteDownloadHandler");
            return JobOutcome.Completed; // frees the download slot; the import leg runs the row
        }
        catch (OperationCanceledException)
        {
            return JobOutcome.Cancelled; // DownloadStageAsync already cleaned its staging + cancelled its registry entry
        }
        catch (Exception ex)
        {
            await FailAsync(workflow, ex.Message);
            return JobOutcome.Failed;
        }
    }

    private async Task FailAsync(WorkflowInfo workflow, string error)
    {
        workflow.Status = WorkflowStatus.Failed;
        workflow.ErrorMessage = error;
        workflow.CompletedAt = DateTime.UtcNow;
        await _workflowRepository.UpdateAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.FAILED, workflow);
    }
}
