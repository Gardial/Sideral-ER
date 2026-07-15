using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RandomMagicConversion;

internal static class ProjectLayout
{
    public static string ResolveProjectDir()
    {
        string current = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            bool hasProjectFile = Directory.GetFiles(current, "*.csproj").Length > 0;
            bool hasSourceLayout = hasProjectFile && Directory.Exists(Path.Combine(current, "Base"));
            bool hasPublishedLayout =
                Directory.Exists(Path.Combine(current, "Data")) &&
                Directory.Exists(Path.Combine(current, "Defs"));

            if (hasSourceLayout || hasPublishedLayout)
                return current;

            DirectoryInfo parent = Directory.GetParent(current);
            if (parent == null)
                break;

            current = parent.FullName;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }

    public static string ResolveItemslotsPath(string projectDir)
    {
        string workspaceDir = Directory.GetParent(projectDir)?.FullName ?? projectDir;

        string[] candidates =
        {
            Path.Combine(projectDir, "Data", "Randomizer", "Base", "itemslots.txt"),
            Path.Combine(workspaceDir, "SoulsRandomizers", "diste", "Base", "itemslots.txt"),
            Path.Combine(workspaceDir, "SoulsRandomizers", "dist", "Base", "itemslots.txt"),
            Path.Combine(workspaceDir, "SoulsRandomizers", "dists", "Base", "itemslots.txt")
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Impossible de trouver itemslots.txt pour le pipeline map loot.");
    }

    public static string ResolveWeaponTextExportPath(string outputFolder)
    {
        string[] candidates =
        {
            Path.Combine(outputFolder, "Weapons_TextEditor_SmithBox.json"),
            Path.Combine(outputFolder, ".smithbox", "Workflow", "Exported Text", "Weapons_TextEditor_SmithBox.json")
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string ResolveEquipParamWeaponNamesPath(string projectDir)
    {
        string workspaceDir = Directory.GetParent(projectDir)?.FullName ?? projectDir;

        string[] candidates =
        {
            Path.Combine(projectDir, "Data", "Randomizer", "Names", "EquipParamWeapon.txt"),
            Path.Combine(workspaceDir, "SoulsRandomizers", "diste", "Names", "EquipParamWeapon.txt"),
            Path.Combine(workspaceDir, "SoulsRandomizers", "dist", "Names", "EquipParamWeapon.txt"),
            Path.Combine(workspaceDir, "SoulsRandomizers", "dists", "Names", "EquipParamWeapon.txt")
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public static string ResolveSoulsRandomizerLayoutPath(string projectDir, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Le nom du layout est vide.", nameof(fileName));

        string workspaceDir = Directory.GetParent(projectDir)?.FullName ?? projectDir;

        string[] candidates =
        {
            Path.Combine(projectDir, "Data", "Randomizer", "Layouts", fileName),
            // Pour les params de classes de depart, la variante "dist" correspond
            // mieux a la structure Elden Ring attendue que "dists".
            Path.Combine(workspaceDir, "SoulsRandomizers", "dist", "Layouts", fileName),
            Path.Combine(workspaceDir, "SoulsRandomizers", "dists", "Layouts", fileName),
            Path.Combine(workspaceDir, "SoulsRandomizers", "diste", "Layouts", fileName)
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Impossible de trouver le layout {fileName}.");
    }

    public static List<(string Locale, string[] SourcePaths)> ResolveAvailableLocalizedItemMsgbndSets(
        string projectDir,
        string baseOverridePath,
        string dlc2OverridePath)
    {
        string baseMsgFolder = Path.Combine(projectDir, "Base", "msg");
        var result = new List<(string Locale, string[] SourcePaths)>();

        if (!Directory.Exists(baseMsgFolder))
            return result;

        foreach (string localeDir in Directory.GetDirectories(baseMsgFolder)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string locale = Path.GetFileName(localeDir);
            string[] sourcePaths = ResolveLocalizedItemMsgbndPaths(
                projectDir,
                locale,
                string.Equals(locale, "frafr", StringComparison.OrdinalIgnoreCase) ? baseOverridePath : null,
                string.Equals(locale, "frafr", StringComparison.OrdinalIgnoreCase) ? dlc2OverridePath : null);

            if (sourcePaths.Length == 0)
                continue;

            result.Add((locale, sourcePaths));
        }

        return result;
    }

    public static List<(string Locale, string[] SourcePaths)> ResolveAvailableLocalizedMenuMsgbndSets(string projectDir)
    {
        string baseMsgFolder = Path.Combine(projectDir, "Base", "msg");
        var result = new List<(string Locale, string[] SourcePaths)>();

        if (!Directory.Exists(baseMsgFolder))
            return result;

        foreach (string localeDir in Directory.GetDirectories(baseMsgFolder)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            string locale = Path.GetFileName(localeDir);
            string[] sourcePaths = ResolveLocalizedMenuMsgbndPaths(projectDir, locale);
            if (sourcePaths.Length == 0)
                continue;

            result.Add((locale, sourcePaths));
        }

        return result;
    }

    public static string ResolveShieldTextPatchOutputPath(string outputFolder, string locale)
    {
        if (string.Equals(locale, "frafr", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(outputFolder, "Weapons_TextEditor_SmithBox_ShieldConversionPatch.json");

        return Path.Combine(
            outputFolder,
            "textpatch",
            locale,
            "Weapons_TextEditor_SmithBox_ShieldConversionPatch.json");
    }

    public static string ResolveStartingClassOriginTextPatchOutputPath(string outputFolder, string locale)
    {
        return Path.Combine(
            outputFolder,
            "textpatch",
            locale,
            "Menu_TextEditor_SmithBox_StartingClassOriginPatch.json");
    }

    private static string ResolveLocalizedItemMsgbndPath(string projectDir, string locale, string fileName)
    {
        string workspaceDir = Directory.GetParent(projectDir)?.FullName ?? projectDir;

        string[] candidates =
        {
            Path.Combine(projectDir, "Base", "msg", locale, fileName),
            Path.Combine(projectDir, "Base", fileName),
            Path.Combine(workspaceDir, "SoulsRandomizers", "diste", "Base", "msg", locale, fileName),
            Path.Combine(workspaceDir, "SoulsRandomizers", "dist", "Base", "msg", locale, fileName),
            Path.Combine(workspaceDir, "SoulsRandomizers", "dists", "Base", "msg", locale, fileName)
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static string ResolveLocalizedMenuMsgbndPath(string projectDir, string locale)
    {
        return ResolveLocalizedItemMsgbndPath(projectDir, locale, "menu.msgbnd.dcx");
    }

    private static string[] ResolveLocalizedMenuMsgbndPaths(string projectDir, string locale)
    {
        string baseLocaleDir = Path.Combine(projectDir, "Base", "msg", locale);
        if (Directory.Exists(baseLocaleDir))
        {
            string[] projectBasePaths = Directory.GetFiles(baseLocaleDir, "menu*.msgbnd.dcx")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (projectBasePaths.Length > 0)
                return projectBasePaths;
        }

        return new[]
            {
                ResolveLocalizedMenuMsgbndPath(projectDir, locale),
                ResolveLocalizedItemMsgbndPath(projectDir, locale, "menu_dlc01.msgbnd.dcx"),
                ResolveLocalizedItemMsgbndPath(projectDir, locale, "menu_dlc02.msgbnd.dcx")
            }
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ResolveLocalizedItemMsgbndPaths(
        string projectDir,
        string locale,
        string baseOverridePath,
        string dlc2OverridePath)
    {
        string baseLocaleDir = Path.Combine(projectDir, "Base", "msg", locale);
        if (Directory.Exists(baseLocaleDir))
        {
            string[] projectBasePaths = Directory.GetFiles(baseLocaleDir, "item*.msgbnd.dcx")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (projectBasePaths.Length > 0)
                return projectBasePaths;
        }

        return new[]
            {
                baseOverridePath,
                ResolveLocalizedItemMsgbndPath(projectDir, locale, "item.msgbnd.dcx"),
                ResolveLocalizedItemMsgbndPath(projectDir, locale, "item_dlc01.msgbnd.dcx"),
                dlc2OverridePath,
                ResolveLocalizedItemMsgbndPath(projectDir, locale, "item_dlc02.msgbnd.dcx")
            }
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
