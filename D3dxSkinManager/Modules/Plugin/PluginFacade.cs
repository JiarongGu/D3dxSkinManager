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
    private readonly IPluginContext _pluginContext;
    private readonly IPluginStateStore _stateStore;
    private readonly IPluginInstallService _installService;
    private readonly Context.Services.IProfilePathService _profilePaths;
    private readonly IPayloadHelper _payloadHelper;

    public PluginFacade(
        IPluginRegistry pluginRegistry,
        IPluginLoader pluginLoader,
        IPluginContext pluginContext,
        IPluginStateStore stateStore,
        IPluginInstallService installService,
        Context.Services.IProfilePathService profilePaths,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _pluginRegistry = pluginRegistry;
        _pluginLoader = pluginLoader;
        _pluginContext = pluginContext;
        _stateStore = stateStore;
        _installService = installService;
        _profilePaths = profilePaths;
        _payloadHelper = payloadHelper;
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "GET_ALL" => await GetAllPluginsAsync(),
            "GET_DIRECTORY" => new { path = _profilePaths.PluginsDirectory },
            "INVOKE" => await InvokePluginHandlerAsync(request),
            "ENABLE" => await SetEnabledAsync(request, enabled: true),
            "DISABLE" => await SetEnabledAsync(request, enabled: false),
            "DOWNLOAD_PACK" => DownloadPackHandler(request),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    /// <summary>Fire-and-forget official pack install (download → extract → live load).</summary>
    private object DownloadPackHandler(IpcRequest request)
    {
        var packId = _payloadHelper.GetRequiredValue<string>(request.Payload, "packId");
        _installService.StartPackInstall(packId);
        return new { started = true };
    }

    /// <summary>Enable/disable is instant and persisted; enabling a never-initialized plugin runs
    /// its init now. Disable hides the plugin from consumers (no dispose — re-enable is cheap).</summary>
    private async Task<object?> SetEnabledAsync(IpcRequest request, bool enabled)
    {
        var pluginId = _payloadHelper.GetRequiredValue<string>(request.Payload, "pluginId");
        var entry = _pluginRegistry.GetEntry(pluginId)
            ?? throw new InvalidOperationException($"Plugin not found: {pluginId}");

        _stateStore.SetDisabled(pluginId, !enabled);

        // Enabling a not-yet-initialized plugin: run its init BEFORE flipping enabled, so it is ready to
        // decide the instant consumers react to the enabled-change event (below).
        if (enabled && !entry.Initialized)
        {
            await entry.Plugin.InitAsync(_pluginContext).ConfigureAwait(false);
            entry.Initialized = true;
        }

        // Flip enabled via the registry so it raises EnabledChanged — capability consumers (the content
        // veil) drop caches computed under the old active-plugin set (verdict logic flips plugin↔CV).
        _pluginRegistry.SetEnabled(pluginId, enabled);

        _logger.Info($"Plugin '{pluginId}' {(enabled ? "enabled" : "disabled")}", "Plugins");
        return new { success = true, enabled };
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
        var pluginInfos = _pluginRegistry.GetAllEntries().Select(e => new PluginInfo
        {
            Id = e.Plugin.Id,
            Name = e.Plugin.Name,
            Version = e.Plugin.Version,
            Description = e.Plugin.Description,
            Author = e.Plugin.Author,
            IsEnabled = e.Enabled,
            Capabilities = GetPluginCapabilities(e.Plugin)
        }).ToList();

        return await Task.FromResult(pluginInfos).ConfigureAwait(false);
    }

    private List<string> GetPluginCapabilities(IPlugin plugin)
    {
        var capabilities = new List<string>();

        // Check if plugin handles messages
        if (plugin.GetHandledMessageTypes().Any())
        {
            capabilities.Add("MessageHandler");
        }

        // Typed capability interfaces
        if (plugin is IImageReviewPlugin)
        {
            capabilities.Add("ImageReview");
        }

        return capabilities;
    }

}
