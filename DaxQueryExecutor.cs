using System;
using System.Text.RegularExpressions;

public static class DaxQueryFilterManager
{
    public static string InjectCobDateFilter(string daxQuery, DateTime cobDate)
    {
        string newFilter = $"KEEPFILTERS(TREATAS({{ DATE({cobDate.Year}, {cobDate.Month}, {cobDate.Day}) }}, 'COB Date'[COB Date]))";

        // Step 1: Strip any previous date filters
        var treatasPattern = new Regex(
            @"KEEPFILTERS\s*\(\s*TREATAS\s*\(\s*\{\s*DATE\s*\(\s*\d{4},\s*\d{1,2},\s*\d{1,2}\s*\)\s*\}\s*,\s*'[^']*COB\s*Date'\s*\[\s*COB\s*Date\s*\]\s*\)\s*\)",
            RegexOptions.IgnoreCase);

        var directEqPattern = new Regex(
            @"'[^']*COB\s*Date'\s*\[\s*COB\s*Date\s*\]\s*=\s*DATE\s*\(\s*\d{4},\s*\d{1,2},\s*\d{1,2}\s*\)",
            RegexOptions.IgnoreCase);

        daxQuery = treatasPattern.Replace(daxQuery, "");
        daxQuery = directEqPattern.Replace(daxQuery, "");

        // Step 2: Handle SUMMARIZECOLUMNS
        var summarizeMatch = Regex.Match(daxQuery, @"(?i)SUMMARIZECOLUMNS\s*\((.*?)\)", RegexOptions.Singleline);
        if (summarizeMatch.Success)
        {
            string args = summarizeMatch.Groups[1].Value;
            string[] parts = SplitSummarizeColumnsArgs(args);
            int lastOutputIndex = FindLastOutputIndex(parts);

            // Insert filter after output attributes
            var rebuilt = string.Join(",\n    ", parts[..(lastOutputIndex + 1)])
                        + ",\n    " + newFilter
                        + ((lastOutputIndex < parts.Length - 1)
                            ? ",\n    " + string.Join(",\n    ", parts[(lastOutputIndex + 1)..])
                            : "");

            return daxQuery.Replace(args, rebuilt);
        }

        // Step 3: Try CALCULATETABLE
        var calcMatch = Regex.Match(daxQuery, @"(?i)CALCULATETABLE\s*\(\s*", RegexOptions.IgnoreCase);
        if (calcMatch.Success)
        {
            int insertPos = calcMatch.Index + calcMatch.Length;
            return daxQuery.Insert(insertPos, newFilter + ",\n    ");
        }

        // Step 4: Try VAR __DS0FilterTable
        var ds0VarPattern = new Regex(@"(?i)var\s+__ds0filtertable\s*=\s*treatas\s*\([^\)]*\)");
        if (ds0VarPattern.IsMatch(daxQuery))
        {
            return treatasPattern.Replace(daxQuery,
                $"TREATAS({{ DATE({cobDate.Year}, {cobDate.Month}, {cobDate.Day}) }}, 'COB Date'[COB Date])");
        }

        // Fallback: wrap entire query
        return $"EVALUATE CALCULATETABLE(\n    {daxQuery.Trim()},\n    {newFilter}\n)";
    }

    // Splits arguments while preserving nested parentheses and string quotes
    private static string[] SplitSummarizeColumnsArgs(string args)
    {
        var results = new List<string>();
        int depth = 0;
        int start = 0;
        bool inString = false;

        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '"' || c == '\'') inString = !inString;
            if (!inString)
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

    // Heuristic: anything that starts with KEEPFILTERS or FILTER is not output
    private static int FindLastOutputIndex(string[] parts)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].TrimStart().StartsWith("KEEPFILTERS", StringComparison.OrdinalIgnoreCase)
                || parts[i].TrimStart().StartsWith("FILTER", StringComparison.OrdinalIgnoreCase)
                || parts[i].TrimStart().StartsWith("TREATAS", StringComparison.OrdinalIgnoreCase))
            {
                return i - 1;
            }
        }

        return parts.Length - 1;
    }
}
