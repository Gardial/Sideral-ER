using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class StartingClassOriginTextPatchResult
{
    public bool Generated { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public int EntryCount { get; init; }
}

public sealed class StartingClassOriginTextPatchGenerator
{
    private static readonly StartingClassOriginDescriptor[] StartingClasses =
    {
        new(3000, 297130),
        new(3001, 297131),
        new(3002, 297132),
        new(3003, 297133),
        new(3004, 297134),
        new(3005, 297135),
        new(3006, 297138),
        new(3007, 297136),
        new(3008, 297137),
        new(3009, 297139)
    };

    private static readonly (string SlotName, string Label)[] WeaponSlotLabels =
    {
        ("equip_Wep_Right", "R1"),
        ("equip_Subwep_Right", "R2"),
        ("equip_Wep_Left", "L1"),
        ("equip_Subwep_Left", "L2")
    };

    private static readonly string[] SpellSlotNames =
    {
        "equip_Spell_01",
        "equip_Spell_02",
        "equip_Spell_03",
        "equip_Spell_04",
        "equip_Spell_05",
        "equip_Spell_06",
        "equip_Spell_07"
    };

    public StartingClassOriginTextPatchResult Generate(
        string projectDir,
        string defsFolder,
        string regulationPath,
        ConversionRun run,
        IEnumerable<string> localizedItemMsgbndPaths,
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new ArgumentException("Le chemin du projet est vide.", nameof(projectDir));

        if (string.IsNullOrWhiteSpace(defsFolder) || !Directory.Exists(defsFolder))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsFolder}");

        if (string.IsNullOrWhiteSpace(regulationPath) || !File.Exists(regulationPath))
            throw new FileNotFoundException("regulation.bin introuvable pour le patch des origines.", regulationPath);

        string[] itemMsgbndPaths = localizedItemMsgbndPaths?
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (itemMsgbndPaths.Length == 0)
        {
            return new StartingClassOriginTextPatchResult
            {
                Generated = false
            };
        }

        PARAM charaInitParam = ReadCharaInitParam(projectDir, defsFolder, regulationPath);
        Dictionary<int, string> weaponNameLookup = LoadFmgLookup(itemMsgbndPaths, "WeaponName");
        Dictionary<int, string> goodsNameLookup = LoadFmgLookup(itemMsgbndPaths, "GoodsName");
        Dictionary<int, ConversionMapping> shieldDisplayLookup = BuildShieldDisplayLookup(run);

        var patch = new SmithboxTextExportDocument
        {
            Name = "StartingClassOriginTextPatch",
            FmgWrappers =
            {
                new SmithboxFmgWrapper
                {
                    Name = "GR_LineHelp.fmg",
                    ID = 201,
                    Fmg = new SmithboxFmg
                    {
                        Name = "GR_LineHelp"
                    }
                }
            }
        };

        SmithboxFmgWrapper lineHelpWrapper = patch.FmgWrappers[0];

        foreach (StartingClassOriginDescriptor descriptor in StartingClasses)
        {
            PARAM.Row row = charaInitParam[descriptor.RowId];
            if (row == null)
                continue;

            string description = BuildOriginDescription(row, weaponNameLookup, goodsNameLookup, shieldDisplayLookup);
            if (string.IsNullOrWhiteSpace(description))
                continue;

            lineHelpWrapper.Fmg.Entries.Add(new SmithboxFmgEntry
            {
                ID = descriptor.DescriptionTextId,
                Text = description
            });
        }

        if (lineHelpWrapper.Fmg.Entries.Count == 0)
        {
            return new StartingClassOriginTextPatchResult
            {
                Generated = false
            };
        }

        string outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(outputPath, JsonSerializer.Serialize(patch, jsonOptions));

        return new StartingClassOriginTextPatchResult
        {
            Generated = true,
            OutputPath = outputPath,
            EntryCount = lineHelpWrapper.Fmg.Entries.Count
        };
    }

    private static string BuildOriginDescription(
        PARAM.Row row,
        IReadOnlyDictionary<int, string> weaponNameLookup,
        IReadOnlyDictionary<int, string> goodsNameLookup,
        IReadOnlyDictionary<int, ConversionMapping> shieldDisplayLookup)
    {
        var parts = new List<string>();

        foreach ((string slotName, string label) in WeaponSlotLabels)
        {
            int weaponId = ReadIntOrDefault(row, slotName);
            if (weaponId <= 0)
                continue;

            parts.Add($"{label}: {ResolveWeaponDisplayName(weaponId, weaponNameLookup, shieldDisplayLookup)}");
        }

        for (int index = 0; index < SpellSlotNames.Length; index++)
        {
            int goodsId = ReadIntOrDefault(row, SpellSlotNames[index]);
            if (goodsId <= 0)
                continue;

            parts.Add($"S{index + 1}: {ResolveName(goodsNameLookup, goodsId)}");
        }

        return string.Join("; ", parts);
    }

    private static Dictionary<int, ConversionMapping> BuildShieldDisplayLookup(ConversionRun run)
    {
        if (run?.Mappings == null || run.Mappings.Count == 0)
            return new Dictionary<int, ConversionMapping>();

        return run.Mappings
            .Where(mapping => mapping != null &&
                              (mapping.Kind == ConversionKind.ShieldToStaff || mapping.Kind == ConversionKind.ShieldToSeal) &&
                              mapping.TargetId > 0 &&
                              mapping.SourceId > 0)
            .GroupBy(mapping => mapping.TargetId)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    private static string ResolveWeaponDisplayName(
        int weaponId,
        IReadOnlyDictionary<int, string> weaponNameLookup,
        IReadOnlyDictionary<int, ConversionMapping> shieldDisplayLookup)
    {
        if (shieldDisplayLookup != null &&
            shieldDisplayLookup.TryGetValue(weaponId, out ConversionMapping mapping) &&
            mapping.SourceId > 0)
        {
            return ResolveName(weaponNameLookup, mapping.SourceId);
        }

        return ResolveName(weaponNameLookup, weaponId);
    }

    private static string ResolveName(IReadOnlyDictionary<int, string> lookup, int id)
    {
        if (lookup != null && lookup.TryGetValue(id, out string value) && !string.IsNullOrWhiteSpace(value))
            return value;

        return $"ID {id}";
    }

    private static PARAM ReadCharaInitParam(string projectDir, string defsFolder, string regulationPath)
    {
        string charaInitParamdefPath = LayoutParamdefGenerator.EnsureGeneratedParamdef(
            defsFolder,
            "CharaInitParam.xml",
            ProjectLayout.ResolveSoulsRandomizerLayoutPath(projectDir, "CHARACTER_INIT_PARAM.xml"),
            "CHARACTER_INIT_PARAM");

        BND4 regulation = LoadRegulation(regulationPath);
        BinderFile charaInitFile = regulation.Files.FirstOrDefault(candidate =>
            string.Equals(Path.GetFileNameWithoutExtension(candidate.Name), "CharaInitParam", StringComparison.OrdinalIgnoreCase));

        if (charaInitFile == null)
            throw new InvalidOperationException("CharaInitParam introuvable dans le regulation.");

        PARAM param = PARAM.Read(charaInitFile.Bytes);
        param.ApplyParamdef(PARAMDEF.XmlDeserialize(charaInitParamdefPath));
        return param;
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

    private static Dictionary<int, string> LoadFmgLookup(IEnumerable<string> msgbndPaths, string fileNameFragment)
    {
        var lookup = new Dictionary<int, string>();

        foreach (string msgbndPath in msgbndPaths)
        {
            BND4 bnd = BND4.Read(msgbndPath);

            foreach (BinderFile file in bnd.Files.Where(file =>
                         file.Name.EndsWith(".fmg", StringComparison.OrdinalIgnoreCase) &&
                         file.Name.Contains(fileNameFragment, StringComparison.OrdinalIgnoreCase)))
            {
                FMG fmg = FMG.Read(file.Bytes);
                foreach (FMG.Entry entry in fmg.Entries)
                {
                    if (entry == null || entry.ID <= 0 || string.IsNullOrWhiteSpace(entry.Text))
                        continue;

                    lookup[entry.ID] = entry.Text;
                }
            }
        }

        return lookup;
    }

    private static int ReadIntOrDefault(PARAM.Row row, string fieldName, int fallback = 0)
    {
        try
        {
            object raw = row[fieldName].Value;
            return raw switch
            {
                sbyte value => value,
                byte value => value,
                short value => value,
                ushort value => value,
                int value => value,
                uint value when value <= int.MaxValue => (int)value,
                long value when value >= int.MinValue && value <= int.MaxValue => (int)value,
                ulong value when value <= (ulong)int.MaxValue => (int)value,
                bool value => value ? 1 : 0,
                float value when value >= int.MinValue && value <= int.MaxValue => (int)value,
                double value when value >= int.MinValue && value <= int.MaxValue => (int)value,
                _ when int.TryParse(raw?.ToString(), out int parsedValue) => parsedValue,
                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private readonly record struct StartingClassOriginDescriptor(int RowId, int DescriptionTextId);
}
