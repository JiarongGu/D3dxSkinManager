using D3dxSkinManager.Modules.Context.Services;
using D3dxSkinManager.Modules.Core;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;

namespace D3dxSkinManager.Modules.Tool.Services;

/// <summary>
/// Watches the PROFILE's {profile}/fixtools directory so the fix-tool library stays in sync with the
/// folder: when the user drops in or removes a tool (a loose executable or a folder) on disk, a
/// FIX_TOOLS_CHANGED event is emitted and the UI re-scans. Mirrors ModCacheWatcher's pattern.
/// </summary>
public interface IFixToolsWatcher : IDisposable
{
    void StartWatching();
    void StopWatching();
}

public class FixToolsWatcher : IFixToolsWatcher
{
    private readonly IProfilePathService _profilePaths;
    private readonly IProfileEventBus _eventBus;
    private readonly ILogHelper _logger;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();
    private bool _isDisposed;

    public FixToolsWatcher(IProfilePathService profilePaths, IProfileEventBus eventBus, ILogHelper logger)
    {
        _profilePaths = profilePaths;
        _eventBus = eventBus;
        _logger = logger;
    }

    public void StartWatching()
    {
        lock (_lock)
        {
            if (_watcher != null) return;

            var dir = _profilePaths.FixToolsDirectory;
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); }
                catch (Exception ex) { _logger.Warn($"[FixToolsWatcher] Could not create {dir}: {ex.Message}", "FixToolsWatcher"); return; }
            }

            try
            {
                // Top-level loose executables AND folders are tools → watch both names.
                _watcher = new FileSystemWatcher(dir)
                {
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };
                _watcher.Created += OnChanged;
                _watcher.Deleted += OnChanged;
                _watcher.Renamed += OnRenamed;
                _logger.Info($"[FixToolsWatcher] Watching {dir}", "FixToolsWatcher");
            }
            catch (Exception ex)
            {
                _logger.Error($"[FixToolsWatcher] Failed to start: {ex.Message}", "FixToolsWatcher", ex);
                _watcher?.Dispose();
                _watcher = null;
            }
        }
    }

    public void StopWatching()
    {
        lock (_lock)
        {
            if (_watcher == null) return;
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Emit();
    private void OnRenamed(object sender, RenamedEventArgs e) => Emit();

    private void Emit()
    {
        // Fire-and-forget — never block the FileSystemWatcher thread.
        _ = Task.Run(async () =>
        {
            try { await _eventBus.EmitAsync(ModuleNames.TOOL, ToolEvents.FIX_TOOLS_CHANGED, new { }).ConfigureAwait(false); }
            catch (Exception ex) { _logger.Error($"[FixToolsWatcher] Emit failed: {ex.Message}", "FixToolsWatcher", ex); }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        StopWatching();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
