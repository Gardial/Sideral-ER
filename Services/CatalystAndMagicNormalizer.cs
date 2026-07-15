using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class CatalystAndMagicNormalizer
{
    private const int FirstRealWeaponId = 1000000;
    private const int StaffWepType = 57;
    private const int SealWepType = 61;
    private const int CatalystWeaponCategory = 8;
    private const int CatalystMotionCategory = 41;
    private const int DefaultGuardMotionCategory = 2;
    private const int ParrySwordArtParamId = 302;

    private static readonly string[] WeaponRequirementFields =
    {
        "properStrength",
        "properAgility",
        "properMagic",
        "properFaith",
        "properLuck"
    };

    private static readonly string[] MagicRequirementFields =
    {
        "requirementIntellect",
        "requirementFaith",
        "requirementLuck"
    };

    public CatalystAndMagicNormalizationResult ApplyToRegulation(
        string inputRegulationPath,
        string outputRegulationPath,
        string defsFolder,
        bool removeStatRequirements)
    {
        if (string.IsNullOrWhiteSpace(inputRegulationPath))
            throw new ArgumentException("Le chemin du regulation.bin d'entree est vide.", nameof(inputRegulationPath));

        if (!File.Exists(inputRegulationPath))
            throw new FileNotFoundException("regulation.bin d'entree introuvable.", inputRegulationPath);

        if (string.IsNullOrWhiteSpace(outputRegulationPath))
            throw new ArgumentException("Le chemin du regulation.bin de sortie est vide.", nameof(outputRegulationPath));

        if (string.IsNullOrWhiteSpace(defsFolder) || !Directory.Exists(defsFolder))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsFolder}");

        BND4 bnd = LoadRegulation(inputRegulationPath);
        BinderFile equipWeaponFile = FindParamFile(bnd, "EquipParamWeapon");
        PARAM equipWeaponParam = ReadParamFromXml(
            equipWeaponFile,
            ResolveParamdefPath(defsFolder, "EquipParamWeapon"));

        int guardPreparedCount = 0;
        int parryEnabledCount = 0;
        int weaponRequirementRowsCleared = 0;

        foreach (PARAM.Row row in equipWeaponParam.Rows.Where(IsCatalyst))
        {
            if (IsStaff(row))
            {
                PrepareStaffParryFields(row);
                guardPreparedCount++;
                parryEnabledCount++;
            }
            else if (IsSeal(row))
            {
                PrepareSealParryFields(row);
                guardPreparedCount++;
                parryEnabledCount++;
            }

            if (removeStatRequirements && ClearFields(row, WeaponRequirementFields))
                weaponRequirementRowsCleared++;
        }

        equipWeaponFile.Bytes = equipWeaponParam.Write();

        int magicRequirementRowsCleared = 0;
        if (removeStatRequirements)
            magicRequirementRowsCleared = ClearMagicRequirements(bnd, defsFolder);

        WriteRegulation(outputRegulationPath, bnd);

        return new CatalystAndMagicNormalizationResult
        {
            GuardPreparedCatalystCount = guardPreparedCount,
            ParryEnabledCatalystCount = parryEnabledCount,
            WeaponRequirementRowsCleared = weaponRequirementRowsCleared,
            MagicRequirementRowsCleared = magicRequirementRowsCleared
        };
    }

    private static int ClearMagicRequirements(BND4 bnd, string defsFolder)
    {
        BinderFile magicFile = FindParamFile(bnd, "Magic");
        string magicParamdefPath = ResolveGeneratedParamdefPath(defsFolder, "Magic");
        PARAM magicParam = ReadParamFromXml(magicFile, magicParamdefPath);

        int changedRows = 0;
        foreach (PARAM.Row row in magicParam.Rows)
        {
            if (ShopMagicPoolClassifier.Classify(row.ID) == ShopMagicCategory.None)
                continue;

            if (ClearFields(row, MagicRequirementFields))
                changedRows++;
        }

        magicFile.Bytes = magicParam.Write();
        return changedRows;
    }

    private static bool IsCatalyst(PARAM.Row row)
    {
        if (row == null || row.ID < FirstRealWeaponId)
            return false;

        return IsStaff(row) || IsSeal(row);
    }

    private static bool IsStaff(PARAM.Row row)
    {
        return ReadIntOrDefault(row, "wepType", -1) == StaffWepType;
    }

    private static bool IsSeal(PARAM.Row row)
    {
        return ReadIntOrDefault(row, "wepType", -1) == SealWepType;
    }

    private static void PrepareStaffParryFields(PARAM.Row row)
    {
        SetIntIfFieldExists(row, "wepType", StaffWepType);
        PrepareCatalystCoreFields(row);

        SetIntIfFieldExists(row, "enableMagic", 1);
        SetIntIfFieldExists(row, "enableSorcery", 0);
        SetIntIfFieldExists(row, "enableMiracle", 0);
        SetIntIfFieldExists(row, "enableVowMagic", 0);

        SetIntIfFieldExists(row, "postureControlId_Right", 2);
        SetIntIfFieldExists(row, "postureControlId_Left", 2);
        SetIntIfFieldExists(row, "enableParry", 1);
        SetMinimumIntIfFieldExists(row, "parryDamageLife", 10);
    }

    private static void PrepareSealParryFields(PARAM.Row row)
    {
        SetIntIfFieldExists(row, "wepType", SealWepType);
        PrepareCatalystCoreFields(row);

        SetIntIfFieldExists(row, "wepmotionBothHandId", 15);
        SetIntIfFieldExists(row, "spAtkcategory", 240);
        SetIntIfFieldExists(row, "enableMagic", 0);
        SetIntIfFieldExists(row, "enableSorcery", 0);
        SetIntIfFieldExists(row, "enableMiracle", 1);
        SetIntIfFieldExists(row, "enableVowMagic", 0);
        SetIntIfFieldExists(row, "postureControlId_Right", 0);
        SetIntIfFieldExists(row, "postureControlId_Left", 0);
        SetIntIfFieldExists(row, "swordArtsParamId", ParrySwordArtParamId);
        SetIntIfFieldExists(row, "enableParry", 1);
        SetMinimumIntIfFieldExists(row, "parryDamageLife", 10);
    }

    private static void PrepareCatalystCoreFields(PARAM.Row row)
    {
        SetIntIfFieldExists(row, "weaponCategory", CatalystWeaponCategory);
        SetIntIfFieldExists(row, "wepmotionCategory", CatalystMotionCategory);
        SetIntIfFieldExists(row, "wepmotionOneHandId", 0);
        SetIntIfFieldExists(row, "guardmotionCategory", DefaultGuardMotionCategory);
        SetIntIfFieldExists(row, "rightHandEquipable", 1);
        SetIntIfFieldExists(row, "leftHandEquipable", 1);
        SetIntIfFieldExists(row, "bothHandEquipable", 1);
        SetIntIfFieldExists(row, "bothHandleEquiable", 1);
        SetIntIfFieldExists(row, "enableGuard", 1);
        SetIntIfFieldExists(row, "wepCollidableType0", 1);
        SetIntIfFieldExists(row, "wepCollidableType1", 1);
    }

    private static bool ClearFields(PARAM.Row row, IEnumerable<string> fieldNames)
    {
        bool changed = false;

        foreach (string fieldName in fieldNames)
        {
            if (!TryReadInt(row, fieldName, out int value) || value == 0)
                continue;

            SetIntField(row, fieldName, 0);
            changed = true;
        }

        return changed;
    }

    private static void SetMinimumIntIfFieldExists(PARAM.Row row, string fieldName, int minimumValue)
    {
        if (!TryReadInt(row, fieldName, out int currentValue) || currentValue >= minimumValue)
            return;

        SetIntField(row, fieldName, minimumValue);
    }

    private static void SetIntIfFieldExists(PARAM.Row row, string fieldName, int value)
    {
        if (!HasField(row, fieldName))
            return;

        SetIntField(row, fieldName, value);
    }

    private static bool HasField(PARAM.Row row, string fieldName)
    {
        if (row == null)
            return false;

        try
        {
            return row[fieldName] != null;
        }
        catch
        {
            return false;
        }
    }

    private static int ReadIntOrDefault(PARAM.Row row, string fieldName, int fallback = 0)
    {
        return TryReadInt(row, fieldName, out int value)
            ? value
            : fallback;
    }

    private static bool TryReadInt(PARAM.Row row, string fieldName, out int value)
    {
        value = 0;

        if (row == null)
            return false;

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
                cell.Value = (sbyte)value;
                break;
            case byte _:
                cell.Value = (byte)value;
                break;
            case short _:
                cell.Value = (short)value;
                break;
            case ushort _:
                cell.Value = (ushort)value;
                break;
            case int _:
                cell.Value = value;
                break;
            case uint _:
                cell.Value = (uint)System.Math.Max(0, value);
                break;
            case long _:
                cell.Value = (long)value;
                break;
            case ulong _:
                cell.Value = (ulong)System.Math.Max(0, value);
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

    private static Dictionary<string, object> CaptureFieldValues(PARAM.Row row, IEnumerable<string> fieldNames)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (string fieldName in fieldNames)
        {
            if (!HasField(row, fieldName))
                continue;

            result[fieldName] = row[fieldName].Value;
        }

        return result;
    }

    private static void RestoreFieldValues(PARAM.Row row, IReadOnlyDictionary<string, object> fieldValues)
    {
        foreach ((string fieldName, object value) in fieldValues)
        {
            if (!HasField(row, fieldName))
                continue;

            row[fieldName].Value = value;
        }
    }

    private static PARAM ReadParamFromXml(BinderFile file, string xmlPath)
    {
        PARAM param = PARAM.Read(file.Bytes);
        param.ApplyParamdef(PARAMDEF.XmlDeserialize(xmlPath));
        return param;
    }

    private static string ResolveParamdefPath(string defsFolder, string paramName)
    {
        string path = Path.Combine(defsFolder, $"{paramName}.xml");
        if (File.Exists(path))
            return path;

        throw new FileNotFoundException($"Paramdef introuvable : {path}", path);
    }

    private static string ResolveGeneratedParamdefPath(string defsFolder, string paramName)
    {
        string generatedPath = Path.Combine(defsFolder, "Generated", $"{paramName}.xml");
        if (File.Exists(generatedPath))
            return generatedPath;

        return ResolveParamdefPath(defsFolder, paramName);
    }

    private static BinderFile FindParamFile(BND4 bnd, string paramName)
    {
        BinderFile file = bnd.Files.FirstOrDefault(candidate =>
            string.Equals(Path.GetFileNameWithoutExtension(candidate.Name), paramName, StringComparison.OrdinalIgnoreCase));

        return file ?? throw new InvalidOperationException($"Param introuvable dans le BND : {paramName}");
    }

    private static BND4 LoadRegulation(string inputRegulationPath)
    {
        try
        {
            return SFUtil.DecryptERRegulation(inputRegulationPath);
        }
        catch
        {
            return BND4.Read(File.ReadAllBytes(inputRegulationPath));
        }
    }

    private static void WriteRegulation(string outputRegulationPath, BND4 bnd)
    {
        string outputDir = Path.GetDirectoryName(outputRegulationPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        try
        {
            SFUtil.EncryptERRegulation(outputRegulationPath, bnd);
        }
        catch
        {
            File.WriteAllBytes(outputRegulationPath, bnd.Write());
        }
    }
}

public sealed class CatalystAndMagicNormalizationResult
{
    public int GuardPreparedCatalystCount { get; init; }
    public int ParryEnabledCatalystCount { get; init; }
    public int WeaponRequirementRowsCleared { get; init; }
    public int MagicRequirementRowsCleared { get; init; }
}
