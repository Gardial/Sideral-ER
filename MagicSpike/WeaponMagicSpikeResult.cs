using System.Collections.Generic;

namespace RandomMagicConversion
{
    public sealed class WeaponMagicSpikeResult
    {
        public int TargetWeaponId { get; init; }
        public List<WeaponMagicSpikeOccurrence> Occurrences { get; } = new();
    }

    public sealed class WeaponMagicSpikeOccurrence
    {
        public string ParamName { get; init; } = string.Empty;
        public int RowId { get; init; }
        public string RowName { get; init; } = string.Empty;

        public int SlotIndex { get; init; }
        public string ItemIdField { get; init; } = string.Empty;
        public string CategoryField { get; init; } = string.Empty;

        public int ItemId { get; init; }
        public int CategoryValue { get; init; }
    }
}