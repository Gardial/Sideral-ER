using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class ShieldTextPatchResult
{
    public bool Generated { get; init; }
    public string TemplatePath { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public int WeaponNameEntryCount { get; init; }
    public int WeaponSummaryEntryCount { get; init; }
    public int WeaponDescriptionEntryCount { get; init; }
    public int FallbackNameCount { get; init; }
}

public sealed class ShieldTextPatchGenerator
{
    public ShieldTextPatchResult Generate(
        ConversionRun run,
        string templatePath,
        string outputPath,
        string weaponNamesPath = null,
        IEnumerable<string> localizedMsgbndPaths = null,
        string regulationPath = null,
        string defsFolder = null)
    {
        if (run == null)
            throw new ArgumentNullException(nameof(run));

        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            return new ShieldTextPatchResult
            {
                Generated = false,
                TemplatePath = templatePath ?? string.Empty,
                OutputPath = outputPath ?? string.Empty
            };
        }

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Le chemin de sortie du patch texte est vide.", nameof(outputPath));

        string json = File.ReadAllText(templatePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        SmithboxTextExportDocument template = JsonSerializer.Deserialize<SmithboxTextExportDocument>(json, options);
        if (template == null)
        {
            return new ShieldTextPatchResult
            {
                Generated = false,
                TemplatePath = templatePath,
                OutputPath = outputPath
            };
        }

        var templateWrappers = (template.FmgWrappers ?? new List<SmithboxFmgWrapper>())
            .Where(wrapper => wrapper != null)
            .ToDictionary(wrapper => wrapper.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var patch = new SmithboxTextExportDocument
        {
            Name = "ShieldConversionWeaponTextPatch"
        };

        Dictionary<int, string> weaponNameLookup = LoadWeaponNameLookup(weaponNamesPath);
        LocalizedWeaponTextLookup localizedTextLookup = LoadLocalizedWeaponTextLookup(localizedMsgbndPaths);
        Dictionary<int, int> originWeaponLookup = LoadOriginWeaponLookup(regulationPath, defsFolder);

        var patchWrappers = new Dictionary<string, SmithboxFmgWrapper>(StringComparer.OrdinalIgnoreCase);
        int fallbackNameCount = 0;

        foreach (ConversionMapping mapping in run.Mappings)
        {
            if (mapping.Kind != ConversionKind.ShieldToStaff && mapping.Kind != ConversionKind.ShieldToSeal)
                continue;

            int textSourceId = ResolveTextSourceId(mapping.SourceId, localizedTextLookup, weaponNameLookup, originWeaponLookup);
            int textTargetId = ResolveTextTargetId(mapping);

            string desiredName = ResolveDesiredName(mapping, textSourceId, templateWrappers, localizedTextLookup, weaponNameLookup, ref fallbackNameCount);
            if (!string.IsNullOrWhiteSpace(desiredName))
            {
                SmithboxFmgWrapper patchWrapper = GetOrCreatePatchWrapper(
                    patchWrappers,
                    templateWrappers,
                    "WeaponName.fmg",
                    defaultId: 11);
                UpsertEntry(patchWrapper, textTargetId, desiredName);
            }

            if (TryGetLookupText(localizedTextLookup.WeaponInfoLookup, textSourceId, out string desiredSummary))
            {
                SmithboxFmgWrapper patchWrapper = GetOrCreatePatchWrapper(
                    patchWrappers,
                    templateWrappers,
                    "WeaponSummary.fmg",
                    defaultId: 21);
                UpsertEntry(patchWrapper, textTargetId, desiredSummary);
            }

            if (TryGetLookupText(localizedTextLookup.WeaponCaptionLookup, textSourceId, out string desiredDescription))
            {
                SmithboxFmgWrapper patchWrapper = GetOrCreatePatchWrapper(
                    patchWrappers,
                    templateWrappers,
                    "WeaponDescription.fmg",
                    defaultId: 25);
                UpsertEntry(patchWrapper, textTargetId, desiredDescription);
            }
        }

        patch.FmgWrappers = patchWrappers.Values
            .Where(wrapper => wrapper.Fmg.Entries.Count > 0)
            .OrderBy(wrapper => wrapper.ID)
            .ToList();

        if (patch.FmgWrappers.Count == 0)
        {
            return new ShieldTextPatchResult
            {
                Generated = false,
                TemplatePath = templatePath,
                OutputPath = outputPath
            };
        }

        foreach (SmithboxFmgWrapper wrapper in patch.FmgWrappers)
        {
            wrapper.Fmg.Entries = wrapper.Fmg.Entries
                .OrderBy(entry => entry.ID)
                .ToList();
        }

        string outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        string outputJson = JsonSerializer.Serialize(patch, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(outputPath, outputJson);

        return new ShieldTextPatchResult
        {
            Generated = true,
            TemplatePath = templatePath,
            OutputPath = outputPath,
            WeaponNameEntryCount = CountEntries(patch, "WeaponName.fmg"),
            WeaponSummaryEntryCount = CountEntries(patch, "WeaponSummary.fmg"),
            WeaponDescriptionEntryCount = CountEntries(patch, "WeaponDescription.fmg"),
            FallbackNameCount = fallbackNameCount
        };
    }

    private static string ResolveDesiredName(
        ConversionMapping mapping,
        int textSourceId,
        Dictionary<string, SmithboxFmgWrapper> templateWrappers,
        LocalizedWeaponTextLookup localizedTextLookup,
        Dictionary<int, string> weaponNameLookup,
        ref int fallbackNameCount)
    {
        if (TryGetLookupText(localizedTextLookup.WeaponNameLookup, textSourceId, out string desiredName))
            return desiredName;

        if (TryGetWrapperText(templateWrappers, "WeaponName.fmg", textSourceId, out desiredName))
            return desiredName;

        if (TryGetLookupText(weaponNameLookup, textSourceId, out desiredName))
        {
            fallbackNameCount++;
            return desiredName;
        }

        fallbackNameCount++;
        return BuildFallbackName(mapping, desiredName);
    }

    private static int CountEntries(SmithboxTextExportDocument patch, string wrapperName)
    {
        return patch.FmgWrappers
            .Where(wrapper => string.Equals(wrapper.Name, wrapperName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(wrapper => wrapper.Fmg.Entries)
            .Count();
    }

    private static bool TryGetWrapperText(
        Dictionary<string, SmithboxFmgWrapper> wrappers,
        string wrapperName,
        int id,
        out string text)
    {
        text = null;

        if (wrappers == null || !wrappers.TryGetValue(wrapperName, out SmithboxFmgWrapper wrapper))
            return false;

        return TryGetValidText(wrapper, id, out text);
    }

    private static bool TryGetValidText(SmithboxFmgWrapper wrapper, int id, out string text)
    {
        text = null;

        SmithboxFmgEntry entry = wrapper.Fmg?.Entries?.FirstOrDefault(candidate => candidate.ID == id);
        if (entry == null || string.IsNullOrWhiteSpace(entry.Text))
            return false;

        if (string.Equals(entry.Text, "[ERROR]", StringComparison.OrdinalIgnoreCase))
            return false;

        text = entry.Text;
        return true;
    }

    private static bool TryGetLookupText(Dictionary<int, string> lookup, int id, out string text)
    {
        text = null;

        if (lookup == null)
            return false;

        if (!lookup.TryGetValue(id, out string value) || string.IsNullOrWhiteSpace(value))
            return false;

        if (string.Equals(value, "[ERROR]", StringComparison.OrdinalIgnoreCase))
            return false;

        text = value;
        return true;
    }

    private static string BuildFallbackName(ConversionMapping mapping, string targetText)
    {
        string suffix = mapping.Kind == ConversionKind.ShieldToStaff ? "[Staff]" : "[Seal]";

        if (!string.IsNullOrWhiteSpace(targetText))
            return $"{targetText} {suffix}";

        return $"Converted Shield {suffix}";
    }

    private static int ResolveTextTargetId(ConversionMapping mapping)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));

        return mapping.TargetTextRootId > 0
            ? mapping.TargetTextRootId
            : mapping.TargetId;
    }

    private static int ResolveTextSourceId(
        int sourceId,
        LocalizedWeaponTextLookup localizedTextLookup,
        Dictionary<int, string> weaponNameLookup,
        Dictionary<int, int> originWeaponLookup)
    {
        int currentId = sourceId;
        var visited = new HashSet<int>();

        while (currentId > 0 && visited.Add(currentId))
        {
            if (localizedTextLookup.WeaponNameLookup.ContainsKey(currentId) ||
                localizedTextLookup.WeaponInfoLookup.ContainsKey(currentId) ||
                localizedTextLookup.WeaponCaptionLookup.ContainsKey(currentId) ||
                weaponNameLookup.ContainsKey(currentId))
            {
                return currentId;
            }

            if (!originWeaponLookup.TryGetValue(currentId, out int nextId) || nextId <= 0 || nextId == currentId)
                break;

            currentId = nextId;
        }

        return sourceId;
    }

    private static Dictionary<int, string> LoadWeaponNameLookup(string weaponNamesPath)
    {
        var lookup = new Dictionary<int, string>();

        if (string.IsNullOrWhiteSpace(weaponNamesPath) || !File.Exists(weaponNamesPath))
            return lookup;

        foreach (string rawLine in File.ReadLines(weaponNamesPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            int firstSpace = rawLine.IndexOf(' ');
            if (firstSpace <= 0)
                continue;

            string idPart = rawLine[..firstSpace].Trim();
            string namePart = rawLine[(firstSpace + 1)..].Trim();

            if (!int.TryParse(idPart, out int id))
                continue;

            if (string.IsNullOrWhiteSpace(namePart))
                continue;

            lookup[id] = namePart;
        }

        return lookup;
    }

    private static LocalizedWeaponTextLookup LoadLocalizedWeaponTextLookup(IEnumerable<string> localizedMsgbndPaths)
    {
        var result = new LocalizedWeaponTextLookup();

        if (localizedMsgbndPaths == null)
            return result;

        foreach (string sourcePath in localizedMsgbndPaths.Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
        {
            BND4 bnd = BND4.Read(sourcePath);

            LoadFmgLookup(
                bnd,
                new[] { "WeaponName.fmg", "WeaponName_dlc01.fmg", "WeaponName_dlc02.fmg" },
                result.WeaponNameLookup);
            LoadFmgLookup(
                bnd,
                new[] { "WeaponInfo.fmg", "WeaponInfo_dlc01.fmg", "WeaponInfo_dlc02.fmg" },
                result.WeaponInfoLookup);
            LoadFmgLookup(
                bnd,
                new[] { "WeaponCaption.fmg", "WeaponCaption_dlc01.fmg", "WeaponCaption_dlc02.fmg" },
                result.WeaponCaptionLookup);
        }

        return result;
    }

    private static Dictionary<int, int> LoadOriginWeaponLookup(string regulationPath, string defsFolder)
    {
        var lookup = new Dictionary<int, int>();

        if (string.IsNullOrWhiteSpace(regulationPath) || !File.Exists(regulationPath))
            return lookup;

        if (string.IsNullOrWhiteSpace(defsFolder) || !Directory.Exists(defsFolder))
            return lookup;

        BND4 bnd;
        try
        {
            bnd = SFUtil.DecryptERRegulation(regulationPath);
        }
        catch
        {
            bnd = BND4.Read(regulationPath);
        }

        BinderFile paramFile = bnd.Files.FirstOrDefault(file => file.Name.EndsWith("EquipParamWeapon.param", StringComparison.OrdinalIgnoreCase));
        if (paramFile == null)
            return lookup;

        string xmlPath = Path.Combine(defsFolder, "EquipParamWeapon.xml");
        if (!File.Exists(xmlPath))
            return lookup;

        PARAM param = PARAM.Read(paramFile.Bytes);
        param.ApplyParamdef(PARAMDEF.XmlDeserialize(xmlPath));

        foreach (PARAM.Row row in param.Rows)
        {
            if (!row.Cells.Any(cell => string.Equals(cell.Def.InternalName, "originEquipWep", StringComparison.Ordinal)))
                continue;

            if (row["originEquipWep"]?.Value is int originEquipWep)
                lookup[row.ID] = originEquipWep;
        }

        return lookup;
    }

    private static void LoadFmgLookup(BND4 bnd, IEnumerable<string> fileNameFragments, Dictionary<int, string> targetLookup)
    {
        string[] fragments = fileNameFragments?
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<string>();

        if (fragments.Length == 0)
            return;

        foreach (BinderFile file in bnd.Files.Where(file => fragments.Any(fragment => file.Name.Contains(fragment, StringComparison.Ordinal))))
        {
            FMG fmg = FMG.Read(file.Bytes);
            foreach (FMG.Entry entry in fmg.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Text))
                    continue;

                if (string.Equals(entry.Text, "[ERROR]", StringComparison.OrdinalIgnoreCase))
                    continue;

                targetLookup[entry.ID] = entry.Text;
            }
        }
    }

    private static SmithboxFmgWrapper GetOrCreatePatchWrapper(
        Dictionary<string, SmithboxFmgWrapper> patchWrappers,
        Dictionary<string, SmithboxFmgWrapper> templateWrappers,
        string wrapperName,
        int defaultId)
    {
        if (patchWrappers.TryGetValue(wrapperName, out SmithboxFmgWrapper existing))
            return existing;

        templateWrappers.TryGetValue(wrapperName, out SmithboxFmgWrapper templateWrapper);

        var wrapper = new SmithboxFmgWrapper
        {
            Name = wrapperName,
            ID = templateWrapper?.ID ?? defaultId,
            Fmg = new SmithboxFmg
            {
                Name = templateWrapper?.Fmg?.Name ?? wrapperName,
                Entries = new List<SmithboxFmgEntry>()
            }
        };

        patchWrappers[wrapperName] = wrapper;
        return wrapper;
    }

    private static void UpsertEntry(SmithboxFmgWrapper wrapper, int targetId, string text)
    {
        SmithboxFmgEntry existing = wrapper.Fmg.Entries.FirstOrDefault(entry => entry.ID == targetId);

        if (existing != null)
        {
            existing.Text = text;
            return;
        }

        wrapper.Fmg.Entries.Add(new SmithboxFmgEntry
        {
            ID = targetId,
            Text = text
        });
    }
}

internal sealed class LocalizedWeaponTextLookup
{
    public Dictionary<int, string> WeaponNameLookup { get; } = new();
    public Dictionary<int, string> WeaponInfoLookup { get; } = new();
    public Dictionary<int, string> WeaponCaptionLookup { get; } = new();
}

internal sealed class SmithboxTextExportDocument
{
    public string Name { get; set; } = string.Empty;
    public List<SmithboxFmgWrapper> FmgWrappers { get; set; } = new();
}

internal sealed class SmithboxFmgWrapper
{
    public string Name { get; set; } = string.Empty;
    public int ID { get; set; }
    public SmithboxFmg Fmg { get; set; } = new();
}

internal sealed class SmithboxFmg
{
    public string Name { get; set; } = string.Empty;
    public List<SmithboxFmgEntry> Entries { get; set; } = new();
}

internal sealed class SmithboxFmgEntry
{
    public int ID { get; set; }
    public string Text { get; set; }
}
