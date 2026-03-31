
using System;
using System.Collections.Generic;
using System.Linq;

public static class FilterMerger
{
    public static List<Filter> MergeFilters(IEnumerable<Filter> filters)
    {
        if (filters == null)
            return new List<Filter>();

        var result = new List<Filter>();

        // Keep filters that qualify for merge
        var mergeCandidates = filters
            .Where(CanBeMerged)
            .GroupBy(f => new MergeKey(
                f.Dimension,
                f.Attribute,
                f.FilterType,
                f.ScalarValue))
            .ToList();

        foreach (var group in mergeCandidates)
        {
            var first = group.First();

            var mergedValues = group
                .SelectMany(f => GetAllValues(f))
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.Value))
                .GroupBy(v => new { v.Value, v.IsNumeric })
                .Select(g => g.First())
                .ToList();

            var mergedFilter = new Filter
            {
                Dimension = first.Dimension,
                Attribute = first.Attribute,
                FilterType = first.FilterType,
                SourceTableName = first.SourceTableName,
                SourceColumnName = first.SourceColumnName,
                ScalarValue = null,          // moved into Values
                ScalarIsString = first.ScalarIsString,
                Values = mergedValues
            };

            result.Add(mergedFilter);
        }

        // Keep all non-mergeable filters unchanged
        var nonMergeCandidates = filters
            .Where(f => !CanBeMerged(f));

        result.AddRange(nonMergeCandidates);

        return result;
    }

    private static bool CanBeMerged(Filter filter)
    {
        if (filter == null)
            return false;

        // Must have exactly one scalar value
        if (string.IsNullOrWhiteSpace(filter.ScalarValue))
            return false;

        // Only merge filters that do not already have Values populated
        if (filter.Values != null && filter.Values.Count > 0)
            return false;

        return !string.IsNullOrWhiteSpace(filter.Dimension)
            && !string.IsNullOrWhiteSpace(filter.Attribute);
    }

    private static IEnumerable<FilterValue> GetAllValues(Filter filter)
    {
        var values = new List<FilterValue>();

        if (!string.IsNullOrWhiteSpace(filter.ScalarValue))
        {
            values.Add(new FilterValue(filter.ScalarValue, !filter.ScalarIsString));
        }

        if (filter.Values != null && filter.Values.Count > 0)
        {
            values.AddRange(filter.Values.Where(v => v != null));
        }

        return values;
    }

    private sealed record MergeKey(
        string? Dimension,
        string? Attribute,
        FilterType FilterType,
        string? ScalarValue);
}





public static List<Filter> MergeFilters(IEnumerable<Filter> filters)
{
    if (filters == null)
        return new List<Filter>();

    var result = new List<Filter>();

    var mergeCandidates = filters
        .Where(CanBeMerged)
        .GroupBy(f => new
        {
            f.Dimension,
            f.Attribute,
            f.FilterType
        });

    foreach (var group in mergeCandidates)
    {
        var first = group.First();

        var mergedValues = group
            .Select(f => new FilterValue(f.ScalarValue!, !f.ScalarIsString))
            .GroupBy(v => new { v.Value, v.IsNumeric })
            .Select(g => g.First())
            .ToList();

        result.Add(new Filter
        {
            Dimension = first.Dimension,
            Attribute = first.Attribute,
            FilterType = first.FilterType,
            SourceTableName = first.SourceTableName,
            SourceColumnName = first.SourceColumnName,
            ScalarValue = null,
            ScalarIsString = first.ScalarIsString,
            Values = mergedValues
        });
    }

    result.AddRange(filters.Where(f => !CanBeMerged(f)));

    return result;
}








using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Core;

public sealed class MeasuresYamlInjector
{
    public string Inject(
        string template,
        IEnumerable<MeasureMetadata> measures,
        string filter = "",
        IDictionary<string, string>? dimensionNameMap = null)
    {
        // Parse YAML template into DOM
        var stream = new YamlStream();
        stream.Load(new StringReader(template));

        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        if (!string.IsNullOrEmpty(filter))
        {
            // 1) Inject/replace "filter"
            var filterKey = new YamlScalarNode("filter");
            var filterValue = new YamlScalarNode(filter ?? string.Empty)
            {
                Style = ScalarStyle.Folded
            };

            root.Children[filterKey] = filterValue;
        }

        // 2) Walk "dimensions" node and rename "name"
        if (dimensionNameMap != null && dimensionNameMap.Count > 0)
        {
            RenameDimensionNames(root, dimensionNameMap);
        }

        // 3) Inject/replace "measures"
        var measuresKey = new YamlScalarNode("measures");
        var seq = new YamlSequenceNode();

        if (measures != null)
        {
            foreach (var m in measures)
            {
                var measureMap = new YamlMappingNode
                {
                    { new YamlScalarNode("name"), new YamlScalarNode(m?.Name ?? string.Empty) },
                    { new YamlScalarNode("expr"), new YamlScalarNode(m?.Base?.SqlExpression ?? string.Empty) }
                };

                seq.Add(measureMap);
            }
        }

        root.Children[measuresKey] = seq;

        // 4) Serialize only the root node
        var serializer = new SerializerBuilder()
            .DisableAliases()
            .Build();

        using var sw = new StringWriter();
        serializer.Serialize(sw, root);
        return sw.ToString();
    }

    private static void RenameDimensionNames(
        YamlMappingNode root,
        IDictionary<string, string> dimensionNameMap)
    {
        var dimensionsKey = new YamlScalarNode("dimensions");

        if (!root.Children.TryGetValue(dimensionsKey, out var dimensionsNode))
            return;

        if (dimensionsNode is not YamlSequenceNode dimensionsSequence)
            return;

        foreach (var item in dimensionsSequence.Children)
        {
            if (item is not YamlMappingNode dimensionMap)
                continue;

            var nameKey = new YamlScalarNode("name");

            if (!dimensionMap.Children.TryGetValue(nameKey, out var currentNameNode))
                continue;

            var currentName = (currentNameNode as YamlScalarNode)?.Value;

            if (string.IsNullOrWhiteSpace(currentName))
                continue;

            if (dimensionNameMap.TryGetValue(currentName, out var newName) &&
                !string.IsNullOrWhiteSpace(newName))
            {
                dimensionMap.Children[nameKey] = new YamlScalarNode(newName);
            }
        }
    }
}
