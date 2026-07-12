using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Modules.Plugin.Interfaces;

/// <summary>
/// Base interface for all plugins. Plugins can extend functionality without modifying core code.
/// Plugins subscribe to EventBus events and optionally handle IPC messages from the frontend.
/// </summary>
public interface IPlugin: IAsyncDisposable
{
    /// <summary>Unique plugin ID (e.g., "com.example.myplugin")</summary>
    string Id { get; }

    string Name { get; }
    string Version { get; }
    string Description { get; }
    string Author { get; }

    /// <summary>
    /// Initialize plugin with access to core services. Called once at startup.
    /// </summary>
    Task InitAsync(IPluginContext context);

    /// <summary>
    /// Get message types this plugin handles (e.g., "OPEN_UI", "EXPORT"). Return empty if none.
    /// </summary>
    IEnumerable<string> GetHandledMessageTypes();

    /// <summary>
    /// Handle IPC message from frontend. Called when frontend sends PLUGIN/INVOKE with this plugin's ID.
    /// </summary>
    Task<IpcResponse> HandleMessageAsync(IpcRequest request);
}
