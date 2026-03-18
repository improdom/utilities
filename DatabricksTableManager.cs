Hi Farhan,

As discussed, in order to provide an accurate estimate, we first need to conduct a technical assessment of the modules and pages where these libraries are currently in use.

To support this effort, I will need assistance from a developer with broader familiarity across the application, as my experience is limited to a specific area. I have scheduled a session with Nancy and Lokesh tomorrow to begin this assessment and establish an initial estimate.

I will keep you informed as we progress and refine the timeline.

Best regards,






Hi [Manager Name],

As part of the vulnerability scan remediation, we are required to upgrade jQuery from version 1.12.3 to a secure version (3.2+).

The challenge is that our current Kendo UI version (2016.3.914) is tightly coupled with older jQuery versions and does not fully support jQuery 3.x. This means we cannot safely upgrade jQuery in isolation.

Upgrading jQuery alone introduces a high risk of breaking UI functionality, as Kendo components depend on older jQuery behaviors. This could impact key areas such as grids, dropdowns, validation, and event handling.

To meet the security requirement (jQuery 3.2+), we would also need to upgrade Kendo UI to a compatible version. However, even incremental Kendo upgrades can introduce behavioral and rendering changes, which would require regression testing and potential code updates across the application.

Summary:
- jQuery 1.12.3 must be upgraded due to security vulnerabilities  
- Target version (3.2+) is not supported by our current Kendo UI version  
- Upgrading jQuery alone is high risk and likely to break functionality  
- A compatible solution requires upgrading both jQuery and Kendo UI  
- This introduces testing effort and possible code changes  

Conclusion:  
This is not a simple patch but a coordinated upgrade effort that requires validation and controlled execution to avoid impacting application stability.

Please let me know how you would like to proceed. I can outline a phased plan with estimated effort and risk mitigation steps.

Best regards,  
Julio







Hi [Manager Name],

I wanted to provide a quick assessment of the effort and risks associated with upgrading our current jQuery and Kendo UI stack in the existing ASP.NET application.

Our application is currently using an older version of Kendo UI (2016.3.914), which is tightly coupled with jQuery 1.12.3. This creates a challenge because newer versions of jQuery (particularly 3.x and above) introduce breaking changes that are not fully supported by our current Kendo version.

While upgrading jQuery alone may seem like a straightforward way to address known vulnerabilities, in practice it is not risk-free. Kendo UI components rely heavily on jQuery’s internal behaviors, and changes introduced in newer jQuery versions can cause subtle or immediate failures in UI components such as grids, dropdowns, validation, and event handling.

To safely move to a more secure and modern setup, we would also need to upgrade Kendo UI. However, even incremental upgrades to Kendo can introduce behavioral changes, rendering differences, and compatibility issues that would require regression testing and potential code adjustments across the application.

In summary, the main challenges are:

- Strong dependency between Kendo UI and specific jQuery versions  
- Lack of full support for modern jQuery versions in our current Kendo release  
- Risk of UI regressions or broken functionality when upgrading either library independently  
- Need for thorough testing and possible code changes even with minor version upgrades  

A minimal-risk approach would be to upgrade within the same Kendo release family (to the latest 2016 service pack) and align jQuery accordingly. However, this still requires validation and does not completely eliminate risk.

A more robust long-term solution would involve upgrading both Kendo UI and jQuery to supported modern versions, but this should be treated as a controlled effort with proper testing and possibly incremental refactoring.

Please let me know how you would like to proceed. I can outline a phased upgrade plan with estimated effort and testing scope.

Best regards,  
Julio









“Congratulations to the CubIQ Team on reaching the important milestone of delivering the first production release of the CubIQ Platform. This accomplishment reflects the team’s dedication, collaboration, and hard work in turning a complex initiative into a successful production deployment.






eaching the important milestone of delivering the first production release of the CubIQ Platform. This accomplishment reflects the team’s dedication, collaboration, and hard work in turning a complex initiative into a successful production deployment.




Hi,

I performed a deeper analysis and ran the reconciliation on both CubIQ and CubIQ App. I still observe a small deviation in the coverage; however, it is not as significant as the discrepancy observed in today’s reconciliation for Feb 9th.

This suggests that additional factors are likely contributing to the mismatches.




Hi Venkat / Abhishek,

Could you please verify the following? I believe this was the root cause of the mismatches observed during today’s reconciliation.

