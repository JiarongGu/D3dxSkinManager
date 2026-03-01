using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Mod;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Workflow.Handlers;

/// <summary>
/// Handler for MOD_IMPORT workflow type
/// Manages the import of mods from folders through a simple 4-step process:
/// 1. ExtractMetadata (1% progress)
/// 2. CompressFolder (1-100% progress, driven by compression progress)
/// 3. Wait for user to update context with metadata (paused)
/// 4. ImportMod (100% progress + completed)
/// </summary>
public class ModImportWorkflowHandler : IWorkflowHandler
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IModImportService _modImportService;
    private readonly IModManagementService _modManagementService;
    private readonly IModFacade _modFacade;
    private readonly IProfilePathService _profilePathService;
    private readonly IArchiveHelper _archiveHelper;
    private readonly IFileHelper _fileHelper;
    private readonly IHashHelper _hashHelper;
    private readonly IEventBus _eventBus;
    private readonly ILogHelper _logger;

    public string WorkflowType => "MOD_IMPORT";

    public ModImportWorkflowHandler(
        IWorkflowRepository workflowRepository,
        IModImportService modImportService,
        IModManagementService modManagementService,
        IModFacade modFacade,
        IProfilePathService profilePathService,
        IArchiveHelper archiveHelper,
        IFileHelper fileHelper,
        IHashHelper hashHelper,
        IEventBus eventBus,
        ILogHelper logger)
    {
        _workflowRepository = workflowRepository;
        _modImportService = modImportService;
        _modManagementService = modManagementService;
        _modFacade = modFacade;
        _profilePathService = profilePathService;
        _archiveHelper = archiveHelper;
        _fileHelper = fileHelper;
        _hashHelper = hashHelper;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>
    /// Start a new mod import workflow
    /// Step 1: Extract metadata from folder/file
    /// </summary>
    public async Task<WorkflowInfo> StartImportAsync(string folderPath)
    {
        _logger.Info($"Starting mod import workflow for folder: {folderPath}");

        // Create workflow
        var workflow = new WorkflowInfo
        {
            Id = $"WF-{Guid.NewGuid()}",
            Type = WorkflowType,
            Status = WorkflowStatus.Processing,
            Context = JsonHelper.Serialize(new ModImportWorkflowContext
            {
                Step = ModImportWorkflowSteps.ExtractMetadata,
                FolderPath = folderPath
            }),
            CreatedAt = DateTime.UtcNow
        };

        await _workflowRepository.AddAsync(workflow);

        // Emit workflow created event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.CREATED, workflow);

        // Process step 1: extract metadata
        await ProcessStepAsync(workflow);

        return workflow;
    }

    /// <summary>
    /// Resume workflow (user confirmed metadata, continue to import)
    /// This is called after user updates context via UPDATE_WORKFLOW_CONTEXT
    /// </summary>
    public async Task<WorkflowInfo> ContinueAsync(string workflowId)
    {
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
        workflow.Status = WorkflowStatus.Processing;

        await _workflowRepository.UpdateAsync(workflow);

        // Emit status changed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        // Process import step
        await ProcessStepAsync(workflow);

        return workflow;
    }

    /// <summary>
    /// Cancel the workflow
    /// </summary>
    public async Task<WorkflowInfo> CancelAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context);

        // Clean up temp file if exists
        if (context?.TempArchivePath != null && _fileHelper.FileExists(context.TempArchivePath))
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

        workflow.Status = WorkflowStatus.Cancelled;
        workflow.CompletedAt = DateTime.UtcNow;
        await _workflowRepository.UpdateAsync(workflow);

        // Emit cancelled event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.CANCELLED, workflow);

        return workflow;
    }

    /// <summary>
    /// Pause a workflow (not supported for ModImport - workflows auto-pause at confirmation step)
    /// </summary>
    public async Task<WorkflowInfo> PauseAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        // ModImport workflows automatically pause at CompressFolder step
        // Manual pause is not supported
        throw new NotSupportedException("MOD_IMPORT workflows pause automatically at compression step. Use CANCEL to stop the workflow.");
    }

    /// <summary>
    /// Implementation of IWorkflowHandler.StartAsync
    /// Delegates to StartImportAsync with folderPath from initialData
    /// </summary>
    public async Task<WorkflowInfo> StartAsync(string initialData)
    {
        // initialData should be the folder path for ModImport workflow
        return await StartImportAsync(initialData);
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
    /// Internal state machine - processes the current step
    /// </summary>
    private async Task ProcessStepAsync(WorkflowInfo workflow)
    {
        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context)
            ?? throw new InvalidOperationException("Invalid workflow context");

        try
        {
            switch (context.Step)
            {
                case ModImportWorkflowSteps.ExtractMetadata:
                    await ExtractMetadataAsync(workflow, context);
                    break;

                case ModImportWorkflowSteps.CompressFolder:
                    await CompressFolderAsync(workflow, context);
                    break;

                case ModImportWorkflowSteps.ImportMod:
                    await ImportModAsync(workflow, context);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown step: {context.Step}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Workflow step failed: {ex.Message}", "ModImportWorkflowHandler", ex);
            workflow.Status = WorkflowStatus.Failed;
            workflow.ErrorMessage = ex.Message;
            workflow.CompletedAt = DateTime.UtcNow;
            await _workflowRepository.UpdateAsync(workflow);

            // Emit failed event
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.FAILED, workflow);
        }
    }

    /// <summary>
    /// Step 1: Extract metadata from folder/file
    /// </summary>
    private async Task ExtractMetadataAsync(WorkflowInfo workflow, ModImportWorkflowContext context)
    {
        if (string.IsNullOrEmpty(context.FolderPath))
            throw new InvalidOperationException("Folder path is required");

        var isArchive = _fileHelper.FileExists(context.FolderPath);
        var isDirectory = _fileHelper.DirectoryExists(context.FolderPath);

        if (!isArchive && !isDirectory)
            throw new InvalidOperationException($"Path not found or not supported: {context.FolderPath}");

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
        }
        else
        {
            // For folders
            folderName = Path.GetFileName(context.FolderPath.TrimEnd(Path.DirectorySeparatorChar));
            var files = _fileHelper.GetFiles(context.FolderPath, "*", SearchOption.AllDirectories);
            fileCount = files.Length;
        }

        _logger.Info($"Detected: {folderName} ({fileCount} files)");

        // Pre-fill metadata with detected values
        context.FolderName = folderName;
        context.FileCount = fileCount;
        context.Name = folderName;  // User can edit this
        context.Author = null;      // User should fill this
        context.Description = null; // User should fill this
        context.Category = null;    // User must select
        context.Tags = new List<string>();
        context.Grading = "G";
        context.Progress = 1;       // ExtractMetadata => 1% progress

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
            await ProcessStepAsync(workflow);
        }
        else
        {
            // Archive file - skip compression, pause for user input
            _logger.Info("Archive file detected, pausing for user metadata confirmation");
            workflow.Status = WorkflowStatus.WaitingForInput;
            await _workflowRepository.UpdateAsync(workflow);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);
        }
    }

    /// <summary>
    /// Step 2: Compress folder into temporary archive (only if source is folder)
    /// Progress ranges from 1% to 100% during compression
    /// </summary>
    private async Task CompressFolderAsync(WorkflowInfo workflow, ModImportWorkflowContext context)
    {
        if (string.IsNullOrEmpty(context.FolderPath))
            throw new InvalidOperationException("Folder path is required");

        if (!_fileHelper.DirectoryExists(context.FolderPath))
            throw new InvalidOperationException($"Folder not found: {context.FolderPath}");

        _logger.Info($"Compressing folder: {context.FolderPath}");

        // Create temp archive in profile's temp directory
        var tempPath = Path.Combine(_profilePathService.TempDirectory, $"{Guid.NewGuid()}.7z");

        // TODO: Add progress callback to CompressFolderAsync to emit progress events
        // For now, emit start of compression
        context.Progress = 10;
        workflow.Context = JsonHelper.Serialize(context);
        await _workflowRepository.UpdateAsync(workflow);
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.PROGRESS, new
        {
            WorkflowId = workflow.Id,
            Progress = context.Progress,
            Step = context.Step
        });

        await _archiveHelper.CompressFolderAsync(context.FolderPath, tempPath, ArchiveFormat.SevenZip);

        _logger.Info($"Created temp archive: {tempPath}");

        // Calculate SHA256 of the compressed file to detect duplicates
        var archiveSha = await _hashHelper.CalculateFileSHA256Async(tempPath);
        _logger.Info($"Archive SHA256: {archiveSha}");

        // Check if a mod with this SHA already exists
        var existingMod = await _modFacade.GetModByIdAsync(archiveSha);
        if (existingMod != null)
        {
            _logger.Info($"Duplicate mod detected: {archiveSha} (Name: {existingMod.Name})");

            // Delete the temp archive
            if (_fileHelper.FileExists(tempPath))
            {
                File.Delete(tempPath);
            }

            // Mark workflow as failed with duplicate error
            workflow.Status = WorkflowStatus.Failed;
            workflow.ErrorMessage = $"This mod already exists in your library: \"{existingMod.Name}\"";
            await _workflowRepository.UpdateAsync(workflow);

            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.FAILED, workflow);

            throw new InvalidOperationException($"Duplicate mod: {existingMod.Name} (SHA: {archiveSha})");
        }

        // Update context - compression done, SHA verified, wait for user confirmation
        context.TempArchivePath = tempPath;
        context.Progress = 100; // Compression complete, only confirmation step left

        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.WaitingForInput;
        await _workflowRepository.UpdateAsync(workflow);

        // Emit progress and status changed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.PROGRESS, new
        {
            WorkflowId = workflow.Id,
            Progress = context.Progress,
            Step = context.Step
        });
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        // Pause here - user needs to click Confirm button to continue
    }

    /// <summary>
    /// Step 3: Import mod with user-edited metadata
    /// Sets progress to 100% and marks workflow as completed
    /// </summary>
    private async Task ImportModAsync(WorkflowInfo workflow, ModImportWorkflowContext context)
    {
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
        var updateRequest = new UpdateModRequest
        {
            Name = context.Name,
            Author = context.Author,
            Description = context.Description,
            Category = context.Category,
            Grading = context.Grading ?? "G",
            Tags = context.Tags ?? new List<string>()
        };

        modInfo = await _modManagementService.UpdateModAsync(modInfo.SHA, updateRequest);

        _logger.Info($"Mod imported successfully: {modInfo.SHA}");

        // Clean up temp file (only if we created it during compression)
        var shouldDeleteTemp = context.TempArchivePath != context.FolderPath;
        if (shouldDeleteTemp)
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
        context.ImportedModSha = modInfo.SHA;
        context.Step = ModImportWorkflowSteps.ImportMod;
        context.Progress = 100; // Import complete

        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.Completed;
        workflow.CompletedAt = DateTime.UtcNow;
        await _workflowRepository.UpdateAsync(workflow);

        // Emit progress event (100%)
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.PROGRESS, new
        {
            WorkflowId = workflow.Id,
            Progress = context.Progress,
            Step = context.Step
        });

        // Emit completed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.COMPLETED, workflow);
    }
}
