using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Core.Services;
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
public class PluginsFacade : IPluginsFacade
{
    private readonly IPluginRegistry _pluginRegistry;
    private readonly IPluginLoader _pluginLoader;
    private readonly IPayloadHelper _payloadHelper;

    public PluginsFacade(
        IPluginRegistry pluginRegistry,
        IPluginLoader pluginLoader,
        IPayloadHelper payloadHelper)
    {
        _pluginRegistry = pluginRegistry;
        _pluginLoader = pluginLoader;
        _payloadHelper = payloadHelper;
    }

    public async Task<MessageResponse> HandleMessageAsync(MessageRequest request)
    {
        try
        {
            Console.WriteLine($"[PluginsFacade] Handling message: {request.Type}");

            object? responseData = request.Type switch
            {
                "PLUGINS_GET_ALL" => await GetAllPluginsAsync(),
                "PLUGINS_ENABLE" => await EnablePluginAsync(request),
                "PLUGINS_DISABLE" => await DisablePluginAsync(request),
                _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
            };

            return MessageResponse.CreateSuccess(request.Id, responseData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PluginsFacade] Error handling message: {ex.Message}");
            return MessageResponse.CreateError(request.Id, ex.Message);
        }
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

        return await Task.FromResult(pluginInfos);
    }

    public async Task<bool> EnablePluginAsync(string pluginId)
    {
        // TODO: Implement plugin enable/disable functionality
        await Task.CompletedTask;
        throw new NotImplementedException("Plugin enable/disable not yet implemented");
    }

    public async Task<bool> DisablePluginAsync(string pluginId)
    {
        // TODO: Implement plugin enable/disable functionality
        await Task.CompletedTask;
        throw new NotImplementedException("Plugin enable/disable not yet implemented");
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

    private async Task<bool> EnablePluginAsync(MessageRequest request)
    {
        var pluginId = _payloadHelper.GetRequiredValue<string>(request.Payload, "pluginId");
        return await EnablePluginAsync(pluginId);
    }

    private async Task<bool> DisablePluginAsync(MessageRequest request)
    {
        var pluginId = _payloadHelper.GetRequiredValue<string>(request.Payload, "pluginId");
        return await DisablePluginAsync(pluginId);
    }
}
