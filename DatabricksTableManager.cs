public IReadOnlyList<MrvPartitionInfo> Get(
    IReadOnlyCollection<string>? measureNames = null,
    IReadOnlyCollection<string>? scenarioGroups = null,
    IReadOnlyCollection<string>? mdstDescriptions = null)
{
    var snap = _snapshot ?? throw new InvalidOperationException("Cache not loaded.");

    var sets = new List<HashSet<int>>(capacity: 3);

    // Helper local function to resolve multiple keys against a lookup
    static HashSet<int>? ResolveMany(
        IReadOnlyCollection<string> values,
        IReadOnlyDictionary<string, HashSet<int>> lookup)
    {
        HashSet<int>? union = null;

        foreach (var raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (!lookup.TryGetValue(raw.Trim(), out var idxSet))
                continue;

            union ??= new HashSet<int>();
            union.UnionWith(idxSet);
        }

        return union;
    }

    // Measure filter
    if (measureNames is { Count: > 0 })
    {
        var s = ResolveMany(measureNames, snap.ByMeasure);
        if (s is null || s.Count == 0)
            return Array.Empty<MrvPartitionInfo>();

        sets.Add(s);
    }

    // Scenario group filter
    if (scenarioGroups is { Count: > 0 })
    {
        var s = ResolveMany(scenarioGroups, snap.ByScenario);
        if (s is null || s.Count == 0)
            return Array.Empty<MrvPartitionInfo>();

        sets.Add(s);
    }

    // MDST description filter
    if (mdstDescriptions is { Count: > 0 })
    {
        var s = ResolveMany(mdstDescriptions, snap.ByMdst);
        if (s is null || s.Count == 0)
            return Array.Empty<MrvPartitionInfo>();

        sets.Add(s);
    }

    // No filters → empty (same behavior as your original)
    if (sets.Count == 0)
        return Array.Empty<MrvPartitionInfo>();

    // Intersect smallest → largest for efficiency
    sets.Sort((a, b) => a.Count.CompareTo(b.Count));

    var resultIdx = new HashSet<int>(sets[0]);
    for (var i = 1; i < sets.Count; i++)
        resultIdx.IntersectWith(sets[i]);

    // Materialize rows
    return resultIdx.Select(i => snap.Rows[i]).ToList();
}