The issue appears to be that the models were inverted:
	•	CubeIQ was running in DirectQuery mode
	•	CubeIQ_App was running in Import mode

In CubeIQ, the values are loaded into memory and Power BI ignores case differences because it is not case-sensitive.
However, CubeIQ_App retrieves all data directly from Databricks, which is case-sensitive, resulting in the mismatches.

We did not catch this earlier because the reconciliation was only executed in CubeIQ. The discrepancy was only discovered when the reconciliation was accidentally executed in CubeIQ_App.

Could you please rerun the reconciliation to confirm that coverage is correct now that the issue has been addressed?

Regarding today’s fix, I have already deployed the change to app_dev. We will move it to TEST once the ETL side is ready.






TV Change EQ ex Funds Slide - Single Stock +/-20% - New :=
VAR HasOneMag =
    HASONEVALUE ( Scenario[Stress Magnitude Num] )
VAR Mag =
    SELECTEDVALUE ( Scenario[Stress Magnitude Num] )
VAR MagInRange =
    Mag >= -20 && Mag <= 20 && Mag <> 0
VAR Temp1 =
    MINX (
        CROSSJOIN (
            ALL ( Scenario[Scenario Name] ),
            FILTER (
                ALL ( Scenario[Stress Magnitude Num] ),
                Scenario[Stress Magnitude Num] >= -20
                    && Scenario[Stress Magnitude Num] <= 20
                    && Scenario[Stress Magnitude Num] <> 0
            )
        ),
        [TV Change EQ ex Funds - Single Stock]
    )
RETURN
    IF (
        HasOneMag,
        IF ( MagInRange, [TV Change EQ ex Funds - Single Stock], BLANK() ),
        IF ( ISBLANK ( Temp1 ), BLANK(), MIN ( 0, Temp1 ) )
    )








TV Change EQ ex Funds Slide - Single Stock +/-20% :=
VAR HasOneMag =
    HASONEVALUE ( Scenario[Stress Magnitude] )
VAR Mag =
    SELECTEDVALUE ( Scenario[Stress Magnitude] )
VAR MagInRange =
    Mag >= -20 && Mag <= 20 && Mag <> 0

-- “Temp1” logic used only when magnitude is NOT on the axis
VAR Temp1 =
    MINX (
        CROSSJOIN (
            ALL ( Scenario[Scenario Name] ),
            FILTER (
                ALL ( Scenario[Stress Magnitude] ),
                Scenario[Stress Magnitude] >= -20
                    && Scenario[Stress Magnitude] <= 20
                    && Scenario[Stress Magnitude] <> 0
            )
        ),
        [TV Change EQ ex Funds - Single Stock]
    )
RETURN
    IF (
        HasOneMag,
        -- MDX SCOPE behavior: show base only for allowed magnitudes, else BLANK
        IF ( MagInRange, [TV Change EQ ex Funds - Single Stock], BLANK() ),
        -- MDX formula behavior (totals): worst-case, capped at <= 0
        IF ( ISBLANK ( Temp1 ), BLANK(), MIN ( 0, Temp1 ) )
    )








from pyspark.sql.functions import *
from pyspark.sql.types import *

pbi_bm_id = f"""({",".join([item.strip() for item in dbutils.widgets.get("pbi_benchmark_run_id").split(",")])})"""
cube_bm_id = f"""({",".join([item.strip() for item in dbutils.widgets.get("cube_benchmark_run_id").split(",")])})"""

def read_from_sql_mi_table(table_name):
    hostname = dbutils.secrets.get("mtrc-abd-secret-scope", "MarvelAppConfig-DbServer")
    dbname   = dbutils.secrets.get("mtrc-abd-secret-scope", "MarvelAppConfig-DbName")
    username = dbutils.secrets.get("mtrc-abd-secret-scope", "MarvelAppConfig-DbUser")
    password = dbutils.secrets.get("mtrc-abd-secret-scope", "MarvelAppConfig-DbPassword")

    driver = "com.microsoft.sqlserver.jdbc.SQLServerDriver"
    connectionString = (
        f"jdbc:sqlserver://{hostname}:1433;"
        f"database={dbname};"
        "encrypt=true;"
        "trustServerCertificate=false;"
        "loginTimeout=60;"
        "authentication=ActiveDirectoryPassword;"
    )

    return (
        spark.read.format("jdbc")
        .option("driver", driver)
        .option("url", connectionString)
        .option("dbtable", table_name)
        .option("user", username)
        .option("password", password)
        .load()
    )

