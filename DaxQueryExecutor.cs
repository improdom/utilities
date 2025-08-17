using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular; // optional: only used for heuristic generation

public sealed class PowerBIModelWarmer
{
    public sealed record Settings(
        string XmlaEndpoint,          // e.g. "powerbi://api.powerbi.com/v1.0/myorg/WorkspaceName"
        string DatabaseName,          // dataset name
        string? AccessToken = null,   // AAD token for the workspace; if null, rely on integrated creds
        int MaxConcurrentQueries = 6, // how many warm-up queries at once
        TimeSpan CommandTimeout = default,      // default 180s
        TimeSpan PerQueryTimeout = default,     // default 60s
        int MaxRetries = 3,                     // retry each query up to N times
        TimeSpan RetryBaseDelay = default,      // default 750ms
        bool UseHeuristicIfNoPlaylist = true,   // fallback planner
        int HeuristicTopMeasures = 15,          // limit
        int HeuristicTopDimCombos = 3           // how many dimension combos per measure
    )
    {
        public TimeSpan CommandTimeoutOrDefault => CommandTimeout == default ? TimeSpan.FromSeconds(180) : CommandTimeout;
        public TimeSpan PerQueryTimeoutOrDefault => PerQueryTimeout == default ? TimeSpan.FromSeconds(60) : PerQueryTimeout;
        public TimeSpan RetryBaseDelayOrDefault => RetryBaseDelay == default ? TimeSpan.FromMilliseconds(750) : RetryBaseDelay;
    }

    public sealed record WarmupPlan(
        IReadOnlyList<string> DaxPlaylist,  // preferred explicit playlist
        bool Generated                    = false
    );

    public sealed record Result(
        int Submitted, int Succeeded, int Failed, TimeSpan Elapsed,
        IReadOnlyList<(string dax, int attempt, TimeSpan duration, string? error)> Details
    );

    private readonly Settings _cfg;

    public PowerBIModelWarmer(Settings cfg) => _cfg = cfg;

    // Entry point: give me a playlist, or pass null to use heuristics if enabled
    public async Task<Result> WarmAsync(
        IEnumerable<string>? daxPlaylist,
        CancellationToken ct)
    {
        var swAll = Stopwatch.StartNew();

        WarmupPlan plan;
        if (daxPlaylist != null && daxPlaylist.Any())
        {
            plan = new WarmupPlan(daxPlaylist.ToList(), Generated: false);
        }
        else if (_cfg.UseHeuristicIfNoPlaylist)
        {
            plan = new WarmupPlan(await BuildHeuristicPlaylistAsync(ct), Generated: true);
        }
        else
        {
            return new Result(0, 0, 0, TimeSpan.Zero, Array.Empty<(string, int, TimeSpan, string?)>());
        }

        var detail = new List<(string dax, int attempt, TimeSpan duration, string? error)>();
        var succeeded = 0;
        var failed = 0;

        // Bounded-concurrency dispatcher using Channels
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(plan.DaxPlaylist.Count)
        {
            SingleWriter = true, SingleReader = false, FullMode = BoundedChannelFullMode.Wait
        });

