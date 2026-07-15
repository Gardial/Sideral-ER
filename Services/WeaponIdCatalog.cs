using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class WeaponIdCatalog
{
    private readonly HashSet<int> _weaponIds;
    private readonly HashSet<int> _shieldIds;
    private readonly HashSet<int> _ammoIds;

    private WeaponIdCatalog(HashSet<int> weaponIds, HashSet<int> shieldIds, HashSet<int> ammoIds)
    {
        _weaponIds = weaponIds;
        _shieldIds = shieldIds;
        _ammoIds = ammoIds;
    }

    public bool IsWeapon(int weaponId)
    {
        return _weaponIds.Contains(weaponId);
    }

    public bool IsShield(int weaponId)
    {
        return _shieldIds.Contains(weaponId);
    }

    public bool IsAmmo(int weaponId)
    {
        return _ammoIds.Contains(weaponId);
    }

    public bool IsNonShieldWeapon(int weaponId)
    {
        return IsWeapon(weaponId) && !IsShield(weaponId);
    }

    public bool IsConvertibleWeaponForMagic(int weaponId)
    {
        return IsWeapon(weaponId) && !IsShield(weaponId) && !IsAmmo(weaponId);
    }

    public static WeaponIdCatalog LoadFromRegulation(string regulationPath, string defsDirectory)
    {
        if (string.IsNullOrWhiteSpace(regulationPath))
            throw new ArgumentException("Le chemin du regulation de reference est vide.", nameof(regulationPath));

        if (string.IsNullOrWhiteSpace(defsDirectory))
            throw new ArgumentException("Le dossier Defs est vide.", nameof(defsDirectory));

        if (!File.Exists(regulationPath))
            throw new FileNotFoundException("regulation de reference introuvable.", regulationPath);

        if (!Directory.Exists(defsDirectory))
            throw new DirectoryNotFoundException($"Defs introuvable : {defsDirectory}");

        BND4 bnd = LoadRegulation(regulationPath);
        BinderFile equipWeaponFile = bnd.Files.FirstOrDefault(file =>
            string.Equals(Path.GetFileNameWithoutExtension(file.Name), "EquipParamWeapon", StringComparison.OrdinalIgnoreCase));

        if (equipWeaponFile == null)
            throw new InvalidOperationException("EquipParamWeapon introuvable dans le regulation de reference.");

        PARAM equipWeaponParam = PARAM.Read(equipWeaponFile.Bytes);
        List<PARAMDEF> paramdefs = LoadParamdefs(defsDirectory);

        if (!equipWeaponParam.ApplyParamdefCarefully(paramdefs))
            throw new InvalidOperationException("Paramdef non applique sur EquipParamWeapon.");

        HashSet<int> weaponIds = equipWeaponParam.Rows
            .Where(row => row.ID > 0)
            .Select(row => row.ID)
            .ToHashSet();

        HashSet<int> shieldIds = equipWeaponParam.Rows
            .Where(row => row.ID > 0 && IsShield(row))
            .Select(row => row.ID)
            .ToHashSet();

        HashSet<int> ammoIds = equipWeaponParam.Rows
            .Where(row => row.ID > 0 && IsAmmoId(row.ID))
            .Select(row => row.ID)
            .ToHashSet();

        return new WeaponIdCatalog(weaponIds, shieldIds, ammoIds);
    }

    private static bool IsShield(PARAM.Row row)
    {
        if (!TryReadInt(row, "wepType", out int wepType))
            return false;

        return wepType == 48
            || wepType == 65
            || wepType == 67
            || wepType == 69
            || wepType == 90;
    }

    private static bool IsAmmoId(int weaponId)
    {
        // Elden Ring stores arrows, bolts, great arrows and greatbolts
        // in the 50,000,000-59,999,999 weapon ID block.
        return weaponId >= 50_000_000 && weaponId < 60_000_000;
    }

    private static BND4 LoadRegulation(string regulationPath)
    {
        try
        {
            return SFUtil.DecryptERRegulation(regulationPath);
        }
        catch
        {
            return BND4.Read(File.ReadAllBytes(regulationPath));
        }
    }

    private static List<PARAMDEF> LoadParamdefs(string defsDirectory)
    {
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
