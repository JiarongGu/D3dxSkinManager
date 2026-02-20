using System;

namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// Exception for mod operations with error codes
/// Frontend should handle these exceptions and display appropriate user messages
/// </summary>
public class ModException : Exception
{
    /// <summary>
    /// Error code for frontend error handling
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Additional context data for error handling
    /// </summary>
    public new object? Data { get; }

    public ModException(string errorCode, string message, object? data = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Data = data;
    }

    public ModException(string errorCode, string message, Exception innerException, object? data = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Data = data;
    }
}
