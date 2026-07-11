using System.IO;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Constants;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Workflow.Repositories;
using D3dxSkinManager.Modules.Workflow.Entities;
using D3dxSkinManager.Modules.Workflow.Handlers;

namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>One entry found in a profile's temp dir (for orphan selection).</summary>
public readonly record struct TempEntry(string Path, string Name, bool IsDirectory);

/// <summary>
/// Resumes workflows left Pending/Processing by a crash/close AND sweeps orphaned import temp files.
/// Wired into profile init (ProfileServiceRouter) so it runs backend-side automatically — not only
/// when the import screen mounts. Idempotent: the handler skips a workflow already running in-process.
/// </summary>
public class WorkflowResumeService : IWorkflowResumeService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IEnumerable<IWorkflowHandler> _handlers;
    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;

    public WorkflowResumeService(
        IWorkflowRepository workflowRepository,
        IEnumerable<IWorkflowHandler> handlers,
        IProfilePathService profilePaths,
        ILogHelper logger)
    {
        _workflowRepository = workflowRepository;
        _handlers = handlers;
        _profilePaths = profilePaths;
        _logger = logger;
    }

    public async Task ResumeAllWorkflowsAsync()
    {
        try
        {
            // Sweep crash-leftover temp FIRST (keeps active workflows' compress temp), then resume.
            await CleanupOrphanedImportTempAsync().ConfigureAwait(false);

            _logger.Info("Resuming all pending/processing workflows...");

            // Get all workflow types
            var handlerDict = _handlers.ToDictionary(h => h.WorkflowType, h => h);

            foreach (var workflowType in handlerDict.Keys)
            {
                // Get all active workflows (Pending, Processing)
                // Note: WaitingForInput workflows should not auto-resume as they need user action
                var workflows = await _workflowRepository.GetActiveByTypeAsync(workflowType);

                var pendingOrProcessing = workflows
                    .Where(w => w.Status == WorkflowStatus.Pending || w.Status == WorkflowStatus.Processing)
                    .ToList();

                if (pendingOrProcessing.Count == 0)
                    continue;

                _logger.Info($"Found {pendingOrProcessing.Count} {workflowType} workflow(s) to resume");

                var handler = handlerDict[workflowType];

                foreach (var workflow in pendingOrProcessing)
                {
                    try
                    {
                        _logger.Info($"Resuming workflow {workflow.Id} (Type: {workflow.Type}, Status: {workflow.Status})");

                        // Resume workflow from current step
                        // This will trigger async processing with concurrency control
                        await handler.ResumeFromCurrentStepAsync(workflow.Id);
                        _logger.Info($"Successfully triggered resume for workflow {workflow.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Failed to resume workflow {workflow.Id}: {ex.Message}", "WorkflowResumeService", ex);

                        // Mark as failed
                        workflow.Status = WorkflowStatus.Failed;
                        workflow.ErrorMessage = $"Failed to resume: {ex.Message}";
                        workflow.CompletedAt = DateTime.UtcNow;
                        await _workflowRepository.UpdateAsync(workflow);
                    }
                }
            }

            _logger.Info("Workflow resume completed");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to resume workflows: {ex.Message}", "WorkflowResumeService", ex);
        }
    }

    /// <summary>
    /// Delete crash-leftover temp from {profile}/temp: import compress temps (.mic) whose workflow is
    /// no longer ACTIVE, all archive-update temps (.auc), and remote-import staging dirs (remote-*).
    /// KEEPS .mic belonging to an active workflow so a resumed import still finds its archive.
    /// </summary>
    public async Task CleanupOrphanedImportTempAsync()
    {
        try
        {
            var tempDir = _profilePaths.TempDirectory;
            if (!Directory.Exists(tempDir)) return;

            // Active workflow ids across all types (only these should retain their compress temp).
            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in _handlers.Select(h => h.WorkflowType).Distinct())
                foreach (var wf in await _workflowRepository.GetActiveByTypeAsync(type).ConfigureAwait(false))
                    active.Add(wf.Id);

            var entries = Directory.EnumerateFileSystemEntries(tempDir)
                .Select(p => new TempEntry(p, Path.GetFileName(p), Directory.Exists(p)));

            var orphans = SelectOrphanTempEntries(entries, active);
            foreach (var path in orphans)
            {
                try
                {
                    if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                    else if (File.Exists(path)) File.Delete(path);
                    _logger.Info($"Cleaned orphaned import temp: {path}");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to delete orphaned temp {path}: {ex.Message}");
                }
            }
            if (orphans.Count > 0)
                _logger.Info($"Cleaned {orphans.Count} orphaned import temp entr(ies)");
        }
        catch (Exception ex)
        {
            _logger.Error($"Import temp cleanup failed: {ex.Message}", "WorkflowResumeService", ex);
        }
    }

    /// <summary>Pure orphan-selection (unit-tested): which temp entries are safe to delete given the
    /// set of active workflow ids. .mic for an active workflow is retained; everything else transient.</summary>
    public static List<string> SelectOrphanTempEntries(IEnumerable<TempEntry> entries, ISet<string> activeWorkflowIds)
    {
        var toDelete = new List<string>();
        foreach (var e in entries)
        {
            if (e.IsDirectory)
            {
                // Remote-import staging is fire-and-forget (not a DB workflow) → always orphaned on restart.
                if (e.Name.StartsWith("remote-", StringComparison.OrdinalIgnoreCase))
                    toDelete.Add(e.Path);
            }
            else if (TempFileConstants.IsModImportCompressTemp(e.Name))
            {
                var workflowId = Path.GetFileNameWithoutExtension(e.Name);
                if (!activeWorkflowIds.Contains(workflowId))
                    toDelete.Add(e.Path); // no active workflow owns it → orphan
            }
            else if (TempFileConstants.IsArchiveUpdateCompressTemp(e.Name))
            {
                // Archive-update temp is transient (a fast planner step); none legitimately active at startup.
                toDelete.Add(e.Path);
            }
        }
        return toDelete;
    }

    public async Task PauseAllWorkflowsAsync()
    {
        try
        {
            _logger.Info("Pausing all running workflows for shutdown...");

            var handlerDict = _handlers.ToDictionary(h => h.WorkflowType, h => h);

            foreach (var workflowType in handlerDict.Keys)
            {
                var workflows = await _workflowRepository.GetActiveByTypeAsync(workflowType);

                var processing = workflows
                    .Where(w => w.Status == WorkflowStatus.Processing)
                    .ToList();

                if (processing.Count == 0)
                    continue;

                _logger.Info($"Pausing {processing.Count} {workflowType} workflow(s)");

                foreach (var workflow in processing)
                {
                    try
                    {
                        // Set to Pending so it can be resumed later via UI
                        workflow.Status = WorkflowStatus.Pending;
                        await _workflowRepository.UpdateAsync(workflow);

                        _logger.Verbose($"Paused workflow {workflow.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Failed to pause workflow {workflow.Id}: {ex.Message}", "WorkflowResumeService", ex);
                    }
                }
            }

            _logger.Info("All workflows paused");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to pause workflows: {ex.Message}", "WorkflowResumeService", ex);
        }
    }
}
