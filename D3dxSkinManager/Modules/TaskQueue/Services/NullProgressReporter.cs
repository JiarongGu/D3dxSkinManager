namespace D3dxSkinManager.Modules.TaskQueue.Services;


/// <summary>
/// No-op implementation of IProgressReporter for operations that don't need progress tracking
/// </summary>
public class NullProgressReporter : IProgressReporter
{
    public static readonly NullProgressReporter Instance = new();

    private NullProgressReporter() { }

    public Task ReportProgressAsync(int percentComplete, string? currentStep = null) => Task.CompletedTask;
    public Task ReportCompletionAsync() => Task.CompletedTask;
    public Task ReportFailureAsync(string errorMessage) => Task.CompletedTask;
    public Task ReportCancellationAsync() => Task.CompletedTask;
    public bool IsCancelled => false;
}
