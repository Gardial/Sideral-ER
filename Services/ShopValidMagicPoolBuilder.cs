using System.Collections.Generic;
using System.Linq;
using SoulsFormats;

namespace RandomMagicConversion;

internal static class ShopValidMagicPoolBuilder
{
    public static ShopValidMagicPoolBuildResult Build(
        PARAM shopLineupParam,
        ShopMagicPoolMode poolMode,
        MagicProgressionBand progressionBand = MagicProgressionBand.All)
    {
        Dictionary<int, ShopMagicShopInfo> shopInfoByGoodsId = shopLineupParam.Rows
            .Where(row =>
                TryReadInt(row, "equipType", out int equipType) &&
                TryReadInt(row, "equipId", out int equipId) &&
                equipType == 3 &&
                equipId > 0)
            .GroupBy(row =>
            {
                TryReadInt(row, "equipId", out int equipId);
                return equipId;
            })
            .ToDictionary(
                group => group.Key,
                group => new ShopMagicShopInfo
                {
                    MinPrice = group
                        .Select(row => TryReadInt(row, "value", out int price) ? price : int.MaxValue)
                        .DefaultIfEmpty(int.MaxValue)
                        .Min(),
                    HasImmediateAccess = group.Any(row =>
                        !TryReadInt(row, "eventFlag_forRelease", out int releaseFlag) ||
                        releaseFlag <= 0)
                });

        List<int> shopGoodsIds = shopInfoByGoodsId.Keys
            .OrderBy(id => id)
            .ToList();

        var sorceryIds = new List<int>();
        var incantationIds = new List<int>();
        var excludedIds = new List<int>();

        foreach (int goodsId in shopGoodsIds)
        {
            switch (ShopMagicPoolClassifier.Classify(goodsId))
            {
                case ShopMagicCategory.Sorcery:
                    sorceryIds.Add(goodsId);
                    break;

                case ShopMagicCategory.Incantation:
                    incantationIds.Add(goodsId);
                    break;

                default:
                    excludedIds.Add(goodsId);
                    break;
            }
        }

        List<int> modeFilteredIds = shopGoodsIds
            .Where(goodsId => IsAllowedInMode(goodsId, poolMode))
            .OrderBy(goodsId => GetComparablePrice(shopInfoByGoodsId, goodsId))
            .ThenBy(goodsId => goodsId)
            .ToList();

        ProgressionTieredMagicPool tieredPool = BuildTieredPool(modeFilteredIds, shopInfoByGoodsId);

        List<int> selectedIds = modeFilteredIds
            .Where(goodsId => IsAllowedInBand(goodsId, progressionBand, tieredPool))
            .ToList();

        return new ShopValidMagicPoolBuildResult
        {
            SelectedIds = selectedIds,
            SorceryIds = sorceryIds,
            IncantationIds = incantationIds,
            ExcludedIds = excludedIds,
            ProgressionBand = progressionBand.ToString(),
            EarlyIds = tieredPool.EarlyIds,
            MidIds = tieredPool.MidIds,
            LateIds = tieredPool.LateIds,
            MinPriceByGoodsId = shopInfoByGoodsId.ToDictionary(pair => pair.Key, pair => pair.Value.MinPrice),
            MinSelectedPrice = selectedIds.Count > 0
                ? selectedIds.Min(goodsId => GetComparablePrice(shopInfoByGoodsId, goodsId))
                : -1,
            MaxSelectedPrice = selectedIds.Count > 0
                ? selectedIds.Max(goodsId => GetComparablePrice(shopInfoByGoodsId, goodsId))
                : -1
        };
    }

    private static bool IsAllowedInMode(int goodsId, ShopMagicPoolMode poolMode)
    {
        ShopMagicCategory category = ShopMagicPoolClassifier.Classify(goodsId);

        return poolMode switch
        {
            ShopMagicPoolMode.Both => category == ShopMagicCategory.Sorcery || category == ShopMagicCategory.Incantation,
            ShopMagicPoolMode.SorceryOnly => category == ShopMagicCategory.Sorcery,
            ShopMagicPoolMode.IncantationOnly => category == ShopMagicCategory.Incantation,
            _ => false
        };
    }

