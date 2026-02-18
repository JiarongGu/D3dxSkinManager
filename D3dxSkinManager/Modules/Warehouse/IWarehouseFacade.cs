using System.Collections.Generic;
using D3dxSkinManager.Facades;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Warehouse;

/// <summary>
/// Interface for Mod Warehouse facade
/// Handles: WAREHOUSE_SEARCH, WAREHOUSE_DOWNLOAD, etc.
/// Prefix: WAREHOUSE_*
/// </summary>
public interface IWarehouseFacade : IModuleFacade
{

    // TODO: Define warehouse operations when feature is implemented
    // Task<List<WarehouseItem>> SearchWarehouseAsync(string query);
    // Task<bool> DownloadModAsync(string modId);
}
