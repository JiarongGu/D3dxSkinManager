using System.Collections.Concurrent;
using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Services;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Mod.Models;
using D3dxSkinManager.Modules.Context;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Profiles;
using SharpSevenZip;

namespace D3dxSkinManager.Modules.Workflow.Handlers;

/// <summary>
/// Handler for MOD_IMPORT workflow type
/// Manages the import of mods from folders or archive files:
///
/// For FOLDER imports (4 steps):
/// 1. ExtractMetadata (1% progress) - Validate and extract basic info
/// 2. CompressFolder (1-100% progress) - Compress folder to archive
/// 3. Wait for user to update context with metadata (paused at 100%)
/// 4. ImportMod (100% progress + completed) - Import with user metadata
///
/// For FILE imports (3 steps - compression skipped):
/// 1. ExtractMetadata (100% progress) - Validate archive, check password, extract basic info
/// 2. Wait for user to update context with metadata (paused at 100%)
/// 3. ImportMod (100% progress + completed) - Import with user metadata
///
/// Archive validation includes:
/// - Format detection (ZIP, 7Z, RAR, TAR, GZIP, BZIP2)
/// - Password protection check (rejects password-protected archives)
/// </summary>
public class ModImportWorkflowHandler : IWorkflowHandler, IImportJobHandler
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IModImportService _modImportService;
    private readonly IModMetadataService _metadataService;
    private readonly IProfilePathService _profilePathService;
    private readonly IProfileService _profileService;
    private readonly IProfileContext _profileContext;
    private readonly IArchiveHelper _archiveHelper;
    private readonly IFileHelper _fileHelper;
    private readonly IEventBus _eventBus;
    private readonly ILogHelper _logger;
    private readonly IModEnrichmentService _enrichmentService;
    private readonly IImportQueueActor _queue;
    private readonly ICategoryService _categoryService;

    public string WorkflowType => "MOD_IMPORT";

    /// <summary>The job type this handler runs for the shared import queue (== <see cref="WorkflowType"/>).</summary>
    public string JobType => WorkflowType;

    public ModImportWorkflowHandler(
        IWorkflowRepository workflowRepository,
        IModImportService modImportService,
        IModMetadataService metadataService,
        IProfilePathService profilePathService,
        IProfileService profileService,
        IProfileContext profileContext,
        IArchiveHelper archiveHelper,
        IFileHelper fileHelper,
        IEventBus eventBus,
        ILogHelper logger,
        IModEnrichmentService enrichmentService,
        IImportQueueActor queue,
        ICategoryService categoryService)
    {
        _workflowRepository = workflowRepository;
        _modImportService = modImportService;
        _metadataService = metadataService;
        _profilePathService = profilePathService;
        _profileService = profileService;
        _profileContext = profileContext;
        _archiveHelper = archiveHelper;
        _fileHelper = fileHelper;
        _eventBus = eventBus;
        _logger = logger;
        _enrichmentService = enrichmentService;
        _queue = queue;
        _categoryService = categoryService;
    }

    /// <summary>
    /// Run ONE leg of an import job for the <see cref="IImportQueueActor"/> — set Processing, run the
    /// current step (which chains to its next resting point), and report the outcome. The actor owns the
    /// slot + worker task; this owns the DB/status work. Replaces the old per-method
    /// <c>Task.Run(TryAcquireSlot → ProcessStepAsync)</c> block (was triplicated across Start/Continue/Resume).
    /// </summary>
    public async Task<JobOutcome> ProcessAsync(string jobId, CancellationToken ct)
    {
        var workflow = await _workflowRepository.GetByIdAsync(jobId);
        if (workflow == null)
        {
            _logger.Info($"Import job {jobId} vanished before it ran — skipping");
            return JobOutcome.Completed;
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            workflow.Status = WorkflowStatus.Processing;
            await _workflowRepository.UpdateAsync(workflow);
            await PopulateCategoryNameInContextAsync(workflow);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

            // Runs the current step to its next resting point (WaitingForInput / Completed / Failed) and
            // persists that status itself (incl. its own error handling).
            await ProcessStepAsync(workflow, ct);
        }
        catch (OperationCanceledException)
        {
            // Cancelled while running — CancelAsync already marked it Deleting + queued cleanup.
            _logger.Info($"Import job {jobId} was cancelled — cleanup will remove it");
            return JobOutcome.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.Error($"Import job {jobId} failed: {ex.Message}", "ModImportWorkflowHandler", ex);
            workflow.Status = WorkflowStatus.Failed;
            workflow.ErrorMessage = ex.Message;
            workflow.CompletedAt = DateTime.UtcNow;
            await _workflowRepository.UpdateAsync(workflow);
            await PopulateCategoryNameInContextAsync(workflow);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.FAILED, workflow);
            return JobOutcome.Failed;
        }

        // Map the leg's resting status → outcome (informational; the actor frees the slot regardless).
        var after = await _workflowRepository.GetByIdAsync(jobId);
        return after?.Status switch
        {
            WorkflowStatus.WaitingForInput => JobOutcome.Yielded,
            WorkflowStatus.Failed => JobOutcome.Failed,
            WorkflowStatus.Deleting or WorkflowStatus.Cancelled => JobOutcome.Cancelled,
            _ => JobOutcome.Completed,
        };
    }

    /// <summary>
    /// Start a new mod import workflow
    /// Returns immediately after creating workflow - processing happens asynchronously
    /// Step 1: Extract metadata from folder/file
    /// </summary>
    /// <param name="folderPath">Path to folder or archive file</param>
    /// <param name="defaultCategory">Optional default category name to pre-fill</param>
    /// <summary>Admission priority: confirmed (ImportMod step) first, then higher progress, then
    /// earlier-created — so a just-confirmed import jumps ahead of older unconfirmed previews.</summary>
    private static WorkflowPriority BuildPriority(WorkflowInfo workflow, ModImportWorkflowContext context)
        => new(context.Step == ModImportWorkflowSteps.ImportMod, context.Progress ?? 0, workflow.CreatedAt);

    public async Task<WorkflowInfo> StartImportAsync(string folderPath, string? defaultCategory = null)
    {
        // Yield to prevent blocking UI thread
        await Task.Yield();

        _logger.Info($"Starting mod import workflow for folder: {folderPath}" +
            (defaultCategory != null ? $" with default category: {defaultCategory}" : ""));

        // Create workflow
        var workflow = new WorkflowInfo
        {
            Id = Guid.NewGuid().ToString(),
            Type = WorkflowType,
            Status = WorkflowStatus.Pending,
            Context = JsonHelper.Serialize(new ModImportWorkflowContext
            {
                Step = ModImportWorkflowSteps.ExtractMetadata,
                FolderPath = folderPath,
                Category = defaultCategory  // Pre-fill category from selected category in UI
            }),
            CreatedAt = DateTime.UtcNow
        };

        await _workflowRepository.AddAsync(workflow);

        // Populate category name before emitting event
        await PopulateCategoryNameInContextAsync(workflow);

        // Emit workflow created event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.CREATED, workflow);

        // Enqueue onto the shared import queue — the actor admits it by priority within the concurrency
        // bound and runs ProcessAsync on a worker (not-yet-confirmed preview → lowest tier, earlier wins).
        _queue.Enqueue(workflow.Id, WorkflowType,
            new WorkflowPriority(Confirmed: false, Progress: 0, workflow.CreatedAt));

        return workflow;
    }

    /// <summary>
    /// Resume workflow (user confirmed metadata, continue to import)
    /// This is called after user updates context via UPDATE_WORKFLOW_CONTEXT
    /// </summary>
    public async Task<WorkflowInfo> ContinueAsync(string workflowId)
    {
        // Yield to prevent blocking UI thread
        await Task.Yield();

        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context)
            ?? throw new InvalidOperationException("Invalid workflow context");

        if (workflow.Status != WorkflowStatus.WaitingForInput)
            throw new InvalidOperationException($"Workflow is not paused. Current status: {workflow.Status}");

        _logger.Info($"Resuming workflow {workflowId}...");

        // Move to import step
        context.Step = ModImportWorkflowSteps.ImportMod;
        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.Pending;  // Set to Pending first

        await _workflowRepository.UpdateAsync(workflow);

        // Populate category name before emitting event
        await PopulateCategoryNameInContextAsync(workflow);

        // Emit status changed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        // Re-enqueue onto the shared import queue — now CONFIRMED (ImportMod step), so it jumps ahead of
        // unconfirmed previews. If it's still mid-yield in the actor, the re-enqueue is honored on finish.
        _queue.Enqueue(workflow.Id, WorkflowType, BuildPriority(workflow, context));

        return workflow;
    }

    /// <summary>
    /// Cancel/Delete the workflow
    /// Returns immediately, cleanup happens asynchronously
    /// Sets status to Deleting, then emits DELETED event when done
    /// </summary>
    public async Task<WorkflowInfo> CancelAsync(string workflowId)
    {
        // Yield to prevent blocking UI thread
        await Task.Yield();

        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        // Don't allow deletion during final import step (after user confirmation)
        // The workflow will delete itself automatically after completion
        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);
        if (context?.Step == ModImportWorkflowSteps.ImportMod && workflow.Status == WorkflowStatus.Processing)
        {
            _logger.Info($"Cannot delete workflow {workflowId} during final import step - it will auto-delete after completion");
            throw new InvalidOperationException("Cannot delete workflow during final import. Please wait for it to complete.");
        }

        _logger.Info($"Deleting workflow {workflowId}...");

        // Cancel via the queue — drops it if still queued, signals its token if running (compression, etc.).
        _queue.Cancel(workflowId);

        // Set status to Deleting immediately
        workflow.Status = WorkflowStatus.Deleting;
        await _workflowRepository.UpdateAsync(workflow);

        // Populate category name before emitting event
        await PopulateCategoryNameInContextAsync(workflow);

        // Emit status changed event to show "Deleting..." in UI
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        // Perform cleanup asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait a bit for cancellation to complete
                await Task.Delay(100);

                var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);

                // Clean up temp file if exists (only if we created it, not the user's original file)
                // For folders: IsArchiveFile=false, we created temp .7z -> delete
                // For archives: IsArchiveFile=true, TempArchivePath = user's original -> don't delete
                if (context?.TempArchivePath != null &&
                    !context.IsArchiveFile &&
                    _fileHelper.FileExists(context.TempArchivePath))
                {
                    var deleted = await _fileHelper.DeleteFileAsync(context.TempArchivePath);
                    if (deleted)
                    {
                        _logger.Info($"Deleted temp archive: {context.TempArchivePath}");
                    }
                    else
                    {
                        _logger.Error($"Failed to delete temp archive: {context.TempArchivePath}");
                    }
                }

                // Delete workflow from database
                await _workflowRepository.DeleteAsync(workflowId);

                // Emit DELETED event (UI will remove it from the list)
                await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.DELETED, workflowId);

                _logger.Info($"Workflow {workflowId} deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to delete workflow {workflowId}: {ex.Message}", "ModImportWorkflowHandler", ex);

                // Mark as failed instead of deleting
                workflow.Status = WorkflowStatus.Failed;
                workflow.ErrorMessage = $"Failed to delete: {ex.Message}";
                workflow.CompletedAt = DateTime.UtcNow;
                await _workflowRepository.UpdateAsync(workflow);
                await PopulateCategoryNameInContextAsync(workflow);
                await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.FAILED, workflow);
            }
        });

        return workflow;
    }

    /// <summary>
    /// Pause a running workflow (user requested)
    /// Changes status to Paused and cancels the running task
    /// </summary>
    public async Task<WorkflowInfo> PauseAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        // Only Pending or Processing workflows can be paused
        if (workflow.Status != WorkflowStatus.Pending && workflow.Status != WorkflowStatus.Processing)
            throw new InvalidOperationException($"Cannot pause workflow in status: {workflow.Status}");

        _logger.Info($"Pausing workflow {workflowId}...");

        // Drop it from the queue / signal its running token — the actor frees the slot on finish.
        _queue.Cancel(workflowId);

        // Update status to Paused
        workflow.Status = WorkflowStatus.Paused;
        await _workflowRepository.UpdateAsync(workflow);
        await PopulateCategoryNameInContextAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        _logger.Info($"Workflow {workflowId} paused successfully");
        return workflow;
    }

    /// <summary>
    /// Implementation of IWorkflowHandler.StartAsync
    /// Delegates to StartImportAsync with folderPath from initialData
    /// </summary>
    public async Task<WorkflowInfo> StartAsync(string initialData)
    {
        // initialData can be either:
        // 1. Simple string: folder path only (backward compatible)
        // 2. JSON object: { folderPath, defaultCategory }

        string folderPath;
        string? defaultCategory = null;

        // Try to parse as JSON first
        if (initialData.TrimStart().StartsWith("{"))
        {
            try
            {
                var data = JsonHelper.Deserialize<Dictionary<string, string>>(initialData);
                if (data != null && data.TryGetValue("folderPath", out var path))
                {
                    folderPath = path;
                    data.TryGetValue("defaultCategory", out defaultCategory);
                }
                else
                {
                    // Fallback to treating as simple path
                    folderPath = initialData;
                }
            }
            catch
            {
                // If JSON parsing fails, treat as simple path
                folderPath = initialData;
            }
        }
        else
        {
            // Simple string path (backward compatible)
            folderPath = initialData;
        }

        return await StartImportAsync(folderPath, defaultCategory);
    }

    /// <summary>
    /// Update workflow context (partial update of metadata fields)
    /// Uses JsonHelper to handle naming convention conversion (camelCase <-> PascalCase)
    /// </summary>
    public async Task<WorkflowInfo> UpdateContextAsync(string workflowId, string contextUpdate)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow {workflowId} not found");

        // Deserialize current context
        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);
        if (context == null)
            throw new InvalidOperationException("Invalid workflow context");

        // Convert the update JSON to context object
        // JsonHelper handles naming conventions (camelCase <-> PascalCase) automatically
        var updateContext = JsonHelper.Deserialize<ModImportWorkflowContext>(contextUpdate);
        if (updateContext == null)
            throw new InvalidOperationException("Failed to deserialize context update");

        // Merge non-null fields from update into existing context
        if (!string.IsNullOrEmpty(updateContext.Name))
            context.Name = updateContext.Name;
        if (updateContext.Author != null)
            context.Author = updateContext.Author;
        if (updateContext.Description != null)
            context.Description = updateContext.Description;
        if (updateContext.Category != null)
            context.Category = updateContext.Category;
        if (updateContext.Tags != null)
            context.Tags = updateContext.Tags;
        if (updateContext.Grading != null)
            context.Grading = updateContext.Grading;

        // Serialize and save
        workflow.Context = JsonHelper.Serialize(context);
        await _workflowRepository.UpdateAsync(workflow);

        _logger.Info($"Updated context for workflow {workflowId}");

        return workflow;
    }

    /// <summary>
    /// Resume workflow from current step (used for application restart)
    /// Restarts processing from wherever the workflow left off
    /// </summary>
    public async Task<WorkflowInfo> ResumeFromCurrentStepAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        // No manual double-run guard needed: the import queue actor DEDUPES an Enqueue for a job already
        // queued or running (backend profile-init resume + the frontend screen-mount resume can both fire),
        // and the terminal / WaitingForInput status checks below reject anything that already finished.
        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context)
            ?? throw new InvalidOperationException("Invalid workflow context");

        // Don't resume workflows that are waiting for user input
        if (workflow.Status == WorkflowStatus.WaitingForInput)
        {
            _logger.Info($"Workflow {workflowId} is waiting for user input, cannot resume");
            throw new InvalidOperationException("Cannot resume workflow waiting for user input");
        }

        // Don't resume already completed/failed/cancelled workflows
        if (workflow.Status == WorkflowStatus.Completed ||
            workflow.Status == WorkflowStatus.Failed ||
            workflow.Status == WorkflowStatus.Cancelled)
        {
            _logger.Info($"Workflow {workflowId} is in terminal state ({workflow.Status}), cannot resume");
            throw new InvalidOperationException($"Cannot resume workflow in terminal state: {workflow.Status}");
        }

        // Only resume Pending, Processing, or Paused workflows
        if (workflow.Status != WorkflowStatus.Pending &&
            workflow.Status != WorkflowStatus.Processing &&
            workflow.Status != WorkflowStatus.Paused)
        {
            _logger.Info($"Workflow {workflowId} has invalid status for resume: {workflow.Status}");
            throw new InvalidOperationException($"Cannot resume workflow with status: {workflow.Status}");
        }

        _logger.Info($"Resuming workflow {workflowId} from step: {context.Step}");

        // Reset progress to the beginning of the current step
        // This ensures UI shows correct progress when restarting after app reboot
        context.Progress = context.Step switch
        {
            ModImportWorkflowSteps.ExtractMetadata => 0,
            ModImportWorkflowSteps.CompressFolder => 1,  // After metadata extraction
            ModImportWorkflowSteps.ImportMod => 90,      // After compression (or 100 for file imports)
            _ => context.Progress  // Keep existing progress if unknown step
        };

        // Update workflow context with reset progress
        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.Pending;
        await _workflowRepository.UpdateAsync(workflow);
        await PopulateCategoryNameInContextAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        // Enqueue onto the shared import queue — the actor admits by priority and runs ProcessAsync from
        // the current step. Idempotent: a duplicate resume of an already-queued/running job is deduped.
        _queue.Enqueue(workflow.Id, WorkflowType, BuildPriority(workflow, context));

        return workflow;
    }

    /// <summary>
    /// Internal state machine - processes the current step
    /// </summary>
    private async Task ProcessStepAsync(WorkflowInfo workflow, CancellationToken cancellationToken = default)
    {
        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context)
            ?? throw new InvalidOperationException("Invalid workflow context");

        try
        {
            switch (context.Step)
            {
                case ModImportWorkflowSteps.ExtractMetadata:
                    await ExtractMetadataAsync(workflow, context, cancellationToken);
                    break;

                case ModImportWorkflowSteps.CompressFolder:
                    await CompressFolderAsync(workflow, context, cancellationToken);
                    break;

                case ModImportWorkflowSteps.ImportMod:
                    await ImportModAsync(workflow, context, cancellationToken);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown step: {context.Step}");
            }
        }
        catch (OperationCanceledException)
        {
            // Workflow was cancelled by user (via CancelAsync)
            // CancelAsync already set status to Deleting and started cleanup task
            // The async cleanup will delete temp files and remove workflow from database
            // No need to mark as failed - just let it delete
            _logger.Info($"Workflow {workflow.Id} was cancelled - will be deleted by cleanup task");
        }
        catch (OperationException opEx)
        {
            // Known operation error with structured error code
            _logger.Error($"Workflow step failed: {opEx.Message}", "ModImportWorkflowHandler", opEx);
            workflow.Status = WorkflowStatus.Failed;
            workflow.ErrorMessage = opEx.GetStructuredMessage();
            workflow.CompletedAt = DateTime.UtcNow;
            await _workflowRepository.UpdateAsync(workflow);

            // Populate category name before emitting event
            await PopulateCategoryNameInContextAsync(workflow);

            // Emit failed event
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.FAILED, workflow);
        }
        catch (Exception ex)
        {
            // Unknown error - wrap as UNKNOWN_ERROR
            _logger.Error($"Workflow step failed with unexpected error: {ex.Message}", "ModImportWorkflowHandler", ex);
            workflow.Status = WorkflowStatus.Failed;

            var opEx = new OperationException(
                WorkflowErrorCodes.UNKNOWN_ERROR,
                new Dictionary<string, string> { { "message", ex.Message } },
                ex.Message,
                ex
            );
            workflow.ErrorMessage = opEx.GetStructuredMessage();
            workflow.CompletedAt = DateTime.UtcNow;
            await _workflowRepository.UpdateAsync(workflow);

            // Populate category name before emitting event
            await PopulateCategoryNameInContextAsync(workflow);

            // Emit failed event
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.FAILED, workflow);
        }
    }

    /// <summary>
    /// Step 1: Extract metadata from folder/file
    /// </summary>
    private async Task ExtractMetadataAsync(WorkflowInfo workflow, ModImportWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.FolderPath))
            throw new InvalidOperationException("Folder path is required");

        var isArchive = _fileHelper.FileExists(context.FolderPath);
        var isDirectory = _fileHelper.DirectoryExists(context.FolderPath);

        if (!isArchive && !isDirectory)
            throw new InvalidOperationException($"Path not found: {context.FolderPath}");

        // If it's a file, validate it's a supported archive format
        if (isArchive)
        {
            _logger.Info("Validating archive file...");
            var validation = await _archiveHelper.ValidateArchiveAsync(context.FolderPath);

            if (!validation.IsValid)
            {
                _logger.Error($"Invalid or unsupported file type: {validation.ErrorMessage}");
                throw new OperationException(
                    WorkflowErrorCodes.MI_UNSUPPORTED_FILE_TYPE,
                    message: "Unsupported file type"
                );
            }

            if (validation.IsPasswordProtected)
            {
                _logger.Error("Archive is password protected");
                throw new OperationException(
                    WorkflowErrorCodes.MI_PASSWORD_PROTECTED,
                    message: "Password-protected archive"
                );
            }

            _logger.Info($"Archive validation successful. Type: {validation.DetectedType}");
        }

        _logger.Info($"Extracting metadata from: {context.FolderPath}");

        // Get basic info
        string folderName;
        int fileCount;

        if (isArchive)
        {
            // For archives, use filename without extension
            folderName = Path.GetFileNameWithoutExtension(context.FolderPath);
            // TODO: Could extract archive to temp and count files, but for now just set 0
            fileCount = 0;
            context.TempArchivePath = context.FolderPath;  // Use the archive directly
            context.IsArchiveFile = true;  // Mark as archive for cleanup logic
        }
        else
        {
            // For folders
            folderName = Path.GetFileName(context.FolderPath.TrimEnd(Path.DirectorySeparatorChar));
            var files = _fileHelper.GetFiles(context.FolderPath, "*", SearchOption.AllDirectories);
            fileCount = files.Length;
            context.IsArchiveFile = false;  // Mark as folder for cleanup logic
        }

        _logger.Info($"Detected: {folderName} ({fileCount} files)");

        // Pre-fill metadata with detected values
        // Preserve existing category if it was pre-filled from UI (don't overwrite with null)
        var existingCategory = context.Category;

        context.FolderName = folderName;
        context.FileCount = fileCount;
        context.Name = folderName;  // User can edit this
        context.Author = null;      // User should fill this
        context.Description = null; // User should fill this
        context.Category = existingCategory;  // Preserve pre-filled category from selected category in UI
        context.Tags = new List<string>();
        context.Grading = "G";
        context.Progress = isArchive ? 100 : 1;  // File imports: 100% after metadata, Folder imports: 1%

        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.Processing;
        await _workflowRepository.UpdateAsync(workflow);

        // Emit progress event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.PROGRESS, new
        {
            WorkflowId = workflow.Id,
            Progress = context.Progress,
            Step = context.Step
        });

        // If it's a folder, start compressing
        if (!isArchive)
        {
            _logger.Info("Starting folder compression");
            context.Step = ModImportWorkflowSteps.CompressFolder;
            workflow.Context = JsonHelper.Serialize(context);
            await _workflowRepository.UpdateAsync(workflow);

            // Continue to compression step
            await ProcessStepAsync(workflow, cancellationToken);
        }
        else
        {
            // Archive file - skip compression, go directly to waiting for user confirmation
            // Progress is already set to 100% for file imports
            _logger.Info("Archive file detected, skipping compression and pausing for user metadata confirmation");
            workflow.Status = WorkflowStatus.WaitingForInput;
            await _workflowRepository.UpdateAsync(workflow);
            await PopulateCategoryNameInContextAsync(workflow);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);
        }
    }

    /// <summary>
    /// Step 2: Compress folder into temporary archive (only if source is folder)
    /// Progress ranges from 1% to 100% during compression
    /// </summary>
    private async Task CompressFolderAsync(WorkflowInfo workflow, ModImportWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Yield to prevent blocking UI thread
        await Task.Yield();

        if (string.IsNullOrEmpty(context.FolderPath))
            throw new InvalidOperationException("Folder path is required");

        if (!_fileHelper.DirectoryExists(context.FolderPath))
            throw new OperationException(
                WorkflowErrorCodes.MI_FOLDER_NOT_FOUND,
                new Dictionary<string, string> { { "path", context.FolderPath } },
                $"Folder not found: {context.FolderPath}"
            );

        _logger.Info($"Compressing folder: {context.FolderPath}");

        // Load compression settings from profile configuration
        var (format, level) = await GetCompressionSettingsAsync();

        // Create temp archive in profile's temp directory using workflow ID (not random GUID)
        // This makes debugging easier and prevents race conditions
        var tempPath = Path.Combine(_profilePathService.TempDirectory, TempFileConstants.GetModImportCompressTempName(workflow.Id));

        // CRITICAL: Set TempArchivePath BEFORE starting compression to prevent race conditions
        // Progress callbacks fire-and-forget save context, so TempArchivePath must be set first
        // Otherwise, progress updates may overwrite the path after compression completes
        context.TempArchivePath = tempPath;
        workflow.Context = JsonHelper.Serialize(context);
        await _workflowRepository.UpdateAsync(workflow);

        try
        {
            // Compress with real-time progress reporting
            // Compression takes 0-100% (no SHA calculation needed with GUID-based IDs)
            var lastReportedProgress = 0;
            var lastEventTime = DateTime.MinValue;
            var eventThrottle = TimeSpan.FromMilliseconds(500); // Throttle events to max 2/sec (reduced from 10/sec)

            await _archiveHelper.CompressFolderAsync(
                context.FolderPath,
                tempPath,
                format: format,
                compressionLevel: level,
                progressCallback: progressPercent =>
                {
                    // Scale compression progress to 0-90% range
                    var scaledProgress = (int)(progressPercent * 0.9);
                    var now = DateTime.UtcNow;

                    // Report progress every 10% OR every 500ms (whichever comes first)
                    // This prevents flooding the event bus during fast compression
                    var shouldReport = scaledProgress >= lastReportedProgress + 10 ||
                                     scaledProgress >= 90 ||
                                     (now - lastEventTime) >= eventThrottle;

                    if (shouldReport)
                    {
                        lastReportedProgress = scaledProgress;
                        lastEventTime = now;

                        // Update context in memory
                        context.Progress = scaledProgress;

                        // Persist progress to database so users can see it when reopening the screen.
                        // Re-read from DB before writing so user edits made during compression
                        // (e.g. Name, Category) are not overwritten by this stale in-memory context.
                        // Fire and forget - don't block compression callback
                        var progressToSave = scaledProgress;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var dbWorkflow = await _workflowRepository.GetByIdAsync(workflow.Id);
                                if (dbWorkflow == null) return;
                                var dbContext = JsonHelper.Deserialize<ModImportWorkflowContext>(dbWorkflow.Context);
                                if (dbContext == null) return;
                                dbContext.Progress = progressToSave;
                                await _workflowRepository.UpdateContextAsync(workflow.Id, JsonHelper.Serialize(dbContext));
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn($"Failed to persist compression progress to database: {ex.Message}");
                            }
                        });

                        // Emit progress event - fire and forget (don't await)
                        // Reduced frequency (2/sec instead of 10/sec) minimizes thread pool pressure
                        _ = _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.PROGRESS, new
                        {
                            WorkflowId = workflow.Id,
                            Progress = scaledProgress,
                            Step = context.Step
                        });

                        _logger.Verbose($"Compression progress: {scaledProgress}% (raw: {progressPercent}%)");
                    }
                },
                cancellationToken: cancellationToken
            );

            _logger.Info($"Created temp archive: {tempPath}");

            // Compression complete — re-read from DB so user edits made during compression
            // (e.g. Name, Category) are preserved; only update system-owned fields.
            var freshWorkflow = await _workflowRepository.GetByIdAsync(workflow.Id) ?? workflow;
            var freshContext = JsonHelper.Deserialize<ModImportWorkflowContext>(freshWorkflow.Context) ?? context;

            _logger.Info("Skipping duplicate detection (using GUID-based IDs)");

            freshContext.Progress = 100;
            freshWorkflow.Context = JsonHelper.Serialize(freshContext);
            freshWorkflow.Status = WorkflowStatus.WaitingForInput;
            await _workflowRepository.UpdateAsync(freshWorkflow);

            // Keep local references in sync for the events emitted below
            workflow.Context = freshWorkflow.Context;
            workflow.Status = freshWorkflow.Status;

        // Populate category name before emitting event
        await PopulateCategoryNameInContextAsync(workflow);

        // Emit progress and status changed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.PROGRESS, new
        {
            WorkflowId = workflow.Id,
            Progress = freshContext.Progress,
            Step = freshContext.Step
        });
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        // Pause here - user needs to click Confirm button to continue
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled - clean up partial temp file
            _logger.Info($"Compression cancelled for workflow {workflow.Id}, cleaning up temp file");
            if (_fileHelper.FileExists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                    _logger.Info($"Deleted partial temp file: {tempPath}");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to delete temp file {tempPath}: {ex.Message}");
                }
            }
            throw; // Re-throw to propagate cancellation
        }
    }

    /// <summary>
    /// Step 3: Import mod with user-edited metadata
    /// Sets progress to 100% and marks workflow as completed
    /// </summary>
    private async Task ImportModAsync(WorkflowInfo workflow, ModImportWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // Yield to prevent blocking UI thread
        await Task.Yield();

        if (string.IsNullOrEmpty(context.TempArchivePath))
            throw new InvalidOperationException("Temp archive path is required");

        if (string.IsNullOrEmpty(context.Name))
            throw new InvalidOperationException("Mod name is required");

        if (!_fileHelper.FileExists(context.TempArchivePath))
            throw new InvalidOperationException($"Temp archive not found: {context.TempArchivePath}");

        // Category can be empty/null - it will be treated as "Unclassified"

        _logger.Info($"Importing mod from archive: {context.TempArchivePath}");

        // Step 1: Import mod from archive
        var modInfo = await _modImportService.ImportAsync(context.TempArchivePath);

        if (modInfo == null)
        {
            throw new InvalidOperationException("Import returned null - mod may already exist or import failed");
        }

        // Step 2: Update metadata with user-edited values from context
        var updateRequest = new UpdateModMetadataRequest
        {
            Name = context.Name,
            Author = context.Author,
            Description = context.Description,
            Grading = context.Grading ?? "G",
            Tags = context.Tags ?? new List<string>()
        };

        modInfo = await _metadataService.UpdateAsync(modInfo.Id, updateRequest);

        // Update category separately if provided
        if (!string.IsNullOrEmpty(context.Category))
        {
            modInfo = await _metadataService.UpdateCategoryAsync(modInfo.Id, context.Category);
        }

        _logger.Info($"Mod imported successfully: {modInfo.Id}");

        // Step 3: Auto-import preview images from original folder (only for folder imports)
        // For folder imports, we have access to the original folder path and can scan it for preview images
        // This mirrors the auto-import logic used when viewing mod previews from cache
        if (!context.IsArchiveFile && !string.IsNullOrEmpty(context.FolderPath) && _fileHelper.DirectoryExists(context.FolderPath))
        {
            try
            {
                _logger.Info($"Attempting to auto-import preview images from folder: {context.FolderPath}");
                var previewCount = await _modImportService.ScanAndImportPreviewsFromFolderAsync(modInfo.Id, context.FolderPath).ConfigureAwait(false);
                if (previewCount > 0)
                {
                    _logger.Info($"Auto-imported {previewCount} preview image(s) from folder for mod {modInfo.Id}");
                }
            }
            catch (Exception ex)
            {
                // Don't fail the import if preview import fails, just log and continue
                _logger.Warn($"Failed to auto-import previews from folder: {ex.Message}");
            }
        }

        // Clean up temp file (only if we created it during compression)
        // For folders: IsArchiveFile=false, we created temp .7z -> delete
        // For archives: IsArchiveFile=true, TempArchivePath = user's original -> don't delete
        if (!context.IsArchiveFile && !string.IsNullOrEmpty(context.TempArchivePath) && _fileHelper.FileExists(context.TempArchivePath))
        {
            var deleted = await _fileHelper.DeleteFileAsync(context.TempArchivePath);
            if (deleted)
            {
                _logger.Info($"Deleted temp archive: {context.TempArchivePath}");
            }
            else
            {
                _logger.Warn($"Failed to delete temp archive: {context.TempArchivePath}");
            }
        }

        // Update context
        context.ImportedModId = modInfo.Id;
        context.Step = ModImportWorkflowSteps.ImportMod;
        context.Progress = 100; // Import complete

        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.Completed;
        workflow.CompletedAt = DateTime.UtcNow;
        await _workflowRepository.UpdateAsync(workflow);

        // Populate category name before emitting event
        await PopulateCategoryNameInContextAsync(workflow);

        // Emit progress event (100%)
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.PROGRESS, new
        {
            WorkflowId = workflow.Id,
            Progress = context.Progress,
            Step = context.Step
        });

        // Emit MOD.IMPORTED event to refresh frontend mod list and category tree
        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.IMPORTED, modInfo);

        // Emit completed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.COMPLETED, workflow);

        // Auto-delete workflow after successful completion
        // MOD_IMPORT workflows are temporary and don't need to be kept in the queue
        try
        {
            await _workflowRepository.DeleteAsync(workflow.Id);
            _logger.Info($"Auto-deleted completed workflow: {workflow.Id}", "ModImportWorkflowHandler");

            // Emit deleted event to update frontend
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.DELETED, workflow.Id);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to auto-delete completed workflow {workflow.Id}: {ex.Message}", "ModImportWorkflowHandler");
        }
    }

    /// <summary>
    /// Batch populate CategoryName fields for multiple workflows
    /// Uses ModQueryService.PopulateCategoryNamesBulkAsync for efficient single database query
    /// This avoids N+1 query problem when displaying workflow lists
    /// </summary>
    public async Task PopulateCategoryNamesInContextsBulkAsync(List<WorkflowInfo> workflows)
    {
        try
        {
            // Extract all ModImportWorkflowContexts and their category IDs
            var contextsWithCategory = new List<(WorkflowInfo workflow, ModImportWorkflowContext context, ModInfo tempMod)>();

            foreach (var workflow in workflows)
            {
                try
                {
                    var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);
                    if (context != null && !string.IsNullOrEmpty(context.Category))
                    {
                        // Create temporary ModInfo to use batch populate function
                        var tempMod = new ModInfo { Category = context.Category };
                        contextsWithCategory.Add((workflow, context, tempMod));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to deserialize workflow context for {workflow.Id}: {ex.Message}", "ModImportWorkflowHandler");
                }
            }

            if (!contextsWithCategory.Any())
                return;

            // Single batch query to populate all category names
            var modList = contextsWithCategory.Select(x => x.tempMod).ToList();
            await _enrichmentService.PopulateCategoryNamesAsync(modList).ConfigureAwait(false);

            // Update contexts with populated category names
            foreach (var (workflow, context, tempMod) in contextsWithCategory)
            {
                context.CategoryName = tempMod.CategoryName;
                workflow.Context = JsonHelper.Serialize(context);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to batch populate category names in workflow contexts: {ex.Message}", "ModImportWorkflowHandler", ex);
        }
    }

    /// <summary>
    /// Populate CategoryName in workflow context before sending to frontend
    /// Uses CategoryService.GetCategoryNameAsync which utilizes cached category map
    /// </summary>
    private async Task PopulateCategoryNameInContextAsync(WorkflowInfo workflow)
    {
        try
        {
            var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);
            if (context != null && !string.IsNullOrEmpty(context.Category))
            {
                // Get category name from service (uses cache)
                var categoryName = await _categoryService.GetCategoryNameAsync(context.Category).ConfigureAwait(false);
                if (categoryName != null)
                {
                    context.CategoryName = categoryName;
                    workflow.Context = JsonHelper.Serialize(context);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to populate category name for workflow {workflow.Id}: {ex.Message}", "ModImportWorkflowHandler");
        }
    }

    /// <summary>
    /// Get compression settings from profile configuration
    /// Returns (format, level) tuple with defaults if config is unavailable
    /// </summary>
    private async Task<(ArchiveFormat format, CompressionLevel level)> GetCompressionSettingsAsync()
    {
        try
        {
            var profileId = _profileContext.ProfileId;
            if (string.IsNullOrEmpty(profileId))
            {
                _logger.Warn("Profile ID not available, using default compression settings");
                return (ArchiveFormat.SevenZip, CompressionLevel.High);
            }

            var config = await _profileService.GetProfileConfigurationAsync(profileId).ConfigureAwait(false);
            if (config?.ModImport == null)
            {
                _logger.Warn("Profile configuration not available, using default compression settings");
                return (ArchiveFormat.SevenZip, CompressionLevel.High);
            }

            // Map compression type from config
            var format = (config.ModImport.CompressionType?.ToLowerInvariant()) switch
            {
                "zip" => ArchiveFormat.Zip,
                "7z" => ArchiveFormat.SevenZip,
                "rar" => ArchiveFormat.SevenZip, // RAR write not supported, use 7z
                _ => ArchiveFormat.SevenZip
            };

            // Map compression mode from config
            var level = (config.ModImport.CompressionMode?.ToLowerInvariant()) switch
            {
                "fast" => CompressionLevel.Fast,
                "high" => CompressionLevel.High,
                "ultra" => CompressionLevel.Ultra,
                _ => CompressionLevel.High
            };

            _logger.Info($"Using compression settings from profile: format={format}, level={level}");
            return (format, level);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to load compression settings from profile config: {ex.Message}");
            return (ArchiveFormat.SevenZip, CompressionLevel.High);
        }
    }
}
