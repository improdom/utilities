
public class IgnoreEmptyEnumerableConverter<T> : JsonConverter<IEnumerable<T>>
{
    public override IEnumerable<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<IEnumerable<T>>(ref reader, options);

    public override void Write(Utf8JsonWriter writer, IEnumerable<T> value, JsonSerializerOptions options)
    {
        if (value?.Any() == true)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}





private static HashSet<string> ExtractSummarizeColumnsOnly(string dax)
{
    var outp



private static HashSet<string> ExtractSummarizeColumnsOnly(string dax)
{
    var outputCols = new HashSet<string>();

    // Match the full SUMMARIZECOLUMNS(...) call and extract the inner arguments
    var summarizeRegex = new Regex(@"SUMMARIZECOLUMNS\s*\(([\s\S]*?)\)", RegexOptions.IgnoreCase);
    var match = summarizeRegex.Match(dax);

    if (!match.Success) return outputCols;

    var args = match.Groups[1].Value;

    // Parse only top-level comma-separated arguments (avoid nested function calls)
    var topLevelArgs = SplitTopLevelArguments(args);

    // Match only valid table[column] references
    var columnRegex = new Regex(@"^\s*'?(?<table>[^\[\]']+)'?\s*\[\s*(?<column>[^\[\]]+)\s*\]\s*$");

    foreach (var arg in topLevelArgs)
    {
        var columnMatch = columnRegex.Match(arg);
        if (columnMatch.Success)
        {
            var table = columnMatch.Groups["table"].Value.Trim();
            var column = columnMatch.Groups["column"].Value.Trim();

            outputCols.Add(table.Contains(" ") ? $"'{table}'[{column}]" : $"{table}[{column}]");
        }
    }

    return outputCols;
}


private static List<string> SplitTopLevelArguments(string input)
{
    var result = new List<string>();
    var current = new StringBuilder();
    int nested = 0;

    foreach (char c in input)
    {
        if (c == '(') nested++;
        else if (c == ')') nested--;

        if (c == ',' && nested == 0)
        {
            result.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(c);
        }
    }

    if (current.Length > 0)
        result.Add(current.ToString());

    return result;
}
