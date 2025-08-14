public sealed class CategoryQueue
    {
        // One queue per category
        private readonly ConcurrentQueue<EventSet> _s = new();
        private readonly ConcurrentQueue<EventSet> _m = new();
        private readonly ConcurrentQueue<EventSet> _l = new();
        private readonly ConcurrentQueue<EventSet> _x = new();

        // Coordination
        private readonly SemaphoreSlim _nonEmpty = new(0); // one-bit signal
        private readonly object _gate = new();             // atomic selection/drain
        private int _total;                                 // total items (best-effort)

        // Hooks (optional)
        public event Action<EventSet>? ItemEnqueued;
        public event Action<IReadOnlyList<EventSet>>? BatchDequeued;

        // ------------------- Producers -------------------
        public void Enqueue(EventSet item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            switch (item.Category)
            {
                case PartitionCategory.Small:      _s.Enqueue(item); break;
                case PartitionCategory.Medium:     _m.Enqueue(item); break;
                case PartitionCategory.Large:      _l.Enqueue(item); break;
                case PartitionCategory.ExtraLarge: _x.Enqueue(item); break;
                default: throw new ArgumentOutOfRangeException(nameof(item.Category));
            }

            ItemEnqueued?.Invoke(item);

            // wake consumer only on 0 -> 1 transition
            if (Interlocked.Increment(ref _total) == 1)
                _nonEmpty.Release();
        }

        public void EnqueueBatch(IEnumerable<EventSet> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            foreach (var it in items) Enqueue(it);
        }

        public (int small, int medium, int large, int extraLarge, int total) GetCounts()
        {
            var s = _s.Count; var m = _m.Count; var l = _l.Count; var x = _x.Count;
            return (s, m, l, x, s + m + l + x);
        }

        // ------------------- Consumer -------------------
        public async Task<IReadOnlyList<EventSet>> DequeueBatchAsync(CancellationToken ct)
        {
            await _nonEmpty.WaitAsync(ct).ConfigureAwait(false);

            var batch = new List<EventSet>(16);
            int removed = 0;

            lock (_gate)
            {
                // Nothing to do (rare race)
                if (Volatile.Read(ref _total) == 0)
                    return Array.Empty<EventSet>();

                // helpers (used only under lock)
                static int Count(ConcurrentQueue<EventSet> q) => q.Count;

                bool TakeN(ConcurrentQueue<EventSet> q, int n)
                {
                    for (int i = 0; i < n; i++)
                    {
                        if (!q.TryDequeue(out var e)) return false; // guarded by Count normally
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

                // snapshot counts (single consumer => consistent enough for decisions)
                int cx = Count(_x);
                int cl = Count(_l);
                int cm = Count(_m);
                int cs = Count(_s);

                // ---------------- RULES (strict order) ----------------

                // 1) XL exclusive
                if (cx >= 1)
                {
                    TakeN(_x, 1);
                }
                // 2) 2×L exclusive
                else if (cl >= 2)
                {
                    TakeN(_l, 2);
                }
                // 3) 1×L + up to 5×M (no S here)
                else if (cl == 1 && cm >= 1)
                {
                    TakeN(_l, 1);
                    int takeM = Math.Min(5, cm);
                    TakeN(_m, takeM);
                }
                // 4) 10×M
                else if (cm >= 10)
                {
                    TakeN(_m, 10);
                }
                // 5) 1..9 M  -> all M + top with S to 10
                else if (cm >= 1)
                {
                    int takenM = TakeUpTo(_m, cm); // drain all current M
                    int needS  = Math.Max(0, 10 - takenM);
                    TakeUpTo(_s, needS);
                }
                // 6) only S remain  -> drain all S
                else
                {
                    DrainAll(_s);
                }

                // finalize counters & signal
                int remaining = Interlocked.Add(ref _total, -removed);
                if (remaining < 0)
                {
                    // defensive clamp (should not happen, but safe)
                    Interlocked.Exchange(ref _total, 0);
                    remaining = 0;
                }
                if (remaining > 0)
                    _nonEmpty.Release();

                BatchDequeued?.Invoke(batch);
                return batch;
            }
        }
    }
