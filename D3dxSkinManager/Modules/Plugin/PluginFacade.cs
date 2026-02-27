using System.Text.Json;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Models;
using D3dxSkinManager.Modules.Plugin.Services;

namespace D3dxSkinManager.Modules.Plugin;

/// <summary>
/// Facade interface for plugin management operations.
/// Routes plugin-related IPC messages to appropriate services.
/// </summary>
public interface IPluginFacade : IModuleFacade
{
    // Inherits HandleMessageAsync from IModuleFacade
}

/// <summary>
/// Facade for plugin management operations
/// Module: PLUGIN
/// Responsibility: Plugin listing and management
/// </summary>
public class PluginFacade : BaseFacade, IPluginFacade
{
    protected override string ModuleName => "PluginsFacade";

    private readonly IPluginRegistry _pluginRegistry;
    private readonly IPluginLoader _pluginLoader;
    private readonly IPayloadHelper _payloadHelper;

    public PluginFacade(
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
            "GET_ALL" => await GetAllPluginsAsync(),
            "INVOKE" => await InvokePluginHandlerAsync(request),
            "ENABLE" => await EnablePluginAsync(request),
            "DISABLE" => await DisablePluginAsync(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    /// <summary>
    /// Invoke a specific plugin's message handler.
    /// Payload format: { pluginId: "com.example.plugin", messageType: "OPEN_UI", payload: {...} }
    /// </summary>
    private async Task<object?> InvokePluginHandlerAsync(IpcRequest request)
    {
        var pluginId = _payloadHelper.GetRequiredValue<string>(request.Payload, "pluginId");
        var messageType = _payloadHelper.GetRequiredValue<string>(request.Payload, "messageType");
        var pluginPayload = _payloadHelper.GetOptionalValue<object>(request.Payload, "payload");

        // Get the plugin
        var plugin = _pluginRegistry.GetPlugin(pluginId);
        if (plugin == null)
        {
            throw new InvalidOperationException($"Plugin not found: {pluginId}");
        }

        // Check if plugin can handle this message type
        if (!plugin.GetHandledMessageTypes().Contains(messageType))
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' does not handle message type: {messageType}");
        }

        // Create a sub-request for the plugin
        var pluginRequest = new IpcRequest
        {
            Id = request.Id,
            Type = messageType,
            Module = "PLUGIN",
            ProfileId = request.ProfileId,
            Payload = pluginPayload != null ? JsonSerializer.SerializeToElement(pluginPayload) : null,
            Timestamp = request.Timestamp
        };

        // Invoke the plugin handler
        var response = await plugin.HandleMessageAsync(pluginRequest);

        // Return the plugin's response data
        return response.Success ? response.Data : throw new InvalidOperationException(response.Error ?? "Plugin returned error");
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

        // Check if plugin handles messages
        if (plugin.GetHandledMessageTypes().Any())
        {
            capabilities.Add("MessageHandler");
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
