using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class EnemyLotWeaponToMagicEditor
{
    private const int MagicGoodsItemLotCategory = 1;

    private readonly List<PARAMDEF> _paramdefs;
    private readonly ItemLotSourceIndex _itemLotSourceIndex;
    private readonly bool _verbose;

    public EnemyLotWeaponToMagicEditor(string defsDirectory, string itemslotsPath, bool verbose = false)
    {
        if (string.IsNullOrWhiteSpace(defsDirectory))
            throw new ArgumentException("Le chemin du dossier Defs est vide.", nameof(defsDirectory));

        if (!Directory.Exists(defsDirectory))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsDirectory}");

        _paramdefs = LoadParamdefs(defsDirectory);
        _itemLotSourceIndex = ItemLotSourceIndex.Load(itemslotsPath);
        _verbose = verbose;
    }

    public EnemyLotWeaponToMagicRunResult ApplyToRegulation(
        string inputRegulationPath,
        string outputRegulationPath,
        EnemyLotWeaponToMagicConfig config,
        int seed,
        string mappingOutputPath = null,
        WeaponIdCatalog weaponCatalog = null)
    {
        if (string.IsNullOrWhiteSpace(inputRegulationPath))
            throw new ArgumentException("Le chemin du regulation.bin d'entree est vide.", nameof(inputRegulationPath));

        if (!File.Exists(inputRegulationPath))
            throw new FileNotFoundException("regulation.bin d'entree introuvable.", inputRegulationPath);

        if (string.IsNullOrWhiteSpace(outputRegulationPath))
            throw new ArgumentException("Le chemin du regulation.bin de sortie est vide.", nameof(outputRegulationPath));

        if (config == null)
            throw new ArgumentNullException(nameof(config));

        ShopMagicPoolMode poolMode = ShopMagicPoolModeParser.Parse(config.SpellPoolMode);
        MagicProgressionBand progressionBand = MagicProgressionBandParser.Parse(config.ProgressionBand);

        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine("Enemy Lot Weapon -> Magic Editor (RandomPerRun)");
        Console.WriteLine("==================================================");
        Console.WriteLine($"Input regulation  : {inputRegulationPath}");
        Console.WriteLine($"Output regulation : {outputRegulationPath}");
        Console.WriteLine($"Seed              : {seed}");
        Console.WriteLine($"Entries           : {(config.ReplaceAllEligibleWeapons ? "ALL_ELIGIBLE" : config.Entries.Count)}");
        Console.WriteLine($"Spell pool mode   : {poolMode}");
        Console.WriteLine($"Progression band  : {progressionBand}");

        if (!config.Enabled)
        {
            Console.WriteLine("EnemyLotWeaponToMagicEditor desactive.");
            if (!string.Equals(inputRegulationPath, outputRegulationPath, StringComparison.OrdinalIgnoreCase))
                File.Copy(inputRegulationPath, outputRegulationPath, overwrite: true);

            return new EnemyLotWeaponToMagicRunResult
            {
                Seed = seed,
                SpellPoolMode = poolMode.ToString(),
                ProgressionBand = progressionBand.ToString(),
                SpellPoolCount = 0,
                SorceryPoolCount = 0,
                IncantationPoolCount = 0,
                EarlyPoolCount = 0,
                MidPoolCount = 0,
                LatePoolCount = 0,
                MinSpellPrice = -1,
                MaxSpellPrice = -1,
                ExcludedShopGoodsCount = 0,
                EligibleEnemyLotRowCount = 0
            };
        }

        BND4 bnd = LoadRegulation(inputRegulationPath);

        PARAM itemLotEnemyParam = LoadParam(bnd, "ItemLotParam_enemy");
        PARAM shopLineupParam = LoadParam(bnd, "ShopLineupParam");

        ShopValidMagicPoolBuildResult pool = ShopValidMagicPoolBuilder.Build(shopLineupParam, poolMode, progressionBand);

        if (pool.SelectedIds.Count == 0)
            throw new InvalidOperationException(
                $"Aucun spell shop-valid trouve pour le mode {poolMode} dans ShopLineupParam.");

        int eligibleRowCount = itemLotEnemyParam.Rows.Count(row => RowHasEligibleWeaponSlot(row, weaponCatalog));

        Console.WriteLine($"Spell pool size   : {pool.SelectedIds.Count}");
        Console.WriteLine($"Sorcery count     : {pool.SorceryIds.Count}");
        Console.WriteLine($"Incant count      : {pool.IncantationIds.Count}");
        Console.WriteLine($"Early count       : {pool.EarlyIds.Count}");
        Console.WriteLine($"Mid count         : {pool.MidIds.Count}");
        Console.WriteLine($"Late count        : {pool.LateIds.Count}");
        Console.WriteLine($"Eligible rows     : {eligibleRowCount}");

        if (pool.MinSelectedPrice >= 0 && pool.MaxSelectedPrice >= 0)
            Console.WriteLine($"Price range       : {pool.MinSelectedPrice} -> {pool.MaxSelectedPrice}");

        if (pool.ExcludedIds.Count > 0)
            Console.WriteLine($"Excluded shop IDs : {pool.ExcludedIds.Count}");

        var rng = new Random(MagicSelectionSeedMixer.Mix(seed, 606));
        var spellPicker = new MagicShuffleBag(pool.SelectedIds, rng);
        var result = new EnemyLotWeaponToMagicRunResult
        {
            Seed = seed,
            SpellPoolMode = poolMode.ToString(),
            ProgressionBand = progressionBand.ToString(),
            SpellPoolCount = pool.SelectedIds.Count,
            SorceryPoolCount = pool.SorceryIds.Count,
            IncantationPoolCount = pool.IncantationIds.Count,
            EarlyPoolCount = pool.EarlyIds.Count,
            MidPoolCount = pool.MidIds.Count,
            LatePoolCount = pool.LateIds.Count,
            MinSpellPrice = pool.MinSelectedPrice,
            MaxSpellPrice = pool.MaxSelectedPrice,
            ExcludedShopGoodsCount = pool.ExcludedIds.Count,
            EligibleEnemyLotRowCount = eligibleRowCount
        };

        if (config.ReplaceAllEligibleWeapons)
        {
            List<ItemLotSlotMatch> allEligibleMatches = FindAllEligibleMatches(itemLotEnemyParam, weaponCatalog)
                .OrderBy(match => match.Row.ID)
                .ThenBy(match => match.SlotIndex)
                .ToList();

            Console.WriteLine($"Eligible slots    : {allEligibleMatches.Count}");

            foreach (ItemLotSlotMatch match in allEligibleMatches)
                ApplySlotReplacement(match, spellPicker, result, isBulkMode: true);
        }
        else
        {
            foreach (EnemyLotWeaponToMagicEntry entry in config.Entries)
                ApplyEntry(itemLotEnemyParam, entry, spellPicker, result);
        }

        BinderFile itemLotEnemyFile = FindBinderFile(bnd, "ItemLotParam_enemy");
        itemLotEnemyFile.Bytes = itemLotEnemyParam.Write();
        WriteRegulation(outputRegulationPath, bnd);

        if (!string.IsNullOrWhiteSpace(mappingOutputPath))
        {
            SaveRunMapping(mappingOutputPath, result);
            Console.WriteLine($"Enemy lot mapping sauvegarde : {mappingOutputPath}");
        }

        Console.WriteLine($"Remplacements enemy lots appliques : {result.Mappings.Count}");
        return result;
    }

    private void ApplyEntry(
        PARAM itemLotEnemyParam,
        EnemyLotWeaponToMagicEntry entry,
        MagicShuffleBag spellPicker,
        EnemyLotWeaponToMagicRunResult result)
    {
        List<ItemLotSlotMatch> matches = FindMatches(itemLotEnemyParam, entry)
            .OrderBy(match => match.Row.ID)
            .ThenBy(match => match.SlotIndex)
            .ToList();

        if (matches.Count == 0)
        {
            Console.WriteLine(
                $"Aucune row enemy lot trouvee pour OldItemId={entry.OldItemId} | OldItemCategory={entry.OldItemCategory}");
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"OldItemId={entry.OldItemId} | OldItemCategory={entry.OldItemCategory} | Slots trouves : {matches.Count}");

        foreach (ItemLotSlotMatch match in matches)
            ApplySlotReplacement(match, spellPicker, result);
    }

    private IEnumerable<ItemLotSlotMatch> FindMatches(PARAM itemLotEnemyParam, EnemyLotWeaponToMagicEntry entry)
    {
        foreach (PARAM.Row row in itemLotEnemyParam.Rows)
        {
            for (int slot = 1; slot <= 8; slot++)
            {
                string suffix = slot.ToString("00");
                string itemIdField = $"lotItemId{suffix}";
                string categoryField = $"lotItemCategory{suffix}";

                if (!TryReadInt(row, itemIdField, out int itemId))
                    continue;

                if (!TryReadInt(row, categoryField, out int category))
                    continue;

                if (itemId != entry.OldItemId || category != entry.OldItemCategory)
                    continue;

                yield return new ItemLotSlotMatch
                {
                    Row = row,
                    SlotIndex = slot,
                    OldItemId = itemId,
                    OldItemCategory = category
                };
            }
        }
    }

    private void ApplySlotReplacement(
        ItemLotSlotMatch match,
        MagicShuffleBag spellPicker,
        EnemyLotWeaponToMagicRunResult result,
        bool isBulkMode = false)
    {
        string suffix = match.SlotIndex.ToString("00");
        string itemIdField = $"lotItemId{suffix}";
        string categoryField = $"lotItemCategory{suffix}";

        int newGoodsId = spellPicker.Next();

        bool logReplacement = !isBulkMode;
        if (logReplacement)
        {
            Console.WriteLine(
                $"Enemy lot replacement | RowID={match.Row.ID} | Slot={match.SlotIndex:00} | " +
                $"OldItemId={match.OldItemId} -> NewGoodsId={newGoodsId} | " +
                $"OldCategory={match.OldItemCategory} -> NewCategory={MagicGoodsItemLotCategory}");
        }

        SetIntField(match.Row, itemIdField, newGoodsId);
        SetIntField(match.Row, categoryField, MagicGoodsItemLotCategory);

        if (!TryReadInt(match.Row, itemIdField, out int writtenItemId))
            throw new InvalidOperationException($"Impossible de relire {itemIdField} sur RowID={match.Row.ID}");

        if (!TryReadInt(match.Row, categoryField, out int writtenCategory))
            throw new InvalidOperationException($"Impossible de relire {categoryField} sur RowID={match.Row.ID}");

        string sourceKinds = _itemLotSourceIndex.DescribeKinds(ItemLotParamKind.Enemy, match.Row.ID);

        if (_verbose && logReplacement)
            Console.WriteLine($"Source kinds      : {sourceKinds}");

        result.Mappings.Add(new EnemyLotWeaponToMagicRunMapping
        {
            ParamName = "ItemLotParam_enemy",
            RowId = match.Row.ID,
            SlotIndex = match.SlotIndex,
            OldItemId = match.OldItemId,
            OldItemCategory = match.OldItemCategory,
            NewGoodsId = writtenItemId,
            NewItemCategory = writtenCategory,
            SourceKinds = sourceKinds
        });
    }

    private IEnumerable<ItemLotSlotMatch> FindAllEligibleMatches(PARAM itemLotEnemyParam, WeaponIdCatalog weaponCatalog)
    {
        if (weaponCatalog == null)
            throw new InvalidOperationException("Le mode ReplaceAllEligibleWeapons requiert un WeaponIdCatalog de reference.");

        foreach (PARAM.Row row in itemLotEnemyParam.Rows)
        {
            for (int slot = 1; slot <= 8; slot++)
            {
                string suffix = slot.ToString("00");
                string itemIdField = $"lotItemId{suffix}";
                string categoryField = $"lotItemCategory{suffix}";

                if (!TryReadInt(row, itemIdField, out int itemId))
                    continue;

                if (!TryReadInt(row, categoryField, out int category))
                    continue;

                if (category != 2 || itemId <= 0 || !weaponCatalog.IsConvertibleWeaponForMagic(itemId))
                    continue;

                yield return new ItemLotSlotMatch
                {
                    Row = row,
                    SlotIndex = slot,
                    OldItemId = itemId,
                    OldItemCategory = category
                };
            }
        }
    }

    private static bool RowHasEligibleWeaponSlot(PARAM.Row row, WeaponIdCatalog weaponCatalog)
    {
        if (weaponCatalog == null)
            return false;

        for (int slot = 1; slot <= 8; slot++)
        {
            string suffix = slot.ToString("00");
            string itemIdField = $"lotItemId{suffix}";
            string categoryField = $"lotItemCategory{suffix}";

            if (!TryReadInt(row, itemIdField, out int itemId))
                continue;

            if (!TryReadInt(row, categoryField, out int category))
                continue;

            if (category == 2 && itemId > 0 && weaponCatalog.IsConvertibleWeaponForMagic(itemId))
                return true;
        }

        return false;
    }

    private static void SaveRunMapping(string outputPath, EnemyLotWeaponToMagicRunResult result)
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
        Console.WriteLine($"Chargement des paramdefs enemy lot depuis : {defsDirectory}");

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
        PARAM.Cell cell = row[fieldName];
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

    private sealed class ItemLotSlotMatch
    {
        public PARAM.Row Row { get; init; }
        public int SlotIndex { get; init; }
        public int OldItemId { get; init; }
        public int OldItemCategory { get; init; }
    }
}
