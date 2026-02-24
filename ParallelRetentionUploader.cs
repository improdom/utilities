using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public sealed class DatabricksSqlWarehouseClient
{
    private readonly HttpClient _http;
    private readonly string _host;         // e.g. https://adb-xxx.azuredatabricks.net
    private readonly string _warehouseId;  // SQL Warehouse ID

    public DatabricksSqlWarehouseClient(HttpClient http, string host, string token, string warehouseId)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _host = host.TrimEnd('/');
        _warehouseId = warehouseId;

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<string> ExecuteAsync(string sql, CancellationToken ct = default)
    {
        // POST /api/2.0/sql/statements
        var payload = new
        {
            warehouse_id = _warehouseId,
            statement = sql,
            wait_timeout = "0s" // return immediately; we will poll
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_host}/api/2.0/sql/statements")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Execute failed ({(int)resp.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("statement_id").GetString()
               ?? throw new InvalidOperationException("Missing statement_id in response.");
    }

    public async Task WaitForCompletionAsync(string statementId, CancellationToken ct = default)
    {
        // GET /api/2.0/sql/statements/{statement_id}
        while (true)
        {
            using var resp = await _http.GetAsync($"{_host}/api/2.0/sql/statements/{statementId}", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Poll failed ({(int)resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.GetProperty("status").GetProperty("state").GetString();

            // States include: PENDING, RUNNING, SUCCEEDED, FAILED, CANCELED (and others).
            if (string.Equals(status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "CANCELED", StringComparison.OrdinalIgnoreCase))
            {
                var err = doc.RootElement.TryGetProperty("status", out var s) &&
                          s.TryGetProperty("error", out var e)
                    ? e.ToString()
                    : body;

                throw new InvalidOperationException($"Statement {statementId} ended in {status}: {err}");
            }

            // Backoff: keep it modest to avoid hammering the API
            await Task.Delay(TimeSpan.FromSeconds(1.5), ct);
        }
    }
}

public static class MetricViewBulkDeployer
{
    public static async Task DeployManyAsync(
        DatabricksSqlWarehouseClient sqlClient,
        string schema,
        IReadOnlyList<(string ViewName, string Yaml)> metricViews,
        int maxConcurrency = 12,
        CancellationToken ct = default)
    {
        using var gate = new SemaphoreSlim(maxConcurrency);

        var tasks = metricViews.Select(async mv =>
        {
            await gate.WaitAsync(ct);
            try
            {
                // NOTE: Metric view DDL syntax can vary by workspace feature set.
                // Use the exact CREATE statement you validated manually in SQL.
                var ddl = $"""
                CREATE OR REPLACE METRIC VIEW {schema}.{mv.ViewName}
                AS YAML $$
                {mv.Yaml}
                $$;
                """;

                var stmtId = await sqlClient.ExecuteAsync(ddl, ct);
                await sqlClient.WaitForCompletionAsync(stmtId, ct);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
