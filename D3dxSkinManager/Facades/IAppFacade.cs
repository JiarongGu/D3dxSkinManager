using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Facades;

/// <summary>
/// Top-level application facade that routes IPC messages to appropriate module facades
/// </summary>
public interface IAppFacade
{
    /// <summary>
    /// Routes an incoming message request to the appropriate module facade
    /// </summary>
    /// <param name="request">The message request containing module, type, and payload</param>
    /// <returns>The response from the module facade</returns>
    Task<MessageResponse> HandleMessageAsync(MessageRequest request);
}
