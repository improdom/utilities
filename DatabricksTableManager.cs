public sealed class DequeuePolicy
{
    public int MaxSlotsPerCycle { get; init; } = 16;

    // NEW: optional legacy behavior (usually keep false)
    public bool AlwaysIncludeAllSmalls { get; init; } = false;

    // NEW: explicit priority order (highest first)
    public IReadOnlyList<PartitionCategory> Priority { get; init; } =
        new[] {
            PartitionCategory.ExtraLarge,
            PartitionCategory.Large,
            PartitionCategory.Medium,
            PartitionCategory.Small
        };

    // Fixed partition counts per category (your latest)
    public IReadOnlyDictionary<PartitionCategory,int> PartsPerCategory { get; init; } =
        new Dictionary<PartitionCategory,int> {
            { PartitionCategory.ExtraLarge, 4 },
            { PartitionCategory.Large,      4 },
            { PartitionCategory.Medium,    10 },
            { PartitionCategory.Small,      2 }
        };

    // Per-cycle caps (by event count)
    public IReadOnlyDictionary<PartitionCategory,int> CapPerCycle { get; init; } =
        new Dictionary<PartitionCategory,int> {
            { PartitionCategory.ExtraLarge, 1 },
            { PartitionCategory.Large,      3 },
            { PartitionCategory.Medium,     1 }, // bump to 2 if telemetry allows
            { PartitionCategory.Small,      int.MaxValue }
        };

    public int CostOf(EventSet e) => PartsPerCategory[e.Category];
}
