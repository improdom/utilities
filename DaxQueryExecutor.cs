using System;
using System.Text.RegularExpressions;

public static class DaxQueryFilterManager
{
    public static string InjectCobDateFilter(string daxQuery, DateTime cobDate)
    {
        string newFilter = $"KEEPFILTERS(TREATAS({{ DATE({cobDate.Year}, {cobDate.Month}, {cobDate.Day}) }}, 'COB Date'[COB Date]))";

        // Regex patterns to detect and remove any existing COB Date filters
        var treatasPattern = new Regex(
            @"KEEPFILTERS\s*\(\s*TREATAS\s*\(\s*\{\s*DATE\s*\(\s*\d{4},\s*\d{1,2},\s*\d{1,2}\s*\)\s*\}\s*,\s*'[^']*COB\s*Date'\s*\[\s*COB\s*Date\s*\]\s*\)\s*\)",
            RegexOptions.IgnoreCase);

        var directEqPattern = new Regex(
            @"'[^']*COB\s*Date'\s*\[\s*COB\s*Date\s*\]\s*=\s*DATE\s*\(\s*\d{4},\s*\d{1,2},\s*\d{1,2}\s*\)",
            RegexOptions.IgnoreCase);

        var calcFilterPattern = new Regex(
            @"CALCULATETABLE\s*\(\s*(.*?)\s*,\s*(" + treatasPattern + "|" + directEqPattern + @")\s*(,)?",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Step 1: Remove any matching filters
        daxQuery = treatasPattern.Replace(daxQuery, "");
        daxQuery = directEqPattern.Replace(daxQuery, "");
        daxQuery = calcFilterPattern.Replace(daxQuery, "CALCULATETABLE($1,");

        // Step 2: Clean up possible syntax leftovers
        daxQuery = Regex.Replace(daxQuery, @",\s*,", ",");
        daxQuery = Regex.Replace(daxQuery, @"\(\s*,", "(");
        daxQuery = Regex.Replace(daxQuery, @",\s*\)", ")");

        // Step 3: Inject the new filter into the right location

        // SUMMARIZECOLUMNS
        var summarizeMatch = Regex.Match(daxQuery, @"(?i)SUMMARIZECOLUMNS\s*\(\s*");
        if (summarizeMatch.Success)
        {
            int insertPos = summarizeMatch.Index + summarizeMatch.Length;
            return daxQuery.Insert(insertPos, newFilter + ",\n    ");
        }

        // CALCULATETABLE
        var calcMatch = Regex.Match(daxQuery, @"(?i)CALCULATETABLE\s*\(\s*");
        if (calcMatch.Success)
        {
            int insertPos = calcMatch.Index + calcMatch.Length;
            return daxQuery.Insert(insertPos, newFilter + ",\n    ");
        }

        // VAR __DS0FilterTable = TREATAS(...)
        var ds0VarPattern = new Regex(@"(?i)var\s+__ds0filtertable\s*=\s*treatas\s*\([^\)]*\)");
        if (ds0VarPattern.IsMatch(daxQuery))
        {
            return treatasPattern.Replace(daxQuery,
                $"TREATAS({{ DATE({cobDate.Year}, {cobDate.Month}, {cobDate.Day}) }}, 'COB Date'[COB Date])");
        }

        // Fallback: wrap entire query
        return $"EVALUATE CALCULATETABLE(\n    {daxQuery.Trim()},\n    {newFilter}\n)";
    }
}
