using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class ItemMsgbndTextWriteResult
{
    public bool Generated { get; init; }
    public string BaseSourcePath { get; init; } = string.Empty;
    public string Dlc2SourcePath { get; init; } = string.Empty;
    public string BaseOutputPath { get; init; } = string.Empty;
    public string Dlc2OutputPath { get; init; } = string.Empty;
    public int UpdatedBaseFileCount { get; init; }
    public int UpdatedDlc2FileCount { get; init; }
    public int UpdatedTextEntryCount { get; init; }
    public int MissingTextEntryCount { get; init; }
    public List<string> MissingEntries { get; init; } = new();
}

public sealed class ItemMsgbndTextWriter
{
    public ItemMsgbndTextWriteResult ApplyPatch(
        string patchJsonPath,
        string baseMsgbndSourcePath,
        string dlc2MsgbndSourcePath,
        string baseMsgbndOutputPath,
        string dlc2MsgbndOutputPath)
    {
        if (string.IsNullOrWhiteSpace(patchJsonPath) || !File.Exists(patchJsonPath))
            throw new FileNotFoundException("Patch texte Smithbox introuvable.", patchJsonPath);

        if (string.IsNullOrWhiteSpace(baseMsgbndSourcePath) || !File.Exists(baseMsgbndSourcePath))
            throw new FileNotFoundException("item.msgbnd.dcx source introuvable.", baseMsgbndSourcePath);

        if (string.IsNullOrWhiteSpace(baseMsgbndOutputPath))
            throw new ArgumentException("Le chemin de sortie de item.msgbnd.dcx est vide.", nameof(baseMsgbndOutputPath));

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        string patchJson = File.ReadAllText(patchJsonPath);
        SmithboxTextExportDocument patch = JsonSerializer.Deserialize<SmithboxTextExportDocument>(patchJson, options)
            ?? throw new InvalidOperationException("Impossible de deserialiser le patch texte Smithbox.");

        BND4 baseBnd = BND4.Read(baseMsgbndSourcePath);
        var baseFiles = LoadFmgFiles(baseBnd);

        BND4 dlc2Bnd = null;
        var dlc2Files = new List<MsgbndFmgFile>();
        bool hasDlc2Source = !string.IsNullOrWhiteSpace(dlc2MsgbndSourcePath) && File.Exists(dlc2MsgbndSourcePath);

        if (hasDlc2Source)
        {
            if (string.IsNullOrWhiteSpace(dlc2MsgbndOutputPath))
                throw new ArgumentException("Le chemin de sortie de item_dlc2.msgbnd.dcx est vide.", nameof(dlc2MsgbndOutputPath));

            dlc2Bnd = BND4.Read(dlc2MsgbndSourcePath);
            dlc2Files = LoadFmgFiles(dlc2Bnd);
        }

        var allFiles = baseFiles.Concat(dlc2Files).ToList();

        int updatedEntryCount = 0;
        var missingEntries = new List<string>();

        foreach (SmithboxFmgWrapper wrapper in patch.FmgWrappers ?? Enumerable.Empty<SmithboxFmgWrapper>())
        {
            string[] fileNameFragments = ResolveRequestedFileNameFragments(wrapper.Name);
            if (fileNameFragments.Length == 0)
                continue;

            List<MsgbndFmgFile> candidates = allFiles
                .Where(file => fileNameFragments.Any(fragment => file.BinderFile.Name.Contains(fragment, StringComparison.Ordinal)))
                .ToList();

            foreach (SmithboxFmgEntry patchEntry in wrapper.Fmg.Entries ?? Enumerable.Empty<SmithboxFmgEntry>())
            {
                bool matched = false;

                foreach (MsgbndFmgFile candidate in candidates)
                {
                    FMG.Entry existing = candidate.Fmg.Entries.FirstOrDefault(entry => entry.ID == patchEntry.ID);
                    if (existing == null)
                        continue;

                    existing.Text = patchEntry.Text;
                    candidate.Modified = true;
                    matched = true;
                    updatedEntryCount++;
                }

                if (!matched)
                    missingEntries.Add($"{wrapper.Name}:{patchEntry.ID}");
            }
        }

        if (updatedEntryCount == 0 && missingEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "Aucune entree du patch n'a ete trouvee dans les item.msgbnd.dcx source. " +
                "Les fichiers texte locaux ne correspondent probablement pas au build Elden Ring attendu.");
        }

