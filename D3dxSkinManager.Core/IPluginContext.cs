using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Plugin.Services;

/// <summary>
/// Provides plugins access to host services (EventBus, MessageDispatcher, logging, progress, data path).
/// Part of the plugin SDK; the implementation (PluginContext) lives in the host.
/// </summary>
public interface IPluginContext
{
    IMessageDispatcher MessageDispatcher { get; }
    IEventBus EventBus { get; }

    /// <summary>Plugin data directory — the folder the plugin's OWN DLL was loaded from (its install
    /// dir), so a pack is ONE folder (dll + any extracted natives together) rather than split across an
    /// install dir and a separate per-id data dir.</summary>
    string GetPluginDataPath(string pluginId);

    string RegisterEventHandler(string modulePattern, string typePattern, Func<EventMessage, Task> handler);
    void UnregisterEventHandler(string registrationId);

    /// <summary>NOTE: Plugin events are NOT currently used. Reserved for future cross-plugin communication.</summary>
    Task EmitEventAsync(string eventType, object? payload = null);

    /// <summary>Track a long-running plugin operation in the app's Activity panel / status bar.
    /// Returns a handle — wrap work in a <c>using</c> (auto-completes) or call Complete/Fail. The
    /// host owns the ProcessRegistry entry, so plugins never touch it (or ProcessType) directly.</summary>
    IPluginProgress ReportProgress(string title, bool cancellable = false);

    void Log(LogLevel level, string message, Exception? exception = null);
}
