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
