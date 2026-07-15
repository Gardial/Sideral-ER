using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RandomMagicConversion;

internal enum AshOfWarSourceParamKind
{
    ShopLineupParam,
    ItemLotParamMap,
    ItemLotParamEnemy
}

internal sealed class AshOfWarSourceEntry
{
    public AshOfWarSourceParamKind ParamKind { get; init; }
    public int RowId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string SourceDescription { get; init; } = string.Empty;
    public bool IsEniaRemembranceShop { get; init; }

    public string ParamName => ParamKind switch
    {
        AshOfWarSourceParamKind.ShopLineupParam => "ShopLineupParam",
        AshOfWarSourceParamKind.ItemLotParamMap => "ItemLotParam_map",
        AshOfWarSourceParamKind.ItemLotParamEnemy => "ItemLotParam_enemy",
        _ => throw new InvalidOperationException($"Param kind unsupported: {ParamKind}")
    };
}

internal sealed class AshOfWarSourceSelection
{
    public List<AshOfWarSourceEntry> Entries { get; init; } = new();
    public int ExcludedEniaShopRowCount { get; init; }
}

internal sealed class AshOfWarSourceIndex
{
    private static readonly Regex EntryRegex = new(
        "^\\s*-\\s*(?<quote>['\\\"]?)(?<itemName>.+?) - (?<entryType>shop|enemy lot|lot) (?<rowId>\\d+)\\[(?<source>[^\\]]+)\\]",
        RegexOptions.Compiled);

    private readonly List<AshOfWarSourceEntry> _entries;

    private AshOfWarSourceIndex(List<AshOfWarSourceEntry> entries)
    {
        _entries = entries;
    }

    public static AshOfWarSourceIndex Load(string itemslotsPath)
    {
        if (string.IsNullOrWhiteSpace(itemslotsPath))
            throw new ArgumentException("Le chemin de itemslots.txt est vide.", nameof(itemslotsPath));

        if (!File.Exists(itemslotsPath))
            throw new FileNotFoundException("itemslots.txt introuvable.", itemslotsPath);

        var entriesByKey = new Dictionary<string, AshOfWarSourceEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in File.ReadLines(itemslotsPath))
        {
            Match match = EntryRegex.Match(rawLine);
            if (!match.Success)
                continue;

            string itemName = match.Groups["itemName"].Value.Trim();
            if (!itemName.StartsWith("Ash of War:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!int.TryParse(match.Groups["rowId"].Value, out int rowId))
                continue;

            AshOfWarSourceParamKind paramKind = ParseParamKind(match.Groups["entryType"].Value);
            string sourceDescription = match.Groups["source"].Value.Trim();
            bool isEniaShop = paramKind == AshOfWarSourceParamKind.ShopLineupParam &&
                (sourceDescription.Contains("Finger Reader Enia", StringComparison.OrdinalIgnoreCase) ||
                 rawLine.Contains("Remembrance of ", StringComparison.OrdinalIgnoreCase));

            string key = $"{paramKind}:{rowId}";
            if (entriesByKey.ContainsKey(key))
                continue;

            entriesByKey[key] = new AshOfWarSourceEntry
            {
                ParamKind = paramKind,
                RowId = rowId,
                ItemName = itemName,
                SourceDescription = sourceDescription,
                IsEniaRemembranceShop = isEniaShop
            };
        }

        return new AshOfWarSourceIndex(entriesByKey.Values
            .OrderBy(entry => entry.ParamName)
            .ThenBy(entry => entry.RowId)
            .ToList());
    }

    public AshOfWarSourceSelection SelectEntries(AshOfWarToMagicConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        List<AshOfWarSourceEntry> filtered = _entries
            .Where(entry => IsIncludedByKind(entry, config))
            .ToList();

        int excludedEniaShopRowCount = 0;

        if (config.ExcludeEniaRemembranceShop)
        {
            excludedEniaShopRowCount = filtered.Count(entry => entry.IsEniaRemembranceShop);
            filtered = filtered
                .Where(entry => !entry.IsEniaRemembranceShop)
                .ToList();
        }

        return new AshOfWarSourceSelection
        {
            Entries = filtered
                .OrderBy(entry => entry.ParamName)
                .ThenBy(entry => entry.RowId)
                .ToList(),
            ExcludedEniaShopRowCount = excludedEniaShopRowCount
        };
    }

    private static AshOfWarSourceParamKind ParseParamKind(string entryType)
    {
        return entryType.Trim().ToLowerInvariant() switch
        {
            "shop" => AshOfWarSourceParamKind.ShopLineupParam,
            "lot" => AshOfWarSourceParamKind.ItemLotParamMap,
            "enemy lot" => AshOfWarSourceParamKind.ItemLotParamEnemy,
            _ => throw new InvalidOperationException($"Type d'entree itemslots non supporte : {entryType}")
        };
    }

    private static bool IsIncludedByKind(AshOfWarSourceEntry entry, AshOfWarToMagicConfig config)
    {
        return entry.ParamKind switch
        {
            AshOfWarSourceParamKind.ShopLineupParam => config.IncludeShopRows,
            AshOfWarSourceParamKind.ItemLotParamMap => config.IncludeItemLotMapRows,
            AshOfWarSourceParamKind.ItemLotParamEnemy => config.IncludeItemLotEnemyRows,
            _ => false
        };
    }
}
