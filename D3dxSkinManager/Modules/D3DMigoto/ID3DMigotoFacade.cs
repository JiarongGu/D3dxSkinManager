using System.Collections.Generic;
using D3dxSkinManager.Facades;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.D3DMigoto.Models;

namespace D3dxSkinManager.Modules.D3DMigoto;

/// <summary>
/// Interface for 3DMigoto Management facade
/// Handles: D3DMIGOTO_GET_VERSIONS, D3DMIGOTO_DEPLOY, etc.
/// Prefix: D3DMIGOTO_*
/// </summary>
public interface ID3DMigotoFacade : IModuleFacade
{

    Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync();
    Task<string?> GetCurrentVersionAsync();
    Task<DeploymentResult> DeployVersionAsync(string versionName);
    Task<bool> LaunchAsync();
}
