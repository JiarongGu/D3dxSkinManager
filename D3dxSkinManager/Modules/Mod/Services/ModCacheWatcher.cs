using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Mod.Services;

/// <summary>
/// Watches the mod cache directory for external changes (folder deletions)
/// Emits events when mod cache status changes so UI can reflect the actual state
/// </summary>
public interface IModCacheWatcher : IDisposable
{
    void StartWatching();
    void StopWatching();
}

public class ModCacheWatcher : IModCacheWatcher
{
    private readonly IProfilePathService _profilePaths;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();
    private bool _isDisposed;

    public ModCacheWatcher(
        IProfilePathService profilePaths,
        IProfileEventBus eventBus,
        ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _eventBus = eventBus;
        _logger = logger;
    }

    public void StartWatching()
    {
        lock (_lock)
        {
            if (_watcher != null)
            {
                _logger.Warn("ModCacheWatcher already started", "ModCacheWatcher");
                return;
            }

            var cacheDir = _profilePaths.CacheModsDirectory;
            _logger.Info($"Attempting to start ModCacheWatcher for directory: {cacheDir}", "ModCacheWatcher");

            if (!Directory.Exists(cacheDir))
            {
                _logger.Warn($"Cache directory does not exist, creating it: {cacheDir}", "ModCacheWatcher");
                try
                {
                    Directory.CreateDirectory(cacheDir);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to create cache directory: {ex.Message}", "ModCacheWatcher", ex);
                    return;
                }
            }

            try
            {
                _watcher = new FileSystemWatcher(cacheDir)
                {
                    NotifyFilter = NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                // Watch for folder deletions and renames
                _watcher.Deleted += OnCacheFolderDeleted;
                _watcher.Renamed += OnCacheFolderRenamed;

                _logger.Info($"âœ?ModCacheWatcher STARTED successfully watching: {cacheDir}", "ModCacheWatcher");
                _logger.Info($"âœ?Watching for: DirectoryName changes (Deleted, Renamed)", "ModCacheWatcher");
            }
            catch (Exception ex)
            {
                _logger.Error($"â?Failed to start ModCacheWatcher: {ex.Message}", "ModCacheWatcher", ex);
                _watcher?.Dispose();
                _watcher = null;
            }
        }
    }

    public void StopWatching()
    {
        lock (_lock)
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Deleted -= OnCacheFolderDeleted;
                _watcher.Renamed -= OnCacheFolderRenamed;
                _watcher.Dispose();
                _watcher = null;

                _logger.Info("Stopped watching mod cache directory", "ModCacheWatcher");
            }
        }
    }

    private void OnCacheFolderDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            var folderName = Path.GetFileName(e.FullPath);

            // Check if it's a mod folder (id) or disabled mod folder (DISABLED-SHA)
            if (string.IsNullOrEmpty(folderName))
                return;

            string? modSha = null;
            bool wasLoaded = false;

            if (folderName.StartsWith("DISABLED-"))
            {
                // Disabled cache folder deleted (DISABLED-{SHA})
                modSha = folderName.Substring("DISABLED-".Length);
                wasLoaded = false;
                _logger.Info($"Disabled cache folder deleted externally: {folderName}", "ModCacheWatcher");
            }
            else if (!folderName.Contains("-") && folderName.Length == 64) // SHA-256 is 64 chars
            {
                // Active mod folder deleted ({SHA})
                modSha = folderName;
                wasLoaded = true;
                _logger.Info($"Active mod cache folder deleted externally: {folderName}", "ModCacheWatcher");
            }

            if (!string.IsNullOrEmpty(modSha))
            {
                // Emit event so frontend can refresh mod status
                // Use fire-and-forget pattern (don't block FileSystemWatcher thread)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CACHE_CHANGED, new
                        {
                            Sha = modSha,
                            WasLoaded = wasLoaded,
                            ChangeType = "deleted"
                        }).ConfigureAwait(false);

                        _logger.Info($"Emitted CACHE_CHANGED event for mod {modSha}", "ModCacheWatcher");
                    }
                    catch (Exception emitEx)
                    {
                        _logger.Error($"Failed to emit CACHE_CHANGED event: {emitEx.Message}", "ModCacheWatcher", emitEx);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling cache folder deletion: {ex.Message}", "ModCacheWatcher", ex);
        }
    }

    private void OnCacheFolderRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            var oldName = Path.GetFileName(e.OldFullPath);
            var newName = Path.GetFileName(e.FullPath);

            _logger.Verbose($"Cache folder renamed: {oldName} -> {newName}", "ModCacheWatcher");

            // Detect load/unload via rename (DISABLED-{SHA} <-> {SHA})
            // This is already handled by our load/unload operations + events
            // But we emit cache changed event for consistency

            string? modSha = null;
            bool nowLoaded = false;

            if (oldName?.StartsWith("DISABLED-") == true && newName?.Length == 64)
            {
                // Renamed from DISABLED-{SHA} to {SHA} = loaded
                modSha = newName;
                nowLoaded = true;
            }
            else if (oldName?.Length == 64 && newName?.StartsWith("DISABLED-") == true)
            {
                // Renamed from {SHA} to DISABLED-{SHA} = unloaded
                modSha = oldName;
                nowLoaded = false;
            }

            if (!string.IsNullOrEmpty(modSha))
            {
                // Use fire-and-forget pattern (don't block FileSystemWatcher thread)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _eventBus.EmitAsync(ModuleNames.MOD, ModEvents.CACHE_CHANGED, new
                        {
                            Sha = modSha,
                            WasLoaded = !nowLoaded,
                            NowLoaded = nowLoaded,
                            ChangeType = "renamed"
                        }).ConfigureAwait(false);

                        _logger.Info($"Emitted CACHE_CHANGED event for mod {modSha} (renamed)", "ModCacheWatcher");
                    }
                    catch (Exception emitEx)
                    {
                        _logger.Error($"Failed to emit CACHE_CHANGED event: {emitEx.Message}", "ModCacheWatcher", emitEx);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error handling cache folder rename: {ex.Message}", "ModCacheWatcher", ex);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        StopWatching();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
