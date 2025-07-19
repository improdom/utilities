private static HashSet<string> ExtractSummarizeColumns(string dax)
{
    var outputCols = new HashSet<string>();
    
    // Regex to match the full SUMMARIZECOLUMNS(...) block
    var summarizeRegex = new Regex(@"SUMMARIZECOLUMNS\s*\((.*?)\)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    foreach (Match match in summarizeRegex.Matches(dax))
    {
        var args = match.Groups[1].Value;

        // Updated regex to match 'Table'[Column] or Table[Column]
        var columnRegex = new Regex(@"'?(?<table>[^\[\]']+)'?\[(?<column>[^\[\]]+)\]", RegexOptions.IgnoreCase);

        foreach (Match col in columnRegex.Matches(args))
        {
            // Optionally use only col.Value or concatenate table and column
            outputCols.Add(col.Value);  // or $"{col.Groups["table"].Value}[{col.Groups["column"].Value}]"
        }
    }

    return outputCols;
}
