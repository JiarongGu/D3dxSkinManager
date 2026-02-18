using D3dxSkinManager.Facades;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Plugins;

/// <summary>
/// Facade interface for plugin management operations.
/// Routes plugin-related IPC messages to appropriate services.
/// </summary>
public interface IPluginsFacade : IModuleFacade
{
    // Inherits HandleMessageAsync from IModuleFacade
}
