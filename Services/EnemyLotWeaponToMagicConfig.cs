using System.Collections.Generic;

namespace RandomMagicConversion;

public sealed class EnemyLotWeaponToMagicConfig
{
    public bool Enabled { get; init; }
    public bool ReplaceAllEligibleWeapons { get; init; }
    public string SpellPoolMode { get; init; } = ShopMagicPoolMode.Both.ToString();
    public string ProgressionBand { get; init; } = MagicProgressionBand.All.ToString();
    public List<EnemyLotWeaponToMagicEntry> Entries { get; init; } = new();
}

public sealed class EnemyLotWeaponToMagicEntry
{
    public int OldItemId { get; init; }
    public int OldItemCategory { get; init; } = 2;
}

public sealed class EnemyLotWeaponToMagicRunResult
{
    public int Seed { get; init; }
    public string SpellPoolMode { get; init; } = ShopMagicPoolMode.Both.ToString();
    public string ProgressionBand { get; init; } = MagicProgressionBand.All.ToString();
    public int SpellPoolCount { get; init; }
    public int SorceryPoolCount { get; init; }
    public int IncantationPoolCount { get; init; }
    public int EarlyPoolCount { get; init; }
    public int MidPoolCount { get; init; }
    public int LatePoolCount { get; init; }
    public int MinSpellPrice { get; init; }
    public int MaxSpellPrice { get; init; }
    public int ExcludedShopGoodsCount { get; init; }
    public int EligibleEnemyLotRowCount { get; init; }
    public List<EnemyLotWeaponToMagicRunMapping> Mappings { get; init; } = new();
}

public sealed class EnemyLotWeaponToMagicRunMapping
{
    public string ParamName { get; init; } = string.Empty;
    public int RowId { get; init; }
    public int SlotIndex { get; init; }
    public int OldItemId { get; init; }
    public int OldItemCategory { get; init; }
    public int NewGoodsId { get; init; }
    public int NewItemCategory { get; init; }
    public string SourceKinds { get; init; } = string.Empty;
}
