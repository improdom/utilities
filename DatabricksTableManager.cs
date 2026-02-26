

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MrvMetricYamlGenerator.Databricks;

public sealed class MetricViewBatchDeployer
{
    private readonly HttpClient _http;
    private readonly string _host;        // e.g. https://adb-xxx.azuredatabricks.net
    private readonly string _warehouseId; // SQL Warehouse ID

    public MetricViewBatchDeployer(HttpClient http, string host, string token, string warehouseId)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _host = (host ?? throw new ArgumentNullException(nameof(host))).TrimEnd('/');
        _warehouseId = warehouseId ?? throw new ArgumentNullException(nameof(warehouseId));

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public sealed record MetricViewDefinition(
        string Catalog,
        string Schema,
        string ViewName,
        string Yaml
    );

    public sealed record DeployResult(
        string Catalog,
        string Schema,
        string ViewName,
        bool Success,
        string? StatementId,
        string? Error
    );

    /// <summary>
    /// Deploys metric views in batches of 10. Each batch is run concurrently, then awaited before the next batch begins.
    /// </summary>
    public async Task<IReadOnlyList<DeployResult>> DeployInBatchesOf10Async(
        IReadOnlyList<MetricViewDefinition> views,
        CancellationToken ct = default)
    {
        if (views == null) throw new ArgumentNullException(nameof(views));

        var results = new List<DeployResult>(views.Count);

        foreach (var batch in Batch(views, batchSize: 10))
        {
            // Submit 10 statements concurrently
            var submitTasks = batch.Select(async v =>
            {
                try
                {
                    var ddl = BuildMetricViewDdl(v.Catalog, v.Schema, v.ViewName, v.Yaml);
                    var stmtId = await SubmitStatementAsync(ddl, ct);
                    return (v, stmtId, error: (string?)null);
                }
                catch (Exception ex)
                {
                    return (v, stmtId: (string?)null, error: ex.ToString());
                }
            }).ToArray();

            var submitted = await Task.WhenAll(submitTasks);

            // Wait for the ones that submitted successfully
            var waitTasks = submitted.Select(async s =>
            {
                if (s.stmtId is null)
                {
                    results.Add(new DeployResult(s.v.Catalog, s.v.Schema, s.v.ViewName, false, null, s.error));
                    return;
                }

                try
                {
                    await WaitForCompletionAsync(s.stmtId, ct);
                    results.Add(new DeployResult(s.v.Catalog, s.v.Schema, s.v.ViewName, true, s.stmtId, null));
                }
                catch (Exception ex)
                {
                    results.Add(new DeployResult(s.v.Catalog, s.v.Schema, s.v.ViewName, false, s.stmtId, ex.ToString()));
                }
            }).ToArray();

            await Task.WhenAll(waitTasks);
        }

        return results;
    }

    // -----------------------------
    // DDL builder
    // -----------------------------
    private static string BuildMetricViewDdl(string catalog, string schema, string viewName, string yaml)
    {
        if (string.IsNullOrWhiteSpace(catalog)) throw new ArgumentException("catalog is required");
        if (string.IsNullOrWhiteSpace(schema)) throw new ArgumentException("schema is required");
        if (string.IsNullOrWhiteSpace(viewName)) throw new ArgumentException("viewName is required");
        if (string.IsNullOrWhiteSpace(yaml)) throw new ArgumentException("yaml is required");

        var quotedView = QuoteIdentifier(viewName);

        // Correct Databricks syntax for metric views in SQL Warehouse
        return $"""
        CREATE OR REPLACE VIEW {catalog}.{schema}.{quotedView}
        WITH METRICS
        LANGUAGE YAML
        AS $$
        {yaml}
        $$;
        """;
    }

    private static string QuoteIdentifier(string name)
        => $"`{name.Replace("`", "``")}`"; // Spark SQL escaping for backticks

