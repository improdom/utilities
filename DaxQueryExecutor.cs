using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.Extensions.Logging;

namespace Arc.PowerBI.Warmup
{
    /// <summary>
    /// Executes ONLY simple DMV queries (no joins, no aliases).
    /// Merges results in C# to provide residency ratios, dictionary sizes,
    /// table/column names, and measure dependencies.
    /// </summary>
    public sealed class DmvClient
    {
        private readonly string _connStr;
        private readonly ILogger _log;

        public DmvClient(string xmlaConnectionString, ILogger log)
        {
            _connStr = xmlaConnectionString ?? throw new ArgumentNullException(nameof(xmlaConnectionString));
            _log = log;
        }

        // Raw DMV rows (minimal fields needed)
        private sealed record SegRow(object TABLE_ID, object COLUMN_ID, bool IS_RESIDENT);
        private sealed record StcRow(object TABLE_ID, object COLUMN_ID, long DICTIONARY_SIZE);
        private sealed record TmColRow(object COLUMN_ID, object TABLE_ID, string NAME);
        private sealed record TmTabRow(object TABLE_ID, string NAME, bool IS_PRIVATE);

        public sealed record ResidencySummary(string TableName, string ColumnName, int ResidentSegments, int TotalSegments);
        public sealed record DictSize(string TableName, string ColumnName, long DictionarySize);

        public Dictionary<(string Table, string Column), (int resident, int total)> GetResidency()
        {
            var segs = LoadSegments();
            var cols = LoadTmSchemaColumns();
            var tabs = LoadTmSchemaTables();  // for IS_PRIVATE filtering

            // Build lookup maps by IDs
            var colById = cols.ToDictionary(c => c.COLUMN_ID, c => c, new ObjComparer());
            var tabById = tabs.ToDictionary(t => t.TABLE_ID, t => t, new ObjComparer());

            // Group by (TABLE_ID, COLUMN_ID) and aggregate resident/total
            var grouped = segs.GroupBy(s => (s.TABLE_ID, s.COLUMN_ID), new ObjTupleComparer());

            var result = new Dictionary<(string, string), (int, int)>(new TupleComparer());
            foreach (var g in grouped)
            {
                if (!colById.TryGetValue(g.Key, out var col)) continue;
                if (!tabById.TryGetValue(col.TABLE_ID, out var tab)) continue;
                if (tab.IS_PRIVATE) continue; // skip private tables

                int resident = g.Count(r => r.IS_RESIDENT);
                int total = g.Count();

                // Guard
                if (total <= 0) continue;

                result[(tab.NAME, col.NAME)] = (resident, total);
            }
            return result;
        }

        public Dictionary<(string Table, string Column), long> GetDictionarySizes()
        {
            var stc = LoadStc();
            var cols = LoadTmSchemaColumns();
            var tabs = LoadTmSchemaTables();

            var colById = cols.ToDictionary(c => c.COLUMN_ID, c => c, new ObjComparer());
            var tabById = tabs.ToDictionary(t => t.TABLE_ID, t => t, new ObjComparer());

            var result = new Dictionary<(string, string), long>(new TupleComparer());

            foreach (var row in stc)
            {
                if (!colById.TryGetValue(row.COLUMN_ID, out var col)) continue;
                if (!tabById.TryGetValue(col.TABLE_ID, out var tab)) continue;
                if (tab.IS_PRIVATE) continue;

                result[(tab.NAME, col.NAME)] = row.DICTIONARY_SIZE;
            }
            return result;
        }

