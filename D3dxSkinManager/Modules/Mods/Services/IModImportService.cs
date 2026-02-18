using System.Threading.Tasks;
using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for mod import service
/// </summary>
public interface IModImportService
{
    Task<ModInfo?> ImportAsync(string filePath);
}
