using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient; // preferred
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YourNamespace
{
    public sealed class MrvPartitionLookupCache
    {
        // If you have a materialized map table, use it (FAST).
        // If you don't, set UseMaterializedMapTable = false (it will use the join query).
        public bool UseMaterializedMapTable { get; init; } = true;

        // Optional TTL (e.g., TimeSpan.FromMinutes(30)). Set to null for "cache forever".
        public TimeSpan? CacheTtl { get; init; } = TimeSpan.FromHours(1);

        // Command timeout for SQL
        public int CommandTimeoutSeconds { get; init; } = 60;

        private readonly string _connectionString;

        // Cache: key -> lazy task that loads once even under concurrency
        private readonly ConcurrentDictionary<CacheKey, CacheEntry> _cache = new();

        public MrvPartitionLookupCache(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public sealed class MrvPartitionInfo
        {
            public long Partition { get; init; }
            public long RmSeg { get; init; }
            public string FactTable { get; init; } = string.Empty;

            public string Measure { get; init; } = string.Empty;
            public string Scenario { get; init; } = string.Empty;
            public string Mdst { get; init; } = string.Empty;
        }

        private readonly record struct CacheKey(string? Measure, string? Scenario, string? Mdst);

        private sealed class CacheEntry
        {
            public CacheEntry(Lazy<Task<IReadOnlyList<MrvPartitionInfo>>> loader, DateTimeOffset createdUtc)
            {
                Loader = loader;
                CreatedUtc = createdUtc;
            }

            public Lazy<Task<IReadOnlyList<MrvPartitionInfo>>> Loader { get; }
            public DateTimeOffset CreatedUtc { get; }
        }

        private static string? Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return s.Trim();
        }

        /// <summary>
        /// Gets partitions for the provided filters. Results are cached per unique filter combination.
        /// Filters are AND'ed together (only the provided filters apply).
        /// </summary>
        public Task<IReadOnlyList<MrvPartitionInfo>> GetAsync(
            string? rmRiskMeasureName = null,
            string? scenarioGroup = null,
            string? mdstDescription = null,
            CancellationToken ct = default)
        {
            var key = new CacheKey(
                Normalize(rmRiskMeasureName),
                Normalize(scenarioGroup),
                Normalize(mdstDescription)
            );

            // Optional TTL eviction
            if (CacheTtl is not null &&
                _cache.TryGetValue(key, out var existing) &&
                DateTimeOffset.UtcNow - existing.CreatedUtc > CacheTtl.Value)
            {
                _cache.TryRemove(key, out _);
            }

            var entry = _cache.GetOrAdd(
                key,
                k => new CacheEntry(
                    new Lazy<Task<IReadOnlyList<MrvPartitionInfo>>>(
                        () => LoadFromSqlAsync(k, ct),
                        LazyThreadSafetyMode.ExecutionAndPublication),
                    DateTimeOffset.UtcNow));

            return entry.Loader.Value;
        }

        /// <summary>Optional: clear all cached results.</summary>
        public void Clear() => _cache.Clear();

        private async Task<IReadOnlyList<MrvPartitionInfo>> LoadFromSqlAsync(CacheKey key, CancellationToken ct)
        {
            // NOTE: If you have a materialized mapping table, this query is extremely fast with indexes.
            // Recommended indexes:
            //   (rm_risk_measure_name, scenario_group, mdst_description) INCLUDE (mrv_partition, rm_seg, fact_table_name)
            //
            // If not, we fall back to join + filters, still MUCH cheaper than full scan.
            string sql = UseMaterializedMapTable
                ? @"
SELECT
      rm_seg,
      mrv_partition,
      fact_table_name,
      rm_risk_measure_name,
      scenario_group,
      mdst_description
FROM config.mrv_partition_map
WHERE (@measure IS NULL OR rm_risk_measure_name = @measure)
  AND (@scenario IS NULL OR scenario_group = @scenario)
  AND (@mdst IS NULL OR mdst_description = @mdst);
"
                : @"
SELECT
      a.rm_seg,
      a.mrv_partition,
      a.fact_table_name,
      b.rm_risk_measure_name,
      c.scenario_group,
      d.mdst_description
FROM [config].[risk_fact_partitions] a
JOIN [config].[risk_measure_seg] b   ON a.rm_seg = b.rm_seg
JOIN [config].[scenario_group_seg] c ON a.sg_seg = c.sg_seg
JOIN [config].[curve_quality_seg] d  ON a.cq_seg = d.cq_seg
WHERE (@measure IS NULL OR b.rm_risk_measure_name = @measure)
  AND (@scenario IS NULL OR c.scenario_group = @scenario)
  AND (@mdst IS NULL OR d.mdst_description = @mdst)
GROUP BY
      a.rm_seg,
      a.mrv_partition,
      a.fact_table_name,
      b.rm_risk_measure_name,
      c.scenario_group,
      d.mdst_description;
";

            var rows = new List<MrvPartitionInfo>(capacity: 64);

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn)
            {
                CommandType = CommandType.Text,
                CommandTimeout = CommandTimeoutSeconds
            };

            cmd.Parameters.Add(new SqlParameter("@measure", SqlDbType.VarChar, 200) { Value = (object?)key.Measure ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@scenario", SqlDbType.VarChar, 200) { Value = (object?)key.Scenario ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@mdst", SqlDbType.VarChar, 200) { Value = (object?)key.Mdst ?? DBNull.Value });

            await conn.OpenAsync(ct).ConfigureAwait(false);

            // SequentialAccess is good when rows can be large; harmless here.
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct).ConfigureAwait(false);

            int ordRmSeg = reader.GetOrdinal("rm_seg");
            int ordPart = reader.GetOrdinal("mrv_partition");
            int ordFact = reader.GetOrdinal("fact_table_name");
            int ordMeasure = reader.GetOrdinal("rm_risk_measure_name");
            int ordScenario = reader.GetOrdinal("scenario_group");
            int ordMdst = reader.GetOrdinal("mdst_description");

            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                rows.Add(new MrvPartitionInfo
                {
                    RmSeg = reader.GetInt64(ordRmSeg),
                    Partition = reader.GetInt64(ordPart),
                    FactTable = reader.GetString(ordFact).Trim(),
                    Measure = reader.GetString(ordMeasure).Trim(),
                    Scenario = reader.GetString(ordScenario).Trim(),
                    Mdst = reader.GetString(ordMdst).Trim(),
                });
            }

            return rows;
        }
    }
}
