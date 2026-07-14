using System.Text.Json;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;
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
    private readonly IPluginManagementService _pluginManagement;
    private readonly IPluginInstallService _installService;
    private readonly Context.Services.IProfilePathService _profilePaths;
    private readonly IPayloadHelper _payloadHelper;

    public PluginFacade(
        IPluginRegistry pluginRegistry,
        IPluginManagementService pluginManagement,
        IPluginInstallService installService,
        Context.Services.IProfilePathService profilePaths,
        IPayloadHelper payloadHelper,
        ILogHelper logger) : base(logger)
    {
        _pluginRegistry = pluginRegistry;
        _pluginManagement = pluginManagement;
        _installService = installService;
        _profilePaths = profilePaths;
        _payloadHelper = payloadHelper;
    }

    protected override async Task<object?> RouteMessageAsync(IpcRequest request)
    {
        return request.Type switch
        {
            "GET_ALL" => _pluginManagement.GetAllPlugins(),
            "GET_DIRECTORY" => GetDirectory(),
            "INVOKE" => await InvokePluginHandlerAsync(request),
            "ENABLE" => await SetEnabledAsync(request, enabled: true),
            "DISABLE" => await SetEnabledAsync(request, enabled: false),
            "GET_AVAILABLE_PACKS" => await _installService.GetAvailablePacksAsync(),
            "DOWNLOAD_PACK" => DownloadPackHandler(request),
            "CHECK_UPDATES" => await _installService.CheckUpdatesAsync(),
            "GET_LOAD_FAILURES" => await _installService.GetLoadFailuresAsync(),
            "GET_PENDING_UPDATES" => _installService.GetPendingUpdates(),
            _ => throw new InvalidOperationException($"Unknown message type: {request.Type}")
        };
    }

    /// <summary>The plugins directory, ENSURED to exist so "open folder" never fails. Profile init
    /// normally creates it, but a profile that predates the plugin system (or a partial migration) may
    /// lack it — create-on-demand keeps the opener robust.</summary>
    private object GetDirectory()
    {
        Directory.CreateDirectory(_profilePaths.PluginsDirectory);
        return new { path = _profilePaths.PluginsDirectory };
    }

    /// <summary>Fire-and-forget official pack install (download → extract → live load).</summary>
    private object DownloadPackHandler(IpcRequest request)
    {
        var packId = _payloadHelper.GetRequiredValue<string>(request.Payload, "packId");
        _installService.StartPackInstall(packId);
        return new { started = true };
    }

    /// <summary>Enable/disable is instant and persisted (the management service owns the init +
    /// registry-flip logic — the facade just parses the request and delegates).</summary>
    private async Task<object?> SetEnabledAsync(IpcRequest request, bool enabled)
    {
        var pluginId = _payloadHelper.GetRequiredValue<string>(request.Payload, "pluginId");
        await _pluginManagement.SetEnabledAsync(pluginId, enabled).ConfigureAwait(false);
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
}
