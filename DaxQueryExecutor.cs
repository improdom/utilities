foreach (var t in Model.Tables.Where(t => t.InMemory)) {
    foreach (var c in t.Columns) {
        if (c.Aggregation != null)ll {
            var a = c.Aggregation;
            Print($"{t.Name}.{c.Name} | {a.Summarization} -> {a.DetailTable?.Name}.{a.DetailColumn?.Name}");
        
}

----------
    using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed class PartitionStrategyOptions
{
    // Root section: "PartitionStrategy"
    [JsonPropertyName("partitionSourceKeyMapping")]
    public List<PartitionSourceKeyMapItem> PartitionSourceKeyMapping { get; set; } = new();

    [JsonPropertyName("PartitionCategories")]
    public List<PartitionCategoryItem> PartitionCategories { get; set; } = new();
}

public sealed class PartitionSourceKeyMapItem
{
    [JsonPropertyName("compositeSourceKey")]
    public string CompositeSourceKey { get; set; } = string.Empty;

    [JsonPropertyName("partitionCategory")]
    public string PartitionCategory { get; set; } = string.Empty;
}

public sealed class PartitionCategoryItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("numberOfPartitions")]
    public int NumberOfPartitions { get; set; }
}
