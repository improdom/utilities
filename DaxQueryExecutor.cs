using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.Extensions.Logging;

namespace Arc.PowerBI.Warmup
{
    public sealed class VertiPaqWarmupPlanner_Perf
    {
        private readonly ILogger _log;
        public VertiPaqWarmupPlanner_Perf(ILogger logger) => _log = logger;

        public sealed class Options
        {
            public string XmlaConnectionString { get; set; } = default!;
            public string ModelName { get; set; } = default!;
            public Func<Task<bool>>? IsRefreshInProgressAsync { get; set; } = null;

            public string RecentPartitionColumn { get; set; } = "'Date'[Date]";
            public int RecentPartitionCount { get; set; } = 1;

            public double ResidentRatioThreshold { get; set; } = 0.20;
            public int MaxColumnsPerTable { get; set; } = 5;
            public int MaxColumnsTotal { get; set; } = 24;
            public int MaxTablesPerRun { get; set; } = 10;
            public int ColdScanTopK { get; set; } = 400;

            public bool IncludeMeasureDependencies { get; set; } = true;
            public HashSet<string>? MeasuresWhitelist { get; set; } = null;
            public int MustIncludeMeasureColsCap { get; set; } = 8;

            public string[] DimensionPrefixes { get; set; } = new[] { "Dim", "Lookup", "Ref" };
            public string[] FactTables { get; set; } = Array.Empty<string>();
            public Func<string, bool>? ShouldSkipTable { get; set; } = null;
            public long VeryHighCardinalityThreshold { get; set; } = 1_000_000;

            public int MaxParallel { get; set; } = 3;
            public int CommandTimeoutSeconds { get; set; } = 60;

            public List<(string Table, string Column)> ExplicitAttributes { get; set; } = new();

            public bool UseSoftTimeTarget { get; set; } = true;
            public int SoftTimeTargetSeconds { get; set; } = 600;
            public int PilotBatchSize { get; set; } = 80;
            public int MinBatchSize { get; set; } = 50;
            public int MaxBatchSize { get; set; } = 800;
            public double BatchSizeSmoothing { get; set; } = 0.35;

            public bool WarmAllSecondaryKeys { get; set; } = false;
            public string SecondaryKeyColumn { get; set; } = "'Fact'[event_run_time_id]";
            public int SecondaryKeysBatchSize { get; set; } = 200;
            public int SecondaryKeysMaxBatches { get; set; } = 100;
        }

        private static readonly Dictionary<string, HashSet<(string Table, string Column)>> _measureDepsCache =
            new(StringComparer.OrdinalIgnoreCase);

