// Tabular Editor script: Build a DAX that UNIONs TOPN(1) from every IMPORT table,
// projecting to a consistent schema: (__Table, __Payload).
// The payload string concatenates *all* columns in the single row with type-aware formatting.

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.AnalysisServices.Tabular;

bool IsCalcGroup(Table t) => t is CalculationGroupTable;

bool IsImportTable(Table t)
{
    try
    {
        return t.IsEnabled
            && t.Partitions != null
            && t.Partitions.Any(p => p.Mode == PartitionMode.Import);
    }
    catch { return false; }
}

string EscapeTable(string tableName)
{
    // DAX table name needs single quotes; embedded single quotes are doubled.
    return "'" + tableName.Replace("'", "''") + "'";
}

string EscapeColumn(string colName)
{
    // DAX column reference uses [ ] and embedded ] must be doubled: ]
    return "[" + colName.Replace("]", "]]") + "]";
}

string BuildValueExpr(string tableName, Column col)
{
    var t = EscapeTable(tableName);
    var c = EscapeColumn(col.Name);

    switch (col.DataType)
    {
        case DataType.String:
            // Cast to string via concatenation with "" to avoid type issues on BLANK().
            return t + c + " & \"\"";
        case DataType.DateTime:
            return $"FORMAT({t}{c}, \"yyyy-mm-ddTHH:nn:ss\")";
        case DataType.Decimal:
        case DataType.Double:
        case DataType.Int64:
            return $"FORMAT({t}{c}, \"General Number\")";
        case DataType.Boolean:
            return $"IF({t}{c}, \"TRUE\", \"FALSE\")";
        // Handle Currency the same as Decimal if present in your TE version
        case DataType.Currency:
            return $"FORMAT({t}{c}, \"General Number\")";
        default:
            // Binaries/variants or unknown types: safe literal
            return "\"<unsupported>\"";
    }
}

string BuildPayloadExpr(string tableName, IEnumerable<Column> cols)
{
    // Create: "Col1=" & <expr1> & " | Col2=" & <expr2> & ...
    var parts = new List<string>();
    foreach (var col in cols)
    {
        var safeLabel = col.Name.Replace("\"", "\"\""); // for embedding in string literal
        var valExpr = BuildValueExpr(tableName, col);
        parts.Add($"\"{safeLabel}=\" & {valExpr}");
    }
    if (parts.Count == 0) return "\"\"";
    return string.Join(" & \" | \" & ", parts);
}

var importTables = Model.Tables
    .Where(t => !IsCalcGroup(t))
    .Where(t => IsImportTable(t))
    .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (importTables.Count == 0)
{
    var msg = "-- No Import tables found.";
    Clipboard.SetText(msg);
    Output.WriteLine(msg);
    return;
}

var sbUnion = new StringBuilder();
for (int i = 0; i < importTables.Count; i++)
{
    var t = importTables[i];
    var tEsc = EscapeTable(t.Name);

    // Collect all *data* columns (exclude measure-like/calculated table columns if needed)
    var tableCols = t.Columns
        .Where(c => c.IsVisible || !c.IsVisible) // include hidden too; adjust if you want visible only
        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var payloadExpr = BuildPayloadExpr(t.Name, tableCols);

    // SELECTCOLUMNS(TOPN(1,'Table'), "__Table","TableName","__Payload", <payload>)
    var block = $@"        SELECTCOLUMNS(
            TOPN(1, {tEsc}),
            ""__Table"", ""{t.Name.Replace("\"", "\"\"")}"",
            ""__Payload"", {payloadExpr}
        )";

    sbUnion.Append(block);
    if (i < importTables.Count - 1) sbUnion.Append(",\n");
}

// Final DAX
var dax = 
$@"EVALUATE
FILTER(
    UNION(
{sbUnion}
    ),
    FALSE()
)";

// Output + clipboard
Clipboard.SetText(dax);
Output.WriteLine($"-- Generated warm-up DAX for {importTables.Count} import table(s)");
foreach (var t in importTables) Output.WriteLine($"--   included: {t.Name}");
Output.WriteLine();
Output.WriteLine(dax);
