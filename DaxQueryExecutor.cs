private static HashSet<string> ExtractMeasures(string dax)
{
    var measures = new HashSet<string>();

    // Match [Measure Name] not preceded by a table name (like Table[Column])
    var measureRegex = new Regex(@"(?<!['\w\]]\s*)\[(?<measure>[^\[\]]+)\]", RegexOptions.IgnoreCase);

    foreach (Match match in measureRegex.Matches(dax))
    {
        var measureName = match.Groups["measure"].Value.Trim();
        measures.Add(measureName);
    }

    return measures;
}
