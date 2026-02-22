using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Plugins.Interfaces;
using D3dxSkinManager.Modules.Plugins.Models;
using D3dxSkinManager.Modules.Plugins.Services;

namespace D3dxSkinManager.Modules.Plugins;

/// <summary>
/// Facade interface for plugin management operations.
/// Routes plugin-related IPC messages to appropriate services.
/// </summary>
public interface IPluginsFacade : IModuleFacade
{
    // Inherits HandleMessageAsync from IModuleFacade
}

/// <summary>
/// Facade for plugin management operations
/// Responsibility: Plugin listing and management
/// IPC Prefix: PLUGINS_*
/// </summary>
public class PluginsFacade : BaseFacade, IPluginsFacade
{
    protected override string ModuleName => "PluginsFacade";

    private readonly IPluginRegistry _pluginRegistry;
    private readonly IPluginLoader _pluginLoader;
    private readonly IPayloadHelper _payloadHelper;

    public PluginsFacade(
        IPluginRegistry pluginRegistry,
        IPluginLoader pluginLoader,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _pluginRegistry = pluginRegistry;
        _pluginLoader = pluginLoader;
        _payloadHelper = payloadHelper;
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "PLUGINS_GET_ALL" => await GetAllPluginsAsync(),
            "PLUGINS_ENABLE" => await EnablePluginAsync(request),
            "PLUGINS_DISABLE" => await DisablePluginAsync(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    public async Task<List<PluginInfo>> GetAllPluginsAsync()
    {
        var plugins = _pluginRegistry.GetAllPlugins();

        var pluginInfos = plugins.Select(p => new PluginInfo
        {
            Id = p.Id,
            Name = p.Name,
            Version = p.Version,
            Description = p.Description,
            Author = p.Author,
            IsEnabled = true, // All loaded plugins are enabled
            Capabilities = GetPluginCapabilities(p)
        }).ToList();

        return await Task.FromResult(pluginInfos).ConfigureAwait(false);
    }

    public async Task<bool> EnablePluginAsync(string pluginId)
    {
        // TODO: Implement plugin enable/disable functionality
        // For now, return false to indicate the operation is not supported
        _logger.Warn($"Plugin enable requested for '{pluginId}' but this feature is not yet implemented", "Plugins");
        await Task.CompletedTask;
        return false;
    }

    public async Task<bool> DisablePluginAsync(string pluginId)
    {
        // TODO: Implement plugin enable/disable functionality
        // For now, return false to indicate the operation is not supported
        _logger.Warn($"Plugin disable requested for '{pluginId}' but this feature is not yet implemented", "Plugins");
        await Task.CompletedTask;
        return false;
    }

    private List<string> GetPluginCapabilities(IPlugin plugin)
    {
        var capabilities = new List<string>();

        if (plugin is IMessageHandlerPlugin)
        {
            capabilities.Add("MessageHandler");
        }

        if (plugin is IServicePlugin)
        {
            capabilities.Add("ServiceProvider");
        }

        return capabilities;
    }

    private async Task<bool> EnablePluginAsync(IpcRequest request)
    {
        var pluginId = _payloadHelper.GetRequiredValue<string>(request.Payload, "pluginId");
        return await EnablePluginAsync(pluginId).ConfigureAwait(false);
    }

    private async Task<bool> DisablePluginAsync(IpcRequest request)
    {
        var pluginId = _payloadHelper.GetRequiredValue<string>(request.Payload, "pluginId");
        return await DisablePluginAsync(pluginId).ConfigureAwait(false);
    }
}
