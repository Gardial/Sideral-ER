using System.Collections.Generic;

namespace RandomMagicConversion;

public sealed class BossRewardWeaponToMagicConfig
{
    public bool Enabled { get; init; }
    public bool ReplaceAllEligibleWeapons { get; init; }
    public string SpellPoolMode { get; init; } = ShopMagicPoolMode.Both.ToString();
    public string ProgressionBand { get; init; } = MagicProgressionBand.All.ToString();
    public List<BossRewardWeaponToMagicEntry> Entries { get; init; } = new();
}

public sealed class BossRewardWeaponToMagicEntry
{
    public int OldItemId { get; init; }
    public int OldItemCategory { get; init; } = 2;
}

public sealed class BossRewardWeaponToMagicRunResult
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
    public int EligibleBossRewardRowCount { get; init; }
    public List<BossRewardWeaponToMagicRunMapping> Mappings { get; init; } = new();
}

public sealed class BossRewardWeaponToMagicRunMapping
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
