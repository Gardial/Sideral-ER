using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RandomMagicConversion;

public sealed class GenerationRunner
{
    public GenerationResult Run(GenerationRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        EnsureOodleDllsAvailable();

        string projectDir = string.IsNullOrWhiteSpace(request.ProjectDir)
            ? ProjectLayout.ResolveProjectDir()
            : request.ProjectDir;

        Directory.SetCurrentDirectory(projectDir);

        string baseFolder = Path.Combine(projectDir, "Base");
        string outputFolder = Path.Combine(projectDir, "Output");
        string defsFolder = Path.Combine(projectDir, "Defs");
        string logsFolder = Path.Combine(projectDir, "Logs");

        string inputRegulation = !string.IsNullOrWhiteSpace(request.InputRegulationPath)
            ? Path.GetFullPath(request.InputRegulationPath)
            : Path.Combine(baseFolder, "regulation_base.bin");

        string outputRegulation = !string.IsNullOrWhiteSpace(request.OutputRegulationPath)
            ? Path.GetFullPath(request.OutputRegulationPath)
            : Path.Combine(outputFolder, "regulation.bin");

        string shopConfigPath = !string.IsNullOrWhiteSpace(request.ShopConfigPath)
            ? Path.GetFullPath(request.ShopConfigPath)
            : Path.Combine(projectDir, "Data", "shop_weapon_to_magic.json");

        string mapLootConfigPath = !string.IsNullOrWhiteSpace(request.MapLootConfigPath)
            ? Path.GetFullPath(request.MapLootConfigPath)
            : Path.Combine(projectDir, "Data", "map_loot_weapon_to_magic.json");

        string bossRewardConfigPath = !string.IsNullOrWhiteSpace(request.BossRewardConfigPath)
            ? Path.GetFullPath(request.BossRewardConfigPath)
            : Path.Combine(projectDir, "Data", "boss_reward_weapon_to_magic.json");

        string enemyLotConfigPath = !string.IsNullOrWhiteSpace(request.EnemyLotConfigPath)
            ? Path.GetFullPath(request.EnemyLotConfigPath)
            : Path.Combine(projectDir, "Data", "enemy_lot_weapon_to_magic.json");

        string farmableEnemyLootSuppressionConfigPath = !string.IsNullOrWhiteSpace(request.FarmableEnemyLootSuppressionConfigPath)
            ? Path.GetFullPath(request.FarmableEnemyLootSuppressionConfigPath)
            : Path.Combine(projectDir, "Data", "farmable_enemy_loot_suppression.json");

        string ashOfWarConfigPath = !string.IsNullOrWhiteSpace(request.AshOfWarConfigPath)
            ? Path.GetFullPath(request.AshOfWarConfigPath)
            : Path.Combine(projectDir, "Data", "ash_of_war_to_magic.json");

        string startingClassConfigPath = !string.IsNullOrWhiteSpace(request.StartingClassConfigPath)
            ? Path.GetFullPath(request.StartingClassConfigPath)
            : Path.Combine(projectDir, "Data", "starting_class_weapon_to_magic.json");

        string baseItemMsgbndOverridePath = !string.IsNullOrWhiteSpace(request.BaseItemMsgbndOverridePath)
            ? Path.GetFullPath(request.BaseItemMsgbndOverridePath)
            : null;

        string dlc2ItemMsgbndOverridePath = !string.IsNullOrWhiteSpace(request.Dlc2ItemMsgbndOverridePath)
            ? Path.GetFullPath(request.Dlc2ItemMsgbndOverridePath)
            : null;

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        Console.WriteLine("=== SIDERAL ===");
        Console.WriteLine($"ProjectDir        : {projectDir}");
        Console.WriteLine($"Base folder       : {baseFolder}");
        Console.WriteLine($"Output folder     : {outputFolder}");
        Console.WriteLine($"Defs folder       : {defsFolder}");
        Console.WriteLine($"Input regulation  : {inputRegulation}");
        Console.WriteLine($"Output regulation : {outputRegulation}");
        Console.WriteLine($"Shop config path  : {shopConfigPath}");
        Console.WriteLine($"Map loot config   : {mapLootConfigPath}");
        Console.WriteLine($"Boss config path  : {bossRewardConfigPath}");
        Console.WriteLine($"Enemy config path : {enemyLotConfigPath}");
        Console.WriteLine($"Farmable enemy    : {farmableEnemyLootSuppressionConfigPath}");
        Console.WriteLine($"AoW config path   : {ashOfWarConfigPath}");
        Console.WriteLine($"Start class config: {startingClassConfigPath}");
        Console.WriteLine($"Base msgbnd arg   : {baseItemMsgbndOverridePath}");
        Console.WriteLine($"DLC2 msgbnd arg   : {dlc2ItemMsgbndOverridePath}");
        Console.WriteLine($"Shield upgrade mode: {(request.UseRandomizerFriendlyShieldUpgradePath ? "Randomizer-safe" : "Catalyst-native")}");
        Console.WriteLine($"No stat requirements: {(request.RemoveStandaloneStatRequirements ? "enabled" : "disabled")}");
        Console.WriteLine($"Process arch      : {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"OS arch           : {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        Console.WriteLine();

        if (!File.Exists(inputRegulation))
            throw new FileNotFoundException($"regulation source introuvable : {inputRegulation}", inputRegulation);

        if (!Directory.Exists(defsFolder))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsFolder}");

        Directory.CreateDirectory(outputFolder);
        Directory.CreateDirectory(logsFolder);
        string weaponNamesPath = ProjectLayout.ResolveEquipParamWeaponNamesPath(projectDir);
        WeaponIdCatalog weaponCatalog = WeaponIdCatalog.LoadFromRegulation(inputRegulation, defsFolder);

        int seed = request.SeedOverride ?? Environment.TickCount;
        var run = new ConversionRun
        {
            Seed = seed
        };

        Console.WriteLine($"Seed du run       : {run.Seed}");
        Console.WriteLine();

        if (request.EnableShieldConversion)
        {
            Console.WriteLine("=== Pipeline 1 : Shields -> Staffs / Seals ===");
            var editor = new RegulationEditor();
            editor.ProcessEquipParamWeapon(
                inputRegulation,
                outputRegulation,
                defsFolder,
                run,
                weaponNamesPath,
                request.UseRandomizerFriendlyShieldUpgradePath);
        }
        else
        {
            Console.WriteLine("=== Pipeline 1 : Shields -> Staffs / Seals ===");
            Console.WriteLine("Pipeline desactive.");

            if (!string.Equals(inputRegulation, outputRegulation, StringComparison.OrdinalIgnoreCase))
                File.Copy(inputRegulation, outputRegulation, overwrite: true);
        }

        string mappingPath = Path.Combine(logsFolder, "last_run_mapping.json");
        ConversionRunSerializer.Save(mappingPath, run);

        Console.WriteLine($"Mappings generes  : {run.Count}");
        Console.WriteLine($"Mapping sauvegarde: {mappingPath}");

        Console.WriteLine();
        Console.WriteLine("=== Pipeline 2 : Starting Class Weapon -> Magic ===");

        StartingClassWeaponToMagicConfig startingClassConfig = OverrideStartingClassConfig(
            StartingClassWeaponToMagicConfigLoader.Load(startingClassConfigPath),
            request.EnableStartingClassConversion,
            request.SpellPoolModeOverride);

        Console.WriteLine($"Start class active : {startingClassConfig.Enabled}");
        Console.WriteLine($"Start class mode   : {startingClassConfig.SpellPoolMode}");
        Console.WriteLine($"Start class band   : {startingClassConfig.ProgressionBand}");

        string startingClassMappingPath = Path.Combine(logsFolder, "last_starting_class_magic_mapping.json");

        var startingClassEditor = new StartingClassWeaponToMagicEditor(defsFolder, projectDir, verbose: true);
        StartingClassWeaponToMagicRunResult startingClassRunResult = startingClassEditor.ApplyToRegulation(
            inputRegulationPath: outputRegulation,
            outputRegulationPath: outputRegulation,
            config: startingClassConfig,
            seed: run.Seed,
            mappingOutputPath: startingClassMappingPath);

        Console.WriteLine($"Start class pool   : {startingClassRunResult.SpellPoolCount}");
        Console.WriteLine($"Classes rebuilt    : {startingClassRunResult.RebuiltClassCount}");
        Console.WriteLine($"Start mapping saved: {startingClassMappingPath}");

        Console.WriteLine();
        Console.WriteLine("=== Pipeline 3 : Shop Weapon -> Magic (RandomPerRun) ===");

        ShopWeaponToMagicConfig shopConfig = OverrideShopConfig(
            ShopWeaponToMagicConfigLoader.Load(shopConfigPath),
            request.EnableShopConversion,
            request.SpellPoolModeOverride);

        Console.WriteLine($"Shop config active : {shopConfig.Enabled}");
        Console.WriteLine($"Shop pool mode     : {shopConfig.SpellPoolMode}");
        Console.WriteLine($"Shop progression   : {shopConfig.ProgressionBand}");
        Console.WriteLine($"Shop entries       : {shopConfig.Entries.Count}");

        string shopMappingPath = Path.Combine(logsFolder, "last_shop_magic_mapping.json");

        var shopEditor = new ShopWeaponToMagicEditor(defsFolder, verbose: true);
        ShopWeaponToMagicRunResult shopRunResult = shopEditor.ApplyToRegulation(
            inputRegulationPath: outputRegulation,
            outputRegulationPath: outputRegulation,
            config: shopConfig,
            seed: run.Seed,
            mappingOutputPath: shopMappingPath,
            weaponCatalog: weaponCatalog);

        Console.WriteLine($"Shop spell pool    : {shopRunResult.SpellPoolCount}");
        Console.WriteLine($"Shop replacements  : {shopRunResult.Mappings.Count}");
        Console.WriteLine($"Shop mapping saved : {shopMappingPath}");

        Console.WriteLine();
        Console.WriteLine("=== Pipeline 4 : Map Loot Weapon -> Magic (RandomPerRun) ===");

        MapLootWeaponToMagicConfig mapLootConfig = OverrideMapLootConfig(
            MapLootWeaponToMagicConfigLoader.Load(mapLootConfigPath),
            request.EnableMapLootConversion,
            request.SpellPoolModeOverride);

        Console.WriteLine($"Map loot config active : {mapLootConfig.Enabled}");
        Console.WriteLine($"Map loot pool mode     : {mapLootConfig.SpellPoolMode}");
        Console.WriteLine($"Map loot progression   : {mapLootConfig.ProgressionBand}");
        Console.WriteLine($"Map loot entries       : {mapLootConfig.Entries.Count}");

        string itemslotsPath = ProjectLayout.ResolveItemslotsPath(projectDir);
        Console.WriteLine($"Itemslots path         : {itemslotsPath}");

        string mapLootMappingPath = Path.Combine(logsFolder, "last_map_loot_magic_mapping.json");

        var mapLootEditor = new MapLootWeaponToMagicEditor(defsFolder, itemslotsPath, verbose: true);
        MapLootWeaponToMagicRunResult mapLootRunResult = mapLootEditor.ApplyToRegulation(
            inputRegulationPath: outputRegulation,
            outputRegulationPath: outputRegulation,
            config: mapLootConfig,
            seed: run.Seed,
            mappingOutputPath: mapLootMappingPath,
            weaponCatalog: weaponCatalog);

        Console.WriteLine($"Map loot spell pool    : {mapLootRunResult.SpellPoolCount}");
        Console.WriteLine($"Map loot replacements  : {mapLootRunResult.Mappings.Count}");
        Console.WriteLine($"Map loot mapping saved : {mapLootMappingPath}");

        Console.WriteLine();
        Console.WriteLine("=== Pipeline 5 : Boss Reward Weapon -> Magic (RandomPerRun) ===");

        BossRewardWeaponToMagicConfig bossRewardConfig = OverrideBossRewardConfig(
            BossRewardWeaponToMagicConfigLoader.Load(bossRewardConfigPath),
            request.EnableBossRewardConversion,
            request.SpellPoolModeOverride);

        Console.WriteLine($"Boss reward config active : {bossRewardConfig.Enabled}");
        Console.WriteLine($"Boss reward pool mode     : {bossRewardConfig.SpellPoolMode}");
        Console.WriteLine($"Boss reward progression   : {bossRewardConfig.ProgressionBand}");
        Console.WriteLine($"Boss reward entries       : {bossRewardConfig.Entries.Count}");

        string bossRewardMappingPath = Path.Combine(logsFolder, "last_boss_reward_magic_mapping.json");

        var bossRewardEditor = new BossRewardWeaponToMagicEditor(defsFolder, itemslotsPath, verbose: true);
        BossRewardWeaponToMagicRunResult bossRewardRunResult = bossRewardEditor.ApplyToRegulation(
            inputRegulationPath: outputRegulation,
            outputRegulationPath: outputRegulation,
            config: bossRewardConfig,
            seed: run.Seed,
            mappingOutputPath: bossRewardMappingPath,
            weaponCatalog: weaponCatalog);

        Console.WriteLine($"Boss reward spell pool    : {bossRewardRunResult.SpellPoolCount}");
        Console.WriteLine($"Boss reward replacements  : {bossRewardRunResult.Mappings.Count}");
        Console.WriteLine($"Boss reward mapping saved : {bossRewardMappingPath}");

        Console.WriteLine();
        Console.WriteLine("=== Pipeline 6 : Enemy Lot Weapon -> Magic (RandomPerRun) ===");

        EnemyLotWeaponToMagicConfig enemyLotConfig = OverrideEnemyLotConfig(
            EnemyLotWeaponToMagicConfigLoader.Load(enemyLotConfigPath),
            request.EnableEnemyLotConversion,
            request.SpellPoolModeOverride);

        Console.WriteLine($"Enemy lot config active : {enemyLotConfig.Enabled}");
        Console.WriteLine($"Enemy lot pool mode     : {enemyLotConfig.SpellPoolMode}");
        Console.WriteLine($"Enemy lot progression   : {enemyLotConfig.ProgressionBand}");
        Console.WriteLine($"Enemy lot entries       : {enemyLotConfig.Entries.Count}");

        string enemyLotMappingPath = Path.Combine(logsFolder, "last_enemy_lot_magic_mapping.json");

        var enemyLotEditor = new EnemyLotWeaponToMagicEditor(defsFolder, itemslotsPath, verbose: true);
        EnemyLotWeaponToMagicRunResult enemyLotRunResult = enemyLotEditor.ApplyToRegulation(
            inputRegulationPath: outputRegulation,
            outputRegulationPath: outputRegulation,
            config: enemyLotConfig,
            seed: run.Seed,
            mappingOutputPath: enemyLotMappingPath,
            weaponCatalog: weaponCatalog);

        Console.WriteLine($"Enemy lot spell pool    : {enemyLotRunResult.SpellPoolCount}");
        Console.WriteLine($"Enemy lot replacements  : {enemyLotRunResult.Mappings.Count}");
        Console.WriteLine($"Enemy lot mapping saved : {enemyLotMappingPath}");

        Console.WriteLine();
        Console.WriteLine("=== Pipeline 7 : Farmable Enemy Loot Suppression ===");

        FarmableEnemyLootSuppressionConfig farmableEnemyLootConfig = OverrideFarmableEnemyLootSuppressionConfig(
            FarmableEnemyLootSuppressionConfigLoader.Load(farmableEnemyLootSuppressionConfigPath),
            request.EnableFarmableEnemyLootSuppression);

        Console.WriteLine($"Farmable enemy config   : {farmableEnemyLootConfig.Enabled}");
        Console.WriteLine($"Min source occurrences  : {farmableEnemyLootConfig.MinimumSourceOccurrences}");
        Console.WriteLine($"Require zero flag       : {farmableEnemyLootConfig.RequireZeroGetItemFlag}");

        string farmableEnemyLootMappingPath = Path.Combine(logsFolder, "last_farmable_enemy_loot_suppression.json");

        var farmableEnemyLootSuppressor = new FarmableEnemyLootSuppressor(defsFolder, itemslotsPath, verbose: true);
        FarmableEnemyLootSuppressionRunResult farmableEnemyLootRunResult = farmableEnemyLootSuppressor.ApplyToRegulation(
            inputRegulationPath: outputRegulation,
            outputRegulationPath: outputRegulation,
            config: farmableEnemyLootConfig,
            enemyLotRunResult: enemyLotRunResult,
            weaponCatalog: weaponCatalog,
            mappingOutputPath: farmableEnemyLootMappingPath);

        Console.WriteLine($"Rows suppressed         : {farmableEnemyLootRunResult.SuppressedRowCount}");
        Console.WriteLine($"Slots cleared           : {farmableEnemyLootRunResult.ClearedSlotCount}");
        Console.WriteLine($"Suppression mapping     : {farmableEnemyLootMappingPath}");

        Console.WriteLine();
        Console.WriteLine("=== Pipeline 8 : Ashes of War -> Magic (RandomPerRun) ===");

        AshOfWarToMagicConfig ashOfWarConfig = OverrideAshOfWarConfig(
            AshOfWarToMagicConfigLoader.Load(ashOfWarConfigPath),
            request.EnableAshOfWarConversion,
            request.SpellPoolModeOverride);

        Console.WriteLine($"AoW config active        : {ashOfWarConfig.Enabled}");
        Console.WriteLine($"AoW pool mode            : {ashOfWarConfig.SpellPoolMode}");
        Console.WriteLine($"AoW progression          : {ashOfWarConfig.ProgressionBand}");
        Console.WriteLine($"AoW include shops        : {ashOfWarConfig.IncludeShopRows}");
        Console.WriteLine($"AoW include lot map      : {ashOfWarConfig.IncludeItemLotMapRows}");
        Console.WriteLine($"AoW include lot enemy    : {ashOfWarConfig.IncludeItemLotEnemyRows}");
        Console.WriteLine($"AoW exclude Enia shop    : {ashOfWarConfig.ExcludeEniaRemembranceShop}");

        string ashOfWarMappingPath = Path.Combine(logsFolder, "last_ash_of_war_magic_mapping.json");

        var ashOfWarEditor = new AshOfWarToMagicEditor(defsFolder, itemslotsPath, verbose: true);
        AshOfWarToMagicRunResult ashOfWarRunResult = ashOfWarEditor.ApplyToRegulation(
            inputRegulationPath: outputRegulation,
            outputRegulationPath: outputRegulation,
            config: ashOfWarConfig,
            seed: run.Seed,
            mappingOutputPath: ashOfWarMappingPath);

        Console.WriteLine($"AoW spell pool          : {ashOfWarRunResult.SpellPoolCount}");
        Console.WriteLine($"AoW replacements        : {ashOfWarRunResult.Mappings.Count}");
        Console.WriteLine($"AoW mapping saved       : {ashOfWarMappingPath}");

        Console.WriteLine();
        Console.WriteLine("=== Finalisation : Catalysts / Requirements ===");

        var catalystNormalizer = new CatalystAndMagicNormalizer();
        CatalystAndMagicNormalizationResult normalizationResult = catalystNormalizer.ApplyToRegulation(
            inputRegulationPath: outputRegulation,
            outputRegulationPath: outputRegulation,
            defsFolder: defsFolder,
            removeStatRequirements: request.RemoveStandaloneStatRequirements);

        Console.WriteLine($"Catalyst parry params   : {normalizationResult.GuardPreparedCatalystCount}");
        Console.WriteLine($"Catalysts parry enabled : {normalizationResult.ParryEnabledCatalystCount}");
        Console.WriteLine($"Weapon req rows cleared : {normalizationResult.WeaponRequirementRowsCleared}");
        Console.WriteLine($"Magic req rows cleared  : {normalizationResult.MagicRequirementRowsCleared}");

        RunTextOutputs(
            request.GenerateTextOutputs,
            projectDir,
            outputFolder,
            defsFolder,
            outputRegulation,
            run,
            startingClassRunResult,
            weaponNamesPath,
            baseItemMsgbndOverridePath,
            dlc2ItemMsgbndOverridePath);

        Console.WriteLine();
        Console.WriteLine("=== Apercu mapping du run ===");

        foreach (ConversionMapping mapping in run.Mappings.Take(25))
            Console.WriteLine($"   - {mapping}");

        if (run.Count > 25)
            Console.WriteLine($"   ... {run.Count - 25} mappings supplementaires non affiches.");

        Console.WriteLine();
        Console.WriteLine("Traitement termine.");
        Console.WriteLine($"Fichier final : {outputRegulation}");

        return new GenerationResult
        {
            Seed = run.Seed,
            OutputRegulationPath = outputRegulation,
            ShieldMappingPath = mappingPath,
            StartingClassMappingPath = startingClassMappingPath,
            ShopMappingPath = shopMappingPath,
            MapLootMappingPath = mapLootMappingPath,
            BossRewardMappingPath = bossRewardMappingPath,
            EnemyLotMappingPath = enemyLotMappingPath,
            FarmableEnemyLootSuppressionMappingPath = farmableEnemyLootMappingPath,
            AshOfWarMappingPath = ashOfWarMappingPath,
            ShieldMappingCount = run.Count,
            StartingClassReplacementCount = startingClassRunResult.RebuiltClassCount,
            ShopReplacementCount = shopRunResult.Mappings.Count,
            MapLootReplacementCount = mapLootRunResult.Mappings.Count,
            BossRewardReplacementCount = bossRewardRunResult.Mappings.Count,
            EnemyLotReplacementCount = enemyLotRunResult.Mappings.Count,
            FarmableEnemyLootSuppressionRowCount = farmableEnemyLootRunResult.SuppressedRowCount,
            AshOfWarReplacementCount = ashOfWarRunResult.Mappings.Count
        };
    }

    private static StartingClassWeaponToMagicConfig OverrideStartingClassConfig(
        StartingClassWeaponToMagicConfig config,
        bool isEnabled,
        string spellPoolModeOverride)
    {
        return new StartingClassWeaponToMagicConfig
        {
            Enabled = isEnabled && config.Enabled,
            SpellPoolMode = string.IsNullOrWhiteSpace(spellPoolModeOverride) ? config.SpellPoolMode : spellPoolModeOverride,
            ProgressionBand = config.ProgressionBand,
            RebuildStartingSpellLoadout = config.RebuildStartingSpellLoadout,
            InjectSupportCatalystWhenNeeded = config.InjectSupportCatalystWhenNeeded,
            EnsureSecondSupportCatalystWhenPossible = config.EnsureSecondSupportCatalystWhenPossible
        };
    }

    private static void RunTextOutputs(
        bool generateTextOutputs,
        string projectDir,
        string outputFolder,
        string defsFolder,
        string outputRegulation,
        ConversionRun run,
        StartingClassWeaponToMagicRunResult startingClassRunResult,
        string weaponNamesPath,
        string baseItemMsgbndOverridePath,
        string dlc2ItemMsgbndOverridePath)
    {
        Console.WriteLine();
        Console.WriteLine("=== Output Auxiliaire : Text Patches ===");

        if (!generateTextOutputs)
        {
            Console.WriteLine("Generation des patches texte desactivee.");
            return;
        }

        string textTemplatePath = ProjectLayout.ResolveWeaponTextExportPath(outputFolder);
        bool canGenerateShieldText = !string.IsNullOrWhiteSpace(textTemplatePath) && File.Exists(textTemplatePath);

        List<(string Locale, string[] SourcePaths)> localizedMsgbndSets = ProjectLayout.ResolveAvailableLocalizedItemMsgbndSets(
            projectDir,
            baseItemMsgbndOverridePath,
            dlc2ItemMsgbndOverridePath);
        Dictionary<string, string[]> localizedMenuMsgbndLookup = ProjectLayout.ResolveAvailableLocalizedMenuMsgbndSets(projectDir)
            .ToDictionary(entry => entry.Locale, entry => entry.SourcePaths, StringComparer.OrdinalIgnoreCase);
        var shieldTextPatchGenerator = new ShieldTextPatchGenerator();
        var startingClassOriginTextPatchGenerator = new StartingClassOriginTextPatchGenerator();

        Console.WriteLine($"Weapon text template    : {(canGenerateShieldText ? textTemplatePath : "<introuvable>")}");
        Console.WriteLine($"Weapon names path       : {weaponNamesPath}");
        Console.WriteLine($"Item locales detectees  : {localizedMsgbndSets.Count}");
        Console.WriteLine($"Menu locales detectees  : {localizedMenuMsgbndLookup.Count}");

        Console.WriteLine();
        Console.WriteLine("=== Output Auxiliaire : MsgBnd Text (toutes langues) ===");

        if (localizedMsgbndSets.Count == 0)
        {
            Console.WriteLine("Aucun item.msgbnd.dcx localise fiable n'a ete trouve. Ecriture directe sautee.");
            return;
        }

        int localesGenerated = 0;
        int totalContainers = 0;
        int totalUpdatedFiles = 0;
        int totalUpdatedEntries = 0;
        int totalMissingEntries = 0;

        foreach ((string locale, string[] sourceMsgbndPaths) in localizedMsgbndSets)
        {
            localizedMenuMsgbndLookup.TryGetValue(locale, out string[] menuMsgbndPaths);
            string msgOutputFolder = Path.Combine(outputFolder, "msg", locale);
            bool localeGenerated = false;
            int localeUpdatedFiles = 0;
            int localeUpdatedEntries = 0;
            int localeMissingEntries = 0;
            string[] originItemNamePaths = sourceMsgbndPaths;

            Console.WriteLine($"Locale                  : {locale}");

            if (canGenerateShieldText)
            {
                string shieldTextPatchPath = ProjectLayout.ResolveShieldTextPatchOutputPath(outputFolder, locale);
                ShieldTextPatchResult shieldTextPatchResult = shieldTextPatchGenerator.Generate(
                    run,
                    textTemplatePath,
                    shieldTextPatchPath,
                    weaponNamesPath,
                    sourceMsgbndPaths,
                    outputRegulation,
                    defsFolder);

                Console.WriteLine($"Shield patch generated  : {shieldTextPatchResult.Generated}");

                if (shieldTextPatchResult.Generated)
                {
                    localeGenerated = true;
                    Console.WriteLine($"Shield patch path       : {shieldTextPatchResult.OutputPath}");
                    Console.WriteLine($"WeaponName entries      : {shieldTextPatchResult.WeaponNameEntryCount}");
                    Console.WriteLine($"WeaponSummary entries   : {shieldTextPatchResult.WeaponSummaryEntryCount}");
                    Console.WriteLine($"WeaponDescription entry : {shieldTextPatchResult.WeaponDescriptionEntryCount}");
                    Console.WriteLine($"Fallback names used     : {shieldTextPatchResult.FallbackNameCount}");

                    try
                    {
                        var msgbndTextWriter = new ItemMsgbndTextWriter();

                        foreach (string sourceMsgbndPath in sourceMsgbndPaths)
                        {
                            string outputMsgbndPath = Path.Combine(msgOutputFolder, Path.GetFileName(sourceMsgbndPath));
                            ItemMsgbndTextWriteResult msgbndWriteResult = msgbndTextWriter.ApplyPatch(
                                shieldTextPatchResult.OutputPath,
                                sourceMsgbndPath,
                                null,
                                outputMsgbndPath,
                                null);

                            totalContainers++;
                            totalUpdatedFiles += msgbndWriteResult.UpdatedBaseFileCount;
                            totalUpdatedEntries += msgbndWriteResult.UpdatedTextEntryCount;
                            totalMissingEntries += msgbndWriteResult.MissingTextEntryCount;
                            localeUpdatedFiles += msgbndWriteResult.UpdatedBaseFileCount;
                            localeUpdatedEntries += msgbndWriteResult.UpdatedTextEntryCount;
                            localeMissingEntries += msgbndWriteResult.MissingTextEntryCount;

                            Console.WriteLine($"Shield msg source       : {msgbndWriteResult.BaseSourcePath}");
                            Console.WriteLine($"Shield msg output       : {msgbndWriteResult.BaseOutputPath}");
                            Console.WriteLine($"Shield files updated    : {msgbndWriteResult.UpdatedBaseFileCount}");
                            Console.WriteLine($"Shield entries updated  : {msgbndWriteResult.UpdatedTextEntryCount}");
                            Console.WriteLine($"Shield entries missing  : {msgbndWriteResult.MissingTextEntryCount}");
                        }

                        originItemNamePaths = sourceMsgbndPaths
                            .Select(sourceMsgbndPath =>
                            {
                                string outputMsgbndPath = Path.Combine(msgOutputFolder, Path.GetFileName(sourceMsgbndPath));
                                return File.Exists(outputMsgbndPath) ? outputMsgbndPath : sourceMsgbndPath;
                            })
                            .ToArray();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ecriture shield sautee  : {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Patch shield saute pour cette locale.");
                }
            }
            else
            {
                Console.WriteLine("Patch shield saute : aucun export texte d'armes disponible.");
            }

            bool canGenerateStartingClassOriginText =
                startingClassRunResult != null &&
                startingClassRunResult.RebuiltClassCount > 0 &&
                menuMsgbndPaths != null &&
                menuMsgbndPaths.Length > 0;

            if (canGenerateStartingClassOriginText)
            {
                string startingClassOriginPatchPath = ProjectLayout.ResolveStartingClassOriginTextPatchOutputPath(outputFolder, locale);
                StartingClassOriginTextPatchResult startingClassOriginPatchResult = startingClassOriginTextPatchGenerator.Generate(
                    projectDir,
                    defsFolder,
                    outputRegulation,
                    run,
                    originItemNamePaths,
                    startingClassOriginPatchPath);

                Console.WriteLine($"Origin patch generated  : {startingClassOriginPatchResult.Generated}");

                if (startingClassOriginPatchResult.Generated)
                {
                    localeGenerated = true;
                    Console.WriteLine($"Origin patch path       : {startingClassOriginPatchResult.OutputPath}");
                    Console.WriteLine($"Origin entries          : {startingClassOriginPatchResult.EntryCount}");

                    try
                    {
                        var msgbndTextWriter = new ItemMsgbndTextWriter();
                        foreach (string menuMsgbndPath in menuMsgbndPaths)
                        {
                            string outputMenuMsgbndPath = Path.Combine(msgOutputFolder, Path.GetFileName(menuMsgbndPath));
                            ItemMsgbndTextWriteResult menuWriteResult = msgbndTextWriter.ApplyPatch(
                                startingClassOriginPatchResult.OutputPath,
                                menuMsgbndPath,
                                null,
                                outputMenuMsgbndPath,
                                null);

                            totalContainers++;
                            totalUpdatedFiles += menuWriteResult.UpdatedBaseFileCount;
                            totalUpdatedEntries += menuWriteResult.UpdatedTextEntryCount;
                            totalMissingEntries += menuWriteResult.MissingTextEntryCount;
                            localeUpdatedFiles += menuWriteResult.UpdatedBaseFileCount;
                            localeUpdatedEntries += menuWriteResult.UpdatedTextEntryCount;
                            localeMissingEntries += menuWriteResult.MissingTextEntryCount;

                            Console.WriteLine($"Origin msg source       : {menuWriteResult.BaseSourcePath}");
                            Console.WriteLine($"Origin msg output       : {menuWriteResult.BaseOutputPath}");
                            Console.WriteLine($"Origin files updated    : {menuWriteResult.UpdatedBaseFileCount}");
                            Console.WriteLine($"Origin entries updated  : {menuWriteResult.UpdatedTextEntryCount}");
                            Console.WriteLine($"Origin entries missing  : {menuWriteResult.MissingTextEntryCount}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ecriture origin sautee  : {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Patch origin saute pour cette locale.");
                }
            }
            else
            {
                Console.WriteLine("Patch origin saute : menu.msgbnd ou pipeline de classes indisponible.");
            }

            if (localeGenerated)
                localesGenerated++;

            Console.WriteLine($"Locale files updated    : {localeUpdatedFiles}");
            Console.WriteLine($"Locale entries updated  : {localeUpdatedEntries}");
            Console.WriteLine($"Locale entries missing  : {localeMissingEntries}");

            if (localeMissingEntries > 0)
                Console.WriteLine("Certaines entrees n'existent pas dans un ou plusieurs conteneurs de cette locale, ce qui est normal.");

            Console.WriteLine();
        }

        Console.WriteLine($"Locales generees        : {localesGenerated}");
        Console.WriteLine($"Msgbnd containers total : {totalContainers}");
        Console.WriteLine($"Files updated total     : {totalUpdatedFiles}");
        Console.WriteLine($"Entries updated total   : {totalUpdatedEntries}");
        Console.WriteLine($"Entries missing total   : {totalMissingEntries}");

        if (totalMissingEntries > 0)
            Console.WriteLine("Certaines entrees n'existent pas dans un ou plusieurs conteneurs DLC/base, ce qui est normal.");
    }

    private static ShopWeaponToMagicConfig OverrideShopConfig(
        ShopWeaponToMagicConfig config,
        bool isEnabled,
        string spellPoolModeOverride)
    {
        return new ShopWeaponToMagicConfig
        {
            Enabled = isEnabled && config.Enabled,
            ReplaceAllEligibleWeapons = config.ReplaceAllEligibleWeapons,
            SpellPoolMode = string.IsNullOrWhiteSpace(spellPoolModeOverride) ? config.SpellPoolMode : spellPoolModeOverride,
            ProgressionBand = config.ProgressionBand,
            Entries = config.Entries.ToList()
        };
    }

    private static MapLootWeaponToMagicConfig OverrideMapLootConfig(
        MapLootWeaponToMagicConfig config,
        bool isEnabled,
        string spellPoolModeOverride)
    {
        return new MapLootWeaponToMagicConfig
        {
            Enabled = isEnabled && config.Enabled,
            ReplaceAllEligibleWeapons = config.ReplaceAllEligibleWeapons,
            SpellPoolMode = string.IsNullOrWhiteSpace(spellPoolModeOverride) ? config.SpellPoolMode : spellPoolModeOverride,
            ProgressionBand = config.ProgressionBand,
            Entries = config.Entries.ToList()
        };
    }

    private static BossRewardWeaponToMagicConfig OverrideBossRewardConfig(
        BossRewardWeaponToMagicConfig config,
        bool isEnabled,
        string spellPoolModeOverride)
    {
        return new BossRewardWeaponToMagicConfig
        {
            Enabled = isEnabled && config.Enabled,
            ReplaceAllEligibleWeapons = config.ReplaceAllEligibleWeapons,
            SpellPoolMode = string.IsNullOrWhiteSpace(spellPoolModeOverride) ? config.SpellPoolMode : spellPoolModeOverride,
            ProgressionBand = config.ProgressionBand,
            Entries = config.Entries.ToList()
        };
    }

    private static EnemyLotWeaponToMagicConfig OverrideEnemyLotConfig(
        EnemyLotWeaponToMagicConfig config,
        bool isEnabled,
        string spellPoolModeOverride)
    {
        return new EnemyLotWeaponToMagicConfig
        {
            Enabled = isEnabled && config.Enabled,
            ReplaceAllEligibleWeapons = config.ReplaceAllEligibleWeapons,
            SpellPoolMode = string.IsNullOrWhiteSpace(spellPoolModeOverride) ? config.SpellPoolMode : spellPoolModeOverride,
            ProgressionBand = config.ProgressionBand,
            Entries = config.Entries.ToList()
        };
    }

    private static FarmableEnemyLootSuppressionConfig OverrideFarmableEnemyLootSuppressionConfig(
        FarmableEnemyLootSuppressionConfig config,
        bool isEnabled)
    {
        return new FarmableEnemyLootSuppressionConfig
        {
            Enabled = isEnabled && config.Enabled,
            MinimumSourceOccurrences = config.MinimumSourceOccurrences,
            RequireZeroGetItemFlag = config.RequireZeroGetItemFlag
        };
    }

    private static AshOfWarToMagicConfig OverrideAshOfWarConfig(
        AshOfWarToMagicConfig config,
        bool isEnabled,
        string spellPoolModeOverride)
    {
        return new AshOfWarToMagicConfig
        {
            Enabled = isEnabled && config.Enabled,
            SpellPoolMode = string.IsNullOrWhiteSpace(spellPoolModeOverride) ? config.SpellPoolMode : spellPoolModeOverride,
            ProgressionBand = config.ProgressionBand,
            IncludeShopRows = config.IncludeShopRows,
            IncludeItemLotMapRows = config.IncludeItemLotMapRows,
            IncludeItemLotEnemyRows = config.IncludeItemLotEnemyRows,
            ExcludeEniaRemembranceShop = config.ExcludeEniaRemembranceShop
        };
    }

    private static void EnsureOodleDllsAvailable()
    {
        string baseDir = AppContext.BaseDirectory;
        string libDir = Path.Combine(baseDir, "lib");

        if (!Directory.Exists(libDir))
            return;

        foreach (string fileName in new[] { "oo2core_6_win64.dll", "oo2core_9_win64.dll", "libzstd.dll" })
        {
            string sourcePath = Path.Combine(libDir, fileName);
            string destPath = Path.Combine(baseDir, fileName);

            if (!File.Exists(sourcePath) || File.Exists(destPath))
                continue;

            File.Copy(sourcePath, destPath, overwrite: false);
        }
    }
}
