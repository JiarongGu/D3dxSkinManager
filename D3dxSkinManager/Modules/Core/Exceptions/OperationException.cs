using D3dxSkinManager.Modules.Core.Utilities;

namespace D3dxSkinManager.Modules.Core.Exceptions;

/// <summary>
/// Unified exception for all operation errors with structured error information
/// Carries error code and parameters for i18n translation on frontend
/// Frontend uses pattern: errors.{errorCode} for i18n lookup
/// </summary>
public class OperationException : Exception
{
    /// <summary>
    /// Error code for i18n lookup (e.g., "MOD_DELETE_FAILED", "WORKFLOW_MI_DUPLICATE_MOD")
    /// Frontend uses this with pattern: errors.{Code}
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Optional parameters for error message interpolation
    /// Example: { "name": "MyMod", "sha": "abc123..." }
    /// Frontend uses these with i18n: t('errors.MOD_DELETE_FAILED', { name: 'MyMod' })
    /// Serializes as "parameters" in JSON due to camelCase naming policy
    /// </summary>
    public Dictionary<string, string>? Parameters { get; }

    /// <summary>
    /// Create an operation exception with error code and parameters
    /// </summary>
    /// <param name="code">Error code for i18n (e.g., "MOD_DELETE_FAILED")</param>
    /// <param name="parameters">Optional parameters for message interpolation</param>
    /// <param name="message">Optional fallback message for logging</param>
    /// <param name="innerException">Optional inner exception</param>
    public OperationException(
        string code,
        Dictionary<string, string>? parameters = null,
        string? message = null,
        Exception? innerException = null)
        : base(message ?? code, innerException)
    {
        Code = code;
        Parameters = parameters;
    }

    /// <summary>
    /// Create an operation exception with a single parameter
    /// </summary>
    public OperationException(string code, string paramKey, string paramValue, string? message = null)
        : this(code, new Dictionary<string, string> { { paramKey, paramValue } }, message)
    {
    }

    /// <summary>
    /// Get the structured error message as JSON
    /// Format: { "code": "ERROR_CODE", "parameters": {...} }
    /// Frontend parseError() function expects this format
    /// Uses JsonHelper to ensure camelCase naming policy
    /// </summary>
    public string GetStructuredMessage()
    {
        return JsonHelper.Serialize(new
        {
            Code,
            Parameters
        });
    }
}
