namespace D3dxSkinManager.Modules.Plugins.Models;

/// <summary>
/// Plugin information model for IPC communication
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// Unique plugin identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Plugin display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Plugin description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Plugin author
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Whether the plugin is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// List of plugin capabilities (MessageHandler, ServiceProvider, etc.)
    /// </summary>
    public List<string> Capabilities { get; set; } = new();
}
