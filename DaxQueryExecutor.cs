using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class DaxQueryFilterManager
{
    /// <summary>
    /// Ensures the provided DAX query includes a filter on 'COB Date'[COB Date], replacing any existing one.
    /// </summary>
    public static string InjectCobDateFilter(string daxQuery, DateTime cobDate)
    {
        string newFilter = $"KEEPFILTERS(TREATAS({{ DATE({cobDate.Year}, {cobDate.Month}, {cobDate.Day}) }}, 'COB Date'[COB Date]))";

        // Strip existing date filters
        var treatasPattern = new Regex(
            @"KEEPFILTERS\s*\(\s*TREATAS\s*\(\s*\{\s*DATE\s*\(\s*\d{4},\s*\d{1,2},\s*\d{1,2}\s*\)\s*\}\s*,\s*'[^']*COB\s*Date'\s*\[\s*COB\s*Date\s*\]\s*\)\s*\)",
            RegexOptions.IgnoreCase);

        var directEqPattern = new Regex(
            @"'[^']*COB\s*Date'\s*\[\s*COB\s*Date\s*\]\s*=\s*DATE\s*\(\s*\d{4},\s*\d{1,2},\s*\d{1,2}\s*\)",
            RegexOptions.IgnoreCase);

        daxQuery = treatasPattern.Replace(daxQuery, "");
        daxQuery = directEqPattern.Replace(daxQuery, "");

        // Handle SUMMARIZECOLUMNS
        var summarizeMatch = Regex.Match(daxQuery, @"(?i)SUMMARIZECOLUMNS\s*\((.*?)\)", RegexOptions.Singleline);
        if (summarizeMatch.Success)
        {
            string args = summarizeMatch.Groups[1].Value;
            string[] parts = SplitSummarizeColumnsArgs(args);
            int lastOutputIndex = FindLastOutputIndex(parts);

            var output = string.Join(",\n    ", parts[..(lastOutputIndex + 1)]);
            var filters = (lastOutputIndex < parts.Length - 1)
                ? string.Join(",\n    ", parts[(lastOutputIndex + 1)..])
                : "";

            string rebuilt = string.IsNullOrWhiteSpace(filters)
                ? $"{output},\n    {newFilter}"
                : $"{output},\n    {newFilter},\n    {filters}";

            return daxQuery.Replace(args, rebuilt);
        }

        // Handle CALCULATETABLE
        var calcMatch = Regex.Match(daxQuery, @"(?i)CALCULATETABLE\s*\(\s*", RegexOptions.IgnoreCase);
        if (calcMatch.Success)
        {
            int insertPos = calcMatch.Index + calcMatch.Length;
            return daxQuery.Insert(insertPos, newFilter + ",\n    ");
        }

        // Handle VAR __DS0FilterTable
        var ds0VarPattern = new Regex(@"(?i)var\s+__ds0filtertable\s*=\s*treatas\s*\([^\)]*\)");
        if (ds0VarPattern.IsMatch(daxQuery))
        {
            return treatasPattern.Replace(
                daxQuery,
                $"TREATAS({{ DATE({cobDate.Year}, {cobDate.Month}, {cobDate.Day}) }}, 'COB Date'[COB Date])"
            );
        }

        // Fallback: wrap entire query
        return $"EVALUATE CALCULATETABLE(\n    {daxQuery.Trim()},\n    {newFilter}\n)";
    }

    /// <summary>
    /// Splits the arguments of a SUMMARIZECOLUMNS call, respecting nesting and quotes.
    /// </summary>
    private static string[] SplitSummarizeColumnsArgs(string args)
    {
        var results = new List<string>();
        int depth = 0;
        int start = 0;
        bool inString = false;

        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '"' || c == '\'')
            {
                inString = !inString;
            }
            else if (!inString)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0)
                {
                    results.Add(args.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
        }

        if (start < args.Length)
            results.Add(args.Substring(start).Trim());

        return results.ToArray();
    }

    /// <summary>
    /// Returns the index of the last output column (before filters begin).
    /// </summary>
    private static int FindLastOutputIndex(string[] parts)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].TrimStart();
            if (part.StartsWith("KEEPFILTERS", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("FILTER", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("TREATAS", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("\""))
            {
                return i - 1;
            }
        }

        return parts.Length - 1;
    }
}
