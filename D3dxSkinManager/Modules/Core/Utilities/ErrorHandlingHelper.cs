using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Helper class for standardized error handling across services
/// Eliminates repetitive try-catch blocks with logging
/// </summary>
public static class ErrorHandlingHelper
{
    /// <summary>
    /// Execute async operation with standardized error handling and OperationException wrapping
    /// Use this when you want exceptions to be logged and wrapped in OperationException
    /// </summary>
    public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
        Func<Task<T>> operation,
        ILogHelper logger,
        string operationName,
        string moduleName,
        string errorCode = "UNKNOWN_ERROR",
        Dictionary<string, string>? errorParameters = null)
    {
        try
        {
            return await operation();
        }
        catch (OperationException opEx)
        {
            logger.Error($"Operation '{operationName}' failed: [{opEx.Code}] {opEx.Message}", moduleName, opEx);
            throw;
        }
        catch (Exception ex)
        {
            logger.Error($"Unexpected error in '{operationName}': {ex.Message}", moduleName, ex);
            throw new OperationException(errorCode, errorParameters, $"Failed to {operationName}", ex);
        }
    }

    /// <summary>
    /// Execute async operation with boolean return (no exception throwing)
    /// Returns true on success, false on failure. Logs all errors.
    /// </summary>
    public static async Task<bool> TryExecuteAsync(
        Func<Task> operation,
        ILogHelper logger,
        string operationName,
        string moduleName)
    {
        try
        {
            await operation();
            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"Operation '{operationName}' failed: {ex.Message}", moduleName, ex);
            return false;
        }
    }

    /// <summary>
    /// Execute async operation with result or default on error
    /// Returns result on success, defaultValue on failure. Logs all errors.
    /// </summary>
    public static async Task<T?> TryExecuteWithDefaultAsync<T>(
        Func<Task<T>> operation,
        ILogHelper logger,
        string operationName,
        string moduleName,
        T? defaultValue = default)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            logger.Error($"Operation '{operationName}' failed: {ex.Message}", moduleName, ex);
            return defaultValue;
        }
    }

    /// <summary>
    /// Execute synchronous operation with standardized error handling
    /// </summary>
    public static T ExecuteWithErrorHandling<T>(
        Func<T> operation,
        ILogHelper logger,
        string operationName,
        string moduleName,
        string errorCode = "UNKNOWN_ERROR",
        Dictionary<string, string>? errorParameters = null)
    {
        try
        {
            return operation();
        }
        catch (OperationException opEx)
        {
            logger.Error($"Operation '{operationName}' failed: [{opEx.Code}] {opEx.Message}", moduleName, opEx);
            throw;
        }
        catch (Exception ex)
        {
            logger.Error($"Unexpected error in '{operationName}': {ex.Message}", moduleName, ex);
            throw new OperationException(errorCode, errorParameters, $"Failed to {operationName}", ex);
        }
    }

    /// <summary>
    /// Execute synchronous operation with boolean return
    /// </summary>
    public static bool TryExecute(
        Action operation,
        ILogHelper logger,
        string operationName,
        string moduleName)
    {
        try
        {
            operation();
            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"Operation '{operationName}' failed: {ex.Message}", moduleName, ex);
            return false;
        }
    }
}
