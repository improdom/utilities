public static string InjectOrReplaceCobDateFilter(string daxQuery, DateTime cobDate)
{
    // Normalize query to avoid casing/spacing issues for regex but keep original for rewriting
    string normalized = Regex.Replace(daxQuery, @"\s+", " ").ToLowerInvariant();

    string pattern = @"keepfilters\s*\(\s*treatas\s*\(\s*\{\s*date\s*\(\s*\d{4}\s*,\s*\d{1,2}\s*,\s*\d{1,2}\s*\)\s*\}\s*,\s*'[^']*cob\s*date'\s*\[\s*cob\s*date\s*\]\s*\)\s*\)";
    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
    var match = regex.Match(daxQuery);

    string newDate = $"KEEPFILTERS(TREATAS({{ DATE({cobDate.Year}, {cobDate.Month}, {cobDate.Day}) }}, 'COB Date'[COB Date]))";

    if (match.Success)
    {
        // Replace existing COB Date filter
        string existingFilter = match.Value;
        return daxQuery.Replace(existingFilter, newDate);
    }
    else
    {
        // Inject new filter: after first DEFINE or VAR block (or at the top if none)
        var injectIndex = daxQuery.IndexOf("RETURN", StringComparison.OrdinalIgnoreCase);
        if (injectIndex == -1)
            injectIndex = daxQuery.IndexOf("EVALUATE", StringComparison.OrdinalIgnoreCase);
        if (injectIndex == -1)
            injectIndex = daxQuery.IndexOf("VAR", StringComparison.OrdinalIgnoreCase);

        if (injectIndex > 0)
        {
            return daxQuery.Insert(injectIndex, newDate + "\n");
        }
        else
        {
            // Fallback: add to top of query
            return newDate + "\n" + daxQuery;
        }
    }
}
