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
/// PURELY in-memory (2026-07-10): the old {data}/process-state.json snapshot is GONE. Finished
/// history was purged at startup anyway, and the only entries that mattered across restarts —
/// crash-interrupted RESUMABLE ops — have their real checkpoint in the PROFILE DB (e.g. analysis
/// sessions left "running"). The owning profile-scoped service re-announces those on profile init
/// via <see cref="IProcessRegistry.RegisterInterrupted"/>, so profile state lives in the profile DB
/// and nothing is duplicated into a global file.
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

    /// <summary>
    /// Announce a crash-INTERRUPTED resumable op from its profile-DB checkpoint (e.g. an analysis
    /// session still "running" after an app crash). Deduped by (type, resumePayload) — a profile
    /// switch re-announcing the same session must not stack entries. Returns the entry id.
    /// </summary>
    string RegisterInterrupted(ProcessType type, string title, string resumePayload,
        string? titleKey = null, string? titleArg = null, string? profileId = null, DateTime? startedAtUtc = null);

    /// <summary>Request resuming an interrupted+resumable process — emits PROCESS_RESUME_REQUESTED
    /// (carrying the entry's profileId) for the owning op to continue from its checkpoint, and drops
    /// the interrupted entry.</summary>
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
}

/// <summary>In-memory implementation of <see cref="IProcessRegistry"/>.</summary>
public class ProcessRegistry : IProcessRegistry
{
    private const int MaxHistory = 50; // cap finished entries kept for the Activity panel

    // Progress-driven snapshot emissions are THROTTLED (2026-07-10): the IPC batcher queues every
    // event WITHOUT coalescing, so a tight Report() loop used to ship hundreds of full snapshots to
    // the frontend per second. Report-driven emits now fire at most once per window, with a trailing
    // emit so the latest progress always lands; lifecycle transitions (Start/Finish/Clear/…) still
    // emit immediately.
    private const int SnapshotThrottleMs = 100;

    private readonly IEventBus _eventBus;
    private readonly ILogHelper _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, ProcessInfo> _processes = new();
    private readonly Dictionary<string, CancellationTokenSource> _cts = new();
    private readonly object _emitLock = new();
    private DateTime _lastEmitUtc = DateTime.MinValue;
    private bool _trailingEmitScheduled;

    public ProcessRegistry(IEventBus eventBus, ILogHelper logger)
    {
        _eventBus = eventBus;
        _logger = logger;
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
        EmitSnapshot();
        return info.Id;
    }

    public string RegisterInterrupted(ProcessType type, string title, string resumePayload,
        string? titleKey = null, string? titleArg = null, string? profileId = null, DateTime? startedAtUtc = null)
    {
        lock (_lock)
        {
            // A profile switch re-announces the same checkpoint — reuse the existing entry.
            var existing = _processes.Values.FirstOrDefault(p =>
                p.Status == ProcessStatus.Interrupted && p.Type == type && p.ResumePayload == resumePayload);
            if (existing != null) return existing.Id;

            var info = new ProcessInfo
            {
                Type = type,
                Status = ProcessStatus.Interrupted,
                Title = title,
                TitleKey = titleKey,
                TitleArg = titleArg,
                Resumable = true,
                ResumePayload = resumePayload,
                ProfileId = profileId,
                StartedAt = startedAtUtc ?? DateTime.UtcNow,
                FinishedAt = DateTime.UtcNow,
            };
            _processes[info.Id] = info;
            _logger.Info($"Interrupted {type} announced from its checkpoint ({resumePayload})", "ProcessRegistry");
            EmitSnapshot();
            return info.Id;
        }
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
        EmitSnapshot(immediate: false); // progress ticks are throttled; state above is already updated
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
        // profileId rides along so the resume targets the OWNING profile, not the selected one.
        _ = _eventBus.EmitAsync(ModuleNames.SYSTEM, SystemEvents.PROCESS_RESUME_REQUESTED,
            new { id, type = p.Type, resumePayload = p.ResumePayload, profileId = p.ProfileId });
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

    /// <summary>
    /// Emit the consolidated PROCESS_LIST_UPDATED snapshot. Lifecycle transitions pass
    /// <paramref name="immediate"/> = true (rare, status changes must land now). Report-driven
    /// calls pass false and are throttled to one emit per <see cref="SnapshotThrottleMs"/> window —
    /// a suppressed call schedules ONE trailing emit at the window end, so the final progress value
    /// is never lost even if no further call arrives.
    /// </summary>
    private void EmitSnapshot(bool immediate = true)
    {
        if (!immediate)
        {
            lock (_emitLock)
            {
                var sinceMs = (DateTime.UtcNow - _lastEmitUtc).TotalMilliseconds;
                if (sinceMs < SnapshotThrottleMs)
                {
                    if (_trailingEmitScheduled) return;
                    _trailingEmitScheduled = true;
                    var delay = Math.Max(1, SnapshotThrottleMs - (int)sinceMs);
                    _ = Task.Delay(delay).ContinueWith(_ =>
                    {
                        lock (_emitLock)
                        {
                            _trailingEmitScheduled = false;
                            _lastEmitUtc = DateTime.UtcNow;
                        }
                        EmitNow();
                    }, TaskScheduler.Default);
                    return;
                }
                _lastEmitUtc = DateTime.UtcNow;
            }
            EmitNow();
            return;
        }

        lock (_emitLock) { _lastEmitUtc = DateTime.UtcNow; }
        EmitNow();
    }

    private void EmitNow()
    {
        List<ProcessInfo> snapshot;
        lock (_lock) { snapshot = Snapshot(); }
        // Fire-and-forget: payload is the full snapshot, so a later emit simply supersedes an earlier
        // one (last-write-wins) — no ordering hazard. IpcHandler batches these every 50ms.
        _ = _eventBus.EmitAsync(ModuleNames.SYSTEM, SystemEvents.PROCESS_LIST_UPDATED, new { processes = snapshot });
    }
}
