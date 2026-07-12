using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Routes IPC messages to module facades. Part of the plugin SDK — plugins send messages into the host
/// through <c>IPluginContext.MessageDispatcher</c>. The implementation (MessageDispatcher, incl. the
/// middleware pipeline) lives in the host.
/// </summary>
public interface IMessageDispatcher
{
    Task<IpcResponse> SendAsync(string module, string type, string? profileId = null, object? payload = null);
    Task<T?> SendAsync<T>(string module, string type, string? profileId = null, object? payload = null);
}
