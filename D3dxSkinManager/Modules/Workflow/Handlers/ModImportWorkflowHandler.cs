using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Workflow.Models;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Mod.Services;
using D3dxSkinManager.Modules.Context.Services;

namespace D3dxSkinManager.Modules.Workflow.Handlers;

/// <summary>
/// Handler for MOD_IMPORT workflow type
/// Manages the import of mods from folders through a simple 3-step process
/// </summary>
public class ModImportWorkflowHandler
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IModImportService _modImportService;
    private readonly IModManagementService _modManagementService;
    private readonly IProfilePathService _profilePathService;
    private readonly IArchiveHelper _archiveHelper;
    private readonly IFileHelper _fileHelper;
    private readonly IEventBus _eventBus;
    private readonly ILogHelper _logger;

    public const string WorkflowType = "MOD_IMPORT";

    public ModImportWorkflowHandler(
        IWorkflowRepository workflowRepository,
        IModImportService modImportService,
        IModManagementService modManagementService,
        IProfilePathService profilePathService,
        IArchiveHelper archiveHelper,
        IFileHelper fileHelper,
        IEventBus eventBus,
        ILogHelper logger)
    {
        _workflowRepository = workflowRepository;
        _modImportService = modImportService;
        _modManagementService = modManagementService;
        _profilePathService = profilePathService;
        _archiveHelper = archiveHelper;
        _fileHelper = fileHelper;
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
    /// User confirms to continue the workflow
    /// Processes remaining steps (compress if needed, then import)
    /// </summary>
    public async Task<WorkflowInfo> ContinueAsync(string workflowId)
    {
        var workflow = await _workflowRepository.GetByIdAsync(workflowId);
        if (workflow == null)
            throw new InvalidOperationException($"Workflow not found: {workflowId}");

        var context = JsonHelper.Deserialize<ModImportWorkflowContext>(workflow.Context)
            ?? throw new InvalidOperationException("Invalid workflow context");

        if (context.Step != ModImportWorkflowSteps.WaitingForUserConfirmation)
            throw new InvalidOperationException($"Workflow is not waiting for confirmation. Current step: {context.Step}");

        _logger.Info($"User confirmed workflow {workflowId}, continuing...");

        // Check if already compressed (TempArchivePath exists) or if it's an archive file
        var alreadyCompressed = !string.IsNullOrEmpty(context.TempArchivePath);

        // Move directly to import step
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

                case ModImportWorkflowSteps.WaitingForUserConfirmation:
                    // Do nothing - waiting for user to edit metadata and confirm
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

        // Set to waiting for user confirmation BUT continue processing in background
        context.Step = ModImportWorkflowSteps.WaitingForUserConfirmation;
        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.WaitingForInput;
        await _workflowRepository.UpdateAsync(workflow);

        // Emit status changed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        // If it's a folder, start compressing in the background
        // User can edit metadata while compression happens
        if (!isArchive)
        {
            _logger.Info("Starting background compression while user reviews metadata");
            context.Step = ModImportWorkflowSteps.CompressFolder;
            workflow.Context = JsonHelper.Serialize(context);
            workflow.Status = WorkflowStatus.Processing;
            await _workflowRepository.UpdateAsync(workflow);
            await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

            // Continue to compression step
            await ProcessStepAsync(workflow);
        }
    }

    /// <summary>
    /// Step 2: Compress folder into temporary archive (only if source is folder)
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

        await _archiveHelper.CompressFolderAsync(context.FolderPath, tempPath, ArchiveFormat.SevenZip);

        _logger.Info($"Created temp archive: {tempPath}");

        // Update context - compression done, wait for user to confirm metadata
        context.TempArchivePath = tempPath;
        context.Step = ModImportWorkflowSteps.WaitingForUserConfirmation;

        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.WaitingForInput;
        await _workflowRepository.UpdateAsync(workflow);

        // Emit status changed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.STATUS_CHANGED, workflow);

        // Don't auto-continue - user needs to confirm metadata
    }

    /// <summary>
    /// Step 3: Import mod with user-edited metadata
    /// </summary>
    private async Task ImportModAsync(WorkflowInfo workflow, ModImportWorkflowContext context)
    {
        if (string.IsNullOrEmpty(context.TempArchivePath))
            throw new InvalidOperationException("Temp archive path is required");

        if (string.IsNullOrEmpty(context.Name))
            throw new InvalidOperationException("Mod name is required");

        if (string.IsNullOrEmpty(context.Category))
            throw new InvalidOperationException("Category is required");

        if (!_fileHelper.FileExists(context.TempArchivePath))
            throw new InvalidOperationException($"Temp archive not found: {context.TempArchivePath}");

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
            Grading = context.Grading,
            Tags = context.Tags
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
        context.Step = ModImportWorkflowSteps.Completed;

        workflow.Context = JsonHelper.Serialize(context);
        workflow.Status = WorkflowStatus.Completed;
        workflow.CompletedAt = DateTime.UtcNow;
        await _workflowRepository.UpdateAsync(workflow);

        // Emit completed event
        await _eventBus.EmitAsync(ModuleNames.WORKFLOW, WorkflowEvents.COMPLETED, workflow);
    }
}
