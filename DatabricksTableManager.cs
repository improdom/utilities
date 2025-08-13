public sealed class DequeuePolicy
{
    public int MaxSlotsPerCycle { get; init; } = 16;

    public IReadOnlyDictionary<PartitionCategory,int> PartsPerCategory { get; init; } =
        new Dictionary<PartitionCategory,int> {
            { PartitionCategory.ExtraLarge, 4 }, // your constraint
            { PartitionCategory.Large,      4 },
            { PartitionCategory.Medium,    10 },
            { PartitionCategory.Small,      2 }  // crucial for packing
        };

    public IReadOnlyDictionary<PartitionCategory,int> CapPerCycle { get; init; } =
        new Dictionary<PartitionCategory,int> {
            { PartitionCategory.ExtraLarge, 1 },
            { PartitionCategory.Large,      3 },
            { PartitionCategory.Medium,     1 }, // bump to 2 if stable
            { PartitionCategory.Small,      int.MaxValue }
        };

    public int CostOf(EventSet e) => PartsPerCategory[e.Category];
}
