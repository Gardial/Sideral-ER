using System.Collections.Generic;

namespace RandomMagicConversion;

internal enum ShopMagicCategory
{
    None,
    Sorcery,
    Incantation
}

internal static class ShopMagicPoolClassifier
{
    // These shop-valid Shadow of the Erdtree IDs are the currently observed DLC block
    // in local Magic row order. Non-spell outliers such as 8000, 53630 and 999999999
    // stay excluded on purpose.
    private static readonly HashSet<int> DlcSorceryIds = new()
    {
        2004300,
        2004310,
        2004320,
        2004500,
        2004510,
        2004700,
        2004710,
        2004900,
        2004910,
        2005000,
        2006200,
        2006210,
        2006300,
        2006400
    };

    private static readonly HashSet<int> DlcIncantationIds = new()
    {
        2006650,
        2006660,
        2006670,
        2006680,
        2006690,
        2006700,
        2006710,
        2006800,
        2006900,
        2006910,
        2006920,
        2007000,
        2007010,
        2007020,
        2007200,
        2007210,
        2007300,
        2007410,
        2007420,
        2007600,
        2007700,
        2007710,
        2007720,
        2007730,
        2007740,
        2007800,
        2007810,
        2007820
    };

    public static ShopMagicCategory Classify(int goodsId)
    {
        if (IsSorcery(goodsId))
            return ShopMagicCategory.Sorcery;

        if (IsIncantation(goodsId))
            return ShopMagicCategory.Incantation;

        return ShopMagicCategory.None;
    }

    public static bool IsDlcSpell(int goodsId)
    {
        return DlcSorceryIds.Contains(goodsId) || DlcIncantationIds.Contains(goodsId);
    }

    private static bool IsSorcery(int goodsId)
    {
        return IsInRange(goodsId, 4000, 4910)
            || IsInRange(goodsId, 5000, 5030)
            || IsInRange(goodsId, 5100, 5110)
            || goodsId == 6500
            || DlcSorceryIds.Contains(goodsId);
    }

    private static bool IsIncantation(int goodsId)
    {
        return goodsId == 5040
            || IsInRange(goodsId, 6000, 6490)
            || IsInRange(goodsId, 6510, 7903)
            || DlcIncantationIds.Contains(goodsId);
    }

    private static bool IsInRange(int value, int minInclusive, int maxInclusive)
    {
        return value >= minInclusive && value <= maxInclusive;
    }
}
