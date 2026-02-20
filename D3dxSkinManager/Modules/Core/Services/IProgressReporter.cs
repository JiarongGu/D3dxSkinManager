using System;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for reporting progress of long-running operations
/// Operations that support progress reporting should accept an IProgressReporter parameter
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Report progress update
    /// </summary>
    /// <param name="percentComplete">Progress percentage (0-100)</param>
    /// <param name="currentStep">Optional description of current step</param>
    Task ReportProgressAsync(int percentComplete, string? currentStep = null);

    /// <summary>
    /// Report task completion
    /// </summary>
    Task ReportCompletionAsync();

    /// <summary>
    /// Report task failure
    /// </summary>
    /// <param name="errorMessage">Error message</param>
    Task ReportFailureAsync(string errorMessage);

    /// <summary>
    /// Report task cancellation
    /// </summary>
    Task ReportCancellationAsync();

    /// <summary>
    /// Check if task has been cancelled
    /// Operations should periodically check this and abort if true
    /// </summary>
    bool IsCancelled { get; }
}

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
