using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SoulsFormats;

namespace RandomMagicConversion;

internal sealed class ItemLotRowDebugger
{
    private readonly List<PARAMDEF> _paramdefs;

    public ItemLotRowDebugger(string defsDirectory)
    {
        if (string.IsNullOrWhiteSpace(defsDirectory))
            throw new ArgumentException("Le chemin du dossier Defs est vide.", nameof(defsDirectory));

        if (!Directory.Exists(defsDirectory))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsDirectory}");

        _paramdefs = LoadParamdefs(defsDirectory);
    }

    public void DumpRow(string regulationPath, string paramName, int rowId)
    {
        if (string.IsNullOrWhiteSpace(regulationPath))
            throw new ArgumentException("Le chemin du regulation.bin est vide.", nameof(regulationPath));

        if (!File.Exists(regulationPath))
            throw new FileNotFoundException("regulation.bin introuvable.", regulationPath);

        if (string.IsNullOrWhiteSpace(paramName))
            throw new ArgumentException("Le nom du param est vide.", nameof(paramName));

        Console.WriteLine("==================================================");
        Console.WriteLine("ItemLot row dump");
        Console.WriteLine("==================================================");
        Console.WriteLine($"Input regulation : {regulationPath}");
        Console.WriteLine($"Param           : {paramName}");
        Console.WriteLine($"RowID           : {rowId}");

        BND4 bnd = LoadRegulation(regulationPath);
        PARAM param = LoadParam(bnd, paramName);

        PARAM.Row row = param.Rows.FirstOrDefault(r => r.ID == rowId);
        if (row == null)
            throw new InvalidOperationException($"Row introuvable : {rowId} dans {paramName}");

        Console.WriteLine($"Row name        : {row.Name ?? string.Empty}");
        Console.WriteLine();
        Console.WriteLine("Slots");

        for (int slot = 1; slot <= 8; slot++)
        {
            string suffix = slot.ToString("00");
            int itemId = ReadInt(row, $"lotItemId{suffix}");
            int category = ReadInt(row, $"lotItemCategory{suffix}");
            int basePoint = ReadInt(row, $"lotItemBasePoint{suffix}");
            int cumulatePoint = ReadInt(row, $"cumulateLotPoint{suffix}");
            int getItemFlagId = ReadInt(row, $"getItemFlagId{suffix}");

            Console.WriteLine(
                $"  Slot {slot:00} | ItemId={itemId} | Category={category} | " +
                $"BasePoint={basePoint} | CumulatePoint={cumulatePoint} | GetItemFlagId={getItemFlagId}");
        }

        Console.WriteLine();
        Console.WriteLine("Interesting row fields");

        foreach (string fieldName in InterestingFieldNames)
        {
            if (!TryReadCellValue(row, fieldName, out object value))
                continue;

            Console.WriteLine($"  {fieldName} = {FormatValue(value)}");
        }
    }

    private static readonly string[] InterestingFieldNames =
    {
        "getItemFlagId",
        "cumulateNumFlagId",
        "cumulateNumMax",
        "lotItem_Rarity",
        "lotItemNum01",
        "lotItemNum02",
        "lotItemNum03",
        "lotItemNum04",
        "lotItemNum05",
        "lotItemNum06",
        "lotItemNum07",
        "lotItemNum08",
        "enableLuck01",
        "enableLuck02",
        "enableLuck03",
        "enableLuck04",
        "enableLuck05",
        "enableLuck06",
        "enableLuck07",
        "enableLuck08",
        "cumulateReset01",
        "cumulateReset02",
        "cumulateReset03",
        "cumulateReset04",
        "cumulateReset05",
        "cumulateReset06",
        "cumulateReset07",
        "cumulateReset08",
        "canExecByFriendlyGhost",
        "canExecByHostileGhost",
        "clearCount",
        "canExecByCrystal",
        "canExecBySpecificShop",
        "affectToAllLot"
    };

    private static string FormatValue(object value)
    {
        return value switch
        {
            null => "null",
            bool v => v ? "true" : "false",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool TryReadCellValue(PARAM.Row row, string fieldName, out object value)
    {
        value = null;

        try
        {
            value = row[fieldName].Value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int ReadInt(PARAM.Row row, string fieldName)
    {
        if (!TryReadInt(row, fieldName, out int value))
            throw new InvalidOperationException($"Impossible de lire le champ {fieldName} sur RowID={row.ID}");

        return value;
    }

    private static PARAM LoadParam(BND4 bnd, string paramName)
    {
        BinderFile file = bnd.Files.FirstOrDefault(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f.Name), paramName, StringComparison.OrdinalIgnoreCase));

        if (file == null)
            throw new InvalidOperationException($"Param introuvable dans le BND : {paramName}");

        PARAM param = PARAM.Read(file.Bytes);

        bool applied;
        try
        {
            applied = param.ApplyParamdefCarefully(LoadParamdefsFromCache());
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

    private static readonly object ParamdefCacheLock = new();
    private static List<PARAMDEF> _cachedParamdefs;

    private static List<PARAMDEF> LoadParamdefsFromCache()
    {
        lock (ParamdefCacheLock)
        {
            return _cachedParamdefs ?? throw new InvalidOperationException("Paramdefs non initialises.");
        }
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

    private static List<PARAMDEF> LoadParamdefs(string defsDirectory)
    {
        Console.WriteLine($"Chargement des paramdefs item lot depuis : {defsDirectory}");

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

        lock (ParamdefCacheLock)
        {
            _cachedParamdefs = defs;
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
}
