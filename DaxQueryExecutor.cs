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

public sealed class PowerBIModelWarmer
{
    public sealed record Settings(
        string XmlaEndpoint,          // e.g. "powerbi://api.powerbi.com/v1.0/myorg/Workspace"
        string DatabaseName,          // dataset name
        string? AccessToken = null,   // AAD token for the workspace; if null, use integrated
        int MaxConcurrentQueries = 6, // degree of parallelism
        TimeSpan CommandTimeout = default,      // default 180s
        TimeSpan PerQueryTimeout = default,     // default 60s
        int MaxRetries = 3,                     // retry attempts per query
        TimeSpan RetryBaseDelay = default       // default 750ms
    )
    {
        public TimeSpan CommandTimeoutOrDefault => CommandTimeout == default ? TimeSpan.FromSeconds(180) : CommandTimeout;
        public TimeSpan PerQueryTimeoutOrDefault => PerQueryTimeout == default ? TimeSpan.FromSeconds(60) : PerQueryTimeout;
        public TimeSpan RetryBaseDelayOrDefault => RetryBaseDelay == default ? TimeSpan.FromMilliseconds(750) : RetryBaseDelay;
    }

    public sealed record Result(
        int Submitted, int Succeeded, int Failed, TimeSpan Elapsed,
        IReadOnlyList<(string dax, int attempt, TimeSpan duration, string? error)> Details
    );

    private readonly Settings _cfg;

    public PowerBIModelWarmer(Settings cfg) => _cfg = cfg;

    /// <summary>
    /// Execute warm-up DAX playlist
    /// </summary>
    public async Task<Result> WarmAsync(IEnumerable<string> daxPlaylist, CancellationToken ct)
    {
        var queries = daxPlaylist.ToList();
        if (queries.Count == 0)
            return new Result(0, 0, 0, TimeSpan.Zero, Array.Empty<(string, int, TimeSpan, string?)>());

        var swAll = Stopwatch.StartNew();
        var detail = new List<(string, int, TimeSpan, string?)>();
        var succeeded = 0;
        var failed = 0;

        // Bounded concurrency dispatcher
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(queries.Count)
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

        foreach (var dax in queries)
            await channel.Writer.WriteAsync(dax, ct).ConfigureAwait(false);
        channel.Writer.Complete();

        await Task.WhenAll(workers).ConfigureAwait(false);
        swAll.Stop();

        return new Result(queries.Count, succeeded, failed, swAll.Elapsed, detail);
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
                // Exponential backoff + jitter
                var delay = _cfg.RetryBaseDelayOrDefault * Math.Pow(2, attempts - 1);
                var jitterMs = Random.Shared.Next(100, 500);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(10_000, delay.TotalMilliseconds) + jitterMs), ct);
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

        // force execution
        using var rdr = await Task.Run(() => cmd.ExecuteReader(CommandBehavior.CloseConnection), ct).ConfigureAwait(false);
        if (rdr.FieldCount > 0 && rdr.Read()) { /* we just touch it */ }
    }
}
