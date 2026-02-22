using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core
{

    /// <summary>
    /// Common interface for all module facades to enable polymorphic routing
    /// </summary>
    public interface IModuleFacade
    {
        Task<IpcResponse> HandleMessageAsync(IpcRequest request);
    }
}
