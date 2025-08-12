using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PowerBi.Orchestration.Microservice.PartitionProcessor
{
    // --------- Categories & Event ---------
    public enum PartitionCategory { Small = 0, Medium = 1, Large = 2, ExtraLarge = 3 }

    public sealed class EventSet
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString("N");
        public PartitionCategory Category { get; init; } = PartitionCategory.Small;

        /// <summary>
        /// Estimated row count for this event’s data volume.
        /// Used to compute dynamic partitions (cost) during dequeue.
        /// </summary>
        public long EstimatedRows { get; init; } = 0;

        // (Optional) Add any other metadata you already have…
        // public string SourceKey { get; init; } = "";
        // public DateTime TimeStampUtc { get; init; } = DateTime.UtcNow;
    }

    // --------- Dequeue (budget, sizing & caps) ---------
    public sealed class DequeuePolicy
    {
        /// <summary>Priority order to consider non-small categories.</summary>
        public IReadOnlyList<PartitionCategory> Priority { get; init; } =
            new[] { PartitionCategory.ExtraLarge, PartitionCategory.Large, PartitionCategory.Medium, PartitionCategory.Small };

        /// <summary>
        /// Max “slots” per cycle (your safe parallelism). For a 4×D16s_v3 gateway cluster, start with 16.
        /// </summary>
        public int MaxSlotsPerCycle { get; init; } = 16;

        /// <summary>
        /// If true, drains ALL small events first (legacy behavior). Recommended: false (use smalls as fillers).
        /// </summary>
        public bool AlwaysIncludeAllSmalls { get; init; } = false;

        /// <summary>
        /// Base row target per partition. Typical 20–35M; start with 25M and tune from telemetry.
        /// </summary>
        public int TargetRowsPerPartition { get; init; } = 25_000_000;

        /// <summary>Min partitions per event, by category.</summary>
        public IReadOnlyDictionary<PartitionCategory, int> MinParts { get; init; } =
            new Dictionary<PartitionCategory, int>
            {
                { PartitionCategory.ExtraLarge, 4 },
                { PartitionCategory.Large,      4 },
                { PartitionCategory.Medium,     2 },
                { PartitionCategory.Small,      2 }
            };

        /// <summary>Max partitions per event, by category (guardrails).</summary>
        public IReadOnlyDictionary<PartitionCategory, int> MaxParts { get; init; } =
            new Dictionary<PartitionCategory, int>
            {
                { PartitionCategory.ExtraLarge, 16 },
                { PartitionCategory.Large,      12 },
                { PartitionCategory.Medium,     10 },
                { PartitionCategory.Small,       8 }
            };

        /// <summary>Max number of events of a category per cycle (prevents monopolies).</summary>
        public IReadOnlyDictionary<PartitionCategory, int> CapPerCycle { get; init; } =
            new Dictionary<PartitionCategory, int>
            {
                { PartitionCategory.ExtraLarge, 1 },
                { PartitionCategory.Large,      3 },
                { PartitionCategory.Medium,     2 },
                { PartitionCategory.Small,      int.MaxValue } // smalls act as filler
            };

        /// <summary>
        /// Computes the slot cost for an event = partitions to create, from EstimatedRows and category guardrails.
        /// </summary>
        public int CostOf(EventSet e, long? fallbackRowsByCategory = null)
        {
            long rows = e.EstimatedRows > 0 ? e.EstimatedRows : (fallbackRowsByCategory ?? RowsFallback(e.Category));
            int raw  = (int)Math.Ceiling((double)Math.Max(rows, 1) / TargetRowsPerPartition);
            int min  = MinParts[e.Category];
            int max  = MaxParts[e.Category];
            return Math.Max(min, Math.Min(max, raw));
        }

        private static long RowsFallback(PartitionCategory c) => c switch
        {
            PartitionCategory.ExtraLarge => 225_000_000, // midpoints for your bands
            PartitionCategory.Large      => 150_000_000,
            PartitionCategory.Medium     =>  75_000_000,
            _                            =>  25_000_000
        };
    }

    // --------- Multi-producer / single-consumer queue ---------
    public sealed class CategorizedQueue
    {
        private readonly ConcurrentQueue<EventSet> _s = new();
        private readonly ConcurrentQueue<EventSet> _m = new();
        private readonly ConcurrentQueue<EventSet> _l = new();
        private readonly ConcurrentQueue<EventSet> _x = new();

        private readonly object _gate = new();              // atomic batch selection/drain
        private readonly SemaphoreSlim _nonEmpty = new(0);  // one-bit “has items” signal
        private int _total;                                  // total items across all queues

        private readonly DequeuePolicy _policy;

        public CategorizedQueue(DequeuePolicy? policy = null) => _policy = policy ?? new DequeuePolicy();

        public event Action<EventSet>? ItemEnqueued;
        public event Action<IReadOnlyList<EventSet>>? BatchDequeued;

        public void Enqueue(EventSet e)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            switch (e.Category)
            {
                case PartitionCategory.Small:      _s.Enqueue(e); break;
                case PartitionCategory.Medium:     _m.Enqueue(e); break;
                case PartitionCategory.Large:      _l.Enqueue(e); break;
                case PartitionCategory.ExtraLarge: _x.Enqueue(e); break;
                default: throw new ArgumentOutOfRangeException(nameof(e.Category));
            }

            if (Interlocked.Increment(ref _total) == 1)
                _nonEmpty.Release(); // signal on 0->1 transition

            ItemEnqueued?.Invoke(e);
        }

        public void EnqueueBatch(IEnumerable<EventSet> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            foreach (var e in items) Enqueue(e);
        }

        public (int small, int medium, int large, int extraLarge, int total) GetCounts()
        {
            var s = _s.Count; var m = _m.Count; var l = _l.Count; var x = _x.Count;
            return (s, m, l, x, s + m + l + x);
        }

        /// <summary>
        /// Dequeues ONE batch per call using budget-based packing.
        /// Packs events up to MaxSlotsPerCycle, enforces per-category caps,
        /// uses Smalls as fillers (unless AlwaysIncludeAllSmalls = true).
        /// </summary>
        public async Task<IReadOnlyList<EventSet>> DequeueBatchAsync(CancellationToken ct)
        {
            await _nonEmpty.WaitAsync(ct).ConfigureAwait(false);

            var batch = new List<EventSet>();
            lock (_gate)
            {
                if (Volatile.Read(ref _total) == 0)
                    return Array.Empty<EventSet>();

                int budget = _policy.MaxSlotsPerCycle;
                int removed = 0;

                int Take(ConcurrentQueue<EventSet> q, PartitionCategory cat, int capEvents)
                {
                    int taken = 0;
                    while (capEvents > 0 && q.TryPeek(out var head))
                    {
                        int cost = _policy.CostOf(head);
                        if (budget < cost) break;

                        if (q.TryDequeue(out head))
                        {
                            batch.Add(head);
                            removed++;
                            taken++;
                            capEvents--;
                            budget -= cost;
                        }
                        else break;
                    }
                    return taken;
                }

                // Optionally pre-drain smalls (legacy behavior; not recommended for throughput)
                if (_policy.AlwaysIncludeAllSmalls)
                    Take(_s, PartitionCategory.Small, int.MaxValue);

                // XL -> L -> M (respect caps and budget)
                foreach (var cat in _policy.Priority)
                {
                    if (cat == PartitionCategory.Small) continue;
                    var q = GetQueue(cat);
                    if (q.IsEmpty) continue;
                    var cap = _policy.CapPerCycle.TryGetValue(cat, out var c) ? c : int.MaxValue;
                    Take(q, cat, cap);
                }

                // Fill remaining budget with smalls
                if (budget > 0)
                    Take(_s, PartitionCategory.Small, int.MaxValue);

                // Update totals and re-arm if more work remains
                int remaining = Interlocked.Add(ref _total, -removed);
                if (remaining > 0)
                    _nonEmpty.Release();

                BatchDequeued?.Invoke(batch);
            }
            return batch;
        }

        private ConcurrentQueue<EventSet> GetQueue(PartitionCategory c) =>
            c switch
            {
                PartitionCategory.Small      => _s,
                PartitionCategory.Medium     => _m,
                PartitionCategory.Large      => _l,
                PartitionCategory.ExtraLarge => _x,
                _ => throw new ArgumentOutOfRangeException(nameof(c))
            };
    }

    // --------- (Optional) Partition plan + bounded-concurrency scheduler ---------
    /// <summary>
    /// Starting plan for partitions-per-category when EstimatedRows is missing.
    /// You can ignore this if you always provide EstimatedRows and compute cost on dequeue only.
    /// </summary>
    public sealed class PartitionPlan
    {
        public int SmallPartitions { get; init; } = 4;
        public int MediumPartitions { get; init; } = 10;
        public int LargePartitions  { get; init; } = 4;
        public int XlPartitions     { get; init; } = 4;

        public int Get(PartitionCategory c) => c switch
        {
            PartitionCategory.Small      => SmallPartitions,
            PartitionCategory.Medium     => MediumPartitions,
            PartitionCategory.Large      => LargePartitions,
            PartitionCategory.ExtraLarge => XlPartitions,
            _ => throw new ArgumentOutOfRangeException(nameof(c))
        };
    }

    /// <summary>
    /// Expands a dequeued batch into (event, partitionIndex) jobs and runs them with bounded concurrency.
    /// </summary>
    public sealed class RefreshScheduler
    {
        private readonly int _maxConcurrency;
        private readonly TimeSpan _perPartitionTimeout;
        private readonly Func<string, int, PartitionCategory, CancellationToken, Task> _refreshPartitionAsync;
        private readonly DequeuePolicy _policy;
        private readonly PartitionPlan _fallbackPlan;

        public RefreshScheduler(
            int maxConcurrency,
            TimeSpan perPartitionTimeout,
            Func<string, int, PartitionCategory, CancellationToken, Task> refreshPartitionAsync,
            DequeuePolicy? policy = null,
            PartitionPlan? fallbackPlan = null)
        {
            _maxConcurrency = Math.Max(1, maxConcurrency);
            _perPartitionTimeout = perPartitionTimeout;
            _refreshPartitionAsync = refreshPartitionAsync ?? throw new ArgumentNullException(nameof(refreshPartitionAsync));
            _policy = policy ?? new DequeuePolicy();
            _fallbackPlan = fallbackPlan ?? new PartitionPlan();
        }

        /// <summary>
        /// Runs one batch: computes partitions for each event (from EstimatedRows; falls back to plan),
        /// enqueues jobs, executes under global throttle. Smalls are appended after larger categories
        /// to keep the pipeline full.
        /// </summary>
        public async Task ExecuteBatchAsync(IEnumerable<EventSet> batch, CancellationToken ct)
        {
            var items = batch?.ToList() ?? throw new ArgumentNullException(nameof(batch));
            if (items.Count == 0) return;

            var work = new ConcurrentQueue<(string evt, int p, PartitionCategory cat)>();

            // Non-small first (driving categories), then smalls
            foreach (var e in items.Where(x => x.Category != PartitionCategory.Small))
            {
                int k = _policy.CostOf(e); // same logic as dequeue (rows -> partitions)
                for (int i = 0; i < k; i++)
                    work.Enqueue((e.EventId, i, e.Category));
            }
            foreach (var e in items.Where(x => x.Category == PartitionCategory.Small))
            {
                // If EstimatedRows is missing, use fallback plan for S
                int k = e.EstimatedRows > 0 ? _policy.CostOf(e) : _fallbackPlan.Get(PartitionCategory.Small);
                for (int i = 0; i < k; i++)
                    work.Enqueue((e.EventId, i, PartitionCategory.Small));
            }

            using var throttler = new SemaphoreSlim(_maxConcurrency);
            var tasks = new List<Task>(work.Count);

            while (work.TryDequeue(out var job))
            {
                await throttler.WaitAsync(ct).ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        linked.CancelAfter(_perPartitionTimeout);
                        await _refreshPartitionAsync(job.evt, job.p, job.cat, linked.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        sw.Stop();
                        throttler.Release();
                        // TODO: record telemetry: job.cat, sw.Elapsed, job.p
                    }
                }, ct));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    // --------- Example wiring (replace with your host/DI setup) ---------
    public static class Example
    {
        // Plug your XMLA/TMSL/REST call here. Map (eventId, partitionIndex, category) to a concrete partition name.
        private static Task RefreshPartitionAsync(string eventId, int partitionIndex, PartitionCategory cat, CancellationToken ct)
        {
            // Example: var partitionName = $"{cat}_{eventId}_part_{partitionIndex:D2}";
            // Execute refresh…
            return Task.CompletedTask;
        }

        public static async Task RunAsync(CancellationToken ct)
        {
            // Policy tuned for a 4×D16s_v3 gateway cluster
            var policy = new DequeuePolicy
            {
                MaxSlotsPerCycle = 16,
                TargetRowsPerPartition = 25_000_000,
                AlwaysIncludeAllSmalls = false
            };

            var queue = new CategorizedQueue(policy);

            // Producers add work
            queue.Enqueue(new EventSet { EventId = "XL_001", Category = PartitionCategory.ExtraLarge, EstimatedRows = 240_000_000 });
            queue.Enqueue(new EventSet { EventId = "S_010",  Category = PartitionCategory.Small,      EstimatedRows =  12_000_000 });
            queue.Enqueue(new EventSet { EventId = "L_020",  Category = PartitionCategory.Large,      EstimatedRows = 140_000_000 });
            // …from multiple API threads

            // Single consumer loop (hosted service)
            var scheduler = new RefreshScheduler(
                maxConcurrency: 16,                               // Align with policy.MaxSlotsPerCycle or slightly lower
                perPartitionTimeout: TimeSpan.FromMinutes(30),
                refreshPartitionAsync: RefreshPartitionAsync,
                policy: policy);

            while (!ct.IsCancellationRequested)
            {
                var batch = await queue.DequeueBatchAsync(ct);
                await scheduler.ExecuteBatchAsync(batch, ct);
            }
        }
    }
}
