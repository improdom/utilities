using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var inputPath = Path.GetFullPath(args[0]);
var outputCsvPath = args.Length > 1 ? Path.GetFullPath(args[1]) : null;

if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
{
    Console.Error.WriteLine($"Input path was not found: {inputPath}");
    return 1;
}

var files = CollectFiles(inputPath).ToList();
if (files.Count == 0)
{
    Console.Error.WriteLine("No supported files were found. Supported extensions: .json, .bim, .xmla, .tmsl, .tsml, .tmdl, .txt");
    return 1;
}

var allMatches = new List<KeepFiltersMatch>();
var totalMeasures = 0;

foreach (var file in files)
{
    var text = await File.ReadAllTextAsync(file);
    var measures = MeasureExtractor.ExtractMeasures(text).ToList();
    totalMeasures += measures.Count;

    foreach (var measure in measures)
    {
        foreach (var filter in KeepFiltersParser.Extract(measure.Expression))
        {
            allMatches.Add(new KeepFiltersMatch(
                Path.GetFileName(file),
                measure.Name,
                filter.Attribute,
                filter.Operator,
                filter.Values));
        }
    }
}

if (allMatches.Count == 0)
{
    Console.WriteLine($"Scanned {files.Count} file(s) and {totalMeasures} measure(s). No KEEPFILTERS clauses were found.");
    return 0;
}

PrintDetailedMatches(allMatches);
Console.WriteLine();
PrintAttributeSummary(allMatches);

if (!string.IsNullOrWhiteSpace(outputCsvPath))
{
    await File.WriteAllTextAsync(outputCsvPath, CsvExporter.Export(allMatches), Encoding.UTF8);
    Console.WriteLine();
    Console.WriteLine($"CSV exported to: {outputCsvPath}");
}

Console.WriteLine();
Console.WriteLine($"Scanned {files.Count} file(s), {totalMeasures} measure(s), found {allMatches.Count} KEEPFILTERS match(es).");
return 0;

static IEnumerable<string> CollectFiles(string inputPath)
{
    var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".bim", ".xmla", ".tmsl", ".tsml", ".tmdl", ".txt"
    };

    if (File.Exists(inputPath))
    {
        yield return inputPath;
        yield break;
    }

    foreach (var file in Directory.EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories))
    {
        if (supportedExtensions.Contains(Path.GetExtension(file)))
        {
            yield return file;
        }
    }
}

static void PrintDetailedMatches(IEnumerable<KeepFiltersMatch> matches)
{
    Console.WriteLine("Detailed matches");
    Console.WriteLine(new string('=', 16));

    var rows = matches
        .Select(match => new[]
        {
            match.Measure,
            match.Attribute,
            match.Operator,
            string.Join(", ", match.Values),
            match.FileName
        })
        .ToList();

    TablePrinter.Print(
        new[] { "Measure", "Attribute", "Operator", "Values", "File" },
        rows);
}

