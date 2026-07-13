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
/// Handler for the REMOTE_IMPORT workflow type — a remote download+import as a job on the shared import
/// queue actor (import-queue-actor.md). Unlike MOD_IMPORT there is NO preview/confirm pause: one leg runs
/// the whole resolve→download→recompress→import via <see cref="IRemoteImportService.RunImportAsync"/>.
/// The DB-backed WorkflowInfo row IS the durable queue entry, so a crash resumes it (re-runs the download
/// from the persisted <see cref="RemoteImportJob"/> — no half-file resume).
/// </summary>
public class RemoteImportWorkflowHandler : IWorkflowHandler, IImportJobHandler
{
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

    /// <summary>Create a Pending REMOTE_IMPORT row + enqueue it onto the actor. Fails fast on an
    /// unsupported host. Returns immediately (the actor runs it when a slot frees).</summary>
    public async Task<WorkflowInfo> StartRemoteImportAsync(RemoteImportJob job)
    {
        if (!RemoteImportService.IsImportable(job.Option.Type))
            throw new OperationException("REMOTE_DOWNLOAD_UNSUPPORTED", "host", job.Option.Name);

        var workflow = new WorkflowInfo
        {
            Id = Guid.NewGuid().ToString(),
            Type = TypeId,
            Status = WorkflowStatus.Pending,
            Context = JsonHelper.Serialize(job),
            CreatedAt = DateTime.UtcNow,
        };
        await _workflowRepository.AddAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.CREATED, workflow);
        // Remote downloads are user-committed (no preview) → CONFIRMED tier, competing with confirmed local imports.
        _queue.Enqueue(workflow.Id, TypeId, new WorkflowPriority(Confirmed: true, Progress: 0, workflow.CreatedAt));
        _logger.Info($"[RemoteImport] queued '{job.Detail.Title}' ({workflow.Id})", "RemoteImportWorkflowHandler");
        return workflow;
    }

    /// <summary>The actor runs this: one leg = the whole remote download+import. Success removes the queue
    /// row (the mod is imported + the ProcessRegistry entry showed progress); failure marks it Failed.</summary>
    public async Task<JobOutcome> ProcessAsync(string jobId, CancellationToken ct)
    {
        var workflow = await _workflowRepository.GetByIdAsync(jobId);
        if (workflow == null)
        {
            _logger.Info($"[RemoteImport] job {jobId} vanished before it ran — skipping", "RemoteImportWorkflowHandler");
            return JobOutcome.Completed;
        }

        var job = JsonHelper.Deserialize<RemoteImportJob>(workflow.Context);
        if (job == null)
        {
            await FailAsync(workflow, "Invalid remote import context");
            return JobOutcome.Failed;
        }

        try
        {
            workflow.Status = WorkflowStatus.Processing;
            await _workflowRepository.UpdateAsync(workflow);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

            var outcome = await _remoteImportService.RunImportAsync(job, ct);

            switch (outcome)
            {
                case JobOutcome.Completed:
                    await _workflowRepository.DeleteAsync(jobId);
                    await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.DELETED, jobId);
                    break;
                case JobOutcome.Failed:
                    await FailAsync(workflow, "Remote download+import failed (see Activity for detail)");
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
        _queue.Cancel(workflowId); // drop if queued / signal its token if running
        workflow.Status = WorkflowStatus.Paused;
        await _workflowRepository.UpdateAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);
        return workflow;
    }

    public async Task<WorkflowInfo> CancelAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId)
            ?? throw new InvalidOperationException($"Workflow not found: {workflowId}");
        _queue.Cancel(workflowId); // RunImportAsync's finally cleans its own staging + quark copy
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

        workflow.Status = WorkflowStatus.Pending;
        await _workflowRepository.UpdateAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);
        // Idempotent: the actor dedups an Enqueue of an already-queued/running job (a crash resume +
        // any UI resume can't double-run).
        _queue.Enqueue(workflowId, TypeId, new WorkflowPriority(Confirmed: true, Progress: 0, workflow.CreatedAt));
        return workflow;
    }
}