        public async Task PlanAndExecuteWarmupAsync(Options o, CancellationToken ct = default)
        {
            _log.LogInformation("Warm-up start. Model={Model}, XMLA={Xmla}", o.ModelName, o.XmlaConnectionString);

            if (o.IsRefreshInProgressAsync != null)
            {
                try
                {
                    if (await o.IsRefreshInProgressAsync())
                    {
                        _log.LogWarning("Dataset refresh in progress. Skipping warm-up.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Refresh guard check failed; proceeding.");
                }
            }

            var targets = (o.ExplicitAttributes?.Count ?? 0) > 0
                ? CollapseToTargets(o.ExplicitAttributes!, o.MaxColumnsPerTable, o.MaxColumnsTotal, o.MaxTablesPerRun)
                : SelectTargetsDynamically(o);

            if (targets.Count == 0)
            {
                _log.LogInformation("No targets found (nothing cold enough).");
                return;
            }

            _log.LogInformation("Selected {ColCount} columns across {TableCount} tables for warm-up.",
                targets.Count, targets.Select(t => t.Table).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            foreach (var grp in targets.GroupBy(t => t.Table))
            {
                var table = grp.Key;
                if (o.ShouldSkipTable != null && o.ShouldSkipTable(table))
                {
                    _log.LogInformation("Skipping table per policy: {Table}", table);
                    continue;
                }

                var cols = grp.Select(x => x.Column).Distinct().Take(o.MaxColumnsPerTable).ToList();
                _log.LogInformation("Table {Table}: warming {ColCount} projected column(s): {Cols}",
                    table, cols.Count, string.Join(", ", cols));

                bool isFact = ((o.FactTables?.Length ?? 0) == 0)
                              ? !IsDimension(table, o)
                              : (o.FactTables?.Contains(table, StringComparer.OrdinalIgnoreCase) ?? false);

                if (isFact && o.WarmAllSecondaryKeys)
                {
                    _log.LogInformation("Table {Table}: starting adaptive ALL-run-IDs warm (soft target {Target}s).",
                        table, o.SoftTimeTargetSeconds);
                    await WarmFactAllRunsAdaptiveAsync(o, table, cols, ct);
                    _log.LogInformation("Table {Table}: adaptive ALL-run-IDs warm completed.", table);
                }
                else
                {
                    var dax = IsDimension(table, o)
                        ? BuildDimensionWarmupDax(table, cols)
                        : BuildFactWarmupDax(table, cols, o.RecentPartitionColumn, o.RecentPartitionCount);

                    await ExecuteSingleAsync(o, table, dax, ct);
                }
            }

            _log.LogInformation("Warm-up end.");
        }

        private static List<(string Table, string Column)> CollapseToTargets(
          IEnumerable<(string Table, string Column)> attrs, int maxPerTable, int maxTotal, int maxTables)
        {
            var perTable = attrs.GroupBy(x => x.Table)
                                .SelectMany(g => g.Take(maxPerTable))
                                .ToList();

            perTable = perTable.GroupBy(x => x.Table)
                               .Take(maxTables)
                               .SelectMany(g => g)
                               .ToList();

            if (perTable.Count > maxTotal)
                perTable = perTable.Take(maxTotal).ToList();
            return perTable;
        }

        // ---------- Target selection (now via DmvClient) ----------

        private List<(string Table, string Column)> SelectTargetsDynamically(Options o)
        {
            _log.LogInformation("Selecting targets dynamically...");
            var sw = Stopwatch.StartNew();

            var dmv = new DmvClient(o.XmlaConnectionString, _log);

            var residency = dmv.GetResidency();              // (Table, Column) -> (resident, total)
            _log.LogInformation("Residency snapshot: {Count} columns", residency.Count);

            var measureCols = o.IncludeMeasureDependencies
                ? (_measureDepsCache.TryGetValue(o.ModelName, out var cached)
                        ? cached
                        : (_measureDepsCache[o.ModelName] = dmv.GetMeasureColumnDependencies(o.MeasuresWhitelist)))
                : new HashSet<(string, string)>(new TupleComparer());
            _log.LogInformation("Measure dependency cols: {Count}", measureCols.Count);

            var cardinality = dmv.GetDictionarySizes();      // (Table, Column) -> dict bytes

            var scored = new List<Scored>();
            foreach (var kvp in residency)
            {
                var table = kvp.Key.Table;
                var col = kvp.Key.Column;
                var (res, tot) = kvp.Value;
                if (tot <= 0) continue;

                var ratio = (double)res / tot;
                if (ratio >= (1.0 - o.ResidentRatioThreshold)) continue;
                if (o.ShouldSkipTable != null && o.ShouldSkipTable(table)) continue;

                var isDim = IsDimension(table, o);
                var looksFK = EndsWithAny(col, "Id", "Key", "Code");
                var longText = ContainsAny(col, "Name", "Description");
                var refByMeasure = measureCols.Contains((table, col));

                double weight = (isDim ? 1.6 : 1.0) * (looksFK ? 1.3 : 1.0) * (!isDim && longText ? 0.7 : 1.0);
                if (isDim && longText && !refByMeasure &&
                    cardinality.TryGetValue((table, col), out var dictBytes) &&
                    dictBytes >= o.VeryHighCardinalityThreshold)
                {
                    weight *= 0.4;
                }
                if (refByMeasure) weight *= 2.5;

                double cold = 1.0 - ratio;
                scored.Add(new Scored(table, col, cold * weight, refByMeasure));
            }

            if (scored.Count == 0)
            {
                _log.LogInformation("No cold columns under threshold.");
                return new List<(string, string)>();
            }

            var trimmed = scored.OrderByDescending(s => s.Score).Take(o.ColdScanTopK).ToList();
            var mustInclude = trimmed.Where(s => s.RefByMeasure)
                                     .Take(o.MustIncludeMeasureColsCap)
                                     .Select(s => (s.Table, s.Column))
                                     .ToHashSet(new TupleComparer());

            var chosen = new List<(string Table, string Column)>();
            foreach (var g in trimmed.GroupBy(s => s.Table))
                chosen.AddRange(g.OrderByDescending(x => x.Score).Take(o.MaxColumnsPerTable).Select(x => (x.Table, x.Column)));
            foreach (var m in mustInclude) if (!chosen.Contains(m)) chosen.Add(m);

            chosen = chosen
                .GroupBy(x => x.Table)
                .OrderByDescending(g => g.Max(s => trimmed.First(t => t.Table == s.Table && t.Column == s.Column).Score))
                .Take(o.MaxTablesPerRun)
                .SelectMany(g => g)
                .ToList();

            if (chosen.Count > o.MaxColumnsTotal) chosen = chosen.Take(o.MaxColumnsTotal).ToList();

            sw.Stop();
            _log.LogInformation("Target selection finished in {Ms} ms. Chosen {Cols} columns across {Tabs} tables.",
                sw.ElapsedMilliseconds, chosen.Count, chosen.Select(c => c.Table).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            return chosen;
        }

        // ---------- Adaptive fact warm (unchanged) ----------
        private async Task WarmFactAllRunsAdaptiveAsync(Options o, string table, List<string> cols, CancellationToken ct)
        {
            var allKeys = GetSecondaryKeysForLatestCoB(o.XmlaConnectionString, o.RecentPartitionColumn, o.SecondaryKeyColumn);
            _log.LogInformation("Table {Table}: found {KeyCount} run IDs for latest CoB.", table, allKeys.Count);
            if (allKeys.Count == 0) return;

            int remaining = allKeys.Count;
            int cursor = 0;

            // Pilot batch estimate
            int pilotSize = Math.Min(Math.Max(o.PilotBatchSize, o.MinBatchSize), Math.Min(allKeys.Count, o.MaxBatchSize));
            var pilotKeys = allKeys.GetRange(0, pilotSize);
            var swPilot = Stopwatch.StartNew();
            await ExecuteOneBatchAsync(o, table, cols, pilotKeys, ct);
            swPilot.Stop();

            double msPerKey = Math.Max(1.0, swPilot.Elapsed.TotalMilliseconds / pilotKeys.Count);
            _log.LogInformation("Table {Table}: pilot batch {PilotSize} keys in {Ms} ms (~{MsPerKey:0.0} ms/key).",
                table, pilotKeys.Count, swPilot.ElapsedMilliseconds, msPerKey);

            cursor += pilotSize;
            remaining -= pilotSize;

            var swLoop = Stopwatch.StartNew();
            int batchSize = Math.Clamp(o.SecondaryKeysBatchSize, o.MinBatchSize, o.MaxBatchSize);
            int batchIndex = 0;

            while (remaining > 0)
            {
                double elapsed = swLoop.Elapsed.TotalSeconds;
                double target = o.UseSoftTimeTarget ? o.SoftTimeTargetSeconds : elapsed + 60;
                double secsLeft = Math.Max(1.0, target - elapsed);
                int suggested = (int)Math.Round((secsLeft * 1000.0 * Math.Max(1, o.MaxParallel)) / msPerKey / 4.0);

                int prev = batchSize;
                batchSize = (int)Math.Round((1 - o.BatchSizeSmoothing) * batchSize + o.BatchSizeSmoothing * suggested);
                batchSize = Math.Clamp(batchSize, o.MinBatchSize, o.MaxBatchSize);
                batchSize = Math.Min(batchSize, remaining);

                var keys = allKeys.GetRange(cursor, batchSize);
                var sw = Stopwatch.StartNew();
                await ExecuteOneBatchAsync(o, table, cols, keys, ct);
                sw.Stop();

                double thisMsPerKey = Math.Max(1.0, sw.Elapsed.TotalMilliseconds / keys.Count);
                msPerKey = 0.5 * msPerKey + 0.5 * thisMsPerKey;

                batchIndex++;
                cursor += batchSize;
                remaining -= batchSize;

                _log.LogInformation("Table {Table}: batch {Batch} size={Size} (prev {Prev}) took {Ms} ms (~{MsPerKey:0.0} ms/key). Remaining={Remain}. ETA≈{EtaMin:0.0} min.",
                    table, batchIndex, batchSize, prev, sw.ElapsedMilliseconds, thisMsPerKey, remaining, (remaining * msPerKey) / 1000.0 / 60.0);

                if (batchIndex >= o.SecondaryKeysMaxBatches)
                {
                    _log.LogWarning("Table {Table}: hit SecondaryKeysMaxBatches={MaxBatches}; stopping early.", table, o.SecondaryKeysMaxBatches);
                    break;
                }
            }
        }

        private async Task ExecuteOneBatchAsync(Options o, string table, List<string> cols, List<string> runKeys, CancellationToken ct)
        {
            var dax = BuildFactWarmAllRunsDax(table, cols, o.RecentPartitionColumn, o.SecondaryKeyColumn, runKeys);
            try
            {
                using var conn = new AdomdConnection(o.XmlaConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandTimeout = o.CommandTimeoutSeconds;
                cmd.CommandText = dax;
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read()) { if (ct.IsCancellationRequested) break; }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Table {Table}: batch execution failed (keys={Count}). Continuing.", table, runKeys.Count);
            }
            await Task.CompletedTask;
        }

        private static async Task ExecuteSingleAsync(Options o, string table, string dax, CancellationToken ct)
        {
            try
            {
                using var conn = new AdomdConnection(o.XmlaConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandTimeout = o.CommandTimeoutSeconds;
                cmd.CommandText = dax;
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read()) { if (ct.IsCancellationRequested) break; }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warmup][{DateTime.Now:O}] Error executing DAX for table '{table}': {ex.Message}");
            }
            await Task.CompletedTask;
        }

        // ---------- DAX builders ----------

        private static string BuildDimensionWarmupDax(string table, IEnumerable<string> columns)
        {
            var parts = columns.Select(c => $"DISTINCT('{table}'[{Escape(c)}])");
            var union = string.Join(",\n        ", parts);
            return $@"
EVALUATE
FILTER(
    UNION(
        {union}
    ),
    FALSE()
)";
        }

        private static string BuildFactWarmupDax(string table, IEnumerable<string> columns, string recentPartitionColumn, int recentCount)
        {
            var selects = string.Join(", ", columns.Take(3).Select(c => $"\"{Escape(c)}\", '{table}'[{Escape(c)}]"));
            return $@"
VAR __RecentKeys =
    TOPN({recentCount},
         VALUES({recentPartitionColumn}),
         {recentPartitionColumn}, DESC)
RETURN
EVALUATE
FILTER(
    GENERATE(
        __RecentKeys,
        CALCULATETABLE(
            TOPN(1,
                SELECTCOLUMNS('{table}', {selects})
            )
        )
    ),
    FALSE()
)";
        }

        private static List<string> GetSecondaryKeysForLatestCoB(string connStr, string primaryKey, string secondaryKey)
        {
            using var conn = new AdomdConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
VAR __LatestCob = TOPN(1, VALUES({primaryKey}), {primaryKey}, DESC)
RETURN
EVALUATE
CALCULATETABLE(
    SELECTCOLUMNS(VALUES({secondaryKey}), ""k"", {secondaryKey}),
    __LatestCob
)
ORDER BY [k] DESC";
            var keys = new List<string>();
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var v = rdr.GetValue(0);

                keys.Add(v is string s ? "\"" + s.Replace("\"", "\"\"") + "\"" : v.ToString()!);
            }
            return keys;
        }

        private static string BuildFactWarmAllRunsDax(
            string table, IEnumerable<string> columns, string primaryKey, string secondaryKey, IEnumerable<string> runKeysBatch)
        {
            var selects = string.Join(", ", columns.Take(3).Select(c => $"\"{Escape(c)}\", '{table}'[{Escape(c)}]"));
            var keyList = string.Join(",", runKeysBatch);
            return $@"
VAR __LatestCob = TOPN(1, VALUES({primaryKey}), {primaryKey}, DESC)
VAR __RunKeys   = {{ {keyList} }}
RETURN
EVALUATE
FILTER(
    GENERATE(
        __RunKeys,
        CALCULATETABLE(
            TOPN(1, SELECTCOLUMNS('{table}', {selects})),
            __LatestCob,
            {secondaryKey} IN __RunKeys
        )
    ),
    FALSE()
)";
        }

        // ---------- Utilities and structs (same as previous version) ----------
        private readonly struct Scored
        {
            public Scored(string table, string column, double score, bool refByMeasure)
            { Table = table; Column = column; Score = score; RefByMeasure = refByMeasure; }
            public string Table { get; }
            public string Column { get; }
            public double Score { get; }
            public bool RefByMeasure { get; }
        }

        private sealed class TupleComparer : IEqualityComparer<(string a, string b)>
        {
            public bool Equals((string a, string b) x, (string a, string b) y) =>
                string.Equals(x.a, y.a, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.b, y.b, StringComparison.OrdinalIgnoreCase);
            public int GetHashCode((string a, string b) obj) =>
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.a) ^
                (StringComparer.OrdinalIgnoreCase.GetHashCode(obj.b) * 397);
        }

        private static bool IsDimension(string table, Options o)
        {
            if (o.FactTables?.Length > 0)
                return !o.FactTables.Any(ft => string.Equals(ft, table, StringComparison.OrdinalIgnoreCase));
            return o.DimensionPrefixes.Any(p => table.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        private static bool EndsWithAny(string s, params string[] suffixes) =>
            suffixes.Any(suf => s.EndsWith(suf, StringComparison.OrdinalIgnoreCase));

        private static bool ContainsAny(string s, params string[] needles) =>
            needles.Any(n => s.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);

        private static string Escape(string name) => name.Replace("]", "]]");

        // Keep these from the previous file:
        // - TryParseTableColumn
        // - BuildDimensionWarmupDax
        // - BuildFactWarmupDax
        // - BuildFactWarmAllRunsDax
        // - ExecuteSingleAsync
        // - ExecuteOneBatchAsync
        // - WarmFactAllRunsAdaptiveAsync
        // - GetSecondaryKeysForLatestCoB
    }
}
