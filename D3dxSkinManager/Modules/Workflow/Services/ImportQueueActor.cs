using System.Threading.Channels;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// The import queue as an INTERNAL ACTOR (mailbox + single consumer loop). Producers only
/// <see cref="Enqueue"/> / <see cref="Cancel"/> — they post a message and return; they never touch
/// queue state. ONE loop thread drains the mailbox and owns all state, so there are NO locks (the actor
/// guarantee). It admits the highest-<see cref="WorkflowPriority"/> job when a slot is free and pulls the
/// next on completion.
///
/// Concurrency is split into TWO LANES with independent caps: a "download" lane (network-bound —
/// <c>REMOTE_DOWNLOAD</c>, bounded by <see cref="MaxDownloadConcurrency"/>) and an "import" lane
/// (CPU-bound extract/recompress — <c>MOD_IMPORT</c> + <c>REMOTE_IMPORT</c>, bounded by
/// <see cref="MaxImportConcurrency"/>). A remote job runs its download leg in the download lane, then
/// re-enqueues its import leg into the import lane — so a finished download WAITS for an import slot
/// instead of one shared pool coupling the two. Lanes never share slots.
///
/// The WORK runs on a worker <c>Task.Run</c> off the loop thread (concurrent, bounded); its result
/// re-enters the mailbox as a Finished message — the loop never blocks on a job. This replaces the old
/// per-item "Task.Run that self-awaits a SemaphoreSlim" (WorkflowConcurrencyManager) with one queue +
/// one processor. The durable queue is the WorkflowInfo DB rows; the actor is the in-memory scheduler.
/// </summary>
public interface IImportQueueActor
{
    /// <summary>Queue a job (a Pending WorkflowInfo row must already exist). The <paramref name="jobType"/>
    /// selects the lane (download vs import). Idempotent — a job already queued or running is ignored (a
    /// re-enqueue of a running job is honored AFTER it finishes, so a preview-confirm that races the yield,
    /// or a download→import lane hand-off, isn't lost). Returns immediately.</summary>
    void Enqueue(string jobId, string jobType, WorkflowPriority priority);

    /// <summary>Cancel a job: if running, signal its token; if still queued (either lane), drop it before it starts.</summary>
    void Cancel(string jobId);

    /// <summary>Max IMPORT-lane jobs running at once (default 5 — compression is CPU-bound). Applied on the loop thread.</summary>
    int MaxImportConcurrency { get; set; }

    /// <summary>Max DOWNLOAD-lane jobs running at once (default 4 — network-bound). Applied on the loop thread.</summary>
    int MaxDownloadConcurrency { get; set; }

    /// <summary>Approx running count across both lanes (updated by the loop; eventually-consistent — for metrics/tests).</summary>
    int RunningCount { get; }

    /// <summary>Approx queued count across both lanes (updated by the loop; eventually-consistent — for metrics/tests).</summary>
    int QueuedCount { get; }
}

public sealed class ImportQueueActor : IImportQueueActor, IAsyncDisposable
{
    /// <summary>Job types that run in the DOWNLOAD lane; everything else runs in the import lane.</summary>
    private static readonly IReadOnlySet<string> DefaultDownloadJobTypes =
        new HashSet<string>(new[] { "REMOTE_DOWNLOAD" }, StringComparer.OrdinalIgnoreCase);

    private abstract record Msg;
    private sealed record EnqueueMsg(string Id, string Type, WorkflowPriority Prio) : Msg;
    private sealed record FinishedMsg(string Id) : Msg;
    private sealed record CancelMsg(string Id) : Msg;
    private sealed record SetMaxMsg(bool IsDownload, int Max) : Msg;

    /// <summary>One concurrency lane — its own priority queue, admission metadata, cancelled-set and cap.
    /// Mutated ONLY by the single loop thread ⇒ no locks.</summary>
    private sealed class Lane
    {
        public readonly PriorityQueue<string, WorkflowPriority> Pending = new(new WorkflowPriorityComparer());
        public readonly Dictionary<string, (string Type, WorkflowPriority Prio)> Meta = new();
        public readonly HashSet<string> Cancelled = new();
        public int Max;
        /// <summary>Live count of this lane's running jobs — kept in step with <c>_running</c> so admission is
        /// O(1) instead of scanning all running jobs every Pump. Only the loop thread touches it.</summary>
        public int Running;
    }

