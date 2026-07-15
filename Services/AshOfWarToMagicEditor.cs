using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class AshOfWarToMagicEditor
{
    private const int AshOfWarItemLotCategory = 5;
    private const int MagicGoodsItemLotCategory = 1;
    private const int MagicShopEquipType = 3;

    private readonly List<PARAMDEF> _paramdefs;
    private readonly AshOfWarSourceIndex _sourceIndex;
    private readonly bool _verbose;

    public AshOfWarToMagicEditor(string defsDirectory, string itemslotsPath, bool verbose = false)
    {
        if (string.IsNullOrWhiteSpace(defsDirectory))
            throw new ArgumentException("Le chemin du dossier Defs est vide.", nameof(defsDirectory));

        if (!Directory.Exists(defsDirectory))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsDirectory}");

        _paramdefs = LoadParamdefs(defsDirectory);
        _sourceIndex = AshOfWarSourceIndex.Load(itemslotsPath);
        _verbose = verbose;
    }

    public AshOfWarToMagicRunResult ApplyToRegulation(
        string inputRegulationPath,
        string outputRegulationPath,
        AshOfWarToMagicConfig config,
        int seed,
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

        ShopMagicPoolMode poolMode = ShopMagicPoolModeParser.Parse(config.SpellPoolMode);
        MagicProgressionBand progressionBand = MagicProgressionBandParser.Parse(config.ProgressionBand);
        AshOfWarSourceSelection selection = _sourceIndex.SelectEntries(config);

        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine("Ashes of War -> Magic Editor (RandomPerRun)");
        Console.WriteLine("==================================================");
        Console.WriteLine($"Input regulation      : {inputRegulationPath}");
        Console.WriteLine($"Output regulation     : {outputRegulationPath}");
        Console.WriteLine($"Seed                  : {seed}");
        Console.WriteLine($"Spell pool mode       : {poolMode}");
        Console.WriteLine($"Progression band      : {progressionBand}");
        Console.WriteLine($"Shop rows selected    : {selection.Entries.Count(entry => entry.ParamKind == AshOfWarSourceParamKind.ShopLineupParam)}");
        Console.WriteLine($"ItemLot_map selected  : {selection.Entries.Count(entry => entry.ParamKind == AshOfWarSourceParamKind.ItemLotParamMap)}");
        Console.WriteLine($"ItemLot_enemy selected: {selection.Entries.Count(entry => entry.ParamKind == AshOfWarSourceParamKind.ItemLotParamEnemy)}");

        if (selection.ExcludedEniaShopRowCount > 0)
            Console.WriteLine($"Enia shops excluded   : {selection.ExcludedEniaShopRowCount}");

        if (!config.Enabled)
        {
            Console.WriteLine("AshOfWarToMagicEditor desactive.");
            if (!string.Equals(inputRegulationPath, outputRegulationPath, StringComparison.OrdinalIgnoreCase))
                File.Copy(inputRegulationPath, outputRegulationPath, overwrite: true);

            return new AshOfWarToMagicRunResult
            {
                Seed = seed,
                SpellPoolMode = poolMode.ToString(),
                ProgressionBand = progressionBand.ToString(),
                EarlyPoolCount = 0,
                MidPoolCount = 0,
                LatePoolCount = 0,
                MinSpellPrice = -1,
                MaxSpellPrice = -1,
                ExcludedEniaShopRowCount = selection.ExcludedEniaShopRowCount
            };
        }

        BND4 bnd = LoadRegulation(inputRegulationPath);

        PARAM shopLineupParam = LoadParam(bnd, "ShopLineupParam");
        PARAM itemLotMapParam = config.IncludeItemLotMapRows ? LoadParam(bnd, "ItemLotParam_map") : null;
        PARAM itemLotEnemyParam = config.IncludeItemLotEnemyRows ? LoadParam(bnd, "ItemLotParam_enemy") : null;

        ShopValidMagicPoolBuildResult pool = ShopValidMagicPoolBuilder.Build(shopLineupParam, poolMode, progressionBand);
        if (pool.SelectedIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"Aucun spell shop-valid trouve pour le mode {poolMode} dans ShopLineupParam.");
        }

        Console.WriteLine($"Spell pool size       : {pool.SelectedIds.Count}");
        Console.WriteLine($"Sorcery count         : {pool.SorceryIds.Count}");
        Console.WriteLine($"Incant count          : {pool.IncantationIds.Count}");
        Console.WriteLine($"Early count           : {pool.EarlyIds.Count}");
        Console.WriteLine($"Mid count             : {pool.MidIds.Count}");
        Console.WriteLine($"Late count            : {pool.LateIds.Count}");

        if (pool.MinSelectedPrice >= 0 && pool.MaxSelectedPrice >= 0)
            Console.WriteLine($"Price range           : {pool.MinSelectedPrice} -> {pool.MaxSelectedPrice}");

        if (pool.ExcludedIds.Count > 0)
            Console.WriteLine($"Excluded shop IDs     : {pool.ExcludedIds.Count}");

        var rng = new Random(MagicSelectionSeedMixer.Mix(seed, 404));
        var spellPicker = new MagicShuffleBag(pool.SelectedIds, rng);
        var result = new AshOfWarToMagicRunResult
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
            SelectedShopRowCount = selection.Entries.Count(entry => entry.ParamKind == AshOfWarSourceParamKind.ShopLineupParam),
            SelectedItemLotMapRowCount = selection.Entries.Count(entry => entry.ParamKind == AshOfWarSourceParamKind.ItemLotParamMap),
            SelectedItemLotEnemyRowCount = selection.Entries.Count(entry => entry.ParamKind == AshOfWarSourceParamKind.ItemLotParamEnemy),
            ExcludedEniaShopRowCount = selection.ExcludedEniaShopRowCount
        };

        foreach (AshOfWarSourceEntry entry in selection.Entries.Where(entry => entry.ParamKind == AshOfWarSourceParamKind.ShopLineupParam))
            ApplyShopEntry(shopLineupParam, entry, spellPicker, result);

        if (itemLotMapParam != null)
        {
            foreach (AshOfWarSourceEntry entry in selection.Entries.Where(entry => entry.ParamKind == AshOfWarSourceParamKind.ItemLotParamMap))
                ApplyItemLotEntry(itemLotMapParam, entry, spellPicker, result);
        }

        if (itemLotEnemyParam != null)
        {
            foreach (AshOfWarSourceEntry entry in selection.Entries.Where(entry => entry.ParamKind == AshOfWarSourceParamKind.ItemLotParamEnemy))
                ApplyItemLotEntry(itemLotEnemyParam, entry, spellPicker, result);
        }

        WriteParamBytes(bnd, "ShopLineupParam", shopLineupParam);

        if (itemLotMapParam != null)
            WriteParamBytes(bnd, "ItemLotParam_map", itemLotMapParam);

        if (itemLotEnemyParam != null)
            WriteParamBytes(bnd, "ItemLotParam_enemy", itemLotEnemyParam);

        WriteRegulation(outputRegulationPath, bnd);

        if (!string.IsNullOrWhiteSpace(mappingOutputPath))
        {
            SaveRunMapping(mappingOutputPath, result);
            Console.WriteLine($"Ash of War mapping sauvegarde : {mappingOutputPath}");
        }

        Console.WriteLine($"Remplacements Ashes of War appliques : {result.Mappings.Count}");
        return result;
    }

    private void ApplyShopEntry(
        PARAM shopLineupParam,
        AshOfWarSourceEntry entry,
        MagicShuffleBag spellPicker,
        AshOfWarToMagicRunResult result)
    {
        PARAM.Row row = shopLineupParam.Rows.FirstOrDefault(candidate => candidate.ID == entry.RowId);
        if (row == null)
        {
            Console.WriteLine($"Row shop introuvable pour {entry.ItemName} | RowID={entry.RowId}");
            return;
        }

        if (!TryReadInt(row, "equipId", out int oldGoodsId))
            throw new InvalidOperationException($"Impossible de lire equipId sur RowID={row.ID}");

        if (!TryReadInt(row, "equipType", out int oldEquipType))
            throw new InvalidOperationException($"Impossible de lire equipType sur RowID={row.ID}");

        int price = -1;
        int quantity = -1;
        TryReadInt(row, "value", out price);
        TryReadInt(row, "sellQuantity", out quantity);

        int newGoodsId = spellPicker.Next();

        Console.WriteLine(
            $"AoW shop replacement | RowID={row.ID} | {entry.ItemName} | " +
            $"OldGoodsId={oldGoodsId} -> NewGoodsId={newGoodsId} | " +
            $"OldType={oldEquipType} -> NewType={MagicShopEquipType}");

        SetIntField(row, "equipId", newGoodsId);
        SetIntField(row, "equipType", MagicShopEquipType);

        if (!TryReadInt(row, "equipId", out int writtenGoodsId))
            throw new InvalidOperationException($"Impossible de relire equipId sur RowID={row.ID}");

        if (!TryReadInt(row, "equipType", out int writtenEquipType))
            throw new InvalidOperationException($"Impossible de relire equipType sur RowID={row.ID}");

        result.Mappings.Add(new AshOfWarToMagicRunMapping
        {
            ParamName = "ShopLineupParam",
            RowId = row.ID,
            EntryName = entry.ItemName,
            SourceDescription = entry.SourceDescription,
            OldGoodsId = oldGoodsId,
            NewGoodsId = writtenGoodsId,
            OldValueType = oldEquipType,
            NewValueType = writtenEquipType,
            ValueTypeField = "equipType",
            Price = price,
            SellQuantity = quantity
        });
    }

    private void ApplyItemLotEntry(
        PARAM itemLotParam,
        AshOfWarSourceEntry entry,
        MagicShuffleBag spellPicker,
        AshOfWarToMagicRunResult result)
    {
        PARAM.Row row = itemLotParam.Rows.FirstOrDefault(candidate => candidate.ID == entry.RowId);
        if (row == null)
        {
            Console.WriteLine($"Row {entry.ParamName} introuvable pour {entry.ItemName} | RowID={entry.RowId}");
            return;
        }

        int newGoodsId = spellPicker.Next();
        int replacedSlots = 0;

        for (int slot = 1; slot <= 8; slot++)
        {
            string suffix = slot.ToString("00");
            string itemIdField = $"lotItemId{suffix}";
            string categoryField = $"lotItemCategory{suffix}";

            if (!TryReadInt(row, itemIdField, out int oldGoodsId))
                continue;

            if (!TryReadInt(row, categoryField, out int oldCategory))
                continue;

            if (oldGoodsId <= 0 || oldCategory != AshOfWarItemLotCategory)
                continue;

            Console.WriteLine(
                $"AoW lot replacement | Param={entry.ParamName} | RowID={row.ID} | Slot={slot:00} | {entry.ItemName} | " +
                $"OldGoodsId={oldGoodsId} -> NewGoodsId={newGoodsId} | " +
                $"OldCategory={oldCategory} -> NewCategory={MagicGoodsItemLotCategory}");

            SetIntField(row, itemIdField, newGoodsId);
            SetIntField(row, categoryField, MagicGoodsItemLotCategory);

            if (!TryReadInt(row, itemIdField, out int writtenGoodsId))
                throw new InvalidOperationException($"Impossible de relire {itemIdField} sur RowID={row.ID}");

            if (!TryReadInt(row, categoryField, out int writtenCategory))
                throw new InvalidOperationException($"Impossible de relire {categoryField} sur RowID={row.ID}");

            result.Mappings.Add(new AshOfWarToMagicRunMapping
            {
                ParamName = entry.ParamName,
                RowId = row.ID,
                SlotIndex = slot,
                EntryName = entry.ItemName,
                SourceDescription = entry.SourceDescription,
                OldGoodsId = oldGoodsId,
                NewGoodsId = writtenGoodsId,
                OldValueType = oldCategory,
                NewValueType = writtenCategory,
                ValueTypeField = "lotItemCategory"
            });

            replacedSlots++;
        }

        if (replacedSlots == 0)
        {
            Console.WriteLine(
                $"Aucun slot AoW (category {AshOfWarItemLotCategory}) remplace pour {entry.ItemName} | " +
                $"Param={entry.ParamName} | RowID={row.ID}");

            if (_verbose)
                DumpNonZeroSlots(row);
        }
    }

    private static void DumpNonZeroSlots(PARAM.Row row)
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

            if (itemId <= 0)
                continue;

            Console.WriteLine($"  Slot {slot:00} | ItemId={itemId} | Category={category}");
        }
    }

    private static void SaveRunMapping(string outputPath, AshOfWarToMagicRunResult result)
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

    private static void WriteParamBytes(BND4 bnd, string paramName, PARAM param)
    {
        BinderFile file = FindBinderFile(bnd, paramName);
        file.Bytes = param.Write();
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
        Console.WriteLine($"Chargement des paramdefs Ashes of War depuis : {defsDirectory}");

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
        object current = row[fieldName].Value;
        object boxed = current switch
        {
            sbyte => (sbyte)value,
            byte => (byte)value,
            short => (short)value,
            ushort => (ushort)value,
            int => value,
            uint => (uint)value,
            long => (long)value,
            ulong => (ulong)value,
            float => (float)value,
            double => (double)value,
            bool => value != 0,
            _ => value
        };

        row[fieldName].Value = boxed;
    }
}