# --- NEW: robust business_date parser (handles mixed string formats) ---
def normalize_business_date(df, col_name="business_date"):
    s = trim(col(col_name).cast("string"))
    return df.withColumn(
        col_name,
        coalesce(
            to_date(s, "yyyy-MM-dd"),                          # ISO
            to_date(to_timestamp(s, "M/d/yyyy h:mm:ss a")),    # SQL MI style: 2/10/2026 12:00:00 AM
            to_date(to_timestamp(s, "MM/dd/yyyy h:mm:ss a"))   # just in case of leading zeros
        )
    )

current_timestamp_col = current_timestamp()

# -------------------------
# Power BI Data read
# -------------------------
df_pbi = (
    read_from_sql_mi_table("cubiq.st_mrv_recon_target_data")
    .filter(f"benchmark_run_id in {pbi_bm_id}")
    .select("business_date", "level_value", "src_book_id", "source", "measurename", "level_name", "mrv_id", "measurevalue")
)

df_pbi = (
    normalize_business_date(df_pbi, "business_date")
    .withColumn("measurevalue", col("measurevalue").cast("double"))
    .withColumn("update_time", current_timestamp_col)
)

# -------------------------
# CUBE Data read
# -------------------------
df_cube = (
    read_from_sql_mi_table("cubiq.st_mrv_recon_target_data")
    .filter(f"benchmark_run_id in {cube_bm_id}")
    .select("business_date", "level_value", "src_book_id", "source", "measurename", "level_name", "mrv_id", "measurevalue")
)

df_cube = (
    normalize_business_date(df_cube, "business_date")   # <-- FIX: parse, don’t cast
    .withColumn("measurevalue", col("measurevalue").cast("double"))
    .withColumn("update_time", current_timestamp_col)
)

print("CUBE Record Count  =", df_cube.count())
print("PBI  Record Count  =", df_pbi.count())

display(df_pbi.select("business_date").groupBy("business_date").count().orderBy("business_date"))
display(df_cube.select("business_date").groupBy("business_date").count().orderBy("business_date"))

df_pbi.createOrReplaceTempView("df_pbi")
df_cube.createOrReplaceTempView("df_cube")








using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

