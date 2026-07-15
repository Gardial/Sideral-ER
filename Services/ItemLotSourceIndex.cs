using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RandomMagicConversion;

internal enum ItemLotParamKind
{
    Map,
    Enemy
}

internal enum ItemLotSourceKind
{
    Unknown,
    Asset,
    EventAsset,
    Enemy,
    EventEnemy,
    UnknownEntityEvent,
    Other
}

internal sealed class ItemLotSourceIndex
{
    private static readonly Regex LotRegex = new(
        @"- .* - (?<entryType>enemy lot|lot) (?<rowId>\d+)\[(?<source>[^\]]+)\]",
        RegexOptions.Compiled);

    private readonly Dictionary<int, HashSet<ItemLotSourceKind>> _mapSourceKindsByRowId;
    private readonly Dictionary<int, HashSet<ItemLotSourceKind>> _enemySourceKindsByRowId;
    private readonly Dictionary<int, int> _mapSourceReferenceCountsByRowId;
    private readonly Dictionary<int, int> _enemySourceReferenceCountsByRowId;

    private ItemLotSourceIndex(
        Dictionary<int, HashSet<ItemLotSourceKind>> mapSourceKindsByRowId,
        Dictionary<int, HashSet<ItemLotSourceKind>> enemySourceKindsByRowId,
        Dictionary<int, int> mapSourceReferenceCountsByRowId,
        Dictionary<int, int> enemySourceReferenceCountsByRowId)
    {
        _mapSourceKindsByRowId = mapSourceKindsByRowId;
        _enemySourceKindsByRowId = enemySourceKindsByRowId;
        _mapSourceReferenceCountsByRowId = mapSourceReferenceCountsByRowId;
        _enemySourceReferenceCountsByRowId = enemySourceReferenceCountsByRowId;
    }

    public static ItemLotSourceIndex Load(string itemslotsPath)
    {
        if (string.IsNullOrWhiteSpace(itemslotsPath))
            throw new ArgumentException("Le chemin de itemslots.txt est vide.", nameof(itemslotsPath));

        if (!File.Exists(itemslotsPath))
            throw new FileNotFoundException("itemslots.txt introuvable.", itemslotsPath);

        var mapSourceKindsByRowId = new Dictionary<int, HashSet<ItemLotSourceKind>>();
        var enemySourceKindsByRowId = new Dictionary<int, HashSet<ItemLotSourceKind>>();
        var mapSourceReferenceCountsByRowId = new Dictionary<int, int>();
        var enemySourceReferenceCountsByRowId = new Dictionary<int, int>();

        foreach (string line in File.ReadLines(itemslotsPath))
        {
            Match match = LotRegex.Match(line);
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups["rowId"].Value, out int rowId))
                continue;

            string source = match.Groups["source"].Value.Trim();
            string entryType = match.Groups["entryType"].Value.Trim();
            ItemLotSourceKind kind = ClassifySource(source);
            Dictionary<int, HashSet<ItemLotSourceKind>> sourceKindsByRowId = ResolveSourceDictionary(
                entryType,
                enemySourceKindsByRowId,
                mapSourceKindsByRowId);
            Dictionary<int, int> sourceReferenceCountsByRowId = ResolveReferenceCountDictionary(
                entryType,
                enemySourceReferenceCountsByRowId,
                mapSourceReferenceCountsByRowId);

            if (!sourceKindsByRowId.TryGetValue(rowId, out HashSet<ItemLotSourceKind> kinds))
            {
                kinds = new HashSet<ItemLotSourceKind>();
                sourceKindsByRowId[rowId] = kinds;
            }