        SaveContainer(baseBnd, baseFiles, baseMsgbndOutputPath);

        if (dlc2Bnd != null)
            SaveContainer(dlc2Bnd, dlc2Files, dlc2MsgbndOutputPath);

        return new ItemMsgbndTextWriteResult
        {
            Generated = true,
            BaseSourcePath = baseMsgbndSourcePath,
            Dlc2SourcePath = dlc2MsgbndSourcePath ?? string.Empty,
            BaseOutputPath = baseMsgbndOutputPath,
            Dlc2OutputPath = dlc2MsgbndOutputPath ?? string.Empty,
            UpdatedBaseFileCount = baseFiles.Count(file => file.Modified),
            UpdatedDlc2FileCount = dlc2Files.Count(file => file.Modified),
            UpdatedTextEntryCount = updatedEntryCount,
            MissingTextEntryCount = missingEntries.Count,
            MissingEntries = missingEntries
        };
    }

    public string ReadEntryText(string msgbndPath, string wrapperNameOrFragment, int entryId)
    {
        if (string.IsNullOrWhiteSpace(msgbndPath) || !File.Exists(msgbndPath))
            throw new FileNotFoundException("msgbnd introuvable.", msgbndPath);

        string[] fileNameFragments = ResolveRequestedFileNameFragments(wrapperNameOrFragment);
        BND4 bnd = BND4.Read(msgbndPath);

        foreach (MsgbndFmgFile file in LoadFmgFiles(bnd))
        {
            if (!fileNameFragments.Any(fragment => file.BinderFile.Name.Contains(fragment, StringComparison.Ordinal)))
                continue;

            FMG.Entry entry = file.Fmg.Entries.FirstOrDefault(candidate => candidate.ID == entryId);
            if (entry != null)
                return entry.Text ?? string.Empty;
        }

        return null;
    }

    private static List<MsgbndFmgFile> LoadFmgFiles(BND4 bnd)
    {
        var result = new List<MsgbndFmgFile>();

        foreach (BinderFile file in bnd.Files)
        {
            if (!file.Name.EndsWith(".fmg", StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new MsgbndFmgFile
            {
                BinderFile = file,
                Fmg = FMG.Read(file.Bytes)
            });
        }

        return result;
    }

    private static void SaveContainer(BND4 bnd, List<MsgbndFmgFile> files, string outputPath)
    {
        string outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        foreach (MsgbndFmgFile file in files.Where(file => file.Modified))
            file.BinderFile.Bytes = file.Fmg.Write();

        bnd.Write(outputPath);
    }

    private static string[] ResolveFileNameFragments(string wrapperName)
    {
        return wrapperName switch
        {
            "WeaponName.fmg" => new[] { "WeaponName.fmg", "WeaponName_dlc01.fmg", "WeaponName_dlc02.fmg", "\u6B66\u5668\u540D" },
            "WeaponSummary.fmg" => new[] { "WeaponInfo.fmg", "WeaponInfo_dlc01.fmg", "WeaponInfo_dlc02.fmg", "WeaponSummary.fmg", "\u6B66\u5668\u8AAC\u660E" },
            "WeaponDescription.fmg" => new[] { "WeaponCaption.fmg", "WeaponCaption_dlc01.fmg", "WeaponCaption_dlc02.fmg", "WeaponDescription.fmg", "\u6B66\u5668\u3046\u3093\u3061\u304F" },
            "GR_LineHelp.fmg" => new[] { "GR_LineHelp.fmg" },
            "GR_MenuText.fmg" => new[] { "GR_MenuText.fmg" },
            _ => Array.Empty<string>()
        };
    }

    private static string[] ResolveRequestedFileNameFragments(string wrapperNameOrFragment)
    {
        if (string.IsNullOrWhiteSpace(wrapperNameOrFragment))
            throw new ArgumentException("Le nom du wrapper FMG est vide.", nameof(wrapperNameOrFragment));

        string[] resolved = ResolveFileNameFragments(wrapperNameOrFragment);
        return resolved.Length > 0 ? resolved : new[] { wrapperNameOrFragment };
    }
}

internal sealed class MsgbndFmgFile
{
    public BinderFile BinderFile { get; init; }
    public FMG Fmg { get; init; }
    public bool Modified { get; set; }
}
