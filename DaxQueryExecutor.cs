private static HashSet<string> ExtractTableColumns(string dax)
{
    var outputCols = new HashSet<string>();

    // Matches: 'Table Name'[Column Name] or Table[Column Name]
    // Handles optional single quotes and ignores DAX functions
    var columnRegex = new Regex(
        @"(?<![\w'])\s*'?(?<table>[^\[\]']+)'?\s*\[\s*(?<column>[^\[\]]+)\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    foreach (Match match in columnRegex.Matches(dax))
    {
        var table = match.Groups["table"].Value.Trim();
        var column = match.Groups["column"].Value.Trim();

        if (IsLikelyValidTableName(table))
        {
            outputCols.Add($"{(table.Contains(" ") ? $"'{table}'" : table)}[{column}]");
        }
    }

    return outputCols;
}


private static bool IsLikelyValidTableName(string table)
{
    var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "KEEPFILTERS", "TREATAS", "SUMMARIZECOLUMNS", "SELECTCOLUMNS",
        "FILTER", "CALCULATE", "ADDCOLUMNS", "VALUES", "DISTINCT", "VAR", "RETURN",
        "NOT", "AND", "OR", "TRUE", "FALSE", "IF"
    };

    return !reservedWords.Contains(table);
}
