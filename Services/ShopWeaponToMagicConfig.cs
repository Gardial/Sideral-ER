using System.Collections.Generic;

namespace RandomMagicConversion;

public enum ShopMagicPoolMode
{
    Both,
    SorceryOnly,
    IncantationOnly
}

public sealed class ShopWeaponToMagicConfig
{
    public bool Enabled { get; init; }
    public bool ReplaceAllEligibleWeapons { get; init; }
    public string SpellPoolMode { get; init; } = ShopMagicPoolMode.Both.ToString();
    public string ProgressionBand { get; init; } = MagicProgressionBand.All.ToString();
    public List<ShopWeaponToMagicEntry> Entries { get; init; } = new();
}

public sealed class ShopWeaponToMagicEntry
{
    public int OldEquipId { get; init; }
}

public sealed class ShopWeaponToMagicRunResult
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
    public List<ShopWeaponToMagicRunMapping> Mappings { get; init; } = new();
}

public sealed class ShopWeaponToMagicRunMapping
{
    public int RowId { get; init; }
    public int OldEquipId { get; init; }
    public int NewGoodsId { get; init; }
    public int OldEquipType { get; init; }
    public int NewEquipType { get; init; }
    public int Price { get; init; }
    public int SellQuantity { get; init; }
}