        var workers = Enumerable.Range(0, _cfg.MaxConcurrentQueries)
            .Select(_ => Task.Run(async () =>
            {
                while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    if (!channel.Reader.TryRead(out var dax)) continue;
                    var (ok, attempts, dur, err) = await ExecuteWithRetryAsync(dax, ct).ConfigureAwait(false);
                    lock (detail) detail.Add((dax, attempts, dur, err));
                    if (ok) Interlocked.Increment(ref succeeded); else Interlocked.Increment(ref failed);
                }
            }, ct)).ToList();

        foreach (var dax in plan.DaxPlaylist)
            await channel.Writer.WriteAsync(dax, ct).ConfigureAwait(false);
        channel.Writer.Complete();

        await Task.WhenAll(workers).ConfigureAwait(false);

        swAll.Stop();
        return new Result(plan.DaxPlaylist.Count, succeeded, failed, swAll.Elapsed, detail);
    }

    // ---------- Internals ----------

    private async Task<(bool ok, int attempts, TimeSpan duration, string? error)> ExecuteWithRetryAsync(string dax, CancellationToken ct)
    {
        var attempts = 0;
        var sw = Stopwatch.StartNew();
        Exception? last = null;

        while (attempts < _cfg.MaxRetries)
        {
            attempts++;
            try
            {
                await ExecuteDaxAsync(dax, ct).ConfigureAwait(false);
                sw.Stop();
                return (true, attempts, sw.Elapsed, null);
            }
            catch (Exception ex) when (attempts < _cfg.MaxRetries)
            {
                last = ex;
                // backoff with jitter
                var delay = _cfg.RetryBaseDelayOrDefault * Math.Pow(2, attempts - 1);
                var jitterMs = Random.Shared.Next(100, 500);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(10_000, delay.TotalMilliseconds) + jitterMs), ct)
                          .ConfigureAwait(false);
            }
            catch (Exception final)
            {
                sw.Stop();
                return (false, attempts, sw.Elapsed, final.Message);
            }
        }

        sw.Stop();
        return (false, attempts, sw.Elapsed, last?.Message ?? "Unknown error");
    }

    private async Task ExecuteDaxAsync(string dax, CancellationToken ct)
    {
        using var conn = new AdomdConnection($"Data Source={_cfg.XmlaEndpoint};Catalog={_cfg.DatabaseName};");
        if (!string.IsNullOrWhiteSpace(_cfg.AccessToken))
            conn.AccessToken = _cfg.AccessToken;

        conn.CommandTimeout = (int)_cfg.CommandTimeoutOrDefault.TotalSeconds;
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = dax;
        cmd.CommandTimeout = (int)_cfg.PerQueryTimeoutOrDefault.TotalSeconds;

        // We don’t need results; just iterate once to force execution
        using var rdr = await Task.Run(() => cmd.ExecuteReader(CommandBehavior.CloseConnection), ct).ConfigureAwait(false);
        // Read a single row/page to materialize; for larger result sets, TOPN in the DAX should keep it small
        if (rdr.FieldCount > 0 && rdr.Read()) { /* noop */ }

        // Reader/connection disposed by using scopes
    }

    // ---------- Heuristic playlist (optional but handy) ----------

    private async Task<IReadOnlyList<string>> BuildHeuristicPlaylistAsync(CancellationToken ct)
    {
        // Open TOM to introspect model (measures & likely slicers)
        using var server = new Server();
        if (!string.IsNullOrWhiteSpace(_cfg.AccessToken))
            server.Connect($"DataSource={_cfg.XmlaEndpoint}", _cfg.AccessToken);
        else
            server.Connect($"DataSource={_cfg.XmlaEndpoint}");

        var db = server.Databases[_cfg.DatabaseName];
        var model = db.Model;

        // Pick top measures (by simple heuristics: referenced count, name hints)
        var measures = model.Tables.SelectMany(t => t.Measures).ToList();
        var topMeasures = measures
            .OrderByDescending(m => ScoreMeasure(m))
            .Take(Math.Max(5, _cfg.HeuristicTopMeasures))
            .ToList();

        // Identify common slicer dimensions if present
        var dims = model.Tables.Where(t => t.TableType == TableType.Dimension).ToList();
        var favoriteDimCols = FindLikelySlicers(dims);

        var dax = new List<string>();

        // 1) Touch important tables with light GROUPBY to warm dictionaries/segments
        foreach (var t in model.Tables.Where(t => t.IsHidden == false && t.TableType == TableType.Regular))
        {
            // Use a robust small scan that forces storage engine to touch the table
            // We keep TOPN small to avoid large result transfer.
            var tableName = DaxId(t.Name);
            dax.Add($@"
EVALUATE
TOPN(10,
    SUMMARIZECOLUMNS(
        {FirstN(favoriteDimCols, 2)},
        ""_rows_"", COUNTROWS({tableName})
    )
)");
        }

        // 2) Measures across slicers (the best “realistic” warm-up)
        foreach (var m in topMeasures)
        {
            var mName = $"{DaxId(m.Table.Name)}[{m.Name}]";
            foreach (var slicerGroup in favoriteDimCols.Chunk(Math.Max(1, _cfg.HeuristicTopDimCombos)))
            {
                var slicers = string.Join(",\n        ", slicerGroup);
                dax.Add($@"
EVALUATE
TOPN(50,
    SUMMARIZECOLUMNS(
        {slicers},
        ""_m_"", {mName}
    )
)");
            }
        }

        server.Disconnect();
        await Task.CompletedTask;
        return dax.Distinct().ToList();

        // ---- local helpers ----
        static string DaxId(string name) => $"'{name.Replace("'", "''")}'";

        static double ScoreMeasure(Measure m)
        {
            var name = m.Name.ToLowerInvariant();
            double score = 0;
            if (name.Contains("total") || name.Contains("pnl") || name.Contains("exposure") || name.Contains("amount"))
                score += 5;
            if (!m.IsHidden) score += 2;
            score += m.Expression?.Length / 200.0 ?? 0;
            return score;
        }

        static List<string> FindLikelySlicers(IEnumerable<Table> dims)
        {
            var cols = new List<string>();
            foreach (var d in dims.Where(d => !d.IsHidden))
            {
                foreach (var c in d.Columns.Where(c => !c.IsHidden))
                {
                    var cn = c.Name.ToLowerInvariant();
                    if (cn is "date" or "day" or "cob" or "book" or "desk" or "region" or "portfolio" or "product"
                        or "event_run_time_id" or "eventid" or "cob_date")
                    {
                        cols.Add($"{DaxId(d.Name)}[{c.Name}]");
                    }
                }
            }
            // Fallback: first visible key-ish column per dimension
            if (cols.Count == 0)
            {
                cols.AddRange(dims.Take(5)
                    .SelectMany(d => d.Columns.Where(c => !c.IsHidden).Take(1))
                    .Select(c => $"{DaxId(c.Table.Name)}[{c.Name}]"));
            }
            // Ensure at least one slicer to keep SUMMARIZECOLUMNS valid
            if (cols.Count == 0) cols.Add("'Date'[Date]");
            return cols.Distinct().Take(10).ToList();
        }

        static string FirstN(List<string> list, int n)
            => string.Join(", ", list.Take(Math.Max(1, Math.Min(n, list.Count))));
    }
}
