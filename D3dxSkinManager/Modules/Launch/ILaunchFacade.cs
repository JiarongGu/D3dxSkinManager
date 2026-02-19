using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Launch.Models;

namespace D3dxSkinManager.Modules.Launch;

/// <summary>
/// Interface for launch operations facade
/// </summary>
public interface ILaunchFacade : IModuleFacade
{

    // 3DMigoto methods
    Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync();
    Task<string?> GetCurrentVersionAsync();
    Task<DeploymentResult> DeployVersionAsync(string versionName);
    Task<bool> Launch3DMigotoAsync();

    // Game methods
    Task<bool> LaunchGameAsync(string? customArgs = null);
    Task<bool> LaunchCustomProgramAsync(string programPath, string? arguments = null);
}
