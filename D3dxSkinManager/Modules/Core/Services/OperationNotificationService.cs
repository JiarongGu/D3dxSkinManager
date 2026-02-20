using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Interface for operation notification service
/// Manages active operations and emits notifications to frontend
/// </summary>
public interface IOperationNotificationService
{
    /// <summary>
    /// Create a new operation and return a progress reporter for it
    /// </summary>
    /// <param name="operationName">Human-readable operation name</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>Progress reporter for this operation</returns>
    IProgressReporter CreateOperation(string operationName, object? metadata = null);

    /// <summary>
    /// Cancel an operation by ID
    /// </summary>
    Task CancelOperationAsync(string operationId);

    /// <summary>
    /// Get all active operations
    /// </summary>
    OperationProgress[] GetActiveOperations();

    /// <summary>
    /// Event handler for operation notifications
    /// Frontend can subscribe to receive real-time updates
    /// </summary>
    event EventHandler<OperationNotification>? OperationNotificationReceived;
}

/// <summary>
/// Service for managing operation notifications and progress reporting
/// Coordinates between IProgressReporter instances and frontend notifications
/// </summary>
public class OperationNotificationService : IOperationNotificationService
{
    private readonly ConcurrentDictionary<string, OperationProgress> _activeOperations = new();
    private readonly ILogHelper _logger;

    public event EventHandler<OperationNotification>? OperationNotificationReceived;

    public OperationNotificationService(ILogHelper logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new operation and get a progress reporter for it
    /// </summary>
    public IProgressReporter CreateOperation(string operationName, object? metadata = null)
    {
        var operationId = $"op_{Guid.NewGuid():N}_{DateTime.UtcNow.Ticks}";
        var operation = new OperationProgress
        {
            OperationId = operationId,
            OperationName = operationName,
            Status = OperationStatus.Running,
            PercentComplete = 0,
            StartedAt = DateTime.UtcNow,
            Metadata = metadata
        };

        _activeOperations[operationId] = operation;

        // Emit operation started notification
        EmitNotification(OperationNotificationType.OperationStarted, operation);

        _logger.Info($"Operation started: {operationName} ({operationId})", "OperationNotificationService");

        return new OperationProgressReporter(operationId, this);
    }

    /// <summary>
    /// Cancel an operation by ID
    /// </summary>
    public Task CancelOperationAsync(string operationId)
    {
        if (_activeOperations.TryGetValue(operationId, out var operation))
        {
            operation.Status = OperationStatus.Cancelled;
            operation.CompletedAt = DateTime.UtcNow;

            EmitNotification(OperationNotificationType.OperationCancelled, operation);

            _activeOperations.TryRemove(operationId, out _);

            _logger.Info($"Operation cancelled: {operation.OperationName} ({operationId})", "OperationNotificationService");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Get all active operations
    /// </summary>
    public OperationProgress[] GetActiveOperations()
    {
        return _activeOperations.Values.ToArray();
    }

    /// <summary>
    /// Internal method to update operation progress
    /// Called by OperationProgressReporter
    /// </summary>
    internal Task UpdateProgressAsync(string operationId, int percentComplete, string? currentStep)
    {
        if (_activeOperations.TryGetValue(operationId, out var operation))
        {
            operation.PercentComplete = Math.Clamp(percentComplete, 0, 100);
            operation.CurrentStep = currentStep;

            EmitNotification(OperationNotificationType.ProgressUpdate, operation);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal method to mark operation as completed
    /// Called by OperationProgressReporter
    /// </summary>
    internal Task CompleteOperationAsync(string operationId)
    {
        if (_activeOperations.TryGetValue(operationId, out var operation))
        {
            operation.Status = OperationStatus.Completed;
            operation.PercentComplete = 100;
            operation.CompletedAt = DateTime.UtcNow;

            EmitNotification(OperationNotificationType.OperationCompleted, operation);

            _activeOperations.TryRemove(operationId, out _);

            _logger.Info($"Operation completed: {operation.OperationName} ({operationId}) in {(operation.CompletedAt.Value - operation.StartedAt).TotalSeconds:F2}s", "OperationNotificationService");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal method to mark operation as failed
    /// Called by OperationProgressReporter
    /// </summary>
    internal Task FailOperationAsync(string operationId, string errorMessage)
    {
        if (_activeOperations.TryGetValue(operationId, out var operation))
        {
            operation.Status = OperationStatus.Failed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.ErrorMessage = errorMessage;

            EmitNotification(OperationNotificationType.OperationFailed, operation);

            _activeOperations.TryRemove(operationId, out _);

            _logger.Error($"Operation failed: {operation.OperationName} ({operationId}): {errorMessage}", "OperationNotificationService");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal method to check if operation is cancelled
    /// Called by OperationProgressReporter
    /// </summary>
    internal bool IsOperationCancelled(string operationId)
    {
        return _activeOperations.TryGetValue(operationId, out var operation) && operation.Status == OperationStatus.Cancelled;
    }

    /// <summary>
    /// Emit an operation notification to all subscribers
    /// </summary>
    private void EmitNotification(OperationNotificationType type, OperationProgress operation)
    {
        var notification = new OperationNotification
        {
            Type = type,
            Operation = operation,
            Timestamp = DateTime.UtcNow
        };

        OperationNotificationReceived?.Invoke(this, notification);
    }

    /// <summary>
    /// Progress reporter implementation
    /// </summary>
    private class OperationProgressReporter : IProgressReporter
    {
        private readonly string _operationId;
        private readonly OperationNotificationService _service;

        public OperationProgressReporter(string operationId, OperationNotificationService service)
        {
            _operationId = operationId;
            _service = service;
        }

        public Task ReportProgressAsync(int percentComplete, string? currentStep = null)
        {
            return _service.UpdateProgressAsync(_operationId, percentComplete, currentStep);
        }

        public Task ReportCompletionAsync()
        {
            return _service.CompleteOperationAsync(_operationId);
        }

        public Task ReportFailureAsync(string errorMessage)
        {
            return _service.FailOperationAsync(_operationId, errorMessage);
        }

        public Task ReportCancellationAsync()
        {
            return _service.CancelOperationAsync(_operationId);
        }

        public bool IsCancelled => _service.IsOperationCancelled(_operationId);
    }
}
