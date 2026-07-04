using System.Collections.Concurrent;
using System.Text.Json;
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
    /// <summary>Register + start a process. Returns its id (pass to Report/Complete/Fail/GetToken).
    /// Set <paramref name="resumable"/> if the op checkpoints itself and can resume after a crash;
    /// <paramref name="resumePayload"/> is the op-specific token the resume handler needs.
    /// Pass <paramref name="titleKey"/> (+ optional <paramref name="titleArg"/>, interpolated as
    /// {{arg}}) so the Activity panel renders the title in the UI language; <paramref name="title"/>
    /// stays the English fallback (also used in logs).</summary>
    string Start(ProcessType type, string title, bool cancellable = false, int? progress = null, bool resumable = false, string? resumePayload = null, string? titleKey = null, string? titleArg = null);

    /// <summary>Request resuming an interrupted+resumable process — emits PROCESS_RESUME_REQUESTED for
    /// the owning op to continue from its checkpoint, and drops the interrupted entry.</summary>
    void RequestResume(string id);

    /// <summary>Update progress (0–100, or null for indeterminate) and/or the detail line.
    /// Pass <paramref name="detailKey"/> for a localized stage line (detail stays the fallback).</summary>
    void Report(string id, int? progress = null, string? detail = null, string? detailKey = null);

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

    /// <summary>
    /// Startup self-cleanup: drop stale entries from a previous session — every finished process and
    /// every interrupted-but-NOT-resumable process. Keeps running (none at startup) and resumable
    /// interrupted entries (so the user can still resume them). Returns how many were removed.
    /// </summary>
    int PurgeStaleProcesses();
}

/// <summary>In-memory implementation of <see cref="IProcessRegistry"/>.</summary>
public class ProcessRegistry : IProcessRegistry
{
    private const int MaxHistory = 50; // cap finished entries kept for the Activity panel

    private readonly IEventBus _eventBus;
    private readonly ILogHelper _logger;
    private readonly string _stateFile;
    private readonly object _lock = new();
    private readonly Dictionary<string, ProcessInfo> _processes = new();
    private readonly Dictionary<string, CancellationTokenSource> _cts = new();
    private static readonly JsonSerializerOptions PersistOptions = new() { WriteIndented = false };

    public ProcessRegistry(IEventBus eventBus, ILogHelper logger, IGlobalPathService pathService)
    {
        _eventBus = eventBus;
        _logger = logger;
        _stateFile = Path.Combine(pathService.BaseDataPath, "process-state.json");
        LoadPersisted();
    }

    public string Start(ProcessType type, string title, bool cancellable = false, int? progress = null, bool resumable = false, string? resumePayload = null, string? titleKey = null, string? titleArg = null)
    {
        var info = new ProcessInfo
        {
            Type = type,
            Status = ProcessStatus.Running,
            Title = title,
            TitleKey = titleKey,
            TitleArg = titleArg,
            Progress = progress,
            Cancellable = cancellable,
            Resumable = resumable,
            ResumePayload = resumePayload,
        };
        lock (_lock)
        {
            _processes[info.Id] = info;
            if (cancellable) _cts[info.Id] = new CancellationTokenSource();
        }
        _logger.Verbose($"Process started: {type}/{title} ({info.Id})", "ProcessRegistry");
        Persist();
        EmitSnapshot();
        return info.Id;
    }

    public void Report(string id, int? progress = null, string? detail = null, string? detailKey = null)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(id, out var p) || p.Status != ProcessStatus.Running) return;
            if (progress.HasValue) p.Progress = Math.Clamp(progress.Value, 0, 100);
            if (detail != null)
            {
                p.Detail = detail;
                // A keyless detail (e.g. a "3/10" counter) must not keep a stale localized stage.
                p.DetailKey = detailKey;
            }
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

    public void RequestResume(string id)
    {
        ProcessInfo? p;
        lock (_lock)
        {
            if (!_processes.TryGetValue(id, out p)) return;
            if (p.Status != ProcessStatus.Interrupted || !p.Resumable) return;
            _processes.Remove(id); // the resumed op registers a fresh process when it restarts
            DisposeCts(id);
        }
        _logger.Info($"Resume requested for {p.Type} ({id})", "ProcessRegistry");
        // Owning op module (filtering by type) picks this up and continues from its checkpoint.
        _ = _eventBus.EmitAsync(ModuleNames.SYSTEM, SystemEvents.PROCESS_RESUME_REQUESTED,
            new { id, type = p.Type, resumePayload = p.ResumePayload });
        Persist();
        EmitSnapshot();
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
        Persist();
        EmitSnapshot();
    }

    public int PurgeStaleProcesses()
    {
        int removed;
        lock (_lock)
        {
            var stale = _processes.Values
                .Where(p => p.Status != ProcessStatus.Running &&
                            !(p.Status == ProcessStatus.Interrupted && p.Resumable))
                .Select(p => p.Id)
                .ToList();
            foreach (var id in stale)
            {
                _processes.Remove(id);
                DisposeCts(id);
            }
            removed = stale.Count;
        }
        if (removed > 0)
        {
            _logger.Info($"Purged {removed} stale process entr{(removed == 1 ? "y" : "ies")} on startup", "ProcessRegistry");
            Persist();
            EmitSnapshot();
        }
        return removed;
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
        Persist();
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

    // Persist the snapshot to an app-level file (best effort). Written on lifecycle transitions
    // (Start/Finish/Clear) — not on every Report — so a crash leaves the last known state on disk.
    private void Persist()
    {
        List<ProcessInfo> snapshot;
        lock (_lock) { snapshot = Snapshot(); }
        try
        {
            var dir = Path.GetDirectoryName(_stateFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_stateFile, JsonSerializer.Serialize(snapshot, PersistOptions));
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to persist process state: {ex.Message}", "ProcessRegistry");
        }
    }

    // On startup: load the persisted snapshot. Any process still Running/Queued was interrupted by an
    // app exit/crash → mark Interrupted so it's visible (with its last progress) instead of lost or
    // stuck "running". Resumable ops can then be continued from their own checkpoint.
    private void LoadPersisted()
    {
        try
        {
            if (!File.Exists(_stateFile)) return;
            var saved = JsonSerializer.Deserialize<List<ProcessInfo>>(File.ReadAllText(_stateFile), PersistOptions);
            if (saved == null || saved.Count == 0) return;

            lock (_lock)
            {
                foreach (var p in saved)
                {
                    if (p.Status == ProcessStatus.Running || p.Status == ProcessStatus.Queued)
                    {
                        p.Status = ProcessStatus.Interrupted;
                        p.FinishedAt ??= DateTime.UtcNow;
                    }
                    _processes[p.Id] = p;
                }
                PruneHistory();
            }
            _logger.Info($"Restored {saved.Count} process(es) from previous session", "ProcessRegistry");
            Persist();
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to load persisted process state: {ex.Message}", "ProcessRegistry");
        }
    }
}
