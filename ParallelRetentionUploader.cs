using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

public sealed class MeasuresYamlInjector
{
    public string Inject(string template, string filter, IEnumerable<MeasureMetadata> measures)
    {
        // Parse the YAML template into a DOM
        var stream = new YamlStream();
        stream.Load(new StringReader(template));

        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        // 1) Inject/replace "filter" (use folded style => ">" for long expressions)
        var filterKey = new YamlScalarNode("filter");
        var filterValue = new YamlScalarNode(filter ?? string.Empty) { Style = ScalarStyle.Folded };
        root.Children[filterKey] = filterValue;

        // 2) Inject/replace "measures"
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

        // 3) Serialize the ROOT NODE (not stream.Save) so we avoid:
        //    - anchors (&123 / *123)
        //    - document end marker (...)
        var serializer = new SerializerBuilder()
            .DisableAliases() // critical: prevents &... anchors and *... aliases
            .Build();

        using var sw = new StringWriter();
        serializer.Serialize(sw, root);
        return sw.ToString();
    }
}