    private static ProgressionTieredMagicPool BuildTieredPool(
        List<int> orderedIds,
        IReadOnlyDictionary<int, ShopMagicShopInfo> shopInfoByGoodsId)
    {
        var earlyIds = new List<int>();
        var midIds = new List<int>();
        var lateIds = new List<int>();

        if (orderedIds == null || orderedIds.Count == 0)
        {
            return new ProgressionTieredMagicPool
            {
                EarlyIds = earlyIds,
                MidIds = midIds,
                LateIds = lateIds
            };
        }

        List<int> baseGameIds = orderedIds
            .Where(goodsId => !ShopMagicPoolClassifier.IsDlcSpell(goodsId))
            .ToList();

        int earlyLimit = (int)System.Math.Ceiling(baseGameIds.Count / 3d);
        int midLimit = (int)System.Math.Ceiling(baseGameIds.Count * 2d / 3d);

        for (int index = 0; index < baseGameIds.Count; index++)
        {
            int goodsId = baseGameIds[index];
            bool hasImmediateAccess = shopInfoByGoodsId.TryGetValue(goodsId, out ShopMagicShopInfo info) && info.HasImmediateAccess;

            if (index < earlyLimit)
            {
                if (hasImmediateAccess)
                    earlyIds.Add(goodsId);
                else
                    midIds.Add(goodsId);
            }
            else if (index < midLimit)
                midIds.Add(goodsId);
            else
                lateIds.Add(goodsId);
        }

        foreach (int goodsId in orderedIds.Where(ShopMagicPoolClassifier.IsDlcSpell))
            lateIds.Add(goodsId);

        return new ProgressionTieredMagicPool
        {
            EarlyIds = earlyIds,
            MidIds = midIds,
            LateIds = lateIds
        };
    }

    private static bool IsAllowedInBand(
        int goodsId,
        MagicProgressionBand progressionBand,
        ProgressionTieredMagicPool tieredPool)
    {
        bool isEarly = tieredPool.EarlyIds.Contains(goodsId);
        bool isMid = tieredPool.MidIds.Contains(goodsId);
        bool isLate = tieredPool.LateIds.Contains(goodsId);

        return progressionBand switch
        {
            MagicProgressionBand.All => isEarly || isMid || isLate,
            MagicProgressionBand.EarlyOnly => isEarly,
            MagicProgressionBand.EarlyMid => isEarly || isMid,
            MagicProgressionBand.MidOnly => isMid,
            MagicProgressionBand.MidLate => isMid || isLate,
            MagicProgressionBand.LateOnly => isLate,
            _ => false
        };
    }

    private static int GetComparablePrice(IReadOnlyDictionary<int, ShopMagicShopInfo> shopInfoByGoodsId, int goodsId)
    {
        if (shopInfoByGoodsId != null &&
            shopInfoByGoodsId.TryGetValue(goodsId, out ShopMagicShopInfo info))
            return info.MinPrice;

        return int.MaxValue;
    }

    private static bool TryReadInt(PARAM.Row row, string fieldName, out int value)
    {
        value = 0;

        try
        {
            object raw = row[fieldName].Value;
            return TryConvertToInt(raw, out value);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryConvertToInt(object raw, out int value)
    {
        value = 0;

        if (raw is null)
            return false;

        switch (raw)
        {
            case sbyte v: value = v; return true;
            case byte v: value = v; return true;
            case short v: value = v; return true;
            case ushort v: value = v; return true;
            case int v: value = v; return true;
            case uint v when v <= int.MaxValue: value = (int)v; return true;
            case long v when v >= int.MinValue && v <= int.MaxValue: value = (int)v; return true;
            case ulong v when v <= (ulong)int.MaxValue: value = (int)v; return true;
            case bool v: value = v ? 1 : 0; return true;
            case float v when v >= int.MinValue && v <= int.MaxValue: value = (int)v; return true;
            case double v when v >= int.MinValue && v <= int.MaxValue: value = (int)v; return true;
            default:
                return int.TryParse(raw.ToString(), out value);
        }
    }
}

internal sealed class ShopValidMagicPoolBuildResult
{
    public List<int> SelectedIds { get; init; } = new();
    public List<int> SorceryIds { get; init; } = new();
    public List<int> IncantationIds { get; init; } = new();
    public List<int> ExcludedIds { get; init; } = new();
    public string ProgressionBand { get; init; } = MagicProgressionBand.All.ToString();
    public List<int> EarlyIds { get; init; } = new();
    public List<int> MidIds { get; init; } = new();
    public List<int> LateIds { get; init; } = new();
    public Dictionary<int, int> MinPriceByGoodsId { get; init; } = new();
    public int MinSelectedPrice { get; init; } = -1;
    public int MaxSelectedPrice { get; init; } = -1;
}

internal sealed class ProgressionTieredMagicPool
{
    public List<int> EarlyIds { get; init; } = new();
    public List<int> MidIds { get; init; } = new();
    public List<int> LateIds { get; init; } = new();
}

internal sealed class ShopMagicShopInfo
{
    public int MinPrice { get; init; }
    public bool HasImmediateAccess { get; init; }
}