static void PrintAttributeSummary(IEnumerable<KeepFiltersMatch> matches)
{
    Console.WriteLine("Summary by attribute");
    Console.WriteLine(new string('=', 20));

    var rows = matches
        .GroupBy(match => match.Attribute, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => new[]
        {
            group.Key,
            string.Join(", ", group.SelectMany(match => match.Values).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        })
        .ToList();

    TablePrinter.Print(
        new[] { "Attribute", "Values" },
        rows);
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project KeepFiltersExtractor -- <input-file-or-folder> [output.csv]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine(@"  dotnet run --project KeepFiltersExtractor -- C:\models\database.tmsl");
    Console.WriteLine(@"  dotnet run --project KeepFiltersExtractor -- C:\models C:\temp\keepfilters.csv");
}

internal sealed record MeasureDefinition(string Name, string Expression);

internal sealed record ParsedKeepFilters(string Attribute, string Operator, IReadOnlyList<string> Values);

internal sealed record KeepFiltersMatch(
    string FileName,
    string Measure,
    string Attribute,
    string Operator,
    IReadOnlyList<string> Values);

internal static class MeasureExtractor
{
    private static readonly Regex JsonMeasureRegex =
        new("\"name\"\\s*:\\s*\"([^\"]+)\"[\\s\\S]{0,1200}?\"expression\"\\s*:\\s*(\\[[\\s\\S]*?\\]|\"(?:[^\"\\\\]|\\\\.)*\")",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TmdlMeasureRegex =
        new(@"measure\s+([^\r\n=]+?)\s*=\s*([\s\S]*?)(?=\n\s*(?:measure|table|column|partition|annotation)\b|\s*$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IEnumerable<MeasureDefinition> ExtractMeasures(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var measure in ExtractFromJson(text))
        {
            if (seen.Add($"{measure.Name}|{measure.Expression}"))
            {
                yield return measure;
            }
        }

        foreach (var measure in ExtractFromText(text))
        {
            if (seen.Add($"{measure.Name}|{measure.Expression}"))
            {
                yield return measure;
            }
        }
    }

    private static IEnumerable<MeasureDefinition> ExtractFromJson(string text)
    {
        var measures = new List<MeasureDefinition>();
        try
        {
            using var document = JsonDocument.Parse(text);
            measures.AddRange(Walk(document.RootElement, insideMeasures: false));
        }
        catch (JsonException)
        {
        }

        return measures;
    }

    private static IEnumerable<MeasureDefinition> Walk(JsonElement element, bool insideMeasures)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var nextInsideMeasures = insideMeasures;
                if (element.TryGetProperty("measures", out var measuresElement) && measuresElement.ValueKind == JsonValueKind.Array)
                {
                    nextInsideMeasures = true;
                }

                if (insideMeasures &&
                    element.TryGetProperty("name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String &&
                    element.TryGetProperty("expression", out var expressionElement))
                {
                    var expression = ReadExpression(expressionElement);
                    if (!string.IsNullOrWhiteSpace(expression))
                    {
                        yield return new MeasureDefinition(nameElement.GetString() ?? "<unnamed>", expression);
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    foreach (var child in Walk(property.Value, nextInsideMeasures || property.NameEquals("measures")))
                    {
                        yield return child;
                    }
                }

                yield break;
            }
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var child in Walk(item, insideMeasures))
                    {
                        yield return child;
                    }
                }

                yield break;
            default:
                yield break;
        }
    }

    private static IEnumerable<MeasureDefinition> ExtractFromText(string text)
    {
        foreach (Match match in JsonMeasureRegex.Matches(text))
        {
            var name = match.Groups[1].Value.Trim();
            var expression = ReadExpressionToken(match.Groups[2].Value);
            if (!string.IsNullOrWhiteSpace(expression))
            {
                yield return new MeasureDefinition(name, expression);
            }
        }

        foreach (Match match in TmdlMeasureRegex.Matches(text))
        {
            var name = Regex.Replace(match.Groups[1].Value.Trim(), @"\s+", " ");
            var expression = match.Groups[2].Value.Trim();
            if (!string.IsNullOrWhiteSpace(expression))
            {
                yield return new MeasureDefinition(name, expression);
            }
        }
    }

    private static string ReadExpression(JsonElement expressionElement)
    {
        return expressionElement.ValueKind switch
        {
            JsonValueKind.String => expressionElement.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(Environment.NewLine,
                expressionElement
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())),
            _ => string.Empty
        };
    }

    private static string ReadExpressionToken(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            var values = Regex.Matches(trimmed, @"""((?:[^""\\]|\\.)*)""")
                .Select(match => match.Groups[1].Value.Replace("\\\"", "\""));
            return string.Join(Environment.NewLine, values);
        }

        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal))
        {
            return trimmed[1..^1].Replace("\\\"", "\"");
        }

        return trimmed;
    }
}