    // -----------------------------
    // Statement Execution API
    // -----------------------------
    private async Task<string> SubmitStatementAsync(string sql, CancellationToken ct)
    {
        var payload = new
        {
            warehouse_id = _warehouseId,
            statement = sql,
            wait_timeout = "0s" // return immediately; we poll
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_host}/api/2.0/sql/statements")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Submit failed ({(int)resp.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("statement_id").GetString()
               ?? throw new InvalidOperationException("Missing statement_id in response.");
    }

    private async Task WaitForCompletionAsync(string statementId, CancellationToken ct)
    {
        while (true)
        {
            using var resp = await _http.GetAsync($"{_host}/api/2.0/sql/statements/{statementId}", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Poll failed ({(int)resp.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);

            var state = doc.RootElement
                .GetProperty("status")
                .GetProperty("state")
                .GetString();

            if (string.Equals(state, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.Equals(state, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "CANCELED", StringComparison.OrdinalIgnoreCase))
            {
                var err = doc.RootElement.TryGetProperty("status", out var s) &&
                          s.TryGetProperty("error", out var e)
                    ? e.ToString()
                    : body;

                throw new InvalidOperationException($"Statement {statementId} ended in {state}: {err}");
            }

            await Task.Delay(TimeSpan.FromSeconds(1.5), ct);
        }
    }

    // -----------------------------
    // Helpers
    // -----------------------------
    private static IEnumerable<IReadOnlyList<T>> Batch<T>(IReadOnlyList<T> items, int batchSize)
    {
        for (int i = 0; i < items.Count; i += batchSize)
        {
            var count = Math.Min(batchSize, items.Count - i);
            var batch = new T[count];
            for (int j = 0; j < count; j++)
                batch[j] = items[i + j];
            yield return batch;
        }
    }
}


using MrvMetricYamlGenerator.Databricks;

var host = Environment.GetEnvironmentVariable("DATABRICKS_HOST")!;
var token = Environment.GetEnvironmentVariable("DATABRICKS_TOKEN")!;
var warehouseId = Environment.GetEnvironmentVariable("DATABRICKS_WAREHOUSE_ID")!;

using var http = new HttpClient();
var deployer = new MetricViewBatchDeployer(http, host, token, warehouseId);

var views = new List<MetricViewBatchDeployer.MetricViewDefinition>
{
    new("neu_dev_at40482_mtrc", "cubiq_semantic_layer", "Commodity Delta Net", File.ReadAllText("mv1.yaml")),
    new("neu_dev_at40482_mtrc", "cubiq_semantic_layer", "Credit Delta", File.ReadAllText("mv2.yaml")),
    // ...
};

var results = await deployer.DeployInBatchesOf10Async(views);

foreach (var r in results)
    Console.WriteLine($"{r.Catalog}.{r.Schema}.{r.ViewName} => {(r.Success ? "OK" : "FAILED")} {r.Error}");







Hi Team,

While reviewing the recent issue with missing Power BI changes in GitLab, I identified the likely root cause.

The repository currently contains a PBIX file that appears to be used for development. The changes are then manually converted to TMDL format and committed to the repository for deployment. In this setup, the PBIX effectively becomes the source of truth, while the TMDL files are treated as deployment artifacts.

This approach creates two problems:

1. PBIX is a binary file and cannot be properly version-controlled or diffed in GitLab.
2. Maintaining both PBIX and TMDL in parallel introduces a risk of them getting out of sync if both are not consistently updated.

To avoid these issues, we should use PBIP as the primary development format instead of PBIX. PBIP works natively with TMDL and allows the model files to be stored in a text-based structure that can be properly tracked, reviewed, and managed in GitLab. This would eliminate the need for manual conversions and reduce the risk of inconsistencies.

I will schedule a meeting to walk through how PBIP and TMDL should be structured in the GitLab repository and clarify the recommended development workflow going forward.

Please let me know if you have any concerns in the meantime.

Best regards,
Julio Diaz

