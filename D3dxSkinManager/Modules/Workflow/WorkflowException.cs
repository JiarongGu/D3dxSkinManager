namespace D3dxSkinManager.Modules.Workflow;

/// <summary>
/// Custom exception for workflow errors with structured error information
/// Carries error code and parameters for i18n translation
/// </summary>
public class WorkflowException : Exception
{
    /// <summary>
    /// Error code for i18n lookup (e.g., "WORKFLOW_MI_DUPLICATE_MOD")
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Optional parameters for error message interpolation
    /// </summary>
    public Dictionary<string, string>? Parameters { get; }

    /// <summary>
    /// Create a workflow exception with error code and parameters
    /// </summary>
    /// <param name="errorCode">Error code for i18n</param>
    /// <param name="parameters">Optional parameters for message interpolation</param>
    /// <param name="message">Optional fallback message for logging</param>
    /// <param name="innerException">Optional inner exception</param>
    public WorkflowException(
        string errorCode,
        Dictionary<string, string>? parameters = null,
        string? message = null,
        Exception? innerException = null)
        : base(message ?? errorCode, innerException)
    {
        ErrorCode = errorCode;
        Parameters = parameters;
    }

    /// <summary>
    /// Create a workflow exception with a single parameter
    /// </summary>
    public WorkflowException(string errorCode, string paramKey, string paramValue, string? message = null)
        : this(errorCode, new Dictionary<string, string> { { paramKey, paramValue } }, message)
    {
    }

    /// <summary>
    /// Get the structured error message as JSON
    /// </summary>
    public string GetStructuredErrorMessage()
    {
        return WorkflowErrorHelper.CreateErrorMessage(ErrorCode, Parameters);
    }
}