internal static class KeepFiltersParser
{
    private static readonly Regex AttributeRegex =
        new(@"(?:'([^']+)'|([A-Za-z_][A-Za-z0-9_]*))\s*\[([^\]]+)\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OperatorRegex =
        new(@"^(=|==|<>|!=|<=|>=|<|>)\s*(.+)$",
            RegexOptions.Compiled);

    public static IEnumerable<ParsedKeepFilters> Extract(string expression)
    {
        var index = 0;
        while (index < expression.Length)
        {
            var foundAt = expression.IndexOf("KEEPFILTERS", index, StringComparison.OrdinalIgnoreCase);
            if (foundAt < 0)
            {
                yield break;
            }

            var openParen = expression.IndexOf('(', foundAt);
            if (openParen < 0)
            {
                yield break;
            }

            var clause = ReadBalancedParentheses(expression, openParen);
            if (clause is null)
            {
                yield break;
            }

            var parsed = ParseClause(clause[1..^1]);
            if (parsed is not null)
            {
                yield return parsed;
            }

            index = openParen + clause.Length;
        }
    }

    private static ParsedKeepFilters? ParseClause(string clauseText)
    {
        var compact = Regex.Replace(clauseText, @"\s+", " ").Trim();
        var attributeMatch = AttributeRegex.Match(compact);
        if (!attributeMatch.Success)
        {
            return null;
        }

        var tableName = attributeMatch.Groups[1].Success
            ? attributeMatch.Groups[1].Value
            : attributeMatch.Groups[2].Value;
        var columnName = attributeMatch.Groups[3].Value;
        var attribute = $"{tableName}[{columnName}]";
        var remainder = compact[(attributeMatch.Index + attributeMatch.Length)..].Trim();

        if (remainder.StartsWith("IN", StringComparison.OrdinalIgnoreCase))
        {
            var startBrace = remainder.IndexOf('{');
            var endBrace = remainder.LastIndexOf('}');
            if (startBrace >= 0 && endBrace > startBrace)
            {
                return new ParsedKeepFilters(attribute, "IN", ParseValueList(remainder[(startBrace + 1)..endBrace]));
            }
        }

        var operatorMatch = OperatorRegex.Match(remainder);
        if (operatorMatch.Success)
        {
            return new ParsedKeepFilters(
                attribute,
                operatorMatch.Groups[1].Value,
                new[] { CleanupValue(operatorMatch.Groups[2].Value) });
        }

        return new ParsedKeepFilters(attribute, "FILTER", new[] { CleanupValue(remainder) });
    }

    private static IReadOnlyList<string> ParseValueList(string valuesText)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var inString = false;

        for (var i = 0; i < valuesText.Length; i++)
        {
            var current = valuesText[i];
            var previous = i > 0 ? valuesText[i - 1] : '\0';

            if (current == '"' && previous != '\\')
            {
                inString = !inString;
            }

            if (current == ',' && !inString)
            {
                AddCurrentValue();
                continue;
            }

            builder.Append(current);
        }

        AddCurrentValue();
        return values;

        void AddCurrentValue()
        {
            var cleaned = CleanupValue(builder.ToString());
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                values.Add(cleaned);
            }

            builder.Clear();
        }
    }

    private static string CleanupValue(string value)
    {
        return value
            .Trim()
            .TrimStart('{')
            .TrimEnd('}')
            .Trim()
            .Trim('"')
            .Replace("\\\"", "\"");
    }

    private static string? ReadBalancedParentheses(string text, int openParenIndex)
    {
        var depth = 0;
        var inString = false;

        for (var i = openParenIndex; i < text.Length; i++)
        {
            var current = text[i];
            var previous = i > 0 ? text[i - 1] : '\0';

            if (current == '"' && previous != '\\')
            {
                inString = !inString;
            }

            if (inString)
            {
                continue;
            }

            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return text[openParenIndex..(i + 1)];
                }
            }
        }

        return null;
    }
}

internal static class CsvExporter
{
    public static string Export(IEnumerable<KeepFiltersMatch> matches)
    {
        var lines = new List<string>
        {
            "File,Measure,Attribute,Operator,Values"
        };

        lines.AddRange(matches.Select(match => string.Join(",",
            Escape(match.FileName),
            Escape(match.Measure),
            Escape(match.Attribute),
            Escape(match.Operator),
            Escape(string.Join(", ", match.Values)))));

        return string.Join(Environment.NewLine, lines);
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

internal static class TablePrinter
{
    public static void Print(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var widths = new int[headers.Count];
        for (var i = 0; i < headers.Count; i++)
        {
            widths[i] = headers[i].Length;
        }

        foreach (var row in rows)
        {
            for (var i = 0; i < headers.Count && i < row.Length; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        WriteRow(headers, widths);
        WriteRow(widths.Select(width => new string('-', width)).ToArray(), widths);
        foreach (var row in rows)
        {
            WriteRow(row, widths);
        }
    }

    private static void WriteRow(IReadOnlyList<string> values, IReadOnlyList<int> widths)
    {
        var formatted = values.Select((value, index) => value.PadRight(widths[index]));
        Console.WriteLine(string.Join(" | ", formatted));
    }
}
