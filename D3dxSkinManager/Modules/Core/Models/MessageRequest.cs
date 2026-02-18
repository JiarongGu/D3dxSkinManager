using System.Text.Json;

namespace D3dxSkinManager.Modules.Core.Models;

/// <summary>
/// Represents an incoming IPC message from the frontend
/// </summary>
public class MessageRequest
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Module { get; set; }

    /// <summary>
    /// Profile ID for profile-specific operations
    /// If null, uses the currently active profile
    /// </summary>
    public string? ProfileId { get; set; }

    public JsonElement? Payload { get; set; }
}
