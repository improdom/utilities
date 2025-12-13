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
    /// Loads MRV partition mapping from SQL Server and provides
    /// fast in-memory filtering by:
    ///   - rm_risk_measure_name
    ///   - scenario_group
    ///   - mdst_description
    ///
    /// Usage:
    ///   var cache = new MrvPartitionCache(connString);
    ///   await cache.LoadAsync();
    ///   var parts1 = cache.GetPartitions(measureName: "TVChange");
    ///   var parts2 = cache.GetPartitions(measureName: "TVChange", scenarioGroup: "Equity");
    ///   var parts3 = cache.GetPartitions(measureName: "TVChange", scenarioGroup: "Equity", mdstDescription: "CASH_COLL");
    /// </summary>
    public sealed class MrvPartitionCache
    {
        // --- CONFIGURE THIS QUERY FOR YOUR ENVIRONMENT -----------------------
        //
        // You can adjust schemas / table names here if needed.
        private const string DefaultQuery = @"
SELECT  
      a.mrv_partition
    , a.fact_table_name
    , b.rm_risk_measure_name
    , c.scenario_group
    , d.mdst_description
FROM [config].[risk_fact_partitions]   AS a
JOIN [config].[risk_measure_seg]       AS b ON a.rm_seg = b.rm_seg
JOIN [config].[scenario_group_seg]     AS c ON a.sg_seg = c.sg_seg
JOIN [config].[curve_quality_seg]      AS d ON a.cq_seg = d.cq_seg;
";
        // --------------------------------------------------------------------

        private readonly string _connectionString;
        private readonly string _queryText;

        // Immutable snapshot of the cache so reads are lock-free
        private volatile CacheSnapshot? _snapshot;

        public MrvPartitionCache(string connectionString, string? queryText = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _queryText = string.IsNullOrWhiteSpace(queryText) ? DefaultQuery : queryText;
        }

        /// <summary>
        /// Represents an immutable snapshot of all lookup indexes.
        /// </summary>
        private sealed class CacheSnapshot
        {
            public CacheSnapshot(
                Dictionary<string, HashSet<long>> byMeasure,
                Dictionary<string, HashSet<long>> byScenario,
                Dictionary<string, HashSet<long>> byMdst)
            {
                ByMeasure = byMeasure;
                ByScenario = byScenario;
                ByMdst = byMdst;
            }

            public Dictionary<string, HashSet<long>> ByMeasure { get; }
            public Dictionary<string, HashSet<long>> ByScenario { get; }
            public Dictionary<string, HashSet<long>> ByMdst { get; }
        }

        /// <summary>
        /// Executes the SQL query and builds the in-memory cache.
        /// Call this once at startup and then whenever config changes.
        /// </summary>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            var byMeasure = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
            var byScenario = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
            var byMdst = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(_queryText, conn)
            {
                CommandType = CommandType.Text
            })
            {
                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

                using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken)
                                             .ConfigureAwait(false))
                {
                    int ordPartition = reader.GetOrdinal("mrv_partition");
                    int ordMeasure   = reader.GetOrdinal("rm_risk_measure_name");
                    int ordScenario  = reader.GetOrdinal("scenario_group");
                    int ordMdst      = reader.GetOrdinal("mdst_description");

                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        long partition = reader.GetInt64(ordPartition);
                        string measure = reader.IsDBNull(ordMeasure)  ? string.Empty : reader.GetString(ordMeasure).Trim();
                        string scenario = reader.IsDBNull(ordScenario) ? string.Empty : reader.GetString(ordScenario).Trim();
                        string mdst = reader.IsDBNull(ordMdst)        ? string.Empty : reader.GetString(ordMdst).Trim();

                        if (!string.IsNullOrEmpty(measure))
                        {
                            if (!byMeasure.TryGetValue(measure, out var set))
                            {
                                set = new HashSet<long>();
                                byMeasure[measure] = set;
                            }
                            set.Add(partition);
                        }

                        if (!string.IsNullOrEmpty(scenario))
                        {
                            if (!byScenario.TryGetValue(scenario, out var set))
                            {
                                set = new HashSet<long>();
                                byScenario[scenario] = set;
                            }
                            set.Add(partition);
                        }

                        if (!string.IsNullOrEmpty(mdst))
                        {
                            if (!byMdst.TryGetValue(mdst, out var set))
                            {
                                set = new HashSet<long>();
                                byMdst[mdst] = set;
                            }
                            set.Add(partition);
                        }
                    }
                }
            }

            // Publish new snapshot atomically
            _snapshot = new CacheSnapshot(byMeasure, byScenario, byMdst);
        }

        /// <summary>
        /// Returns mrv_partitions that match the provided filters.
        ///
        /// Semantics:
        /// - All non-null parameters are combined with AND.
        /// - If a given filter value does not exist in the cache, an empty result is returned.
        /// - If all parameters are null, an empty result is returned.
        ///
        /// Examples:
        ///   GetPartitions("TVChange", null, null)                       -> all partitions with measure = TVChange
        ///   GetPartitions("TVChange", "Equity", null)                   -> measure + scenario
        ///   GetPartitions("TVChange", "Equity", "CASH_COLL")            -> measure + scenario + mdst
        ///   GetPartitions(null, "Equity", null)                         -> all Equity partitions (any measure)
        ///   GetPartitions(null, null, "CASH_COLL")                      -> all CASH_COLL partitions (any measure)
        /// </summary>
        public IReadOnlyCollection<long> GetPartitions(
            string? measureName = null,
            string? scenarioGroup = null,
            string? mdstDescription = null)
        {
            var snapshot = _snapshot ?? throw new InvalidOperationException("Cache has not been loaded. Call LoadAsync() first.");

            var sets = new List<HashSet<long>>(capacity: 3);

            if (!string.IsNullOrWhiteSpace(measureName))
            {
                if (!snapshot.ByMeasure.TryGetValue(measureName.Trim(), out var set))
                    return Array.Empty<long>();
                sets.Add(set);
            }

            if (!string.IsNullOrWhiteSpace(scenarioGroup))
            {
                if (!snapshot.ByScenario.TryGetValue(scenarioGroup.Trim(), out var set))
                    return Array.Empty<long>();
                sets.Add(set);
            }

            if (!string.IsNullOrWhiteSpace(mdstDescription))
            {
                if (!snapshot.ByMdst.TryGetValue(mdstDescription.Trim(), out var set))
                    return Array.Empty<long>();
                sets.Add(set);
            }

            if (sets.Count == 0)
                return Array.Empty<long>(); // no filters -> nothing to do

            // Optimization: intersect starting from the smallest set
            sets.Sort((a, b) => a.Count.CompareTo(b.Count));

            var result = new HashSet<long>(sets[0]);
            for (int i = 1; i < sets.Count; i++)
                result.IntersectWith(sets[i]);

            return result.ToArray();
        }
    }
}
