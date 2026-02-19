using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules
{

    /// <summary>
    /// Common interface for all module facades to enable polymorphic routing
    /// </summary>
    public interface IModuleFacade
    {
        Task<MessageResponse> HandleMessageAsync(MessageRequest request);
    }

}
