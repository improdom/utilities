using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YourNamespace
{
    public enum PartitionCategory { Small, Medium, Large, ExtraLarge }

    public sealed class EventSet
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString("N");
        public PartitionCategory Category { get; init; }
        // Add any other properties you need
    }

    /// <summary>
    /// Category-aware queue with deterministic batch rules (no slot math).
    /// Producers enqueue; a single consumer calls DequeueBatchAsync() in a loop.
    /// </summary>
    public sealed class CategoryQueue
    {
        // One queue per category
        private readonly ConcurrentQueue<EventSet> _s = new();
        private readonly ConcurrentQueue<EventSet> _m = new();
        private readonly ConcurrentQueue<EventSet> _l = new();
        private readonly ConcurrentQueue<EventSet> _x = new();

        // Coordination
        private readonly SemaphoreSlim _nonEmpty = new(0); // one-bit signal
        private readonly object _gate = new();             // atomically choose & drain a batch
        private int _total;                                 // total items across all queues

        // (Optional) hooks
        public event Action<EventSet>? ItemEnqueued;
        public event Action<IReadOnlyList<EventSet>>? BatchDequeued;

        // ------------------- Producer API -------------------
        public void Enqueue(EventSet item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            switch (item.Category)
            {
                case PartitionCategory.Small:      _s.Enqueue(item); break;
                case PartitionCategory.Medium:     _m.Enqueue(item); break;
                case PartitionCategory.Large:      _l.Enqueue(item); break;
                case PartitionCategory.ExtraLarge: _x.Enqueue(item); break;
                default: throw new ArgumentOutOfRangeException(nameof(item.Category));
            }

            ItemEnqueued?.Invoke(item);
            if (Interlocked.Increment(ref _total) == 1)
                _nonEmpty.Release(); // 0 -> 1 transition: wake consumer
        }

        public void EnqueueBatch(IEnumerable<EventSet> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var it in items) Enqueue(it);
        }

        public (int small, int medium, int large, int extraLarge, int total) GetCounts()
        {
            var s = _s.Count; var m = _m.Count; var l = _l.Count; var x = _x.Count;
            return (s, m, l, x, s + m + l + x);
        }

        // ------------------- Consumer API -------------------
        /// <summary>
        /// Dequeues ONE batch following the fixed rules:
        /// 1) XL>=1 -> 1 XL (exclusive)
        /// 2) else L>=2 -> 2 L (exclusive)
        /// 3) else L==1 && M>=1 -> 1 L + up to 5 M (no S)
        /// 4) else M>=10 -> 10 M
        /// 5) else M in [1..9] -> all M, then top with S to 10 total
        /// 6) else -> all S
        /// </summary>
        public async Task<IReadOnlyList<EventSet>> DequeueBatchAsync(CancellationToken ct)
        {
            await _nonEmpty.WaitAsync(ct).ConfigureAwait(false);

            var batch = new List<EventSet>(capacity: 16);
            int removed = 0;

            lock (_gate)
            {
                if (Volatile.Read(ref _total) == 0)
                    return Array.Empty<EventSet>(); // lost the race; nothing to do

                // ---- helpers (used under lock only) ----
                static int Count(ConcurrentQueue<EventSet> q) => q.Count;

                bool TryTakeN(ConcurrentQueue<EventSet> q, int n)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (!q.TryDequeue(out var e)) return false; // shouldn't happen with Count guard
                        batch.Add(e);
                        removed++;
                    }
                    return true;
                }

                int TakeUpTo(ConcurrentQueue<EventSet> q, int n)
                {
                    int k = 0;
                    for (; k < n; k++)
                    {
                        if (!q.TryDequeue(out var e)) break;
                        batch.Add(e);
                        removed++;
                    }
                    return k;
                }

                void DrainAll(ConcurrentQueue<EventSet> q)
                {
                    while (q.TryDequeue(out var e))
                    {
                        batch.Add(e);
                        removed++;
                    }
                }

                // ---- snapshot counts ----
                int cx = Count(_x);
                int cl = Count(_l);
                int cm = Count(_m);
                int cs = Count(_s);

                // ---- RULES ----

                // Rule 1: 1×XL exclusive
                if (cx >= 1)
                {
                    TryTakeN(_x, 1);
                }
                // Rule 2: 2×L exclusive
                else if (cl >= 2)
                {
                    TryTakeN(_l, 2);
                }
                // Rule 3: 1×L + up to 5×M (no S here)
                else if (cl == 1 && cm >= 1)
                {
                    TryTakeN(_l, 1);
                    int takeM = Math.Min(5, cm);
                    TryTakeN(_m, takeM);
                }
                // Rule 4: 10×M
                else if (cm >= 10)
                {
                    TryTakeN(_m, 10);
                }
                // Rule 5: 1..9 M -> all M + top with S to 10
                else if (cm >= 1)
                {
                    int takenM = TakeUpTo(_m, cm); // drain all current M
                    int needS = Math.Max(0, 10 - takenM);
                    TakeUpTo(_s, needS);
                }
                // Rule 6: only S remain -> drain all S
                else
                {
                    DrainAll(_s);
                }

                // ---- finalize ----
                int remaining = Interlocked.Add(ref _total, -removed);
                if (remaining > 0) _nonEmpty.Release();

                BatchDequeued?.Invoke(batch);
                return batch;
            }
        }
    }
}
