using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.TaskQueue.Models;

namespace D3dxSkinManager.Modules.TaskQueue.Services;

/// <summary>
/// Progress reporter that emits task progress events
/// </summary>
public class EventProgressReporter : IProgressReporter
{
    private readonly string _taskId;
    private readonly IEventEmitter _eventEmitter;
    private readonly ILogHelper _logger;
    private readonly Action<int, string?> _onProgress;

    public EventProgressReporter(
        string taskId,
        IEventEmitter eventEmitter,
        ILogHelper logger,
        Action<int, string?> onProgress)
    {
        _taskId = taskId;
        _eventEmitter = eventEmitter;
        _logger = logger;
        _onProgress = onProgress;
    }

    public bool IsCancelled { get; private set; }

    public async Task ReportProgressAsync(int percentComplete, string? currentStep = null)
    {
        // Update task state
        _onProgress(percentComplete, currentStep);

        // Create progress event
        var progress = new TaskProgress
        {
            TaskId = _taskId,
            Progress = percentComplete,
            CurrentStep = currentStep,
            Message = currentStep
        };

        // Emit progress event
        await _eventEmitter.EmitAsync(
            ModuleNames.TASK_QUEUE,
            TaskQueueEvents.PROGRESS,
            progress
        ).ConfigureAwait(false);

        _logger.Debug(
            $"Task {_taskId} progress: {percentComplete}% - {currentStep}",
            "EventProgressReporter"
        );
    }

    public async Task ReportCompletionAsync()
    {
        await ReportProgressAsync(100, "Completed").ConfigureAwait(false);
    }

    public Task ReportFailureAsync(string errorMessage)
    {
        _logger.Error(
            $"Task {_taskId} failed: {errorMessage}",
            "EventProgressReporter"
        );
        return Task.CompletedTask;
    }

    public Task ReportCancellationAsync()
    {
        IsCancelled = true;
        _logger.Warn($"Task {_taskId} cancelled", "EventProgressReporter");
        return Task.CompletedTask;
    }
}