    private readonly Channel<Msg> _mailbox =
        Channel.CreateUnbounded<Msg>(new UnboundedChannelOptions { SingleReader = true });
    // Resolved LAZILY (on the loop thread, first dispatch) — the handlers depend on this actor, so
    // resolving them in the ctor would be a DI cycle. Only the loop touches _handlers ⇒ no lock.
    private readonly Func<IEnumerable<IImportJobHandler>> _handlerFactory;
    private IReadOnlyDictionary<string, IImportJobHandler>? _handlers;
    private readonly ILogHelper _logger;
    private readonly IReadOnlySet<string> _downloadJobTypes;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    // Actor-owned state — mutated ONLY by the single loop thread ⇒ no locks. A job lives in exactly ONE
    // lane at a time; the download→import hand-off moves it via _reEnqueueAfterFinish (routed by type).
    private readonly Lane _import = new();
    private readonly Lane _download = new();
    private readonly Dictionary<string, (CancellationTokenSource Cts, Lane Lane)> _running = new();
    private readonly Dictionary<string, (string Type, WorkflowPriority Prio)> _reEnqueueAfterFinish = new();

    private volatile int _importMax = 5;
    private volatile int _downloadMax = 4;
    private volatile int _runningCount;
    private volatile int _queuedCount;

    public ImportQueueActor(
        Func<IEnumerable<IImportJobHandler>> handlerFactory,
        ILogHelper logger,
        int maxImportConcurrency = 5,
        int maxDownloadConcurrency = 4,
        IReadOnlySet<string>? downloadJobTypes = null)
    {
        _handlerFactory = handlerFactory;
        _logger = logger;
        _downloadJobTypes = downloadJobTypes ?? DefaultDownloadJobTypes;
        _import.Max = _importMax = Math.Max(1, maxImportConcurrency);
        _download.Max = _downloadMax = Math.Max(1, maxDownloadConcurrency);
        _loop = Task.Run(RunLoopAsync);
    }

    /// <summary>Lazily-resolved handlers by job type (first access is on the loop thread — no lock).</summary>
    private IReadOnlyDictionary<string, IImportJobHandler> Handlers =>
        _handlers ??= _handlerFactory().ToDictionary(h => h.JobType, StringComparer.OrdinalIgnoreCase);

    /// <summary>The lane a job type runs in — download types → download lane, everything else → import lane.</summary>
    private Lane LaneFor(string type) => _downloadJobTypes.Contains(type) ? _download : _import;

    public int MaxImportConcurrency
    {
        get => _importMax;
        set => _mailbox.Writer.TryWrite(new SetMaxMsg(IsDownload: false, value));
    }

    public int MaxDownloadConcurrency
    {
        get => _downloadMax;
        set => _mailbox.Writer.TryWrite(new SetMaxMsg(IsDownload: true, value));
    }

    public int RunningCount => _runningCount;
    public int QueuedCount => _queuedCount;

    public void Enqueue(string jobId, string jobType, WorkflowPriority priority)
        => _mailbox.Writer.TryWrite(new EnqueueMsg(jobId, jobType, priority));

    public void Cancel(string jobId) => _mailbox.Writer.TryWrite(new CancelMsg(jobId));

    private async Task RunLoopAsync()
    {
        try
        {
            await foreach (var msg in _mailbox.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                switch (msg)
                {
                    case EnqueueMsg e: OnEnqueue(e); break;
                    case FinishedMsg f: OnFinished(f.Id); break;
                    case CancelMsg c: OnCancel(c.Id); break;
                    case SetMaxMsg s:
                        if (s.IsDownload) { _download.Max = _downloadMax = Math.Max(1, s.Max); }
                        else { _import.Max = _importMax = Math.Max(1, s.Max); }
                        Pump();
                        break;
                }
            }
        }
        catch (OperationCanceledException) { /* disposing — normal */ }
        catch (Exception ex)
        {
            _logger.Error($"[ImportQueueActor] loop crashed: {ex.Message}", "ImportQueue", ex);
        }
    }

