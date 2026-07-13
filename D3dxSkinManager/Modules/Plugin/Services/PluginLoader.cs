using System.Reflection;
using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Plugin.Interfaces;
using D3dxSkinManager.Modules.Plugin.Models;

namespace D3dxSkinManager.Modules.Plugin.Services;


public interface IPluginLoader
{
    Task<int> LoadPluginsAsync();

    Task InitPluginsAsync();

    /// <summary>Packs that are installed on disk but FAILED to load in the last <see cref="LoadPluginsAsync"/>
    /// — usually an SDK/contract mismatch after an app update. Empty when everything loaded. The loader is a
    /// per-profile singleton, so this stays valid for the facade to query after the initial load.</summary>
    IReadOnlyList<PluginLoadFailure> LoadFailures { get; }
}

/// <summary>
/// Loads plugins from .dll assemblies in the plugins directory.
/// </summary>
public class PluginLoader : IPluginLoader
{
    /// <summary>Staging area for pack UPDATES: a loaded assembly's dll is locked, so an update
    /// downloads into {plugins}/.pending/{packId} and is swapped into place at the next load, before
    /// anything is loaded (see <see cref="ApplyPendingUpdates"/>).</summary>
    public const string PendingDirName = ".pending";

    private readonly IProfilePathService _profilePaths;
    private readonly IPluginContext _pluginContext;
    private readonly IPluginRegistry _registry;
    private readonly IPluginStateStore _stateStore;
    private readonly ILogHelper _logger;

    // Rebuilt each LoadPluginsAsync — packs whose dll produced no usable plugin (contract mismatch / broken).
    private readonly List<PluginLoadFailure> _loadFailures = new();

    public IReadOnlyList<PluginLoadFailure> LoadFailures => _loadFailures;

