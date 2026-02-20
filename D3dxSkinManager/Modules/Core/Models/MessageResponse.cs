namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// Represents an outgoing IPC response to the frontend
/// </summary>
public class MessageResponse
{
    public string Id { get; set; } = string.Empty;
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
    public object? ErrorDetails { get; set; }

    public static MessageResponse CreateSuccess(string id, object? data)
    {
        return new MessageResponse
        {
            Id = id,
            Success = true,
            Data = data
        };
    }

    public static MessageResponse CreateError(string id, string errorMessage, object? errorDetails = null)
    {
        return new MessageResponse
        {
            Id = id,
            Success = false,
            Error = errorMessage,
            ErrorDetails = errorDetails
        };
    }
}
