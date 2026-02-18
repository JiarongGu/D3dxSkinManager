using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Mods;
using D3dxSkinManager.Modules.D3DMigoto;
using D3dxSkinManager.Modules.Game;
using D3dxSkinManager.Modules.Tools;
using D3dxSkinManager.Modules.Settings;
using D3dxSkinManager.Modules.Plugins;
using D3dxSkinManager.Modules.Warehouse;
using D3dxSkinManager.Modules.Migration;
using D3dxSkinManager.Modules.Profiles;

namespace D3dxSkinManager.Facades;

/// <summary>
/// Top-level application facade that routes IPC messages to appropriate module facades
/// Supports both new module-based routing and legacy message type routing
/// </summary>
public class AppFacade : IAppFacade
{
    private readonly IModFacade _modFacade;
    private readonly ID3DMigotoFacade _d3dMigotoFacade;
    private readonly IGameFacade _gameFacade;
    private readonly IToolsFacade _toolsFacade;
    private readonly ISettingsFacade _settingsFacade;
    private readonly IPluginsFacade _pluginsFacade;
    private readonly IWarehouseFacade _warehouseFacade;
    private readonly IMigrationFacade _migrationFacade;
    private readonly IProfileFacade _profileFacade;

    public AppFacade(
        IModFacade modFacade,
        ID3DMigotoFacade d3dMigotoFacade,
        IGameFacade gameFacade,
        IToolsFacade toolsFacade,
        ISettingsFacade settingsFacade,
        IPluginsFacade pluginsFacade,
        IWarehouseFacade warehouseFacade,
        IMigrationFacade migrationFacade,
        IProfileFacade profileFacade)
    {
        _modFacade = modFacade;
        _d3dMigotoFacade = d3dMigotoFacade;
        _gameFacade = gameFacade;
        _toolsFacade = toolsFacade;
        _settingsFacade = settingsFacade;
        _pluginsFacade = pluginsFacade;
        _warehouseFacade = warehouseFacade;
        _migrationFacade = migrationFacade;
        _profileFacade = profileFacade;
    }

    /// <summary>
    /// Routes an incoming message request to the appropriate module facade
    /// Uses the Module field for explicit routing
    /// </summary>
    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        // Validate Module field is present
        if (string.IsNullOrEmpty(request.Module))
        {
            throw new InvalidOperationException(
                $"Module field is required for message routing. Message type: {request.Type}");
        }

        return await RouteByModule(request);
    }

    /// <summary>
    /// Routes a request to a facade based on the Module field
    /// </summary>
    private async Task<MessageResponse> RouteByModule(MessageRequest request)
    {
        var facade = GetFacadeByModuleName(request.Module!);

        if (facade == null)
        {
            throw new InvalidOperationException($"Unknown module: {request.Module}");
        }

        return await facade.HandleMessageAsync(request);
    }

    /// <summary>
    /// Gets the appropriate facade for a given module name
    /// </summary>
    private IModuleFacade? GetFacadeByModuleName(string moduleName)
    {
        return moduleName.ToUpperInvariant() switch
        {
            "MOD" or "MODS" => _modFacade,
            "D3DMIGOTO" => _d3dMigotoFacade,
            "GAME" => _gameFacade,
            "WAREHOUSE" => _warehouseFacade,
            "TOOLS" or "TOOL" => _toolsFacade,
            "PLUGINS" or "PLUGIN" => _pluginsFacade,
            "SETTINGS" or "SETTING" => _settingsFacade,
            "MIGRATION" or "MIGRATE" => _migrationFacade,
            "PROFILE" or "PROFILES" => _profileFacade,
            _ => null
        };
    }
}

/// <summary>
/// Common interface for all module facades to enable polymorphic routing
/// </summary>
public interface IModuleFacade
{
    Task<MessageResponse> HandleMessageAsync(MessageRequest request);
}
