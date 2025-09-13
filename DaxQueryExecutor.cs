// Tabular Editor script: Build a warm-up DAX that TOPN(1) from every IMPORT table.
// - Excludes calculation groups
// - Excludes disabled tables
// - Only includes tables with at least one Import partition
// The generated DAX is copied to the clipboard and written to the output pane.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.AnalysisServices.Tabular;

bool IsImportTable(Table t)
{
    // Include a table if it has at least one Import partition.
    // Also allow models where Mode might be null (older metadata) by treating missing as non-Import.
    try
    {
        return t.Partitions != null
            && t.Partitions.Any(p => p.Mode == PartitionMode.Import);
    }
    catch
    {
        return false;
    }
}

bool IsCalcGroup(Table t)
{
    // In TE, Calculation Groups are represented by CalculationGroupTable.
    return t is CalculationGroupTable;
}

var importTables = Model.Tables
    .Where(t => !IsCalcGroup(t))
    .Where(t => t.IsEnabled)               // skip disabled tables
    .Where(t => IsImportTable(t))
    .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (importTables.Count == 0)
{
    var msg = "-- No Import tables found.";
    Clipboard.SetText(msg);
    Output.Write(msg + Environment.NewLine);
    return;
}

// Build the UNION list: TOPN(1, 'Table')
var topnLines = new List<string>(importTables.Count);
foreach (var t in importTables)
{
    // DAX escaping for single quotes inside table names
    var safeName = t.Name.Replace("'", "''");
    topnLines.Add($"        TOPN(1, '{safeName}')");
}

// If you have an extremely large number of tables and are worried about
// UNION argument limits, you could chunk here. For most models, a single UNION is fine.
var unionBlock = string.Join(",\n", topnLines);

var dax = 
$@"EVALUATE
FILTER(
    UNION(
{unionBlock}
    ),
    FALSE()
)";

// Put the DAX on the clipboard and print a short report
Clipboard.SetText(dax);
Output.Write($"-- Generated warm-up DAX for {importTables.Count} import table(s){Environment.NewLine}");
foreach (var t in importTables)
{
    Output.Write($"--   included: {t.Name}{Environment.NewLine}");
}
Output.Write($"{Environment.NewLine}{dax}{Environment.NewLine}");
