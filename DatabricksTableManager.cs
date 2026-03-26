private static string? TryExtractTableFromM(string? expression)
{
    if (string.IsNullOrWhiteSpace(expression))
        return null;

    var matches = Regex.Matches(
        expression,
        @"\{\s*\[\s*Name\s*=\s*(?:""(?<nameq>[^""]+)""|(?<namev>[A-Za-z_][A-Za-z0-9_]*))\s*,\s*Kind\s*=\s*""(?<kind>[^""]+)""\s*\]\s*\}\s*\[Data\]",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    if (matches.Count == 0)
        return null;

    string? database = null;
    string? schema = null;
    string? table = null;

    foreach (Match match in matches)
    {
        var name = match.Groups["nameq"].Success
            ? match.Groups["nameq"].Value
            : match.Groups["namev"].Value;

        var kind = match.Groups["kind"].Value;

        if (kind.Equals("Database", StringComparison.OrdinalIgnoreCase))
            database = name;
        else if (kind.Equals("Schema", StringComparison.OrdinalIgnoreCase))
            schema = name;
        else if (kind.Equals("Table", StringComparison.OrdinalIgnoreCase))
            table = name;
    }

    if (!string.IsNullOrWhiteSpace(table))
    {
        if (!string.IsNullOrWhiteSpace(schema))
            return $"{schema}.{table}";

        if (!string.IsNullOrWhiteSpace(database))
            return $"{database}.{table}";

        return table;
    }

    return null;
}
