using System.Collections.Generic;

namespace RandomMagicConversion;

public sealed class StartingClassWeaponToMagicConfig
{
    public bool Enabled { get; init; }
    public string SpellPoolMode { get; init; } = ShopMagicPoolMode.Both.ToString();
    public string ProgressionBand { get; init; } = MagicProgressionBand.EarlyOnly.ToString();
    public bool RebuildStartingSpellLoadout { get; init; } = true;
    public bool InjectSupportCatalystWhenNeeded { get; init; } = true;
    public bool EnsureSecondSupportCatalystWhenPossible { get; init; } = true;
}

public sealed class StartingClassWeaponToMagicRunResult
{
    public int Seed { get; init; }
    public string SpellPoolMode { get; init; } = ShopMagicPoolMode.Both.ToString();
    public string ProgressionBand { get; init; } = MagicProgressionBand.EarlyOnly.ToString();
    public int SpellPoolCount { get; init; }
    public int SorceryPoolCount { get; init; }
    public int IncantationPoolCount { get; init; }
    public int RebuiltClassCount { get; init; }
    public List<StartingClassWeaponToMagicRunMapping> Classes { get; init; } = new();
}

public sealed class StartingClassWeaponToMagicRunMapping
{
    public int RowId { get; init; }
    public string ClassName { get; init; } = string.Empty;
    public string School { get; init; } = string.Empty;
    public List<string> ClearedWeaponSlots { get; init; } = new();
    public List<int> ClearedWeaponIds { get; init; } = new();
    public List<int> AssignedSpellIds { get; init; } = new();
    public string InjectedCatalystSlot { get; init; } = string.Empty;
    public int InjectedCatalystId { get; init; }
    public int InjectedCatalystCount { get; init; }
    public List<string> InjectedCatalystSlots { get; init; } = new();
    public List<int> InjectedCatalystIds { get; init; } = new();
    public int OldBaseMag { get; init; }
    public int NewBaseMag { get; init; }
    public int OldBaseFai { get; init; }
    public int NewBaseFai { get; init; }
    public int OldBaseLuc { get; init; }
    public int NewBaseLuc { get; init; }
    public int OldSoulLevel { get; init; }
    public int NewSoulLevel { get; init; }
}