    private void OnEnqueue(EnqueueMsg e)
    {
        // A re-enqueue of a currently-running job (a preview confirm racing the yield's Finished, OR the
        // download→import lane hand-off): defer it until the job actually finishes so the request isn't
        // swallowed by the running-dedup below. The stashed TYPE decides the lane it lands in on finish.
        if (_running.ContainsKey(e.Id))
        {
            _reEnqueueAfterFinish[e.Id] = (e.Type, e.Prio);
            return;
        }
        var lane = LaneFor(e.Type);
        if (lane.Meta.ContainsKey(e.Id))
        {
            lane.Cancelled.Remove(e.Id); // un-cancel a re-queued job
            return; // already queued in this lane — dedup
        }
        lane.Meta[e.Id] = (e.Type, e.Prio);
        lane.Cancelled.Remove(e.Id);
        lane.Pending.Enqueue(e.Id, e.Prio);
        _queuedCount = _import.Meta.Count + _download.Meta.Count;
        Pump();
    }

    private void OnFinished(string id)
    {
        if (_running.Remove(id, out var e)) { e.Cts.Dispose(); e.Lane.Running--; }
        _runningCount = _running.Count;
        // Honor a re-enqueue that arrived while the job was still running (preview confirm, or the
        // download→import hand-off) — routed to its lane by the stashed type.
        if (_reEnqueueAfterFinish.Remove(id, out var meta))
            OnEnqueue(new EnqueueMsg(id, meta.Type, meta.Prio));
        Pump();
    }

    private void OnCancel(string id)
    {
        _reEnqueueAfterFinish.Remove(id);
        if (_running.TryGetValue(id, out var e))
        {
            e.Cts.Cancel(); // running → signal; the worker's Finished frees the slot
            return;
        }
        // Queued → mark cancelled in whichever lane holds it (an id lives in one lane); lazily dropped at dequeue.
        if (_import.Meta.ContainsKey(id)) _import.Cancelled.Add(id);
        if (_download.Meta.ContainsKey(id)) _download.Cancelled.Add(id);
    }

    private void Pump()
    {
        // Each lane admits from its OWN pool — a full download lane never blocks import admission.
        PumpLane(_download);
        PumpLane(_import);
    }

    private void PumpLane(Lane lane)
    {
        while (lane.Running < lane.Max && TryDequeueLive(lane, out var id, out var type))
        {
            if (!Handlers.TryGetValue(type, out var handler))
            {
                _logger.Error($"[ImportQueueActor] no handler for job type '{type}' (job {id}) — dropping", "ImportQueue");
                continue;
            }
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            _running[id] = (cts, lane);
            lane.Running++;
            _runningCount = _running.Count;
            _queuedCount = _import.Meta.Count + _download.Meta.Count;
            RunWorker(id, handler, cts.Token);
        }
    }

    private static bool TryDequeueLive(Lane lane, out string id, out string type)
    {
        while (lane.Pending.TryDequeue(out id!, out _))
        {
            if (lane.Cancelled.Remove(id))
            {
                lane.Meta.Remove(id); // cancelled while queued
                continue;
            }
            if (lane.Meta.Remove(id, out var meta))
            {
                type = meta.Type;
                return true;
            }
        }
        id = string.Empty;
        type = string.Empty;
        return false;
    }

    private void RunWorker(string id, IImportJobHandler handler, CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await handler.ProcessAsync(id, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* cancelled — handler/registry already recorded it */ }
            catch (Exception ex)
            {
                _logger.Error($"[ImportQueueActor] job {id} handler threw: {ex.Message}", "ImportQueue", ex);
            }
            finally
            {
                _mailbox.Writer.TryWrite(new FinishedMsg(id)); // result re-enters the mailbox → frees slot
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _mailbox.Writer.TryComplete();
        try { await _loop.ConfigureAwait(false); } catch { /* stopping */ }
        foreach (var e in _running.Values) e.Cts.Dispose();
        _stop.Dispose();
    }
}

/// <summary>Orders so <c>PriorityQueue.Dequeue</c> returns the MOST important job first: confirmed
/// before unconfirmed, then higher progress, then earlier-created (mirrors the old
/// <c>WorkflowConcurrencyManager</c> priority so behavior is preserved).</summary>
public sealed class WorkflowPriorityComparer : IComparer<WorkflowPriority>
{
    public int Compare(WorkflowPriority a, WorkflowPriority b)
    {
        if (a.Confirmed != b.Confirmed) return a.Confirmed ? -1 : 1;            // confirmed first
        if (a.Progress != b.Progress) return b.Progress.CompareTo(a.Progress);  // higher progress first
        return a.CreatedAtUtc.CompareTo(b.CreatedAtUtc);                        // earlier created first
    }
}
