using System.Text.Json;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Plugin.Services;

/// <summary>
/// Persists per-profile plugin state ({profile}/plugins/plugins.json — currently the DISABLED id
/// list). File-level install/removal of packs needs an app restart (loaded assemblies can't
/// unload); enable/disable is instant and survives restarts through this store.
/// </summary>
public interface IPluginStateStore
{
    bool IsDisabled(string pluginId);
    void SetDisabled(string pluginId, bool disabled);
}

public class PluginStateStore : IPluginStateStore
{
    private sealed class State
    {
        public List<string> Disabled { get; set; } = new();
    }

    private readonly IProfilePathService _profilePaths;
    private readonly ILogHelper _logger;
    private readonly object _lock = new();
    private State? _state;

    public PluginStateStore(IProfilePathService profilePaths, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _logger = logger;
    }

    private string FilePath => Path.Combine(_profilePaths.PluginsDirectory, "plugins.json");

    private State Load()
    {
        if (_state != null) return _state;
        try
        {
            _state = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<State>(File.ReadAllText(FilePath)) ?? new State()
                : new State();
        }
        catch (Exception ex)
        {
            _logger.Warn($"[PluginStateStore] Could not read {FilePath}: {ex.Message}", "PluginStateStore");
            _state = new State();
        }
        return _state;
    }

    public bool IsDisabled(string pluginId)
    {
        lock (_lock)
        {
            return Load().Disabled.Contains(pluginId, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SetDisabled(string pluginId, bool disabled)
    {
        lock (_lock)
        {
            var state = Load();
            var present = state.Disabled.Contains(pluginId, StringComparer.OrdinalIgnoreCase);
            if (disabled && !present) state.Disabled.Add(pluginId);
            if (!disabled && present) state.Disabled.RemoveAll(id => string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase));
            try
            {
                Directory.CreateDirectory(_profilePaths.PluginsDirectory);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[PluginStateStore] Could not write {FilePath}: {ex.Message}", "PluginStateStore");
            }
        }
    }
}
