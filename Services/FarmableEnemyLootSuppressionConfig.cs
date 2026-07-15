using System.Collections.Generic;

namespace RandomMagicConversion;

public sealed class FarmableEnemyLootSuppressionConfig
{
    public bool Enabled { get; init; }
    public int MinimumSourceOccurrences { get; init; } = 3;
    public bool RequireZeroGetItemFlag { get; init; } = true;
}

public sealed class FarmableEnemyLootSuppressionRunResult
{
    public int MinimumSourceOccurrences { get; init; }
    public bool RequireZeroGetItemFlag { get; init; }
    public int SuppressedRowCount { get; init; }
    public int ClearedSlotCount { get; init; }
    public List<FarmableEnemyLootSuppressionRunMapping> Mappings { get; init; } = new();
}

public sealed class FarmableEnemyLootSuppressionRunMapping
{
    public int RowId { get; init; }
    public string SourceKinds { get; init; } = string.Empty;
    public int SourceReferenceCount { get; init; }
    public int OriginalGetItemFlagId { get; init; }
    public int ClearedSlotCount { get; init; }
    public List<int> ClearedSlotIndices { get; init; } = new();
    public List<int> ClearedItemIds { get; init; } = new();
}
