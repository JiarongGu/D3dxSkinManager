using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Services;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Modules.Workflow.Handlers;

/// <summary>
/// The REMOTE_IMPORT workflow — a remote download+import run as a TWO-STAGE job on the shared import queue
/// actor (import-queue-actor.md). Stage 1 (DOWNLOAD lane) is <see cref="RemoteDownloadHandler"/>; it
/// downloads the raw bytes then re-enqueues stage 2 (IMPORT lane), which THIS handler runs
/// (extract→recompress→import). Splitting the legs means a finished download WAITS for a compress slot
/// instead of one shared pool coupling network + CPU. The DB-backed WorkflowInfo row (Type
/// <c>REMOTE_IMPORT</c>, context = <see cref="RemoteImportWorkflowContext"/>) IS the durable queue entry;
/// a crash re-runs from the DOWNLOAD stage (no half-file resume).
/// </summary>
public class RemoteImportWorkflowHandler : IWorkflowHandler, IImportJobHandler
{
    /// <summary>The workflow row type + the IMPORT-stage job type (import lane).</summary>
    public const string TypeId = "REMOTE_IMPORT";
    public string WorkflowType => TypeId;
    public string JobType => TypeId;

    private readonly IWorkflowRepository _workflowRepository;
    private readonly IEventBus _eventBus;
    private readonly IImportQueueActor _queue;
    private readonly IRemoteImportService _remoteImportService;
    private readonly ILogHelper _logger;

    public RemoteImportWorkflowHandler(
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

    /// <summary>Deserialize a REMOTE_IMPORT row's context, tolerating an OLDER row whose context was a bare
    /// <see cref="RemoteImportJob"/> (pre-two-stage) — treat it as a fresh Download-stage context.</summary>
    public static RemoteImportWorkflowContext DeserializeContext(string? raw)
    {
        var ctx = JsonHelper.Deserialize<RemoteImportWorkflowContext>(raw);
        var wrapped = ctx != null && (!string.IsNullOrEmpty(ctx.Job?.Option?.Url) || !string.IsNullOrEmpty(ctx.Job?.SourceId));
        if (wrapped) return ctx!;
        // Back-compat: parse the raw as a bare job (top-level sourceId/option/…).
        var bare = JsonHelper.Deserialize<RemoteImportJob>(raw);
        if (bare != null && (!string.IsNullOrEmpty(bare.Option?.Url) || !string.IsNullOrEmpty(bare.SourceId)))
            return new RemoteImportWorkflowContext { Job = bare, Stage = RemoteImportStage.Download };
        return ctx ?? new RemoteImportWorkflowContext();
    }

    /// <summary>Create a Pending REMOTE_IMPORT row + enqueue its DOWNLOAD stage onto the actor. Fails fast
    /// on an unsupported host. Returns immediately (the actor runs it when a download slot frees).</summary>
    public async Task<WorkflowInfo> StartRemoteImportAsync(RemoteImportJob job)
    {
        if (!RemoteImportService.IsImportable(job.Option.Type))
            throw new OperationException("REMOTE_DOWNLOAD_UNSUPPORTED", "host", job.Option.Name);

        var workflow = new WorkflowInfo
        {
            Id = Guid.NewGuid().ToString(),
            Type = TypeId,
            Status = WorkflowStatus.Pending,
            Context = JsonHelper.Serialize(new RemoteImportWorkflowContext { Job = job, Stage = RemoteImportStage.Download }),
            CreatedAt = DateTime.UtcNow,
        };
        await _workflowRepository.AddAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.CREATED, workflow);
        // Remote downloads are user-committed (no preview) → CONFIRMED tier. Stage 1 runs in the DOWNLOAD lane.
        _queue.Enqueue(workflow.Id, RemoteDownloadHandler.TypeId, new WorkflowPriority(Confirmed: true, Progress: 0, workflow.CreatedAt));
        _logger.Info($"[RemoteImport] queued '{job.Detail.Title}' ({workflow.Id})", "RemoteImportWorkflowHandler");
        return workflow;
    }