        /// <summary>
        /// Returns a case-insensitive set of (Table, Column) pairs referenced by measures.
        /// Uses simple query to DISCOVER_CALC_DEPENDENCY and parses 'Table'[Column] strings.
        /// </summary>
        public HashSet<(string Table, string Column)> GetMeasureColumnDependencies(HashSet<string>? measuresWhitelist = null)
        {
            var result = new HashSet<(string, string)>(new TupleComparer());
            try
            {
                using var conn = new AdomdConnection(_connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();
                // Simple query: no aliases, no joins
                cmd.CommandText = @"
SELECT OBJECT, OBJECT_TYPE, REFERENCED_OBJECT, REFERENCED_OBJECT_TYPE
FROM $SYSTEM.DISCOVER_CALC_DEPENDENCY";

                using var rdr = cmd.ExecuteReader();
                int iObject = rdr.GetOrdinal("OBJECT");
                int iObjType = rdr.GetOrdinal("OBJECT_TYPE");
                int iRef = rdr.GetOrdinal("REFERENCED_OBJECT");
                int iRefType = rdr.GetOrdinal("REFERENCED_OBJECT_TYPE");

                while (rdr.Read())
                {
                    string objType = rdr.IsDBNull(iObjType) ? "" : rdr.GetString(iObjType);
                    if (!"Measure".Equals(objType, StringComparison.OrdinalIgnoreCase)) continue;

                    string refType = rdr.IsDBNull(iRefType) ? "" : rdr.GetString(iRefType);
                    if (!"Column".Equals(refType, StringComparison.OrdinalIgnoreCase)) continue;

                    string obj = rdr.IsDBNull(iObject) ? "" : rdr.GetString(iObject);
                    if (measuresWhitelist != null && measuresWhitelist.Count > 0 &&
                        !measuresWhitelist.Contains(obj, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string referenced = rdr.IsDBNull(iRef) ? "" : rdr.GetString(iRef);
                    if (TryParseTableColumn(referenced, out var t, out var c))
                    {
                        result.Add((t, c));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "DISCOVER_CALC_DEPENDENCY read failed; proceeding without measure dependency boost.");
            }
            return result;
        }

        // ------------------ Low-level loaders (simple DMV queries only) ------------------

        private List<SegRow> LoadSegments()
        {
            var list = new List<SegRow>();
            try
            {
                using var conn = new AdomdConnection(_connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();
                // No joins, no aliases
                cmd.CommandText = @"
SELECT TABLE_ID, COLUMN_ID, IS_RESIDENT
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS";

                using var rdr = cmd.ExecuteReader();
                int iTbl = rdr.GetOrdinal("TABLE_ID");
                int iCol = rdr.GetOrdinal("COLUMN_ID");
                int iRes = rdr.GetOrdinal("IS_RESIDENT");
                while (rdr.Read())
                {
                    var tbl = rdr.GetValue(iTbl);
                    var col = rdr.GetValue(iCol);
                    bool isRes = !rdr.IsDBNull(iRes) && Convert.ToInt32(rdr.GetValue(iRes)) == 1;
                    list.Add(new SegRow(tbl, col, isRes));
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to load DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS");
            }
            return list;
        }

        private List<StcRow> LoadStc()
        {
            var list = new List<StcRow>();
            try
            {
                using var conn = new AdomdConnection(_connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT TABLE_ID, COLUMN_ID, DICTIONARY_SIZE
FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMNS";

                using var rdr = cmd.ExecuteReader();
                int iTbl = rdr.GetOrdinal("TABLE_ID");
                int iCol = rdr.GetOrdinal("COLUMN_ID");
                int iSize = rdr.GetOrdinal("DICTIONARY_SIZE");
                while (rdr.Read())
                {
                    var tbl = rdr.GetValue(iTbl);
                    var col = rdr.GetValue(iCol);
                    long size = rdr.IsDBNull(iSize) ? 0L : Convert.ToInt64(rdr.GetValue(iSize));
                    list.Add(new StcRow(tbl, col, size));
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to load DISCOVER_STORAGE_TABLE_COLUMNS");
            }
            return list;
        }

        private List<TmColRow> LoadTmSchemaColumns()
        {
            var list = new List<TmColRow>();
            try
            {
                using var conn = new AdomdConnection(_connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT COLUMN_ID, TABLE_ID, NAME
FROM $SYSTEM.TMSCHEMA_COLUMNS";

                using var rdr = cmd.ExecuteReader();
                int iCol = rdr.GetOrdinal("COLUMN_ID");
                int iTbl = rdr.GetOrdinal("TABLE_ID");
                int iName = rdr.GetOrdinal("NAME");
                while (rdr.Read())
                {
                    var col = rdr.GetValue(iCol);
                    var tbl = rdr.GetValue(iTbl);
                    var name = rdr.IsDBNull(iName) ? "" : rdr.GetString(iName);
                    list.Add(new TmColRow(col, tbl, name));
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to load TMSCHEMA_COLUMNS");
            }
            return list;
        }

        private List<TmTabRow> LoadTmSchemaTables()
        {
            var list = new List<TmTabRow>();
            try
            {
                using var conn = new AdomdConnection(_connStr);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
SELECT TABLE_ID, NAME, IS_PRIVATE
FROM $SYSTEM.TMSCHEMA_TABLES";

                using var rdr = cmd.ExecuteReader();
                int iTbl = rdr.GetOrdinal("TABLE_ID");
                int iName = rdr.GetOrdinal("NAME");
                int iPriv = rdr.GetOrdinal("IS_PRIVATE");
                while (rdr.Read())
                {
                    var tbl = rdr.GetValue(iTbl);
                    var name = rdr.IsDBNull(iName) ? "" : rdr.GetString(iName);
                    bool isPrivate = !rdr.IsDBNull(iPriv) && Convert.ToInt32(rdr.GetValue(iPriv)) == 1;
                    list.Add(new TmTabRow(tbl, name, isPrivate));
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to load TMSCHEMA_TABLES");
            }
            return list;
        }

        // ------------------ Helpers ------------------

        private static bool TryParseTableColumn(string input, out string table, out string column)
        {
            table = "";
            column = "";
            if (string.IsNullOrWhiteSpace(input)) return false;
            int lb = input.IndexOf('[');
            int rb = input.IndexOf(']');
            if (lb <= 0 || rb <= lb) return false;
            column = input.Substring(lb + 1, rb - lb - 1).Trim();
            var left = input.Substring(0, lb).Trim();
            if (left.StartsWith("'") && left.EndsWith("'") && left.Length >= 2)
                left = left.Substring(1, left.Length - 2);
            table = left.Trim();
            return table.Length > 0 && column.Length > 0;
        }

        private sealed class ObjComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => string.Equals(x?.ToString(), y?.ToString(), StringComparison.OrdinalIgnoreCase);
            public int GetHashCode(object obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj?.ToString() ?? "");
        }

        private sealed class ObjTupleComparer : IEqualityComparer<(object a, object b)>
        {
            private readonly ObjComparer _c = new();
            public bool Equals((object a, object b) x, (object a, object b) y) => _c.Equals(x.a, y.a) && _c.Equals(x.b, y.b);
            public int GetHashCode((object a, object b) obj) => _c.GetHashCode(obj.a) ^ (_c.GetHashCode(obj.b) * 397);
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
    }
}
