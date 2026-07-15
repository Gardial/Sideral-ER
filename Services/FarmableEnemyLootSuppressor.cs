using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class FarmableEnemyLootSuppressor
{
    private readonly List<PARAMDEF> _paramdefs;
    private readonly ItemLotSourceIndex _itemLotSourceIndex;
    private readonly bool _verbose;

    public FarmableEnemyLootSuppressor(string defsDirectory, string itemslotsPath, bool verbose = false)
    {
        if (string.IsNullOrWhiteSpace(defsDirectory))
            throw new ArgumentException("Le chemin du dossier Defs est vide.", nameof(defsDirectory));

        if (!Directory.Exists(defsDirectory))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsDirectory}");

        _paramdefs = LoadParamdefs(defsDirectory);
        _itemLotSourceIndex = ItemLotSourceIndex.Load(itemslotsPath);
        _verbose = verbose;
    }

    public FarmableEnemyLootSuppressionRunResult ApplyToRegulation(
        string inputRegulationPath,
        string outputRegulationPath,
        FarmableEnemyLootSuppressionConfig config,
        EnemyLotWeaponToMagicRunResult enemyLotRunResult = null,
        WeaponIdCatalog weaponCatalog = null,
        string mappingOutputPath = null)
    {
        if (string.IsNullOrWhiteSpace(inputRegulationPath))
            throw new ArgumentException("Le chemin du regulation.bin d'entree est vide.", nameof(inputRegulationPath));

        if (!File.Exists(inputRegulationPath))
            throw new FileNotFoundException("regulation.bin d'entree introuvable.", inputRegulationPath);

        if (string.IsNullOrWhiteSpace(outputRegulationPath))
            throw new ArgumentException("Le chemin du regulation.bin de sortie est vide.", nameof(outputRegulationPath));

        if (config == null)
            throw new ArgumentNullException(nameof(config));

        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine("Farmable Enemy Loot Suppressor");
        Console.WriteLine("==================================================");
        Console.WriteLine($"Input regulation  : {inputRegulationPath}");
        Console.WriteLine($"Output regulation : {outputRegulationPath}");
        Console.WriteLine($"Enabled           : {config.Enabled}");
        Console.WriteLine($"Min occurrences   : {config.MinimumSourceOccurrences}");
        Console.WriteLine($"Require no flag   : {config.RequireZeroGetItemFlag}");

        if (!config.Enabled)
        {
            Console.WriteLine("Suppression des loots farmables desactivee.");
            if (!string.Equals(inputRegulationPath, outputRegulationPath, StringComparison.OrdinalIgnoreCase))
                File.Copy(inputRegulationPath, outputRegulationPath, overwrite: true);

            return new FarmableEnemyLootSuppressionRunResult
            {
                MinimumSourceOccurrences = config.MinimumSourceOccurrences,
                RequireZeroGetItemFlag = config.RequireZeroGetItemFlag,
                SuppressedRowCount = 0,
                ClearedSlotCount = 0
            };
        }

        BND4 bnd = LoadRegulation(inputRegulationPath);
        PARAM itemLotEnemyParam = LoadParam(bnd, "ItemLotParam_enemy");
        HashSet<(int RowId, int SlotIndex)> convertedWeaponSlots = BuildConvertedWeaponSlotIndex(enemyLotRunResult);

        var mappings = new List<FarmableEnemyLootSuppressionRunMapping>();

        foreach (PARAM.Row row in itemLotEnemyParam.Rows)
        {
            if (!ShouldProcessFarmableRow(row, config))
                continue;

            FarmableEnemyLootSuppressionRunMapping mapping = ClearBlockedWeaponLikeSlots(
                row,
                convertedWeaponSlots,
                weaponCatalog);

            if (mapping.ClearedSlotCount == 0)
                continue;

            mappings.Add(mapping);
        }

        BinderFile itemLotEnemyFile = FindBinderFile(bnd, "ItemLotParam_enemy");
        itemLotEnemyFile.Bytes = itemLotEnemyParam.Write();
        WriteRegulation(outputRegulationPath, bnd);

        var result = new FarmableEnemyLootSuppressionRunResult
        {
            MinimumSourceOccurrences = config.MinimumSourceOccurrences,
            RequireZeroGetItemFlag = config.RequireZeroGetItemFlag,
            SuppressedRowCount = mappings.Count,
            ClearedSlotCount = mappings.Sum(mapping => mapping.ClearedSlotCount),
            Mappings = mappings
        };

        if (!string.IsNullOrWhiteSpace(mappingOutputPath))
        {
            SaveRunMapping(mappingOutputPath, result);
            Console.WriteLine($"Farmable enemy loot mapping sauvegarde : {mappingOutputPath}");
        }

        Console.WriteLine($"Rows supprimees   : {result.SuppressedRowCount}");
        Console.WriteLine($"Slots nettoyes    : {result.ClearedSlotCount}");
        return result;
    }

    private bool ShouldProcessFarmableRow(PARAM.Row row, FarmableEnemyLootSuppressionConfig config)
    {
        if (_itemLotSourceIndex.HasSpecialEnemySourceKinds(row.ID))
            return false;

        if (config.RequireZeroGetItemFlag && TryReadInt(row, "getItemFlagId", out int getItemFlagId) && getItemFlagId > 0)
            return false;

        if (!RowHasAnyLoot(row))
            return false;

        if (!LooksLikeFarmableChanceDrop(row))
            return false;

        // We intentionally allow "unknown" rows here as long as they look like regular
        // chance-based enemy drops and have no event flag. The safety now comes from the
        // per-slot filter below: we only clear weapon/shield-derived slots, instead of
        // nuking the entire row as before.
        return true;
    }

    private FarmableEnemyLootSuppressionRunMapping ClearBlockedWeaponLikeSlots(
        PARAM.Row row,
        ISet<(int RowId, int SlotIndex)> convertedWeaponSlots,
        WeaponIdCatalog weaponCatalog)
    {
        var clearedItemIds = new List<int>();
        var clearedSlotIndices = new List<int>();
        int clearedSlotCount = 0;
        int originalGetItemFlagId = TryReadInt(row, "getItemFlagId", out int getItemFlagId)
            ? getItemFlagId
            : 0;

        for (int slot = 1; slot <= 8; slot++)
        {
            string suffix = slot.ToString("00");

            int itemId = TryReadInt(row, $"lotItemId{suffix}", out int existingItemId)
                ? existingItemId
                : 0;
            int category = TryReadInt(row, $"lotItemCategory{suffix}", out int existingCategory)
                ? existingCategory
                : 0;

            bool isConvertedWeaponSlot = convertedWeaponSlots.Contains((row.ID, slot));
            bool isDirectWeaponOrShieldSlot =
                weaponCatalog != null
                && category == 2
                && itemId > 0
                && weaponCatalog.IsWeapon(itemId)
                && !weaponCatalog.IsAmmo(itemId);

            if (!isConvertedWeaponSlot && !isDirectWeaponOrShieldSlot)
                continue;

            if (itemId > 0)
                clearedItemIds.Add(itemId);

            clearedSlotIndices.Add(slot);
            clearedSlotCount++;

            SetIntField(row, $"lotItemId{suffix}", 0);
            SetIntField(row, $"lotItemCategory{suffix}", 0);
            SetIntField(row, $"lotItemBasePoint{suffix}", 0);
            SetIntField(row, $"cumulateLotPoint{suffix}", 0);
            SetIntField(row, $"lotItemNum{suffix}", 0);
            SetIntField(row, $"enableLuck{suffix}", 0);
            SetIntField(row, $"cumulateReset{suffix}", 0);
        }

        string sourceKinds = _itemLotSourceIndex.DescribeKinds(ItemLotParamKind.Enemy, row.ID);
        int sourceReferenceCount = _itemLotSourceIndex.GetSourceReferenceCount(ItemLotParamKind.Enemy, row.ID);

        if (_verbose && clearedSlotCount > 0)
        {
            Console.WriteLine(
                $"Farmable weapon/shield loot suppressed | RowID={row.ID} | Kinds={sourceKinds} | " +
                $"Refs={sourceReferenceCount} | ClearedSlots={string.Join(",", clearedSlotIndices.Select(index => index.ToString("00")))}");
        }

        return new FarmableEnemyLootSuppressionRunMapping
        {
            RowId = row.ID,
            SourceKinds = sourceKinds,
            SourceReferenceCount = sourceReferenceCount,
            OriginalGetItemFlagId = originalGetItemFlagId,
            ClearedSlotCount = clearedSlotCount,
            ClearedSlotIndices = clearedSlotIndices,
            ClearedItemIds = clearedItemIds
        };
    }

    private static HashSet<(int RowId, int SlotIndex)> BuildConvertedWeaponSlotIndex(
        EnemyLotWeaponToMagicRunResult enemyLotRunResult)
    {
        if (enemyLotRunResult?.Mappings == null || enemyLotRunResult.Mappings.Count == 0)
            return new HashSet<(int RowId, int SlotIndex)>();

        return enemyLotRunResult.Mappings
            .Where(mapping => string.Equals(mapping.ParamName, "ItemLotParam_enemy", StringComparison.OrdinalIgnoreCase))
            .Select(mapping => (mapping.RowId, mapping.SlotIndex))
            .ToHashSet();
    }

    private static bool RowHasAnyLoot(PARAM.Row row)
    {
        for (int slot = 1; slot <= 8; slot++)
        {
            string suffix = slot.ToString("00");

            if (!TryReadInt(row, $"lotItemId{suffix}", out int itemId))
                continue;

            if (!TryReadInt(row, $"lotItemCategory{suffix}", out int category))
                continue;

            if (itemId > 0 && category > 0)
                return true;
        }

        return false;
    }

    private static bool LooksLikeFarmableChanceDrop(PARAM.Row row)
    {
        bool hasPopulatedSlot = false;
        bool anyLuckEnabled = false;
        bool anySubGuaranteedPoint = false;

        for (int slot = 1; slot <= 8; slot++)
        {
            string suffix = slot.ToString("00");

            if (!TryReadInt(row, $"lotItemId{suffix}", out int itemId) || itemId <= 0)
                continue;

            if (!TryReadInt(row, $"lotItemCategory{suffix}", out int category) || category <= 0)
                continue;

            hasPopulatedSlot = true;

            if (TryReadInt(row, $"enableLuck{suffix}", out int enableLuck) && enableLuck != 0)
                anyLuckEnabled = true;

            if (TryReadInt(row, $"lotItemBasePoint{suffix}", out int basePoint) && basePoint > 0 && basePoint < 1000)
                anySubGuaranteedPoint = true;
        }

        bool friendlyGhostExecutable = TryReadInt(row, "canExecByFriendlyGhost", out int canExecByFriendlyGhost)
            && canExecByFriendlyGhost != 0;
        bool hostileGhostExecutable = TryReadInt(row, "canExecByHostileGhost", out int canExecByHostileGhost)
            && canExecByHostileGhost != 0;

        return hasPopulatedSlot
            && (anyLuckEnabled || anySubGuaranteedPoint)
            && friendlyGhostExecutable
            && !hostileGhostExecutable;
    }

    private static void SaveRunMapping(string outputPath, FarmableEnemyLootSuppressionRunResult result)
    {
        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(result, options);
        File.WriteAllText(outputPath, json);
    }

    private PARAM LoadParam(BND4 bnd, string paramName)
    {
        BinderFile file = FindBinderFile(bnd, paramName);
        PARAM param = PARAM.Read(file.Bytes);

        bool applied;
        try
        {
            applied = param.ApplyParamdefCarefully(_paramdefs);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Echec ApplyParamdefCarefully sur {paramName} (ParamType: {param.ParamType}). {ex.Message}", ex);
        }

        if (!applied)
            throw new InvalidOperationException($"Paramdef non applique sur {paramName} (ParamType: {param.ParamType}).");

        return param;
    }

    private static BinderFile FindBinderFile(BND4 bnd, string paramName)
    {
        BinderFile file = bnd.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f.Name), paramName, StringComparison.OrdinalIgnoreCase));

        if (file == null)
            throw new InvalidOperationException($"Param introuvable dans le BND : {paramName}");

        return file;
    }

    private static BND4 LoadRegulation(string inputRegulationPath)
    {
        Console.WriteLine("Lecture du regulation.bin...");

        try
        {
            Console.WriteLine("Tentative via SFUtil.DecryptERRegulation(path)...");
            BND4 bnd = SFUtil.DecryptERRegulation(inputRegulationPath);
            Console.WriteLine("Regulation charge via dechiffrement.");
            return bnd;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DecryptERRegulation a echoue : {ex.Message}");
            Console.WriteLine("Fallback sur lecture BND4 brute...");
            byte[] raw = File.ReadAllBytes(inputRegulationPath);
            BND4 bnd = BND4.Read(raw);
            Console.WriteLine("Regulation charge via BND4.Read(raw).");
            return bnd;
        }
    }

    private static void WriteRegulation(string outputRegulationPath, BND4 bnd)
    {
        string outputDir = Path.GetDirectoryName(outputRegulationPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        Console.WriteLine("Ecriture du regulation final...");

        try
        {
            SFUtil.EncryptERRegulation(outputRegulationPath, bnd);
            Console.WriteLine($"Fichier ecrit via EncryptERRegulation : {outputRegulationPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EncryptERRegulation a echoue : {ex.Message}");
            Console.WriteLine("Fallback sur ecriture BND4 brute...");
            byte[] raw = bnd.Write();
            File.WriteAllBytes(outputRegulationPath, raw);
            Console.WriteLine($"Fichier brut ecrit : {outputRegulationPath}");
        }
    }

    private static List<PARAMDEF> LoadParamdefs(string defsDirectory)
    {
        Console.WriteLine($"Chargement des paramdefs farmable enemy loot depuis : {defsDirectory}");

        var defs = new List<PARAMDEF>();

        foreach (string xmlPath in Directory.GetFiles(defsDirectory, "*.xml", SearchOption.AllDirectories))
        {
            try
            {
                defs.Add(PARAMDEF.XmlDeserialize(xmlPath));
            }
            catch
            {
                // ignore
            }
        }

        Console.WriteLine($"Paramdefs charges : {defs.Count}");
        return defs;
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

    private static void SetIntField(PARAM.Row row, string fieldName, int value)
    {
        PARAM.Cell cell;

        try
        {
            cell = row[fieldName];
        }
        catch
        {
            return;
        }

        object raw = cell.Value;

        switch (raw)
        {
            case sbyte _:
                if (value < sbyte.MinValue || value > sbyte.MaxValue)
                    throw new InvalidOperationException($"{fieldName}: valeur hors plage sbyte ({value}).");
                cell.Value = (sbyte)value;
                break;

            case byte _:
                if (value < byte.MinValue || value > byte.MaxValue)
                    throw new InvalidOperationException($"{fieldName}: valeur hors plage byte ({value}).");
                cell.Value = (byte)value;
                break;

            case short _:
                if (value < short.MinValue || value > short.MaxValue)
                    throw new InvalidOperationException($"{fieldName}: valeur hors plage short ({value}).");
                cell.Value = (short)value;
                break;

            case ushort _:
                if (value < ushort.MinValue || value > ushort.MaxValue)
                    throw new InvalidOperationException($"{fieldName}: valeur hors plage ushort ({value}).");
                cell.Value = (ushort)value;
                break;

            case int _:
                cell.Value = value;
                break;

            case uint _:
                if (value < 0)
                    throw new InvalidOperationException($"{fieldName}: valeur negative impossible pour uint ({value}).");
                cell.Value = (uint)value;
                break;

            case long _:
                cell.Value = (long)value;
                break;

            case ulong _:
                if (value < 0)
                    throw new InvalidOperationException($"{fieldName}: valeur negative impossible pour ulong ({value}).");
                cell.Value = (ulong)value;
                break;

            case float _:
                cell.Value = (float)value;
                break;

            case double _:
                cell.Value = (double)value;
                break;

            case bool _:
                cell.Value = value != 0;
                break;

            default:
                cell.Value = value;
                break;
        }
    }
}
