using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.D3DMigoto.Models;

namespace D3dxSkinManager.Modules.D3DMigoto.Services;

/// <summary>
/// Interface for 3DMigoto version management service
/// </summary>
public interface I3DMigotoService
{
    /// <summary>
    /// Get list of available 3DMigoto versions
    /// </summary>
    Task<List<D3DMigotoVersion>> GetAvailableVersionsAsync();

    /// <summary>
    /// Get the currently deployed version name
    /// </summary>
    Task<string?> GetCurrentVersionAsync();

    /// <summary>
    /// Deploy a 3DMigoto version to the work directory
    /// </summary>
    Task<DeploymentResult> DeployVersionAsync(string versionName);

    /// <summary>
    /// Launch 3DMigoto loader
    /// </summary>
    Task<bool> LaunchAsync();
}
