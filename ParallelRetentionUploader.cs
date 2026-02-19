// ======================================================
// Databricks SQL Statement Execution (EXTERNAL_LINKS) -> CacheSnapshot (streaming)
// Works alongside Microsoft.Azure.Databricks.Client, but uses raw REST for the SQL Statements API
// because Microsoft.Azure.Databricks.Client typically doesn't model /api/2.0/sql/statements well.
// ======================================================

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Arc.Databricks.SqlStreaming
{
    // ------------------------------------------------------
    // Domain: what you ultimately want in memory
    // ------------------------------------------------------

    public sealed class MrvPartitionInfo
    {
        public int RmSeg { get; init; }
        public int MrvPartition { get; init; }
        public string FactTableName { get; init; } = "";
        public string RmRiskMeasureName { get; init; } = "";
        public string ScenarioGroup { get; init; } = "";
        public string MdstDescription { get; init; } = "";
    }

    public sealed class CacheSnapshot
    {
        public IReadOnlyList<MrvPartitionInfo> Rows { get; }
        public IReadOnlyDictionary<string, HashSet<int>> ByMeasure { get; }
        public IReadOnlyDictionary<string, HashSet<int>> ByScenarioGroup { get; }
        public IReadOnlyDictionary<string, HashSet<int>> ByMdstDescription { get; }

        public CacheSnapshot(
            List<MrvPartitionInfo> rows,
            Dictionary<string, HashSet<int>> byMeasure,
            Dictionary<string, HashSet<int>> byScenarioGroup,
            Dictionary<string, HashSet<int>> byMdstDescription)
        {
            Rows = rows;
            ByMeasure = byMeasure;
            ByScenarioGroup = byScenarioGroup;
            ByMdstDescription = byMdstDescription;
        }
    }

    // ------------------------------------------------------
    // ExternalLink DTOs (from SQL Statements API result chunks)
    // ------------------------------------------------------

    public sealed class ExternalLink
    {
        [JsonPropertyName("external_link")]
        public string ExternalLinkUrl { get; set; } = "";

        [JsonPropertyName("http_headers")]
        public HttpHeader[]? HttpHeaders { get; set; }

        [JsonPropertyName("expiration")]
        public string? Expiration { get; set; }
    }

    public sealed class HttpHeader
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }

    // ------------------------------------------------------
    // SQL Statements API DTOs (minimal subset)
    // ------------------------------------------------------

    internal sealed class ExecuteStatementRequest
    {
        [JsonPropertyName("statement")]
        public string Statement { get; set; } = "";

        [JsonPropertyName("warehouse_id")]
        public string WarehouseId { get; set; } = "";

        [JsonPropertyName("disposition")]
        public string Disposition { get; set; } = "EXTERNAL_LINKS";

        [JsonPropertyName("format")]
        public string Format { get; set; } = "JSON_ARRAY";

        // Return immediately; we poll status.
        [JsonPropertyName("wait_timeout")]
        public string WaitTimeout { get; set; } = "0s";
    }

    internal sealed class StatementExecutionResponse
    {
        [JsonPropertyName("statement_id")]
        public string StatementId { get; set; } = "";

        [JsonPropertyName("status")]
        public StatementStatus? Status { get; set; }

        [JsonPropertyName("manifest")]
        public ResultManifest? Manifest { get; set; }
    }

    internal sealed class StatementStatus
    {
        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("error")]
        public StatementError? Error { get; set; }
    }

    internal sealed class StatementError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    internal sealed class ResultManifest
    {
        [JsonPropertyName("schema")]
        public ResultSchema? Schema { get; set; }

        [JsonPropertyName("chunks")]
        public ResultChunk[]? Chunks { get; set; }
    }

    internal sealed class ResultSchema
    {
        [JsonPropertyName("columns")]
        public Column[]? Columns { get; set; }
    }

    internal sealed class Column
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("type_name")]
        public string TypeName { get; set; } = "";
    }

    internal sealed class ResultChunk
    {
        // Not strictly needed; included for completeness
        [JsonPropertyName("chunk_index")]
        public int ChunkIndex { get; set; }
    }

    internal sealed class ResultChunkResponse
    {
        // With EXTERNAL_LINKS, Databricks returns these signed URLs
        [JsonPropertyName("external_links")]
        public ExternalLink[]? ExternalLinks { get; set; }

        // Sometimes small chunks come inline anyway
        [JsonPropertyName("data_array")]
        public object?[][]? DataArray { get; set; }
    }

    // ------------------------------------------------------
    // Main client: runs query and streams into CacheSnapshot
    // ------------------------------------------------------

    public sealed class DatabricksSqlExternalLinksCacheLoader
    {
        private readonly HttpClient _http;
        private readonly string _workspaceUrl; // e.g. https://adb-xxxx.azuredatabricks.net
        private readonly Func<CancellationToken, Task<string>> _accessTokenProvider;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public DatabricksSqlExternalLinksCacheLoader(
            string workspaceUrl,
            Func<CancellationToken, Task<string>> accessTokenProvider,
            HttpClient? httpClient = null)
        {
            _workspaceUrl = workspaceUrl.TrimEnd('/');
            _accessTokenProvider = accessTokenProvider;

            _http = httpClient ?? new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            });
        }

        public async Task<CacheSnapshot> ExecuteToCacheSnapshotAsync(
            string sql,
            string warehouseId,
            CancellationToken ct = default)
        {
            // 1) Submit
            var statementId = await ExecuteStatementAsync(sql, warehouseId, ct);

            // 2) Poll until done
            var exec = await WaitUntilDoneAsync(statementId, ct);

            if (!string.Equals(exec.Status?.State, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                var msg = exec.Status?.Error?.Message ?? "Unknown Databricks SQL error";
                throw new InvalidOperationException($"Databricks statement failed: {exec.Status?.State}. {msg}");
            }

            var cols = exec.Manifest?.Schema?.Columns;
            if (cols == null || cols.Length == 0)
            {
                return new CacheSnapshot(
                    new List<MrvPartitionInfo>(),
                    new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase));
            }

            var ord = Ordinals.FromColumns(cols);

            var rows = new List<MrvPartitionInfo>(capacity: 4096);
            var byMeasure = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            var byScenario = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            var byMdst = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            // 3) Iterate chunks and stream rows directly into cache structures
            var chunks = exec.Manifest?.Chunks ?? Array.Empty<ResultChunk>();
            for (int i = 0; i < chunks.Length; i++)
            {
                ct.ThrowIfCancellationRequested();

                var chunk = await GetChunkAsync(statementId, i, ct);

                if (chunk.DataArray != null)
                {
                    foreach (var row in chunk.DataArray)
                        AppendRowToCache(row, ord, rows, byMeasure, byScenario, byMdst);
                    continue;
                }

                if (chunk.ExternalLinks == null || chunk.ExternalLinks.Length == 0)
                    throw new InvalidOperationException($"Chunk {i} returned neither data_array nor external_links.");

                foreach (var link in chunk.ExternalLinks)
                {
                    await StreamExternalLinkJsonArrayAsync(
                        link, ord, rows, byMeasure, byScenario, byMdst, ct);
                }
            }

            return new CacheSnapshot(rows, byMeasure, byScenario, byMdst);
        }

        // ------------------ REST: execute, status, chunks ------------------

        private async Task<string> ExecuteStatementAsync(string sql, string warehouseId, CancellationToken ct)
        {
            var token = await _accessTokenProvider(ct);

            var url = $"{_workspaceUrl}/api/2.0/sql/statements";
            var payload = new ExecuteStatementRequest
            {
                Statement = sql,
                WarehouseId = warehouseId,
                Disposition = "EXTERNAL_LINKS",
                Format = "JSON_ARRAY",
                WaitTimeout = "0s"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"ExecuteStatement failed ({(int)resp.StatusCode}): {body}");

            var parsed = JsonSerializer.Deserialize<StatementExecutionResponse>(body, JsonOptions)
                         ?? throw new InvalidOperationException("Failed to deserialize ExecuteStatement response.");

            if (string.IsNullOrWhiteSpace(parsed.StatementId))
                throw new InvalidOperationException("ExecuteStatement response missing statement_id.");

            return parsed.StatementId;
        }

        private async Task<StatementExecutionResponse> WaitUntilDoneAsync(string statementId, CancellationToken ct)
        {
            var token = await _accessTokenProvider(ct);
            var url = $"{_workspaceUrl}/api/2.0/sql/statements/{statementId}";

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"GetStatement failed ({(int)resp.StatusCode}): {body}");

                var parsed = JsonSerializer.Deserialize<StatementExecutionResponse>(body, JsonOptions)
                             ?? throw new InvalidOperationException("Failed to deserialize GetStatement response.");

                var state = parsed.Status?.State ?? "";
                if (!state.Equals("PENDING", StringComparison.OrdinalIgnoreCase) &&
                    !state.Equals("RUNNING", StringComparison.OrdinalIgnoreCase))
                {
                    return parsed;
                }

                await Task.Delay(400, ct);
            }
        }

        private async Task<ResultChunkResponse> GetChunkAsync(string statementId, int chunkIndex, CancellationToken ct)
        {
            var token = await _accessTokenProvider(ct);
            var url = $"{_workspaceUrl}/api/2.0/sql/statements/{statementId}/result/chunks/{chunkIndex}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"GetChunk failed ({(int)resp.StatusCode}): {body}");

            return JsonSerializer.Deserialize<ResultChunkResponse>(body, JsonOptions)
                   ?? throw new InvalidOperationException("Failed to deserialize GetChunk response.");
        }

        // ------------------ Streaming external links ------------------

        private async Task StreamExternalLinkJsonArrayAsync(
            ExternalLink link,
            Ordinals ord,
            List<MrvPartitionInfo> rows,
            Dictionary<string, HashSet<int>> byMeasure,
            Dictionary<string, HashSet<int>> byScenario,
            Dictionary<string, HashSet<int>> byMdst,
            CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, link.ExternalLinkUrl);

            // Databricks may include headers required to access the signed URL
            if (link.HttpHeaders != null)
            {
                foreach (var h in link.HttpHeaders)
                    req.Headers.TryAddWithoutValidation(h.Name, h.Value);
            }

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);

            // Expected payload with format=JSON_ARRAY:
            // [
            //   [col1, col2, ...],
            //   [col1, col2, ...],
            //   ...
            // ]
            //
            // Stream parse row-by-row (no DataTable, no full JSON string in memory)
            await StreamParseJsonArrayOfRowsAsync(stream, row =>
            {
                AppendRowToCache(row, ord, rows, byMeasure, byScenario, byMdst);
            }, ct);
        }

        private static async Task StreamParseJsonArrayOfRowsAsync(
            Stream stream,
            Action<object?[]> onRow,
            CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                var state = new JsonReaderState(new JsonReaderOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });

                int bytesInBuffer = 0;
                bool isFinal = false;

                // We parse outer array then inner row arrays.
                bool outerArrayStarted = false;

                while (!isFinal)
                {
                    ct.ThrowIfCancellationRequested();

                    int read = await stream.ReadAsync(buffer.AsMemory(bytesInBuffer), ct);
                    if (read == 0) isFinal = true;

                    var span = new ReadOnlySpan<byte>(buffer, 0, bytesInBuffer + read);
                    var reader = new Utf8JsonReader(span, isFinal, state);

                    while (reader.Read())
                    {
                        if (!outerArrayStarted)
                        {
                            if (reader.TokenType == JsonTokenType.StartArray)
                            {
                                outerArrayStarted = true;
                            }
                            continue;
                        }

                        // Each row is an inner array
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            if (TryReadRow(ref reader, out var row))
                            {
                                onRow(row);
                            }
                            // If TryReadRow returned false, it means token boundaries split
                            // and we’ll continue after buffer refill (handled by bytesInBuffer logic).
                        }
                        else if (reader.TokenType == JsonTokenType.EndArray)
                        {
                            // End of outer array
                            // We could break, but keep reading until stream ends.
                        }
                    }

                    state = reader.CurrentState;

                    // carry over unconsumed bytes for the next read
                    int consumed = (int)reader.BytesConsumed;
                    int remaining = span.Length - consumed;
                    if (remaining > 0)
                        span.Slice(consumed).CopyTo(buffer);

                    bytesInBuffer = remaining;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static bool TryReadRow(ref Utf8JsonReader reader, out object?[] row)
        {
            // We are positioned at StartArray (row)
            var values = new List<object?>(capacity: 16);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    row = values.ToArray();
                    return true;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.Null:
                        values.Add(null);
                        break;
                    case JsonTokenType.String:
                        values.Add(reader.GetString());
                        break;
                    case JsonTokenType.True:
                        values.Add(true);
                        break;
                    case JsonTokenType.False:
                        values.Add(false);
                        break;
                    case JsonTokenType.Number:
                        if (reader.TryGetInt64(out long l)) values.Add(l);
                        else if (reader.TryGetDecimal(out decimal d)) values.Add(d);
                        else values.Add(reader.GetDouble());
                        break;

                    // If nested arrays/objects appear, this isn't the expected JSON_ARRAY format for rows
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        row = Array.Empty<object?>();
                        return false;

                    default:
                        values.Add(reader.GetString());
                        break;
                }
            }

            // Ran out of bytes mid-row; caller will refill buffer and continue
            row = Array.Empty<object?>();
            return false;
        }

        // ------------------ Cache building (single-pass) ------------------

        private static void AppendRowToCache(
            object?[] row,
            Ordinals ord,
            List<MrvPartitionInfo> rows,
            Dictionary<string, HashSet<int>> byMeasure,
            Dictionary<string, HashSet<int>> byScenario,
            Dictionary<string, HashSet<int>> byMdst)
        {
            // Map values by ordinal (robust to column ordering)
            var info = new MrvPartitionInfo
            {
                RmSeg = GetInt(row, ord.RmSeg),
                MrvPartition = GetInt(row, ord.MrvPartition),
                FactTableName = GetString(row, ord.FactTableName) ?? "",
                RmRiskMeasureName = GetString(row, ord.RmRiskMeasureName) ?? "",
                ScenarioGroup = GetString(row, ord.ScenarioGroup) ?? "",
                MdstDescription = GetString(row, ord.MdstDescription) ?? ""
            };

            int index = rows.Count;
            rows.Add(info);

            AddIndex(byMeasure, info.RmRiskMeasureName, index);
            AddIndex(byScenario, info.ScenarioGroup, index);
            AddIndex(byMdst, info.MdstDescription, index);
        }

        private static void AddIndex(Dictionary<string, HashSet<int>> dict, string key, int index)
        {
            key ??= "";
            if (!dict.TryGetValue(key, out var set))
                dict[key] = set = new HashSet<int>();
            set.Add(index);
        }

        private static int GetInt(object?[] row, int ordinal)
        {
            if (ordinal < 0 || ordinal >= row.Length || row[ordinal] is null) return 0;

            return row[ordinal] switch
            {
                int x => x,
                long x => checked((int)x),
                decimal x => (int)x,
                double x => (int)x,
                string s when int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) => v,
                _ => 0
            };
        }

        private static string? GetString(object?[] row, int ordinal)
        {
            if (ordinal < 0 || ordinal >= row.Length) return null;
            return row[ordinal]?.ToString();
        }

        // ------------------ Column ordinals helper ------------------

        private readonly struct Ordinals
        {
            public int RmSeg { get; }
            public int MrvPartition { get; }
            public int FactTableName { get; }
            public int RmRiskMeasureName { get; }
            public int ScenarioGroup { get; }
            public int MdstDescription { get; }

            private Ordinals(int rmSeg, int mrvPartition, int factTableName, int rmRiskMeasureName, int scenarioGroup, int mdstDescription)
            {
                RmSeg = rmSeg;
                MrvPartition = mrvPartition;
                FactTableName = factTableName;
                RmRiskMeasureName = rmRiskMeasureName;
                ScenarioGroup = scenarioGroup;
                MdstDescription = mdstDescription;
            }

            public static Ordinals FromColumns(Column[] columns)
            {
                // Adjust names if your SQL aliases differ.
                // These match what your earlier query implied:
                // a.rm_seg, a.mrv_partition, a.fact_table_name, b.rm_risk_measure_name, c.scenario_group, d.mdst_description
                int Find(string name)
                {
                    for (int i = 0; i < columns.Length; i++)
                    {
                        if (string.Equals(columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                    return -1;
                }

                return new Ordinals(
                    rmSeg: Find("rm_seg"),
                    mrvPartition: Find("mrv_partition"),
                    factTableName: Find("fact_table_name"),
                    rmRiskMeasureName: Find("rm_risk_measure_name"),
                    scenarioGroup: Find("scenario_group"),
                    mdstDescription: Find("mdst_description"));
            }
        }
    }

    // ------------------------------------------------------
    // Example usage
    // ------------------------------------------------------
    /*
    // If you already use Microsoft.Azure.Databricks.Client, you likely already have:
    // - workspace URL
    // - a way to get an access token (AAD token for Azure Databricks resource OR PAT)
    //
    // Plug that into the loader like this:

    var loader = new DatabricksSqlExternalLinksCacheLoader(
        workspaceUrl: "https://adb-123456789.10.azuredatabricks.net",
        accessTokenProvider: async ct =>
        {
            // Return an access token string (no "Bearer " prefix)
            // Example: return await myTokenClient.GetDatabricksAccessTokenAsync(ct);
            return myToken;
        });

    string sql = @"
        SELECT DISTINCT
            a.rm_seg,
            a.mrv_partition,
            a.fact_table_name,
            b.rm_risk_measure_name,
            c.scenario_group,
            d.mdst_description
        FROM cubiq_config.risk_fact_partitions a
        JOIN cubiq_config.risk_measure_seg b ON a.rm_seg = b.rm_seg
        JOIN cubiq_config.scenario_group_seg c ON a.sg_seg = c.sg_seg
        JOIN cubiq_config.curve_quality_seg d ON a.cq_seg = d.cq_seg
    ";

    var snapshot = await loader.ExecuteToCacheSnapshotAsync(sql, warehouseId: "<your-warehouse-id>", ct);
    */
}
