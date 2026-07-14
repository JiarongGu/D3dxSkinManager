using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Models;

namespace D3dxSkinManager.Modules.Plugin.Services;

/// <summary>
/// Enable/disable a loaded plugin and list loaded plugins for the management UI.
/// Layer 2: owns the enable/disable business logic (lazy init + state persistence + registry flip)
/// that the facade must not carry.
/// </summary>
public interface IPluginManagementService
{
    /// <summary>
    /// Enable or disable a registered plugin. Enabling a never-initialized plugin runs its
    /// <c>InitAsync</c> first; disabling only hides it from consumers (no dispose — re-enable is cheap).
    /// </summary>
    Task SetEnabledAsync(string pluginId, bool enabled);

    /// <summary>All registered plugins with their runtime state, mapped for the management UI.</summary>
    List<PluginInfo> GetAllPlugins();
}

/// <summary>
/// Implementation of <see cref="IPluginManagementService"/>. Extracted from PluginFacade so the facade
/// stays a thin IPC router (review finding: plugin init + registry event-raising were living in the facade).
/// </summary>
public class PluginManagementService : IPluginManagementService
{
    private readonly IPluginRegistry _registry;
    private readonly IPluginContext _context;
    private readonly IPluginStateStore _stateStore;
    private readonly ILogHelper _logger;

    public PluginManagementService(
        IPluginRegistry registry,
        IPluginContext context,
        IPluginStateStore stateStore,
        ILogHelper logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SetEnabledAsync(string pluginId, bool enabled)
    {
        var entry = _registry.GetEntry(pluginId)
            ?? throw new OperationException("PLUGIN_NOT_FOUND", "pluginId", pluginId, $"Plugin not found: {pluginId}");

        _stateStore.SetDisabled(pluginId, !enabled);

        // Enabling a not-yet-initialized plugin: run its init BEFORE flipping enabled, so it is ready to
        // decide the instant consumers react to the enabled-change event (raised by SetEnabled below).
        if (enabled && !entry.Initialized)
        {
            await entry.Plugin.InitAsync(_context).ConfigureAwait(false);
            entry.Initialized = true;
        }

        // Flip enabled via the registry so it raises EnabledChanged — capability consumers (the content
        // veil) drop caches computed under the old active-plugin set (verdict logic flips plugin<->CV).
        _registry.SetEnabled(pluginId, enabled);

        _logger.Info($"Plugin '{pluginId}' {(enabled ? "enabled" : "disabled")}", "Plugins");
    }

    public List<PluginInfo> GetAllPlugins()
    {
        return _registry.GetAllEntries().Select(e => new PluginInfo
        {
            Id = e.Plugin.Id,
            Name = e.Plugin.Name,
            Version = e.Plugin.Version,
            Description = e.Plugin.Description,
            Author = e.Plugin.Author,
            IsEnabled = e.Enabled,
            Capabilities = GetPluginCapabilities(e.Plugin)
        }).ToList();
    }

    private static List<string> GetPluginCapabilities(IPlugin plugin)
    {
        var capabilities = new List<string>();

        if (plugin.GetHandledMessageTypes().Any())
        {
            capabilities.Add("MessageHandler");
        }

        if (plugin is IImageReviewPlugin)
        {
            capabilities.Add("ImageReview");
        }

        return capabilities;
    }
}
