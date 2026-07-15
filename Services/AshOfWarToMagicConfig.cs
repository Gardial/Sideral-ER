using System.Collections.Generic;

namespace RandomMagicConversion;

public sealed class AshOfWarToMagicConfig
{
    public bool Enabled { get; init; } = true;
    public string SpellPoolMode { get; init; } = ShopMagicPoolMode.Both.ToString();
    public string ProgressionBand { get; init; } = MagicProgressionBand.All.ToString();
    public bool IncludeShopRows { get; init; } = true;
    public bool IncludeItemLotMapRows { get; init; } = true;
    public bool IncludeItemLotEnemyRows { get; init; } = true;
    public bool ExcludeEniaRemembranceShop { get; init; } = true;
}

public sealed class AshOfWarToMagicRunResult
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
    public int SelectedShopRowCount { get; init; }
    public int SelectedItemLotMapRowCount { get; init; }
    public int SelectedItemLotEnemyRowCount { get; init; }
    public int ExcludedEniaShopRowCount { get; init; }
    public List<AshOfWarToMagicRunMapping> Mappings { get; init; } = new();
}

public sealed class AshOfWarToMagicRunMapping
{
    public string ParamName { get; init; } = string.Empty;
    public int RowId { get; init; }
    public int? SlotIndex { get; init; }
    public string EntryName { get; init; } = string.Empty;
    public string SourceDescription { get; init; } = string.Empty;
    public int OldGoodsId { get; init; }
    public int NewGoodsId { get; init; }
    public int OldValueType { get; init; }
    public int NewValueType { get; init; }
    public string ValueTypeField { get; init; } = string.Empty;
    public int Price { get; init; } = -1;
    public int SellQuantity { get; init; } = -1;
}