            kinds.Add(kind);
            sourceReferenceCountsByRowId[rowId] = sourceReferenceCountsByRowId.TryGetValue(rowId, out int currentCount)
                ? currentCount + 1
                : 1;
        }

        return new ItemLotSourceIndex(
            mapSourceKindsByRowId,
            enemySourceKindsByRowId,
            mapSourceReferenceCountsByRowId,
            enemySourceReferenceCountsByRowId);
    }

    public bool IsPureMapLoot(int rowId)
    {
        return MatchesExclusiveKinds(_mapSourceKindsByRowId, rowId, IsAllowedMapKind);
    }

    public bool IsBossReward(int rowId)
    {
        return MatchesExclusiveKinds(_mapSourceKindsByRowId, rowId, IsAllowedBossKind);
    }

    public bool IsEnemyLoot(int rowId)
    {
        return MatchesExclusiveKinds(_enemySourceKindsByRowId, rowId, IsAllowedEnemyLotKind);
    }

    public bool HasSourceInfo(ItemLotParamKind paramKind, int rowId)
    {
        return GetSourceKinds(paramKind).ContainsKey(rowId);
    }

    public bool IsRegularEnemyOnly(int rowId)
    {
        return MatchesExclusiveKinds(_enemySourceKindsByRowId, rowId, kind => kind == ItemLotSourceKind.Enemy);
    }

    public bool HasSpecialEnemySourceKinds(int rowId)
    {
        return _enemySourceKindsByRowId.TryGetValue(rowId, out HashSet<ItemLotSourceKind> kinds)
            && kinds.Any(kind => kind != ItemLotSourceKind.Enemy);
    }

    public bool IsClearlyFarmableEnemyLoot(int rowId, int minimumSourceOccurrences = 3)
    {
        if (minimumSourceOccurrences < 1)
            minimumSourceOccurrences = 1;

        return IsRegularEnemyOnly(rowId)
            && GetSourceReferenceCount(ItemLotParamKind.Enemy, rowId) >= minimumSourceOccurrences;
    }

    public int GetSourceReferenceCount(ItemLotParamKind paramKind, int rowId)
    {
        Dictionary<int, int> referenceCounts = paramKind == ItemLotParamKind.Enemy
            ? _enemySourceReferenceCountsByRowId
            : _mapSourceReferenceCountsByRowId;

        return referenceCounts.TryGetValue(rowId, out int count)
            ? count
            : 0;
    }

    public string DescribeKinds(ItemLotParamKind paramKind, int rowId)
    {
        Dictionary<int, HashSet<ItemLotSourceKind>> sourceKindsByRowId = GetSourceKinds(paramKind);

        if (!sourceKindsByRowId.TryGetValue(rowId, out HashSet<ItemLotSourceKind> kinds) || kinds.Count == 0)
            return ItemLotSourceKind.Unknown.ToString();

        return string.Join(", ", kinds.OrderBy(kind => kind.ToString()));
    }

    private static ItemLotSourceKind ClassifySource(string source)
    {
        if (source.StartsWith("event asset ", StringComparison.OrdinalIgnoreCase))
            return ItemLotSourceKind.EventAsset;

        if (source.StartsWith("asset ", StringComparison.OrdinalIgnoreCase))
            return ItemLotSourceKind.Asset;

        if (source.StartsWith("event enemy ", StringComparison.OrdinalIgnoreCase))
            return ItemLotSourceKind.EventEnemy;

        if (source.StartsWith("enemy ", StringComparison.OrdinalIgnoreCase))
            return ItemLotSourceKind.Enemy;

        if (source.StartsWith("unknown entity event ", StringComparison.OrdinalIgnoreCase))
            return ItemLotSourceKind.UnknownEntityEvent;

        return ItemLotSourceKind.Other;
    }

    private static bool IsAllowedMapKind(ItemLotSourceKind kind)
    {
        return kind == ItemLotSourceKind.Asset || kind == ItemLotSourceKind.EventAsset;
    }

    private static bool IsAllowedBossKind(ItemLotSourceKind kind)
    {
        return kind == ItemLotSourceKind.EventEnemy;
    }

    private static bool IsAllowedEnemyLotKind(ItemLotSourceKind kind)
    {
        return kind == ItemLotSourceKind.Enemy
            || kind == ItemLotSourceKind.EventEnemy
            || kind == ItemLotSourceKind.UnknownEntityEvent;
    }

    private static Dictionary<int, HashSet<ItemLotSourceKind>> ResolveSourceDictionary(
        string entryType,
        Dictionary<int, HashSet<ItemLotSourceKind>> enemySourceKindsByRowId,
        Dictionary<int, HashSet<ItemLotSourceKind>> mapSourceKindsByRowId)
    {
        return entryType.StartsWith("enemy", StringComparison.OrdinalIgnoreCase)
            ? enemySourceKindsByRowId
            : mapSourceKindsByRowId;
    }

    private static Dictionary<int, int> ResolveReferenceCountDictionary(
        string entryType,
        Dictionary<int, int> enemySourceReferenceCountsByRowId,
        Dictionary<int, int> mapSourceReferenceCountsByRowId)
    {
        return entryType.StartsWith("enemy", StringComparison.OrdinalIgnoreCase)
            ? enemySourceReferenceCountsByRowId
            : mapSourceReferenceCountsByRowId;
    }

    private Dictionary<int, HashSet<ItemLotSourceKind>> GetSourceKinds(ItemLotParamKind paramKind)
    {
        return paramKind == ItemLotParamKind.Enemy
            ? _enemySourceKindsByRowId
            : _mapSourceKindsByRowId;
    }

    private bool MatchesExclusiveKinds(
        Dictionary<int, HashSet<ItemLotSourceKind>> sourceKindsByRowId,
        int rowId,
        Func<ItemLotSourceKind, bool> isAllowedKind)
    {
        if (!sourceKindsByRowId.TryGetValue(rowId, out HashSet<ItemLotSourceKind> kinds) || kinds.Count == 0)
            return false;

        bool hasAllowedKind = kinds.Any(isAllowedKind);
        bool hasBlockedKind = kinds.Any(kind => !isAllowedKind(kind));

        return hasAllowedKind && !hasBlockedKind;
    }
}
