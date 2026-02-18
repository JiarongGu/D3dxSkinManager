using System.Threading.Tasks;
using D3dxSkinManager.Facades;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Game;

/// <summary>
/// Interface for Game Launch facade
/// Handles: GAME_LAUNCH, GAME_GET_CONFIG, etc.
/// Prefix: GAME_*
/// </summary>
public interface IGameFacade : IModuleFacade
{

    Task<bool> LaunchGameAsync(string? customArgs = null);
    Task<bool> LaunchCustomProgramAsync(string programPath, string? arguments = null);
}
