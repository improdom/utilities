using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YourNamespace
{
    /// <summary>
    /// Loads MRV mappings from SQL Server and offers fast filtering by:
    ///   - rm_risk_measure_name
    ///   - scenario_group
    ///   - mdst_description
    ///
    /// Returns full row objects containing:
    ///   - rm_seg
    ///   - mrv_partition
    ///   - fact_table_name
    /// </summary>
    public sealed class MrvPartitionCache
    {
        private const string DefaultQuery = @"
SELECT DISTINCT
      a.rm_seg
    , a.mrv_partition
    , a.fact_table_name
    , b.rm_risk_measure_name
    , c.scenario_group
    , d.mdst_description
FROM [config].[risk_fact_partitions] a
JOIN [config].[risk_measure_seg] b       ON a.rm_seg = b.rm_seg
JOIN [config].[scenario_group_seg] c     ON a.sg_seg = c.sg_seg
JOIN [config].[curve_quality_seg] d      ON a.cq_seg = d.cq_seg;
";

        private readonly string _connectionString;
        private readonly string _query;

        private volatile CacheSnapshot? _snapshot;

        public MrvPartitionCache(string connectionString, string? queryOverride = null)
        {
            _connectionString = connectionString;
            _query = queryOverride ?? DefaultQuery;
        }

        // Represents a single row of mapping data
        public sealed class MrvPartitionInfo
        {
            public long Partition { get; init; }
            public long RmSeg { get; init; }
            public string FactTable { get; init; } = string.Empty;
            public string Measure { get; init; } = string.Empty;
            public string Scenario { get; init; } = string.Empty;
            public string Mdst { get; init; } = string.Empty;
        }

        private sealed class CacheSnapshot
        {
            public IReadOnlyList<MrvPartitionInfo> Rows { get; }
            public Dictionary<string, HashSet<int>> ByMeasure { get; }
            public Dictionary<string, HashSet<int>> ByScenario { get; }
            public Dictionary<string, HashSet<int>> ByMdst { get; }

            public CacheSnapshot(
                IReadOnlyList<MrvPartitionInfo> rows,
                Dictionary<string, HashSet<int>> byMeasure,
                Dictionary<string, HashSet<int>> byScenario,
                Dictionary<string, HashSet<int>> byMdst)
            {
                Rows = rows;
                ByMeasure = byMeasure;
                ByScenario = byScenario;
                ByMdst = byMdst;
            }
        }

        /// <summary>
        /// Loads & caches all MRV mapping rows from SQL.
        /// </summary>
        public async Task LoadAsync(CancellationToken ct = default)
        {
            var rows = new List<MrvPartitionInfo>(capacity: 4096);

            var byMeasure = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            var byScenario = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            var byMdst = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(_query, conn);
            await conn.OpenAsync(ct);

            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

            while (await reader.ReadAsync(ct))
            {
                var row = new MrvPartitionInfo
                {
                    RmSeg = reader.GetInt64(reader.GetOrdinal("rm_seg")),
                    Partition = reader.GetInt64(reader.GetOrdinal("mrv_partition")),
                    FactTable = reader.GetString(reader.GetOrdinal("fact_table_name")).Trim(),
                    Measure = reader.GetString(reader.GetOrdinal("rm_risk_measure_name")).Trim(),
                    Scenario = reader.GetString(reader.GetOrdinal("scenario_group")).Trim(),
                    Mdst = reader.GetString(reader.GetOrdinal("mdst_description")).Trim()
                };

                int index = rows.Count;
                rows.Add(row);

                // Build inverted indexes
                if (!byMeasure.TryGetValue(row.Measure, out var mset))
                    byMeasure[row.Measure] = mset = new HashSet<int>();
                mset.Add(index);

                if (!byScenario.TryGetValue(row.Scenario, out var sset))
                    byScenario[row.Scenario] = sset = new HashSet<int>();
                sset.Add(index);

                if (!byMdst.TryGetValue(row.Mdst, out var dset))
                    byMdst[row.Mdst] = dset = new HashSet<int>();
                dset.Add(index);
            }

            _snapshot = new CacheSnapshot(rows, byMeasure, byScenario, byMdst);
        }

        /// <summary>
        /// Filters MRV partitions by any combination of:
        ///   - measureName
        ///   - scenarioGroup
        ///   - mdstDescription
        /// Returned objects include rm_seg and fact_table_name.
        /// </summary>
        public IReadOnlyList<MrvPartitionInfo> Get(
            string? measureName = null,
            string? scenarioGroup = null,
            string? mdstDescription = null)
        {
            var snap = _snapshot ?? throw new InvalidOperationException("Cache not loaded.");

            var sets = new List<HashSet<int>>(3);

            if (!string.IsNullOrWhiteSpace(measureName))
            {
                if (!snap.ByMeasure.TryGetValue(measureName.Trim(), out var s))
                    return Array.Empty<MrvPartitionInfo>();
                sets.Add(s);
            }

            if (!string.IsNullOrWhiteSpace(scenarioGroup))
            {
                if (!snap.ByScenario.TryGetValue(scenarioGroup.Trim(), out var s))
                    return Array.Empty<MrvPartitionInfo>();
                sets.Add(s);
            }

            if (!string.IsNullOrWhiteSpace(mdstDescription))
            {
                if (!snap.ByMdst.TryGetValue(mdstDescription.Trim(), out var s))
                    return Array.Empty<MrvPartitionInfo>();
                sets.Add(s);
            }

            if (sets.Count == 0)
                return Array.Empty<MrvPartitionInfo>(); // no filters

            // Intersect smallest-to-largest
            sets.Sort((a, b) => a.Count.CompareTo(b.Count));

            var resultIdx = new HashSet<int>(sets[0]);
            for (int i = 1; i < sets.Count; i++)
                resultIdx.IntersectWith(sets[i]);

            // materialize row objects
            return resultIdx.Select(i => snap.Rows[i]).ToList();
        }
    }
}