public static class SqlIdentifierHelper
{
    public static string ToSqlIdentifier(string input, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be null or empty.", nameof(input));

        // 1. Normalize accents (if any)
        string normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (char c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        string cleaned = sb.ToString().Normalize(NormalizationForm.FormC);

        // 2. Replace common semantic separators with underscore
        cleaned = cleaned
            .Replace("+", " plus ")
            .Replace("/", " ")
            .Replace("-", " ")
            .Replace("(", " ")
            .Replace(")", " ");

        // 3. Remove everything except letters, numbers and spaces
        cleaned = Regex.Replace(cleaned, @"[^a-zA-Z0-9\s]", "");

        // 4. Convert spaces to underscores
        cleaned = Regex.Replace(cleaned.Trim(), @"\s+", "_");

        // 5. Remove duplicate underscores
        cleaned = Regex.Replace(cleaned, @"_+", "_");

        // 6. Lowercase for consistency (optional but recommended)
        cleaned = cleaned.ToLowerInvariant();

        // 7. Ensure it does not start with a digit
        if (char.IsDigit(cleaned[0]))
            cleaned = "m_" + cleaned;

        // 8. Trim length safely
        if (cleaned.Length > maxLength)
            cleaned = cleaned.Substring(0, maxLength);

        return cleaned;
    }
}








catalog = "your_catalog"
schema = "your_schema"
owner_to_delete = "service_principal_name_or_user"

query = f"""
SELECT metric_view_name
FROM {catalog}.information_schema.metric_views
WHERE table_schema = '{schema}'
AND table_owner = '{owner_to_delete}'
"""

metric_views = spark.sql(query)

for row in metric_views.collect():
    mv_name = row.metric_view_name
    full_name = f"{catalog}.{schema}.{mv_name}"
    print(f"Dropping {full_name}")
    spark.sql(f"DROP METRIC VIEW IF EXISTS {full_name}")





static bool HasAnyExplicitValues(Dictionary<FilterType, List<string>?>? filter)
{
    if (filter is null || filter.Count == 0) return false;

    return (filter.TryGetValue(FilterType.Include, out var inc) && inc is { Count: > 0 })
        || (filter.TryGetValue(FilterType.Exclude, out var exc) && exc is { Count: > 0 });
}




void ApplyFilter(
    Dictionary<FilterType, List<string>?>? filter,
    IReadOnlyDictionary<string, HashSet<int>> lookup)
{
    // If user did not explicitly provide any include/exclude values -> do nothing
    if (!HasAnyExplicitValues(filter))
        return;

    // INCLUDE: only if explicitly provided and non-empty
    if (filter!.TryGetValue(FilterType.Include, out var incValues) && incValues is { Count: > 0 })
    {
        var inc = ResolveMany(incValues, lookup);

        // IMPORTANT: this "force empty" is correct ONLY when include was explicitly requested (Count > 0)
        if (inc is null || inc.Count == 0)
        {
            includeSets.Add(new HashSet<int>()); // forces empty after intersection
            return;
        }

        includeSets.Add(inc);
    }

    // EXCLUDE: only if explicitly provided and non-empty
    if (filter.TryGetValue(FilterType.Exclude, out var excValues) && excValues is { Count: > 0 })
    {
        var exc = ResolveMany(excValues, lookup);
        if (exc is { Count: > 0 })
            excludeUnion.UnionWith(exc);
    }
}




public IReadOnlyList<MrvPartitionInfo> Get(
    Dictionary<FilterMode, IReadOnlyCollection<string>?>? measureNames = null,
    Dictionary<FilterMode, IReadOnlyCollection<string>?>? scenarioGroups = null,
    Dictionary<FilterMode, IReadOnlyCollection<string>?>? mdstDescriptions = null)
{
    var snap = _snapshot ?? throw new InvalidOperationException("Cache not loaded.");

    // Include constraints -> intersect
    var includeSets = new List<HashSet<int>>(capacity: 3);

    // Exclusions -> remove at the end
    var excludeUnion = new HashSet<int>();

    // Helper to process one dimension (measure / scenario / mdst)
    void ApplyFilter(
        Dictionary<FilterMode, IReadOnlyCollection<string>?>? filter,
        IReadOnlyDictionary<string, HashSet<int>> lookup)
    {
        if (filter is null || filter.Count == 0)
            return;

        // INCLUDE
        if (filter.TryGetValue(FilterMode.Include, out var incValues) && incValues is { Count: > 0 })
        {
            var inc = ResolveMany(incValues, lookup);
            if (inc is null || inc.Count == 0)
            {
                // If you asked to INCLUDE some values but none resolve, result must be empty.
                includeSets.Add(new HashSet<int>()); // forces empty after intersection
                return;
            }

            includeSets.Add(inc);
        }

        // EXCLUDE
        if (filter.TryGetValue(FilterMode.Exclude, out var excValues) && excValues is { Count: > 0 })
        {
            var exc = ResolveMany(excValues, lookup);
            if (exc is not null && exc.Count > 0)
                excludeUnion.UnionWith(exc);
        }
    }

    // Apply your three filter dimensions
    ApplyFilter(measureNames, snap.ByMeasure);
    ApplyFilter(scenarioGroups, snap.ByScenario);
    ApplyFilter(mdstDescriptions, snap.ByMdst);

    // --- Build starting result set ---
    HashSet<int> resultIdx;

    if (includeSets.Count > 0)
    {
        // Intersect smallest-to-largest
        includeSets.Sort((a, b) => a.Count.CompareTo(b.Count));

        resultIdx = new HashSet<int>(includeSets[0]);
        for (var i = 1; i < includeSets.Count; i++)
            resultIdx.IntersectWith(includeSets[i]);
    }
    else
    {
        // No include filters => start from ALL partitions (universe)
        // snap.Rows appears to be your row list indexed by partition idx
        resultIdx = new HashSet<int>(Enumerable.Range(0, snap.Rows.Count));
    }

    // --- Apply exclusions last ---
    if (excludeUnion.Count > 0)
        resultIdx.ExceptWith(excludeUnion);

    if (resultIdx.Count == 0)
        return Array.Empty<MrvPartitionInfo>();

    // Materialize rows
    return resultIdx.Select(i => snap.Rows[i]).ToList();
}








SELECT
[CoB Date].[CoB Date].MEMBERS ON ROWS
FROM [Arisk3D]

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