    public PluginLoader(IProfilePathService profilePaths, IPluginContext pluginContext, IPluginRegistry registry, IPluginStateStore stateStore, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _pluginContext = pluginContext;
        _registry = registry;
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<int> LoadPluginsAsync()
    {
        _logger.Log(LogLevel.Info, $"Loading plugins from: {_profilePaths.PluginsDirectory}", "PluginLoader");
        _loadFailures.Clear();

        if (!Directory.Exists(_profilePaths.PluginsDirectory))
        {
            _logger.Log(LogLevel.Info, "Plugins directory does not exist. Creating it.", "PluginLoader");
            Directory.CreateDirectory(_profilePaths.PluginsDirectory);
            return 0;
        }

        // Apply staged pack updates BEFORE loading — nothing is loaded yet, so the (otherwise locked)
        // dll can be swapped into place. This is how a plugin update takes effect.
        ApplyPendingUpdates();

        var loadedCount = 0;
        var pendingRoot = Path.Combine(_profilePaths.PluginsDirectory, PendingDirName);
        var dllFiles = Directory.GetFiles(_profilePaths.PluginsDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(pendingRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        foreach (var dllFile in dllFiles)
        {
            try
            {
                if (await LoadPluginFromAssemblyAsync(dllFile))
                    loadedCount++;
            }
            catch (BadImageFormatException)
            {
                // NATIVE dlls riding in a plugin pack (e.g. onnxruntime.dll) — not assemblies, expected.
                _logger.Log(LogLevel.Debug, $"Skipped non-managed dll: {Path.GetFileName(dllFile)}", "PluginLoader");
            }
            catch (Exception ex)
            {
                // The whole dll threw (missing dependency, FileLoadException, …). The pack is installed but
                // unusable → surface it as a load failure so the UI can offer a re-download/update.
                _logger.Log(LogLevel.Error, $"Failed to load plugin from {dllFile}: {ex.Message}", "PluginLoader", ex);
                RecordLoadFailure(dllFile, ex.Message);
            }
        }

        _logger.Log(LogLevel.Info, $"Loaded {loadedCount} plugin(s)", "PluginLoader");
        return loadedCount;
    }

    /// <summary>Swap any staged pack updates ({plugins}/.pending/{packId}) into place. Called at the
    /// START of a load, before any assembly is loaded, so the live dll (locked once loaded) is free to
    /// replace. A failed swap leaves its staged dir for the next attempt; the empty staging root is
    /// removed when all applied.</summary>
    private void ApplyPendingUpdates()
    {
        var pendingRoot = Path.Combine(_profilePaths.PluginsDirectory, PendingDirName);
        if (!Directory.Exists(pendingRoot)) return;

        foreach (var staged in Directory.GetDirectories(pendingRoot))
        {
            var packId = Path.GetFileName(staged);
            try
            {
                var live = Path.Combine(_profilePaths.PluginsDirectory, packId);
                if (Directory.Exists(live)) Directory.Delete(live, recursive: true);
                Directory.Move(staged, live);
                _logger.Log(LogLevel.Info, $"Applied staged plugin update: {packId}", "PluginLoader");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Failed to apply staged plugin update '{packId}': {ex.Message}", "PluginLoader", ex);
            }
        }

        try { if (!Directory.EnumerateFileSystemEntries(pendingRoot).Any()) Directory.Delete(pendingRoot); }
        catch { /* leftover staging dir is harmless — excluded from the load scan */ }
    }

    /// <summary>The already-loaded assembly whose SIMPLE name matches <paramref name="simpleName"/>, or
    /// null. Dynamic/in-memory assemblies are never candidates (no disk backing). Pure + injectable so the
    /// reuse decision is unit-tested (<c>PluginLoaderTests</c>) without touching the real ALC.</summary>
    public static Assembly? FindAlreadyLoaded(string simpleName, IEnumerable<Assembly> loaded)
    {
        if (string.IsNullOrEmpty(simpleName)) return null;
        return loaded.FirstOrDefault(a =>
            !a.IsDynamic && string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
    }

    private Task<bool> LoadPluginFromAssemblyAsync(string assemblyPath)
    {
        _logger.Log(LogLevel.Debug, $"Loading assembly: {assemblyPath}", "PluginLoader");

        // Reuse an assembly of the same identity if it's ALREADY loaded (e.g. a second profile that also
        // has this pack installed): Assembly.LoadFrom would otherwise throw FileLoadException ("Assembly
        // with same name is already loaded") because the default ALC can't hold the same name twice from
        // different paths. Reading the manifest name (GetAssemblyName) doesn't load it into the ALC. The
        // id-based registry dedup then keeps the first plugin instance (see PluginRegistry.RegisterPlugin).
        var simpleName = AssemblyName.GetAssemblyName(assemblyPath).Name!;
        var assembly = FindAlreadyLoaded(simpleName, AppDomain.CurrentDomain.GetAssemblies());
        if (assembly != null)
            _logger.Log(LogLevel.Debug, $"Reusing already-loaded assembly '{simpleName}' for {Path.GetFileName(assemblyPath)}", "PluginLoader");
        else
            assembly = Assembly.LoadFrom(assemblyPath);
        Type[] allTypes;
        string? contractMismatchReason = null;
        try
        {
            allTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // The opaque "Unable to load one or more of the requested types" hides the real reason in
            // LoaderExceptions — typically a Core type/member the plugin references that this host build no
            // longer matches (contract DRIFT: common after an app update when Core is ahead of the last
            // plugin release; the pack needs a newer build). Surface it, then load whatever DID resolve
            // (best-effort). If NOTHING usable resolves, RecordLoadFailure below flags it as needs-update.
            var reasons = string.Join(" | ", (ex.LoaderExceptions ?? Array.Empty<Exception?>())
                .Where(e => e != null).Select(e => e!.Message).Distinct());
            _logger.Log(LogLevel.Error,
                $"Plugin '{Path.GetFileName(assemblyPath)}' type-load failed (Core contract mismatch?): {reasons}",
                "PluginLoader", ex);
            allTypes = ex.Types.Where(t => t != null).ToArray()!;
            contractMismatchReason = reasons;
        }
        var pluginTypes = allTypes
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        if (pluginTypes.Count == 0)
        {
            // A contract mismatch that resolved NO plugin type = the pack is present but unusable → flag it.
            // Otherwise this is just a non-plugin managed dll (a dependency) — not a failure.
            if (contractMismatchReason != null)
                RecordLoadFailure(assemblyPath, contractMismatchReason);
            else
                _logger.Log(LogLevel.Warn, $"No plugin types found in {Path.GetFileName(assemblyPath)}", "PluginLoader");
            return Task.FromResult(false);
        }

        var loaded = false;
        foreach (var pluginType in pluginTypes)
        {
            try
            {
                var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                if (plugin == null)
                {
                    _logger.Log(LogLevel.Error, $"Failed to create instance of {pluginType.Name}", "PluginLoader");
                    continue;
                }

                _registry.RegisterPlugin(plugin, enabled: !_stateStore.IsDisabled(plugin.Id));
                _logger.Log(LogLevel.Info, $"Loaded plugin: {plugin.Name} v{plugin.Version}", "PluginLoader");
                loaded = true;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Failed to load plugin {pluginType.Name}: {ex.Message}", "PluginLoader", ex);
            }
        }

        // A plugin type was present but NONE instantiated/registered → surface it as a load failure so the
        // UI can offer a re-download (a partial contract mismatch that still loaded a working plugin is not
        // flagged — it is in GET_ALL and works).
        if (!loaded)
            RecordLoadFailure(assemblyPath, contractMismatchReason ?? "Plugin type could not be instantiated");

        return Task.FromResult(loaded);
    }

    /// <summary>Record that a pack's dll failed to yield a usable plugin. The packId is the pack folder
    /// under {plugins} (the download/update key); a dll sitting directly in the plugins root falls back to
    /// its file name. De-duplicated by packId (one pack = one failure row).</summary>
    private void RecordLoadFailure(string dllPath, string reason)
    {
        var packId = PackIdFromPath(dllPath);
        if (_loadFailures.Any(f => string.Equals(f.PackId, packId, StringComparison.OrdinalIgnoreCase)))
            return;
        _loadFailures.Add(new PluginLoadFailure
        {
            PackId = packId,
            DllName = Path.GetFileName(dllPath),
            Reason = reason
        });
    }

    /// <summary>The pack folder name (first path segment under {plugins}) for a dll — the id used to
    /// download/update the pack. A dll directly in the plugins root has no pack folder → use its file name.</summary>
    private string PackIdFromPath(string dllPath)
    {
        var relative = Path.GetRelativePath(_profilePaths.PluginsDirectory, dllPath);
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return firstSegment.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(dllPath)
            : firstSegment;
    }

    public async Task InitPluginsAsync()
    {
        _logger.Log(LogLevel.Info, "Initializing plugins...", "PluginLoader");

        // Only ENABLED plugins run their init; a plugin enabled later gets its init then
        // (PluginFacade.ENABLE checks Initialized).
        var entries = _registry.GetAllEntries().Where(e => e.Enabled && !e.Initialized).ToList();
        var initTasks = entries.Select(async entry =>
        {
            try
            {
                _logger.Log(LogLevel.Debug, $"Initializing plugin: {entry.Plugin.Name}", "PluginLoader");
                await entry.Plugin.InitAsync(_pluginContext).ConfigureAwait(false);
                entry.Initialized = true;
                _logger.Log(LogLevel.Info, $"Initialized plugin: {entry.Plugin.Name}", "PluginLoader");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Failed to initialize plugin {entry.Plugin.Name}: {ex.Message}", "PluginLoader", ex);
            }
        });

        await Task.WhenAll(initTasks).ConfigureAwait(false);
        _logger.Log(LogLevel.Info, $"Initialized {entries.Count} plugin(s)", "PluginLoader");
    }

    public async Task DisposePluginsAsync()
    {
        _logger.Log(LogLevel.Info, "Shutting down plugins...", "PluginLoader");

        var plugins = _registry.GetAllPlugins().ToList();
        var shutdownTasks = plugins.Select(async plugin =>
        {
            try
            {
                _logger.Log(LogLevel.Debug, $"Shutting down plugin: {plugin.Name}", "PluginLoader");
                await plugin.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, $"Error shutting down plugin {plugin.Name}: {ex.Message}", "PluginLoader", ex);
            }
        });

        await Task.WhenAll(shutdownTasks).ConfigureAwait(false);
        _logger.Log(LogLevel.Info, "All plugins shut down", "PluginLoader");
    }
}
