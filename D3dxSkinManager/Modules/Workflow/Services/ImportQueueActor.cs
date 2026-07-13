using System.Threading.Channels;
using D3dxSkinManager.Modules.Core.Helpers;

namespace D3dxSkinManager.Modules.Workflow.Services;

/// <summary>
/// The import queue as an INTERNAL ACTOR (mailbox + single consumer loop). Producers only
/// <see cref="Enqueue"/> / <see cref="Cancel"/> — they post a message and return; they never touch
/// queue state. ONE loop thread drains the mailbox and owns all state (<c>_pending</c>, <c>_running</c>),
/// so there are NO locks (the actor guarantee). It admits the highest-<see cref="WorkflowPriority"/>
/// job when a slot is free, bounded by <see cref="MaxConcurrency"/>, and pulls the next on completion.
///
/// The WORK runs on a worker <c>Task.Run</c> off the loop thread (concurrent, bounded); its result
/// re-enters the mailbox as a Finished message — the loop never blocks on a job. This replaces the old
/// per-item "Task.Run that self-awaits a SemaphoreSlim" (WorkflowConcurrencyManager) with one queue +
/// one processor. The durable queue is the WorkflowInfo DB rows; the actor is the in-memory scheduler.
/// </summary>
public interface IImportQueueActor
{
    /// <summary>Queue a job (a Pending WorkflowInfo row must already exist). Idempotent — a job already
    /// queued or running is ignored (a re-enqueue of a running job is honored AFTER it finishes, so a
    /// preview-confirm that races the yield isn't lost). Returns immediately.</summary>
    void Enqueue(string jobId, string jobType, WorkflowPriority priority);

    /// <summary>Cancel a job: if running, signal its token; if still queued, drop it before it starts.</summary>
    void Cancel(string jobId);

    /// <summary>Max jobs running at once (default 5 — compression is CPU-bound). Applied on the loop thread.</summary>
    int MaxConcurrency { get; set; }

    /// <summary>Approx running count (updated by the loop; eventually-consistent — for metrics/tests).</summary>
    int RunningCount { get; }

    /// <summary>Approx queued count (updated by the loop; eventually-consistent — for metrics/tests).</summary>
    int QueuedCount { get; }
}

public sealed class ImportQueueActor : IImportQueueActor, IAsyncDisposable
{
    private abstract record Msg;
    private sealed record EnqueueMsg(string Id, string Type, WorkflowPriority Prio) : Msg;
    private sealed record FinishedMsg(string Id) : Msg;
    private sealed record CancelMsg(string Id) : Msg;
    private sealed record SetMaxMsg(int Max) : Msg;

    private readonly Channel<Msg> _mailbox =
        Channel.CreateUnbounded<Msg>(new UnboundedChannelOptions { SingleReader = true });
    // Resolved LAZILY (on the loop thread, first dispatch) — the handlers depend on this actor, so
    // resolving them in the ctor would be a DI cycle. Only the loop touches _handlers ⇒ no lock.
    private readonly Func<IEnumerable<IImportJobHandler>> _handlerFactory;
    private IReadOnlyDictionary<string, IImportJobHandler>? _handlers;
    private readonly ILogHelper _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    // Actor-owned state — mutated ONLY by the single loop thread ⇒ no locks.
    private readonly PriorityQueue<string, WorkflowPriority> _pending = new(new WorkflowPriorityComparer());
    private readonly Dictionary<string, (string Type, WorkflowPriority Prio)> _pendingMeta = new();
    private readonly HashSet<string> _cancelledPending = new();
    private readonly Dictionary<string, CancellationTokenSource> _running = new();
    private readonly Dictionary<string, (string Type, WorkflowPriority Prio)> _reEnqueueAfterFinish = new();

    private volatile int _max = 5;
    private volatile int _runningCount;
    private volatile int _queuedCount;

    public ImportQueueActor(Func<IEnumerable<IImportJobHandler>> handlerFactory, ILogHelper logger, int maxConcurrency = 5)
    {
        _handlerFactory = handlerFactory;
        _logger = logger;
        _max = Math.Max(1, maxConcurrency);
        _loop = Task.Run(RunLoopAsync);
    }

    /// <summary>Lazily-resolved handlers by job type (first access is on the loop thread — no lock).</summary>
    private IReadOnlyDictionary<string, IImportJobHandler> Handlers =>
        _handlers ??= _handlerFactory().ToDictionary(h => h.JobType, StringComparer.OrdinalIgnoreCase);

    public int MaxConcurrency
    {
        get => _max;
        set => _mailbox.Writer.TryWrite(new SetMaxMsg(value));
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
                    case SetMaxMsg s: _max = Math.Max(1, s.Max); Pump(); break;
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
        // A re-enqueue of a currently-running job (e.g. preview confirm racing the yield's Finished): defer
        // it until the job actually finishes so the request isn't swallowed by the running-dedup below.
        if (_running.ContainsKey(e.Id))
        {
            _reEnqueueAfterFinish[e.Id] = (e.Type, e.Prio);
            return;
        }
        if (_pendingMeta.ContainsKey(e.Id))
        {
            _cancelledPending.Remove(e.Id); // un-cancel a re-queued job
            return; // already queued — dedup
        }
        _pendingMeta[e.Id] = (e.Type, e.Prio);
        _cancelledPending.Remove(e.Id);
        _pending.Enqueue(e.Id, e.Prio);
        _queuedCount = _pendingMeta.Count;
        Pump();
    }

    private void OnFinished(string id)
    {
        if (_running.Remove(id, out var cts)) cts.Dispose();
        _runningCount = _running.Count;
        // Honor a confirm that arrived while the job was still running.
        if (_reEnqueueAfterFinish.Remove(id, out var meta))
            OnEnqueue(new EnqueueMsg(id, meta.Type, meta.Prio));
        Pump();
    }

    private void OnCancel(string id)
    {
        _reEnqueueAfterFinish.Remove(id);
        if (_running.TryGetValue(id, out var cts))
        {
            cts.Cancel(); // running → signal; the worker's Finished frees the slot
            return;
        }
        if (_pendingMeta.ContainsKey(id))
            _cancelledPending.Add(id); // queued → lazily dropped at dequeue
    }

    private void Pump()
    {
        while (_running.Count < _max && TryDequeueLive(out var id, out var type))
        {
            if (!Handlers.TryGetValue(type, out var handler))
            {
                _logger.Error($"[ImportQueueActor] no handler for job type '{type}' (job {id}) — dropping", "ImportQueue");
                continue;
            }
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            _running[id] = cts;
            _runningCount = _running.Count;
            _queuedCount = _pendingMeta.Count;
            RunWorker(id, handler, cts.Token);
        }
    }

    private bool TryDequeueLive(out string id, out string type)
    {
        while (_pending.TryDequeue(out id!, out _))
        {
            if (_cancelledPending.Remove(id))
            {
                _pendingMeta.Remove(id); // cancelled while queued
                continue;
            }
            if (_pendingMeta.Remove(id, out var meta))
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
        foreach (var cts in _running.Values) cts.Dispose();
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