    /// <summary>The actor runs this as the IMPORT leg (import lane): extract+recompress+import from the
    /// downloaded bytes. Success removes the queue row; failure marks it Failed.</summary>
    public async Task<JobOutcome> ProcessAsync(string jobId, CancellationToken ct)
    {
        var workflow = await _workflowRepository.GetByIdAsync(jobId);
        if (workflow == null)
        {
            _logger.Info($"[RemoteImport] job {jobId} vanished before it ran — skipping", "RemoteImportWorkflowHandler");
            return JobOutcome.Completed;
        }

        var ctx = DeserializeContext(workflow.Context);
        if (ctx.Job == null || string.IsNullOrEmpty(ctx.Job.Option?.Url))
        {
            await FailAsync(workflow, "Invalid remote import context");
            return JobOutcome.Failed;
        }

        // No download result (a crash lost the in-flight staging, or a stray enqueue): fall back to the
        // download stage rather than failing — the actor frees this import slot on Yielded.
        if (ctx.Download == null)
        {
            ctx.Stage = RemoteImportStage.Download;
            workflow.Context = JsonHelper.Serialize(ctx);
            await _workflowRepository.UpdateAsync(workflow);
            _queue.Enqueue(jobId, RemoteDownloadHandler.TypeId, new WorkflowPriority(Confirmed: true, Progress: 0, workflow.CreatedAt));
            _logger.Info($"[RemoteImport] {jobId} had no download result — re-queued to download", "RemoteImportWorkflowHandler");
            return JobOutcome.Yielded;
        }

        try
        {
            workflow.Status = WorkflowStatus.Processing;
            await _workflowRepository.UpdateAsync(workflow);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

            var outcome = await _remoteImportService.ImportStageAsync(ctx.Job, ctx.Download, ct);

            switch (outcome)
            {
                case JobOutcome.Completed:
                    await _workflowRepository.DeleteAsync(jobId);
                    await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.DELETED, jobId);
                    break;
                case JobOutcome.Failed:
                    await FailAsync(workflow, "Remote import failed (see Activity for detail)");
                    break;
                // Cancelled → the Cancel path (CancelAsync) already removed the row.
            }
            return outcome;
        }
        catch (OperationCanceledException)
        {
            return JobOutcome.Cancelled;
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

    // ---- IWorkflowHandler (queue lifecycle; no preview/confirm for remote) ----

    public async Task<WorkflowInfo> StartAsync(string initialData)
    {
        var job = JsonHelper.Deserialize<RemoteImportJob>(initialData)
            ?? throw new InvalidOperationException("Invalid remote import data");
        return await StartRemoteImportAsync(job);
    }

    public Task<WorkflowInfo> ContinueAsync(string workflowId) =>
        throw new InvalidOperationException("Remote imports have no confirm step");

    public Task<WorkflowInfo> UpdateContextAsync(string workflowId, string contextUpdate) =>
        throw new InvalidOperationException("Remote imports have no editable context");

    public async Task<WorkflowInfo> PauseAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId)
            ?? throw new InvalidOperationException($"Workflow not found: {workflowId}");
        _queue.Cancel(workflowId); // drop if queued / signal its token if running (either lane)
        workflow.Status = WorkflowStatus.Paused;
        await _workflowRepository.UpdateAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);
        return workflow;
    }

    public async Task<WorkflowInfo> CancelAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId)
            ?? throw new InvalidOperationException($"Workflow not found: {workflowId}");
        _queue.Cancel(workflowId); // download/import stage's finally cleans its own staging + drive copy
        // Cancelled BETWEEN stages (downloaded, queued for import): drop the staged files the download left.
        var ctx = DeserializeContext(workflow.Context);
        if (ctx.Download != null)
            await _remoteImportService.DiscardDownloadAsync(ctx.Download);
        await _workflowRepository.DeleteAsync(workflowId);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.DELETED, workflowId);
        return workflow;
    }

    public async Task<WorkflowInfo> ResumeFromCurrentStepAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId)
            ?? throw new InvalidOperationException($"Workflow not found: {workflowId}");

        if (workflow.Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled)
            throw new InvalidOperationException($"Cannot resume workflow in terminal state: {workflow.Status}");

        // A crash loses the in-flight staging ({profile}/temp is swept on startup); re-run from DOWNLOAD.
        // Clearing the stale Download result also frees any leftover managed archive on the next attempt.
        var ctx = DeserializeContext(workflow.Context);
        ctx.Stage = RemoteImportStage.Download;
        ctx.Download = null;
        workflow.Context = JsonHelper.Serialize(ctx);
        workflow.Status = WorkflowStatus.Pending;
        await _workflowRepository.UpdateAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);
        // Idempotent: the actor dedups an Enqueue of an already-queued/running job (a crash resume +
        // any UI resume can't double-run).
        _queue.Enqueue(workflowId, RemoteDownloadHandler.TypeId, new WorkflowPriority(Confirmed: true, Progress: 0, workflow.CreatedAt));
        return workflow;
    }
}
