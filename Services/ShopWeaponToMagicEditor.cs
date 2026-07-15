using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class ShopWeaponToMagicEditor
{
    private readonly List<PARAMDEF> _paramdefs;
    private readonly bool _verbose;

    public ShopWeaponToMagicEditor(string defsDirectory, bool verbose = false)
    {
        if (string.IsNullOrWhiteSpace(defsDirectory))
            throw new ArgumentException("Le chemin du dossier Defs est vide.", nameof(defsDirectory));

        if (!Directory.Exists(defsDirectory))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsDirectory}");

        _paramdefs = LoadParamdefs(defsDirectory);
        _verbose = verbose;
    }

    public ShopWeaponToMagicRunResult ApplyToRegulation(
        string inputRegulationPath,
        string outputRegulationPath,
        ShopWeaponToMagicConfig config,
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
        Console.WriteLine("Shop Weapon -> Magic Editor (RandomPerRun)");
        Console.WriteLine("==================================================");
        Console.WriteLine($"Input regulation  : {inputRegulationPath}");
        Console.WriteLine($"Output regulation : {outputRegulationPath}");
        Console.WriteLine($"Seed              : {seed}");
        Console.WriteLine($"Entries           : {(config.ReplaceAllEligibleWeapons ? "ALL_ELIGIBLE" : config.Entries.Count)}");
        Console.WriteLine($"Spell pool mode   : {poolMode}");
        Console.WriteLine($"Progression band  : {progressionBand}");

        if (!config.Enabled)
        {
            Console.WriteLine("ShopWeaponToMagicEditor desactive.");
            if (!string.Equals(inputRegulationPath, outputRegulationPath, StringComparison.OrdinalIgnoreCase))
                File.Copy(inputRegulationPath, outputRegulationPath, overwrite: true);

            return new ShopWeaponToMagicRunResult
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
                ExcludedShopGoodsCount = 0
            };
        }

        BND4 bnd = LoadRegulation(inputRegulationPath);

        BinderFile shopFile = bnd.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f.Name), "ShopLineupParam", StringComparison.OrdinalIgnoreCase));

        if (shopFile == null)
            throw new InvalidOperationException("Param introuvable dans le BND : ShopLineupParam");

        PARAM param = PARAM.Read(shopFile.Bytes);

        bool applied;
        try
        {
            applied = param.ApplyParamdefCarefully(_paramdefs);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Echec ApplyParamdefCarefully sur ShopLineupParam (ParamType: {param.ParamType}). {ex.Message}", ex);
        }

        if (!applied)
            throw new InvalidOperationException(
                $"Paramdef non applique sur ShopLineupParam (ParamType: {param.ParamType}).");

        ShopValidMagicPoolBuildResult pool = ShopValidMagicPoolBuilder.Build(param, poolMode, progressionBand);

        if (pool.SelectedIds.Count == 0)
            throw new InvalidOperationException(
                $"Aucun spell shop-valid trouve pour le mode {poolMode} dans ShopLineupParam.");

        Console.WriteLine($"Spell pool size   : {pool.SelectedIds.Count}");
        Console.WriteLine($"Sorcery count     : {pool.SorceryIds.Count}");
        Console.WriteLine($"Incant count      : {pool.IncantationIds.Count}");
        Console.WriteLine($"Early count       : {pool.EarlyIds.Count}");
        Console.WriteLine($"Mid count         : {pool.MidIds.Count}");
        Console.WriteLine($"Late count        : {pool.LateIds.Count}");

        if (pool.MinSelectedPrice >= 0 && pool.MaxSelectedPrice >= 0)
            Console.WriteLine($"Price range       : {pool.MinSelectedPrice} -> {pool.MaxSelectedPrice}");

        if (pool.ExcludedIds.Count > 0)
            Console.WriteLine($"Excluded shop IDs : {pool.ExcludedIds.Count}");

        if (_verbose)
        {
            Console.WriteLine($"Spell pool sample : {string.Join(", ", pool.SelectedIds.Take(20))}{(pool.SelectedIds.Count > 20 ? " ..." : "")}");

            if (pool.ExcludedIds.Count > 0)
                Console.WriteLine($"Excluded sample   : {string.Join(", ", pool.ExcludedIds.Take(20))}{(pool.ExcludedIds.Count > 20 ? " ..." : "")}");
        }

        var rng = new Random(MagicSelectionSeedMixer.Mix(seed, 101));
        var spellPicker = new MagicShuffleBag(pool.SelectedIds, rng);
        List<int> targetEquipIds = config.ReplaceAllEligibleWeapons
            ? BuildAllEligibleTargetEquipIds(param, weaponCatalog)
            : config.Entries
                .Select(entry => entry.OldEquipId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

        Console.WriteLine($"Target equip IDs  : {targetEquipIds.Count}");
        var result = new ShopWeaponToMagicRunResult
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
            ExcludedShopGoodsCount = pool.ExcludedIds.Count
        };

        foreach (int oldEquipId in targetEquipIds)
        {
            ApplyEntry(param, oldEquipId, spellPicker, result, config.ReplaceAllEligibleWeapons);
        }

        shopFile.Bytes = param.Write();
        WriteRegulation(outputRegulationPath, bnd);

        if (!string.IsNullOrWhiteSpace(mappingOutputPath))
        {
            SaveRunMapping(mappingOutputPath, result);
            Console.WriteLine($"Shop mapping sauvegarde : {mappingOutputPath}");
        }

        Console.WriteLine($"Remplacements shop appliques : {result.Mappings.Count}");
        return result;
    }

    private void ApplyEntry(
        PARAM param,
        int oldEquipId,
        MagicShuffleBag spellPicker,
        ShopWeaponToMagicRunResult result,
        bool isBulkMode)
    {
        List<PARAM.Row> matchingRows = param.Rows
            .Where(row =>
                TryReadInt(row, "equipId", out int equipId) &&
                TryReadInt(row, "equipType", out int equipType) &&
                equipId == oldEquipId &&
                equipType != 3)
            .OrderBy(row => row.ID)
            .ToList();

        if (matchingRows.Count == 0)
        {
            Console.WriteLine($"Aucune row shop trouvee pour OldEquipId={oldEquipId}");
            return;
        }

        if (!isBulkMode)
        {
            Console.WriteLine();
            Console.WriteLine($"OldEquipId={oldEquipId} | Rows trouvees : {matchingRows.Count}");
        }

        foreach (PARAM.Row row in matchingRows)
        {
            ApplyRowReplacement(row, oldEquipId, spellPicker, result, isBulkMode);
        }
    }

    private void ApplyRowReplacement(
        PARAM.Row row,
        int oldEquipIdExpected,
        MagicShuffleBag spellPicker,
        ShopWeaponToMagicRunResult result,
        bool isBulkMode)
    {
        if (!TryReadInt(row, "equipId", out int oldEquipId))
            throw new InvalidOperationException($"Impossible de lire equipId sur RowID={row.ID}");

        if (!TryReadInt(row, "equipType", out int oldEquipType))
            throw new InvalidOperationException($"Impossible de lire equipType sur RowID={row.ID}");

        int price = -1;
        int qty = -1;
        TryReadInt(row, "value", out price);
        TryReadInt(row, "sellQuantity", out qty);

        if (oldEquipId != oldEquipIdExpected)
            throw new InvalidOperationException(
                $"equipId inattendu sur RowID={row.ID}. Attendu={oldEquipIdExpected}, lu={oldEquipId}");

        int newGoodsId = spellPicker.Next();
        int newEquipType = 3;

        bool logReplacement = !isBulkMode;
        if (logReplacement)
        {
            Console.WriteLine(
                $"Shop replacement | RowID={row.ID} | OldEquipId={oldEquipId} -> NewGoodsId={newGoodsId} | " +
                $"OldType={oldEquipType} -> NewType={newEquipType}");

            Console.WriteLine($"Avant : equipId={oldEquipId} | equipType={oldEquipType} | price={price} | qty={qty}");
        }

        SetIntField(row, "equipId", newGoodsId);
        SetIntField(row, "equipType", newEquipType);

        if (!TryReadInt(row, "equipId", out int writtenEquipId))
            throw new InvalidOperationException($"Impossible de relire equipId sur RowID={row.ID}");

        if (!TryReadInt(row, "equipType", out int writtenEquipType))
            throw new InvalidOperationException($"Impossible de relire equipType sur RowID={row.ID}");

        if (logReplacement)
            Console.WriteLine($"Apres : equipId={writtenEquipId} | equipType={writtenEquipType} | price={price} | qty={qty}");

        result.Mappings.Add(new ShopWeaponToMagicRunMapping
        {
            RowId = row.ID,
            OldEquipId = oldEquipId,
            NewGoodsId = writtenEquipId,
            OldEquipType = oldEquipType,
            NewEquipType = writtenEquipType,
            Price = price,
            SellQuantity = qty
        });
    }

    private static List<int> BuildAllEligibleTargetEquipIds(PARAM shopParam, WeaponIdCatalog weaponCatalog)
    {
        if (weaponCatalog == null)
            throw new InvalidOperationException("Le mode ReplaceAllEligibleWeapons requiert un WeaponIdCatalog de reference.");

        return shopParam.Rows
            .Where(row =>
                TryReadInt(row, "equipId", out int equipId) &&
                TryReadInt(row, "equipType", out int equipType) &&
                equipType != 3 &&
                equipId > 0 &&
                weaponCatalog.IsConvertibleWeaponForMagic(equipId))
            .Select(row =>
            {
                TryReadInt(row, "equipId", out int equipId);
                return equipId;
            })
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    private static void SaveRunMapping(string outputPath, ShopWeaponToMagicRunResult result)
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
        Console.WriteLine($"Chargement des paramdefs shop depuis : {defsDirectory}");

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

}
