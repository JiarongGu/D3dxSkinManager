using System.Collections.Concurrent;
using D3dxSkinManager.Modules.Core.Event;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Models;

namespace D3dxSkinManager.Modules.Core.Services;

/// <summary>
/// Authoritative registry of all long-running processes in the app (mod load/extract, import, preset
/// apply, batch ops, analysis, package, migration, cleanup, downloads, …). The single source of truth
/// for the status-bar summary + the Activity panel — like a download manager's transfer list.
///
/// Cross-cutting + app-level: emits a CONSOLIDATED snapshot via the GLOBAL IEventBus
/// (SYSTEM / PROCESS_LIST_UPDATED), which EventBusIpcBridge forwards to the frontend. Registered as a
/// Core singleton so any service (profile-scoped included) can inject it.
///
/// In-memory; completed/failed/cancelled entries are kept as bounded history. Persisting history to a
/// profile DB table (via the Fluent migration system) is a possible later enhancement.
/// </summary>
public interface IProcessRegistry
{
    /// <summary>Register + start a process. Returns its id (pass to Report/Complete/Fail/GetToken).</summary>
    string Start(ProcessType type, string title, bool cancellable = false, int? progress = null);

    /// <summary>Update progress (0–100, or null for indeterminate) and/or the detail line.</summary>
    void Report(string id, int? progress = null, string? detail = null);

    /// <summary>Mark a process completed.</summary>
    void Complete(string id);

    /// <summary>Mark a process failed with an error message.</summary>
    void Fail(string id, string error);

    /// <summary>Request cancellation: cancels the process's token and marks it Cancelled.</summary>
    void Cancel(string id);

    /// <summary>The cancellation token for a cancellable process (CancellationToken.None if unknown).</summary>
    CancellationToken GetToken(string id);

    /// <summary>Snapshot of all tracked processes (running first, then recent history).</summary>
    IReadOnlyList<ProcessInfo> GetAll();

    /// <summary>Remove all finished (completed/failed/cancelled) entries from the history.</summary>
    void ClearCompleted();
}

/// <summary>In-memory implementation of <see cref="IProcessRegistry"/>.</summary>
public class ProcessRegistry : IProcessRegistry
{
    private const int MaxHistory = 50; // cap finished entries kept for the Activity panel

    private readonly IEventBus _eventBus;
    private readonly ILogHelper _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, ProcessInfo> _processes = new();
    private readonly Dictionary<string, CancellationTokenSource> _cts = new();

    public ProcessRegistry(IEventBus eventBus, ILogHelper logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public string Start(ProcessType type, string title, bool cancellable = false, int? progress = null)
    {
        var info = new ProcessInfo
        {
            Type = type,
            Status = ProcessStatus.Running,
            Title = title,
            Progress = progress,
            Cancellable = cancellable,
        };
        lock (_lock)
        {
            _processes[info.Id] = info;
            if (cancellable) _cts[info.Id] = new CancellationTokenSource();
        }
        _logger.Verbose($"Process started: {type}/{title} ({info.Id})", "ProcessRegistry");
        EmitSnapshot();
        return info.Id;
    }

    public void Report(string id, int? progress = null, string? detail = null)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(id, out var p) || p.Status != ProcessStatus.Running) return;
            if (progress.HasValue) p.Progress = Math.Clamp(progress.Value, 0, 100);
            if (detail != null) p.Detail = detail;
        }
        EmitSnapshot();
    }

    public void Complete(string id) => Finish(id, ProcessStatus.Completed, null);

    public void Fail(string id, string error) => Finish(id, ProcessStatus.Failed, error);

    public void Cancel(string id)
    {
        lock (_lock)
        {
            if (_cts.TryGetValue(id, out var cts)) { try { cts.Cancel(); } catch { /* already disposed */ } }
        }
        Finish(id, ProcessStatus.Cancelled, null);
    }

    public CancellationToken GetToken(string id)
    {
        lock (_lock)
        {
            return _cts.TryGetValue(id, out var cts) ? cts.Token : CancellationToken.None;
        }
    }

    public IReadOnlyList<ProcessInfo> GetAll()
    {
        lock (_lock)
        {
            return Snapshot();
        }
    }

    public void ClearCompleted()
    {
        lock (_lock)
        {
            foreach (var id in _processes.Where(kv => kv.Value.Status != ProcessStatus.Running).Select(kv => kv.Key).ToList())
            {
                _processes.Remove(id);
                DisposeCts(id);
            }
        }
        EmitSnapshot();
    }

    private void Finish(string id, ProcessStatus status, string? error)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(id, out var p)) return;
            if (p.Status != ProcessStatus.Running) return; // terminal already
            p.Status = status;
            p.Error = error;
            p.FinishedAt = DateTime.UtcNow;
            if (status == ProcessStatus.Completed) p.Progress = 100;
            DisposeCts(id);
            PruneHistory();
        }
        if (status == ProcessStatus.Failed)
            _logger.Warn($"Process failed: {id} — {error}", "ProcessRegistry");
        EmitSnapshot();
    }

    // Caller holds _lock.
    private void DisposeCts(string id)
    {
        if (_cts.TryGetValue(id, out var cts)) { cts.Dispose(); _cts.Remove(id); }
    }

    // Caller holds _lock. Keep all running + the newest MaxHistory finished entries.
    private void PruneHistory()
    {
        var finished = _processes.Values.Where(p => p.Status != ProcessStatus.Running).ToList();
        if (finished.Count <= MaxHistory) return;
        foreach (var p in finished.OrderBy(p => p.FinishedAt ?? p.StartedAt).Take(finished.Count - MaxHistory))
            _processes.Remove(p.Id);
    }

    // Caller holds _lock. Running first (oldest first), then finished newest-first.
    private List<ProcessInfo> Snapshot()
    {
        var running = _processes.Values.Where(p => p.Status == ProcessStatus.Running).OrderBy(p => p.StartedAt);
        var finished = _processes.Values.Where(p => p.Status != ProcessStatus.Running).OrderByDescending(p => p.FinishedAt ?? p.StartedAt);
        return running.Concat(finished).ToList();
    }

    private void EmitSnapshot()
    {
        List<ProcessInfo> snapshot;
        lock (_lock) { snapshot = Snapshot(); }
        // Fire-and-forget: payload is the full snapshot, so a later emit simply supersedes an earlier
        // one (last-write-wins) — no ordering hazard. IpcHandler batches these every 50ms.
        _ = _eventBus.EmitAsync(ModuleNames.SYSTEM, SystemEvents.PROCESS_LIST_UPDATED, new { processes = snapshot });
    }
}
