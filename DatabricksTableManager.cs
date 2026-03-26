using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public static string Rename(string yaml)
{
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    var serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    var model = deserializer.Deserialize<MetricView>(yaml);

    foreach (var dim in model.Dimensions)
    {
        dim.Name = TransformName(dim.Name);
    }

    return serializer.Serialize(model);
}

private static string TransformName(string name)
{
    return name
        .Replace("_", " ")
        .Trim()
        .ToLowerInvariant(); // or TitleCase if needed
}
