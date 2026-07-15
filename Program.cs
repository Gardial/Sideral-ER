using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using SoulsFormats;

namespace RandomMagicConversion;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            string projectDir = ProjectLayout.ResolveProjectDir();
            Directory.SetCurrentDirectory(projectDir);

            string baseFolder = Path.Combine(projectDir, "Base");
            string outputFolder = Path.Combine(projectDir, "Output");
            string defsFolder = Path.Combine(projectDir, "Defs");
            string logsFolder = Path.Combine(projectDir, "Logs");

            if (args.Length > 0 &&
                (string.Equals(args[0], "--version", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "version", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"{VersionInfo.ProductName} {VersionInfo.DisplayVersion}");
                Console.WriteLine($"Release date: {VersionInfo.ReleaseDate}");
                return 0;
            }

            if (args.Length == 0 || (args.Length > 0 && string.Equals(args[0], "--gui", StringComparison.OrdinalIgnoreCase)))
            {
                ConsoleWindow.Hide();
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }

            if (args.Length > 0 && string.Equals(args[0], "scan-itemlot", StringComparison.OrdinalIgnoreCase))
                return RunItemLotScan(args, baseFolder, defsFolder, logsFolder);

            if (args.Length > 0 && string.Equals(args[0], "dump-itemlot-row", StringComparison.OrdinalIgnoreCase))
                return RunItemLotRowDump(args, baseFolder, outputFolder, defsFolder);

            if (args.Length > 0 && string.Equals(args[0], "inspect-soulsformats", StringComparison.OrdinalIgnoreCase))
                return RunSoulsFormatsInspect();

            if (args.Length > 0 && string.Equals(args[0], "inspect-msgbnd", StringComparison.OrdinalIgnoreCase))
                return RunInspectMsgbnd(args);

            if (args.Length > 0 && string.Equals(args[0], "inspect-msgbnd-entry", StringComparison.OrdinalIgnoreCase))
                return RunInspectMsgbndEntry(args);

            if (args.Length > 0 && string.Equals(args[0], "inspect-msgbnd-fmg", StringComparison.OrdinalIgnoreCase))
                return RunInspectMsgbndFmg(args);

            if (args.Length > 0 && string.Equals(args[0], "inspect-msgbnd-find-text", StringComparison.OrdinalIgnoreCase))
                return RunInspectMsgbndFindText(args);

            if (args.Length > 0 && string.Equals(args[0], "inspect-weapon-origin", StringComparison.OrdinalIgnoreCase))
                return RunInspectWeaponOrigin(args, baseFolder, defsFolder, outputFolder);

            if (args.Length > 0 && string.Equals(args[0], "dump-weapon-row", StringComparison.OrdinalIgnoreCase))
                return RunDumpWeaponRow(args, baseFolder, defsFolder, outputFolder);

            if (args.Length > 0 && string.Equals(args[0], "dump-magic-row", StringComparison.OrdinalIgnoreCase))
                return RunDumpMagicRow(args, baseFolder, defsFolder, outputFolder);

            if (args.Length > 0 && string.Equals(args[0], "audit-magic-requirements", StringComparison.OrdinalIgnoreCase))
                return RunAuditMagicRequirements(args, baseFolder, defsFolder, outputFolder);

            if (args.Length > 0 && string.Equals(args[0], "dump-shop-row", StringComparison.OrdinalIgnoreCase))
                return RunDumpShopRow(args, baseFolder, defsFolder, outputFolder);

            if (args.Length > 0 && string.Equals(args[0], "find-shop-item", StringComparison.OrdinalIgnoreCase))
                return RunFindShopItem(args, baseFolder, defsFolder, outputFolder);

            if (args.Length > 0 && string.Equals(args[0], "audit-remaining-weapons", StringComparison.OrdinalIgnoreCase))
                return RunAuditRemainingWeapons(args, projectDir, baseFolder, defsFolder, outputFolder);

            string[] generationArgs = args.Length > 0 && string.Equals(args[0], "generate", StringComparison.OrdinalIgnoreCase)
                ? args.Skip(1).ToArray()
                : args;

            int? seedOverride = ExtractSeedOverride(ref generationArgs);
            string shopConfigPath = generationArgs.Length > 2 ? generationArgs[2] : Path.Combine(projectDir, "Data", "shop_weapon_to_magic.json");
            bool standaloneProfile = IsStandaloneProfileConfigPath(shopConfigPath);

            var request = new GenerationRequest
            {
                ProjectDir = projectDir,
                InputRegulationPath = generationArgs.Length > 0 ? generationArgs[0] : Path.Combine(baseFolder, "regulation_base.bin"),
                OutputRegulationPath = generationArgs.Length > 1 ? generationArgs[1] : Path.Combine(outputFolder, "regulation.bin"),
                ShopConfigPath = shopConfigPath,
                MapLootConfigPath = generationArgs.Length > 3 ? generationArgs[3] : Path.Combine(projectDir, "Data", "map_loot_weapon_to_magic.json"),
                BossRewardConfigPath = generationArgs.Length > 4 ? generationArgs[4] : Path.Combine(projectDir, "Data", "boss_reward_weapon_to_magic.json"),
                EnemyLotConfigPath = generationArgs.Length > 5 ? generationArgs[5] : Path.Combine(projectDir, "Data", "enemy_lot_weapon_to_magic.json"),
                AshOfWarConfigPath = generationArgs.Length > 6 ? generationArgs[6] : Path.Combine(projectDir, "Data", "ash_of_war_to_magic.json"),
                BaseItemMsgbndOverridePath = generationArgs.Length > 7 ? generationArgs[7] : null,
                Dlc2ItemMsgbndOverridePath = generationArgs.Length > 8 ? generationArgs[8] : null,
                FarmableEnemyLootSuppressionConfigPath = generationArgs.Length > 9 ? generationArgs[9] : Path.Combine(projectDir, "Data", "farmable_enemy_loot_suppression.json"),
                StartingClassConfigPath = generationArgs.Length > 10 ? generationArgs[10] : Path.Combine(projectDir, "Data", "starting_class_weapon_to_magic.json"),
                SeedOverride = seedOverride,
                UseRandomizerFriendlyShieldUpgradePath = !standaloneProfile,
                RemoveStandaloneStatRequirements = standaloneProfile
            };

            new GenerationRunner().Run(request);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("Erreur fatale");
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static int? ExtractSeedOverride(ref string[] args)
    {
        var remaining = new List<string>();
        int? seedOverride = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.StartsWith("--seed=", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg["--seed=".Length..];
                seedOverride = ParseSeedOverride(value);
                continue;
            }

            if (string.Equals(arg, "--seed", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    throw new InvalidOperationException("L'option --seed attend un entier.");

                seedOverride = ParseSeedOverride(args[++i]);
                continue;
            }

            remaining.Add(arg);
        }

        args = remaining.ToArray();
        return seedOverride;
    }

    private static int ParseSeedOverride(string raw)
    {
        if (!int.TryParse(raw, out int seed))
            throw new InvalidOperationException($"Seed invalide : {raw}");

        return seed;
    }

    private static int RunItemLotScan(string[] args, string baseFolder, string defsFolder, string logsFolder)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int targetItemId))
        {
            Console.WriteLine("Usage: scan-itemlot <targetItemId>");
            return 1;
        }

        string inputRegulation = Path.Combine(baseFolder, "regulation_base.bin");
        string logPath = Path.Combine(logsFolder, $"itemlot_scan_{targetItemId}.txt");

        Directory.CreateDirectory(logsFolder);

        var spikeRunner = new WeaponMagicSpikeRunner(defsFolder, verbose: false);
        spikeRunner.ScanItemLots(inputRegulation, targetItemId, logPath);

        Console.WriteLine($"ItemLot scan saved : {logPath}");
        return 0;
    }

    private static int RunItemLotRowDump(string[] args, string baseFolder, string outputFolder, string defsFolder)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int rowId))
        {
            Console.WriteLine("Usage: dump-itemlot-row <rowId> [regulationPath] [paramName]");
            return 1;
        }

        string regulationPath = args.Length > 2
            ? Path.GetFullPath(args[2])
            : Path.Combine(outputFolder, "regulation.bin");

        if (!File.Exists(regulationPath))
            regulationPath = Path.Combine(baseFolder, "regulation_base.bin");

        string paramName = args.Length > 3
            ? args[3]
            : "ItemLotParam_map";

        var debugger = new ItemLotRowDebugger(defsFolder);
        debugger.DumpRow(regulationPath, paramName, rowId);
        return 0;
    }

    private static int RunSoulsFormatsInspect()
    {
        Assembly assembly = typeof(BND4).Assembly;

        Console.WriteLine($"Assembly: {assembly.FullName}");

        foreach (Type type in assembly.GetTypes()
                     .Where(type => type.FullName != null &&
                                    (type.FullName.Contains("FMG", StringComparison.OrdinalIgnoreCase) ||
                                     type.FullName.Contains("BND4", StringComparison.OrdinalIgnoreCase) ||
                                     type.FullName.Contains("BXF4", StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(type => type.FullName))
        {
            Console.WriteLine();
            Console.WriteLine(type.FullName);

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .OrderBy(method => method.Name))
            {
                Console.WriteLine($"  {method}");
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .OrderBy(property => property.Name))
            {
                Console.WriteLine($"  PROPERTY {property.PropertyType.Name} {property.Name}");
            }
        }

        return 0;
    }

    private static int RunInspectMsgbnd(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: inspect-msgbnd <path>");
            return 1;
        }

        string path = Path.GetFullPath(args[1]);
        BND4 bnd = BND4.Read(path);

        Console.WriteLine($"Path: {path}");
        Console.WriteLine($"Files: {bnd.Files.Count}");

        foreach (BinderFile file in bnd.Files.Take(40))
        {
            Console.WriteLine($"{file.ID} | {file.Name} | {file.Bytes.Length}");
        }

        return 0;
    }

    private static int RunInspectMsgbndEntry(string[] args)
    {
        if (args.Length < 4 || !int.TryParse(args[3], out int entryId))
        {
            Console.WriteLine("Usage: inspect-msgbnd-entry <path> <wrapperNameOrFragment> <entryId>");
            return 1;
        }

        string path = Path.GetFullPath(args[1]);
        string wrapperNameOrFragment = args[2];

        var writer = new ItemMsgbndTextWriter();
        string text = writer.ReadEntryText(path, wrapperNameOrFragment, entryId);

        Console.WriteLine($"Path     : {path}");
        Console.WriteLine($"Wrapper  : {wrapperNameOrFragment}");
        Console.WriteLine($"Entry ID : {entryId}");
        Console.WriteLine($"Text     : {text ?? "<null>"}");
        return 0;
    }

    private static int RunInspectMsgbndFmg(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: inspect-msgbnd-fmg <path> <fileNameFragment>");
            return 1;
        }

        string path = Path.GetFullPath(args[1]);
        string fileNameFragment = args[2];
        BND4 bnd = BND4.Read(path);

        foreach (BinderFile file in bnd.Files.Where(file => file.Name.Contains(fileNameFragment, StringComparison.Ordinal)))
        {
            FMG fmg = FMG.Read(file.Bytes);
            Console.WriteLine($"File        : {file.Name}");
            Console.WriteLine($"Entry count : {fmg.Entries.Count}");

            foreach (FMG.Entry entry in fmg.Entries.Take(15))
                Console.WriteLine($"   - {entry.ID} => {entry.Text}");
        }

        return 0;
    }

    private static int RunInspectMsgbndFindText(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: inspect-msgbnd-find-text <path> <fileNameFragment> <needle> [limit]");
            return 1;
        }

        string path = Path.GetFullPath(args[1]);
        string fileNameFragment = args[2];
        string needle = args[3];
        int limit = args.Length > 4 && int.TryParse(args[4], out int parsedLimit)
            ? parsedLimit
            : 20;

        BND4 bnd = BND4.Read(path);
        int matchCount = 0;

        foreach (BinderFile file in bnd.Files.Where(file => file.Name.Contains(fileNameFragment, StringComparison.Ordinal)))
        {
            FMG fmg = FMG.Read(file.Bytes);

            foreach (FMG.Entry entry in fmg.Entries)
            {
                string text = entry.Text ?? string.Empty;
                if (!text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    continue;

                Console.WriteLine($"{file.Name} | {entry.ID} => {text}");
                matchCount++;

                if (matchCount >= limit)
                    return 0;
            }
        }

        Console.WriteLine("Aucune entree correspondante.");
        return 0;
    }

    private static int RunInspectWeaponOrigin(string[] args, string baseFolder, string defsFolder, string outputFolder)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int weaponId))
        {
            Console.WriteLine("Usage: inspect-weapon-origin <weaponId> [regulationPath]");
            return 1;
        }

        string regulationPath = args.Length > 2
            ? Path.GetFullPath(args[2])
            : Path.Combine(outputFolder, "regulation.bin");

        if (!File.Exists(regulationPath))
            regulationPath = Path.Combine(baseFolder, "regulation_base.bin");

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
        {
            Console.WriteLine("EquipParamWeapon.param introuvable.");
            return 1;
        }

        string xmlPath = Path.Combine(defsFolder, "EquipParamWeapon.xml");
        if (!File.Exists(xmlPath))
        {
            Console.WriteLine($"Paramdef introuvable : {xmlPath}");
            return 1;
        }

        PARAM param = PARAM.Read(paramFile.Bytes);
        param.ApplyParamdef(PARAMDEF.XmlDeserialize(xmlPath));

        var visited = new HashSet<int>();
        int currentId = weaponId;

        while (visited.Add(currentId))
        {
            PARAM.Row row = param[currentId];
            if (row == null)
            {
                Console.WriteLine($"Row introuvable : {currentId}");
                break;
            }

            int originEquipWep = GetIntCell(row, "originEquipWep");
            int sortId = GetIntCell(row, "sortId");
            int wepType = GetIntCell(row, "wepType");
            int behaviorVariationId = GetIntCell(row, "behaviorVariationId");

            Console.WriteLine($"Row {currentId} | originEquipWep={originEquipWep} | sortId={sortId} | wepType={wepType} | behaviorVariationId={behaviorVariationId}");

            if (originEquipWep <= 0 || originEquipWep == currentId)
                break;

            currentId = originEquipWep;
        }

        return 0;
    }

    private static int RunDumpWeaponRow(string[] args, string baseFolder, string defsFolder, string outputFolder)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int weaponId))
        {
            Console.WriteLine("Usage: dump-weapon-row <weaponId> [regulationPath]");
            return 1;
        }

        string regulationPath = ResolveRegulationPath(args, 2, baseFolder, outputFolder);
        BND4 bnd = LoadRegulationBnd(regulationPath);
        PARAM param = ReadParam(bnd, "EquipParamWeapon", defsFolder);
        PARAM.Row row = param.Rows.FirstOrDefault(candidate => candidate.ID == weaponId);

        Console.WriteLine($"Regulation path : {regulationPath}");
        Console.WriteLine($"Param           : EquipParamWeapon");
        Console.WriteLine($"RowID           : {weaponId}");

        if (row == null)
        {
            Console.WriteLine("Row introuvable.");
            return 1;
        }

        string[] fields =
        {
            "behaviorVariationId",
            "sortId",
            "weight",
            "weaponCategory",
            "wepmotionCategory",
            "guardmotionCategory",
            "weaponPoseTypeR",
            "weaponPoseTypeL",
            "spAttribute",
            "wepmotionOneHandId",
            "wepmotionBothHandId",
            "rightHandEquipable",
            "leftHandEquipable",
            "bothHandEquipable",
            "bothHandleEquiable",
            "isWeaponCatalyst",
            "enableGuard",
            "enableParry",
            "enableMagic",
            "enableSorcery",
            "enableMiracle",
            "attackBasePhysics",
            "physGuardCutRate",
            "magGuardCutRate",
            "fireGuardCutRate",
            "thunGuardCutRate",
            "darkGuardCutRate",
            "staminaGuardDef",
            "guardAngle",
            "guardBaseRepel",
            "guardLevel",
            "wepCollidableType0",
            "wepCollidableType1",
            "swordArtsParamId",
            "originEquipWep",
            "wepType"
        };

        DumpSelectedFields(row, fields);
        return 0;
    }

    private static int RunDumpShopRow(string[] args, string baseFolder, string defsFolder, string outputFolder)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int rowId))
        {
            Console.WriteLine("Usage: dump-shop-row <rowId> [regulationPath]");
            return 1;
        }

        string regulationPath = ResolveRegulationPath(args, 2, baseFolder, outputFolder);
        BND4 bnd = LoadRegulationBnd(regulationPath);
        PARAM param = ReadParam(bnd, "ShopLineupParam", defsFolder);
        PARAM.Row row = param.Rows.FirstOrDefault(candidate => candidate.ID == rowId);

        Console.WriteLine($"Regulation path : {regulationPath}");
        Console.WriteLine($"Param           : ShopLineupParam");
        Console.WriteLine($"RowID           : {rowId}");

        if (row == null)
        {
            Console.WriteLine("Row introuvable.");
            return 1;
        }

        DumpAllNamedFields(row);
        return 0;
    }

    private static int RunDumpMagicRow(string[] args, string baseFolder, string defsFolder, string outputFolder)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int magicId))
        {
            Console.WriteLine("Usage: dump-magic-row <magicId> [regulationPath]");
            return 1;
        }

        string regulationPath = ResolveRegulationPath(args, 2, baseFolder, outputFolder);
        BND4 bnd = LoadRegulationBnd(regulationPath);
        PARAM param = ReadParam(bnd, "Magic", defsFolder);
        PARAM.Row row = param.Rows.FirstOrDefault(candidate => candidate.ID == magicId);

        Console.WriteLine($"Regulation path : {regulationPath}");
        Console.WriteLine($"Param           : Magic");
        Console.WriteLine($"RowID           : {magicId}");

        if (row == null)
        {
            Console.WriteLine("Row introuvable.");
            return 1;
        }

        DumpAllNamedFields(row);
        return 0;
    }

    private static int RunAuditMagicRequirements(string[] args, string baseFolder, string defsFolder, string outputFolder)
    {
        string regulationPath = ResolveRegulationPath(args, 1, baseFolder, outputFolder);
        BND4 bnd = LoadRegulationBnd(regulationPath);
        PARAM param = ReadParam(bnd, "Magic", defsFolder);

        var rows = param.Rows
            .Where(row => ShopMagicPoolClassifier.Classify((int)row.ID) != ShopMagicCategory.None)
            .Select(row => new
            {
                Row = row,
                Int = GetIntCell(row, "requirementIntellect"),
                Fai = GetIntCell(row, "requirementFaith"),
                Arc = GetIntCell(row, "requirementLuck")
            })
            .Where(entry => entry.Int > 0 || entry.Fai > 0 || entry.Arc > 0)
            .OrderBy(entry => entry.Row.ID)
            .ToList();

        Console.WriteLine($"Regulation path : {regulationPath}");
        Console.WriteLine("Param           : Magic");
        Console.WriteLine($"Rows with reqs  : {rows.Count}");

        foreach (var entry in rows.Take(80))
            Console.WriteLine($"RowID={entry.Row.ID} | INT={entry.Int} | FAI={entry.Fai} | ARC={entry.Arc}");

        if (rows.Count > 80)
            Console.WriteLine($"... {rows.Count - 80} lignes supplementaires non affichees.");

        return rows.Count == 0 ? 0 : 2;
    }

    private static int RunFindShopItem(string[] args, string baseFolder, string defsFolder, string outputFolder)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int equipId))
        {
            Console.WriteLine("Usage: find-shop-item <equipId> [regulationPath]");
            return 1;
        }

        string regulationPath = ResolveRegulationPath(args, 2, baseFolder, outputFolder);
        BND4 bnd = LoadRegulationBnd(regulationPath);
        PARAM param = ReadParam(bnd, "ShopLineupParam", defsFolder);

        Console.WriteLine($"Regulation path : {regulationPath}");
        Console.WriteLine($"Param           : ShopLineupParam");
        Console.WriteLine($"equipId         : {equipId}");

        int count = 0;
        foreach (PARAM.Row row in param.Rows.OrderBy(row => row.ID))
        {
            if (!TryReadAuditInt(row, "equipId", out int currentEquipId) || currentEquipId != equipId)
                continue;

            TryReadAuditInt(row, "equipType", out int equipType);
            TryReadAuditInt(row, "value", out int value);
            TryReadAuditInt(row, "sellQuantity", out int sellQuantity);

            Console.WriteLine($"Row={row.ID} | equipId={currentEquipId} | equipType={equipType} | value={value} | sellQuantity={sellQuantity}");
            count++;
        }

        Console.WriteLine($"Rows trouvees   : {count}");
        return 0;
    }

    private static int RunAuditRemainingWeapons(string[] args, string projectDir, string baseFolder, string defsFolder, string outputFolder)
    {
        string regulationPath = args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.Combine(outputFolder, "regulation.bin");

        if (!File.Exists(regulationPath))
            regulationPath = Path.Combine(baseFolder, "regulation_base.bin");

        var weaponCatalog = WeaponIdCatalog.LoadFromRegulation(regulationPath, defsFolder);
        var weaponNames = LoadWeaponNames(ResolveEquipParamWeaponNamesPath(projectDir));

        BND4 bnd = LoadRegulationBnd(regulationPath);

        PARAM shopParam = ReadParam(bnd, "ShopLineupParam", defsFolder);
        PARAM itemLotMapParam = ReadParam(bnd, "ItemLotParam_map", defsFolder);
        PARAM itemLotEnemyParam = ReadParam(bnd, "ItemLotParam_enemy", defsFolder);

        List<RemainingWeaponAuditEntry> entries = new();
        AuditShopRows(shopParam, weaponCatalog, weaponNames, entries);
        AuditItemLotRows(itemLotMapParam, "ItemLotParam_map", weaponCatalog, weaponNames, entries);
        AuditItemLotRows(itemLotEnemyParam, "ItemLotParam_enemy", weaponCatalog, weaponNames, entries);

        Console.WriteLine($"Regulation path : {regulationPath}");
        Console.WriteLine($"Remaining rows  : {entries.Count}");

        if (entries.Count == 0)
        {
            Console.WriteLine("Aucune arme convertible restante dans ShopLineupParam / ItemLotParam_map / ItemLotParam_enemy.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("Top remaining IDs:");

        foreach (var group in entries
                     .GroupBy(entry => new { entry.SourceParam, entry.ItemId, entry.ItemName })
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key.SourceParam, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.ItemId)
                     .Take(50))
        {
            string rows = string.Join(", ", group.Take(6).Select(entry => entry.DescribeLocation()));
            Console.WriteLine(
                $"{group.Key.SourceParam} | {group.Key.ItemId} | {group.Key.ItemName} | Count={group.Count()} | {rows}");
        }

        return 0;
    }

    private static int GetIntCell(PARAM.Row row, string fieldName)
    {
        PARAM.Cell cell = row.Cells.FirstOrDefault(candidate => string.Equals(candidate.Def.InternalName, fieldName, StringComparison.Ordinal));
        if (cell?.Value is int intValue)
            return intValue;

        if (cell?.Value is byte byteValue)
            return byteValue;

        if (cell?.Value is sbyte sbyteValue)
            return sbyteValue;

        if (cell?.Value is uint uintValue)
            return unchecked((int)uintValue);

        if (cell?.Value is short shortValue)
            return shortValue;

        if (cell?.Value is ushort ushortValue)
            return ushortValue;

        if (cell?.Value is long longValue)
            return unchecked((int)longValue);

        if (cell?.Value is ulong ulongValue && ulongValue <= int.MaxValue)
            return (int)ulongValue;

        return -1;
    }

    private static PARAM ReadParam(BND4 bnd, string paramName, string defsFolder)
    {
        BinderFile file = bnd.Files.FirstOrDefault(candidate =>
            string.Equals(Path.GetFileNameWithoutExtension(candidate.Name), paramName, StringComparison.OrdinalIgnoreCase));

        if (file == null)
            throw new InvalidOperationException($"Param introuvable : {paramName}");

        string xmlPath = Path.Combine(defsFolder, $"{paramName}.xml");
        if (!File.Exists(xmlPath) && paramName.StartsWith("ItemLotParam_", StringComparison.Ordinal))
            xmlPath = Path.Combine(defsFolder, "ItemLotParam.xml");

        if (!File.Exists(xmlPath))
            xmlPath = Path.Combine(defsFolder, "Generated", $"{paramName}.xml");

        if (!File.Exists(xmlPath))
            throw new FileNotFoundException($"Paramdef introuvable : {xmlPath}");

        PARAM param = PARAM.Read(file.Bytes);
        param.ApplyParamdef(PARAMDEF.XmlDeserialize(xmlPath));
        return param;
    }

    private static BND4 LoadRegulationBnd(string regulationPath)
    {
        try
        {
            return SFUtil.DecryptERRegulation(regulationPath);
        }
        catch
        {
            return BND4.Read(regulationPath);
        }
    }

    private static string ResolveRegulationPath(string[] args, int argumentIndex, string baseFolder, string outputFolder)
    {
        string regulationPath = args.Length > argumentIndex
            ? Path.GetFullPath(args[argumentIndex])
            : Path.Combine(outputFolder, "regulation.bin");

        return File.Exists(regulationPath)
            ? regulationPath
            : Path.Combine(baseFolder, "regulation_base.bin");
    }

    private static void DumpSelectedFields(PARAM.Row row, IEnumerable<string> fieldNames)
    {
        foreach (string fieldName in fieldNames)
        {
            PARAM.Cell cell = row.Cells.FirstOrDefault(candidate => string.Equals(candidate.Def.InternalName, fieldName, StringComparison.Ordinal));
            if (cell == null)
                continue;

            Console.WriteLine($"{fieldName} = {FormatCellValue(cell.Value)}");
        }
    }

    private static void DumpAllNamedFields(PARAM.Row row)
    {
        foreach (PARAM.Cell cell in row.Cells)
        {
            string fieldName = cell.Def?.InternalName;
            if (string.IsNullOrWhiteSpace(fieldName) || fieldName.StartsWith("pad", StringComparison.OrdinalIgnoreCase))
                continue;

            Console.WriteLine($"{fieldName} = {FormatCellValue(cell.Value)}");
        }
    }

    private static string FormatCellValue(object value)
    {
        return value switch
        {
            null => "<null>",
            byte[] bytes => BitConverter.ToString(bytes),
            _ => value.ToString()
        };
    }

    private static Dictionary<int, string> LoadWeaponNames(string namesPath)
    {
        var result = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(namesPath) || !File.Exists(namesPath))
            return result;

        foreach (string line in File.ReadLines(namesPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            int separatorIndex = line.IndexOf(' ');
            if (separatorIndex <= 0)
                continue;

            if (!int.TryParse(line[..separatorIndex], out int id))
                continue;

            string name = line[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result[id] = name;
        }

        return result;
    }

    private static void AuditShopRows(
        PARAM shopParam,
        WeaponIdCatalog weaponCatalog,
        IReadOnlyDictionary<int, string> weaponNames,
        ICollection<RemainingWeaponAuditEntry> results)
    {
        foreach (PARAM.Row row in shopParam.Rows)
        {
            if (!TryReadAuditInt(row, "equipId", out int equipId))
                continue;

            if (!TryReadAuditInt(row, "equipType", out int equipType))
                continue;

            if (equipId <= 0 || equipType == 3 || !weaponCatalog.IsConvertibleWeaponForMagic(equipId))
                continue;

            results.Add(new RemainingWeaponAuditEntry(
                "ShopLineupParam",
                row.ID,
                0,
                equipId,
                ResolveWeaponName(equipId, weaponNames)));
        }
    }

    private static void AuditItemLotRows(
        PARAM itemLotParam,
        string paramName,
        WeaponIdCatalog weaponCatalog,
        IReadOnlyDictionary<int, string> weaponNames,
        ICollection<RemainingWeaponAuditEntry> results)
    {
        foreach (PARAM.Row row in itemLotParam.Rows)
        {
            for (int slot = 1; slot <= 8; slot++)
            {
                string suffix = slot.ToString("00");

                if (!TryReadAuditInt(row, $"lotItemId{suffix}", out int itemId))
                    continue;

                if (!TryReadAuditInt(row, $"lotItemCategory{suffix}", out int category))
                    continue;

                if (category != 2 || itemId <= 0 || !weaponCatalog.IsConvertibleWeaponForMagic(itemId))
                    continue;

                results.Add(new RemainingWeaponAuditEntry(
                    paramName,
                    row.ID,
                    slot,
                    itemId,
                    ResolveWeaponName(itemId, weaponNames)));
            }
        }
    }

    private static bool TryReadAuditInt(PARAM.Row row, string fieldName, out int value)
    {
        value = default;

        PARAM.Cell cell = row.Cells.FirstOrDefault(candidate => string.Equals(candidate.Def.InternalName, fieldName, StringComparison.Ordinal));
        if (cell == null || cell.Value == null)
            return false;

        switch (cell.Value)
        {
            case int intValue:
                value = intValue;
                return true;
            case uint uintValue:
                value = unchecked((int)uintValue);
                return true;
            case short shortValue:
                value = shortValue;
                return true;
            case ushort ushortValue:
                value = ushortValue;
                return true;
            case long longValue:
                value = unchecked((int)longValue);
                return true;
            case byte byteValue:
                value = byteValue;
                return true;
            case sbyte sbyteValue:
                value = sbyteValue;
                return true;
            default:
                return false;
        }
    }

    private static string ResolveWeaponName(int weaponId, IReadOnlyDictionary<int, string> weaponNames)
    {
        return weaponNames.TryGetValue(weaponId, out string name)
            ? name
            : $"<unknown:{weaponId}>";
    }

    private static bool IsStandaloneProfileConfigPath(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            return false;

        string fileName = Path.GetFileName(configPath);
        return fileName.Contains(".standalone.", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveItemslotsPath(string projectDir)
    {
        string workspaceDir = Directory.GetParent(projectDir)?.FullName ?? projectDir;

        string[] candidates =
        {
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

    private static string ResolveWeaponTextExportPath(string outputFolder)
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

    private static string ResolveEquipParamWeaponNamesPath(string projectDir)
    {
        string workspaceDir = Directory.GetParent(projectDir)?.FullName ?? projectDir;

        string[] candidates =
        {
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

    private static List<(string Locale, string[] SourcePaths)> ResolveAvailableLocalizedItemMsgbndSets(
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

    private static string ResolveShieldTextPatchOutputPath(string outputFolder, string locale)
    {
        if (string.Equals(locale, "frafr", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(outputFolder, "Weapons_TextEditor_SmithBox_ShieldConversionPatch.json");

        return Path.Combine(
            outputFolder,
            "textpatch",
            locale,
            "Weapons_TextEditor_SmithBox_ShieldConversionPatch.json");
    }

    private static string ResolveProjectDir()
    {
        string current = AppContext.BaseDirectory;

        for (int i = 0; i < 10; i++)
        {
            bool hasBase = Directory.Exists(Path.Combine(current, "Base"));
            bool hasProjectFile = Directory.GetFiles(current, "*.csproj").Length > 0;

            if (hasBase && hasProjectFile)
                return current;

            DirectoryInfo parent = Directory.GetParent(current);
            if (parent == null)
                break;

            current = parent.FullName;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }

    private static void EnsureOodleDllsAvailable()
    {
        string baseDir = AppContext.BaseDirectory;
        string libDir = Path.Combine(baseDir, "lib");

        if (!Directory.Exists(libDir))
            return;

        foreach (string fileName in new[] { "oo2core_6_win64.dll", "oo2core_9_win64.dll" })
        {
            string sourcePath = Path.Combine(libDir, fileName);
            string destPath = Path.Combine(baseDir, fileName);

            if (!File.Exists(sourcePath) || File.Exists(destPath))
                continue;

            File.Copy(sourcePath, destPath, overwrite: false);
        }
    }

    private sealed record RemainingWeaponAuditEntry(
        string SourceParam,
        int RowId,
        int SlotIndex,
        int ItemId,
        string ItemName)
    {
        public string DescribeLocation()
        {
            return SlotIndex > 0
                ? $"Row={RowId} Slot={SlotIndex:00}"
                : $"Row={RowId}";
        }
    }
}
