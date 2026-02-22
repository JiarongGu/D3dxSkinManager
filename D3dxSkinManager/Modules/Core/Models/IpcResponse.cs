using System.Text.Json.Serialization;

namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// Response structure for IPC messages
/// </summary>
public class IpcResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static IpcResponse CreateSuccess(string id, object? data = null)
    {
        return new IpcResponse
        {
            Id = id,
            Success = true,
            Data = data
        };
    }

    public static IpcResponse CreateError(string id, string error, object? data = null)
    {
        return new IpcResponse
        {
            Id = id,
            Success = false,
            Error = error,
            Data = data
        };
    }
}