using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class StartingClassWeaponToMagicEditor
{
    private const int SorcerySupportCatalystId = 33130000; // Astrologer's Staff
    private const int IncantationSupportCatalystId = 34000000; // Finger Seal

    private static readonly string[] WeaponSlotNames =
    {
        "equip_Wep_Right",
        "equip_Subwep_Right",
        "equip_Wep_Left",
        "equip_Subwep_Left"
    };

    private static readonly StartingCatalystSlotDescriptor[] CatalystSlotCycle =
    {
        new("equip_Wep_Right", StartingCatalystKind.Staff),
        new("equip_Subwep_Right", StartingCatalystKind.Seal),
        new("equip_Wep_Left", StartingCatalystKind.Staff),
        new("equip_Subwep_Left", StartingCatalystKind.Seal)
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

    private static readonly string[] AmmoSlotNames =
    {
        "equip_Arrow",
        "equip_Bolt",
        "equip_SubArrow",
        "equip_SubBolt"
    };

    private static readonly string[] AmmoCountNames =
    {
        "arrowNum",
        "boltNum",
        "subArrowNum",
        "subBoltNum"
    };

    private static readonly StartingClassDescriptor[] StartingClasses =
    {
        new(3000, "Vagabond", 0),
        new(3001, "Warrior", 1),
        new(3002, "Hero", 2),
        new(3003, "Bandit", 3),
        new(3004, "Astrologer", 4),
        new(3005, "Prophet", 5),
        new(3006, "Confessor", 6),
        new(3007, "Samurai", 7),
        new(3008, "Prisoner", 8),
        new(3009, "Wretch", 9)
    };

    private readonly List<PARAMDEF> _paramdefs;
    private readonly string _charaInitParamdefPath;
    private readonly string _magicParamdefPath;
    private readonly bool _verbose;

    public StartingClassWeaponToMagicEditor(string defsDirectory, string projectDir, bool verbose = false)
    {
        if (string.IsNullOrWhiteSpace(defsDirectory))
            throw new ArgumentException("Le chemin du dossier Defs est vide.", nameof(defsDirectory));

        if (string.IsNullOrWhiteSpace(projectDir))
            throw new ArgumentException("Le chemin du projet est vide.", nameof(projectDir));

        if (!Directory.Exists(defsDirectory))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsDirectory}");

        _paramdefs = LoadParamdefs(defsDirectory);
        _charaInitParamdefPath = LayoutParamdefGenerator.EnsureGeneratedParamdef(
            defsDirectory,
            "CharaInitParam.xml",
            ProjectLayout.ResolveSoulsRandomizerLayoutPath(projectDir, "CHARACTER_INIT_PARAM.xml"),
            "CHARACTER_INIT_PARAM");
        _magicParamdefPath = LayoutParamdefGenerator.EnsureGeneratedParamdef(
            defsDirectory,
            "Magic.xml",
            ProjectLayout.ResolveSoulsRandomizerLayoutPath(projectDir, "MAGIC_PARAM_ST.xml"),
            "MAGIC_PARAM_ST");
        _verbose = verbose;

        if (_verbose)
        {
            Console.WriteLine($"Starting class CharaInitParam def : {_charaInitParamdefPath}");
            Console.WriteLine($"Starting class Magic def          : {_magicParamdefPath}");
        }
    }

    public StartingClassWeaponToMagicRunResult ApplyToRegulation(
        string inputRegulationPath,
        string outputRegulationPath,
        StartingClassWeaponToMagicConfig config,
        int seed,
        string mappingOutputPath = null)
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
        Console.WriteLine("Starting Class Weapon -> Magic Editor");
        Console.WriteLine("==================================================");
        Console.WriteLine($"Input regulation  : {inputRegulationPath}");
        Console.WriteLine($"Output regulation : {outputRegulationPath}");
        Console.WriteLine($"Seed              : {seed}");
        Console.WriteLine($"Spell pool mode   : {poolMode}");
        Console.WriteLine($"Progression band  : {progressionBand}");
        Console.WriteLine("Compatibilite     : fonctionne si le Randomizer n'ecrase pas les loadouts de depart (option nostarting).");
        Console.WriteLine("Pool de sorts     : pool starter-safe derive des classes vanilla.");

        if (!config.Enabled)
        {
            Console.WriteLine("StartingClassWeaponToMagicEditor desactive.");

            if (!string.Equals(inputRegulationPath, outputRegulationPath, StringComparison.OrdinalIgnoreCase))
                File.Copy(inputRegulationPath, outputRegulationPath, overwrite: true);

            return new StartingClassWeaponToMagicRunResult
            {
                Seed = seed,
                SpellPoolMode = poolMode.ToString(),
                ProgressionBand = progressionBand.ToString()
            };
        }

        BND4 bnd = LoadRegulation(inputRegulationPath);
        PARAM equipWeaponParam = ReadParam(bnd, "EquipParamWeapon");
        PARAM charaInitParam = ReadParamFromXml(bnd, "CharaInitParam", _charaInitParamdefPath);

        Dictionary<int, StartingMagicInfo> magicInfos = BuildStarterSafeMagicInfoLookup(charaInitParam, poolMode);
        if (magicInfos.Count == 0)
            throw new InvalidOperationException("Aucun sort starter-safe disponible pour les classes de depart.");

        List<StartingMagicInfo> sorceryPool = magicInfos.Values
            .Where(info => info.Category == StartingMagicCategory.Sorcery)
            .OrderBy(info => info.GoodsId)
            .ToList();
        List<StartingMagicInfo> incantationPool = magicInfos.Values
            .Where(info => info.Category == StartingMagicCategory.Incantation)
            .OrderBy(info => info.GoodsId)
            .ToList();

        Console.WriteLine($"Spell pool size   : {magicInfos.Count}");
        Console.WriteLine($"Sorcery count     : {sorceryPool.Count}");
        Console.WriteLine($"Incant count      : {incantationPool.Count}");

        if (_verbose)
        {
            Console.WriteLine($"Spell sample      : {string.Join(", ", magicInfos.Keys.Take(20))}{(magicInfos.Count > 20 ? " ..." : string.Empty)}");
            foreach (StartingMagicInfo info in magicInfos.Values.OrderBy(info => info.GoodsId).Take(12))
            {
                Console.WriteLine(
                    $"   magic {info.GoodsId} | cat={info.Category} | slots={info.SlotLength} | int={info.RequirementInt} | fai={info.RequirementFai} | arc={info.RequirementArc}");
            }
        }

        var result = new StartingClassWeaponToMagicRunResult
        {
            Seed = seed,
            SpellPoolMode = poolMode.ToString(),
            ProgressionBand = progressionBand.ToString(),
            SpellPoolCount = magicInfos.Count,
            SorceryPoolCount = sorceryPool.Count,
            IncantationPoolCount = incantationPool.Count
        };

        foreach (StartingClassDescriptor descriptor in StartingClasses)
        {
            PARAM.Row row = charaInitParam[descriptor.RowId];
            if (row == null)
            {
                Console.WriteLine($"Classe introuvable dans CharaInitParam : {descriptor.RowId} ({descriptor.ClassName})");
                continue;
            }

            StartingClassWeaponToMagicRunMapping classMapping = ApplyClassRow(
                descriptor,
                row,
                charaInitParam,
                equipWeaponParam,
                sorceryPool,
                incantationPool,
                config,
                seed);

            if (classMapping == null)
                continue;

            result.Classes.Add(classMapping);
        }

        BinderFile charaInitFile = FindParamFile(bnd, "CharaInitParam");
        charaInitFile.Bytes = charaInitParam.Write();
        WriteRegulation(outputRegulationPath, bnd);

        StartingClassWeaponToMagicRunResult finalResult = new()
        {
            Seed = result.Seed,
            SpellPoolMode = result.SpellPoolMode,
            ProgressionBand = result.ProgressionBand,
            SpellPoolCount = result.SpellPoolCount,
            SorceryPoolCount = result.SorceryPoolCount,
            IncantationPoolCount = result.IncantationPoolCount,
            RebuiltClassCount = result.Classes.Count,
            Classes = result.Classes
        };

        if (!string.IsNullOrWhiteSpace(mappingOutputPath))
        {
            SaveRunMapping(mappingOutputPath, finalResult);
            Console.WriteLine($"Starting class mapping saved : {mappingOutputPath}");
        }

        Console.WriteLine($"Starting classes rebuilt     : {finalResult.RebuiltClassCount}");
        return finalResult;
    }

    private StartingClassWeaponToMagicRunMapping ApplyClassRow(
        StartingClassDescriptor descriptor,
        PARAM.Row row,
        PARAM charaInitParam,
        PARAM equipWeaponParam,
        IReadOnlyList<StartingMagicInfo> sorceryPool,
        IReadOnlyList<StartingMagicInfo> incantationPool,
        StartingClassWeaponToMagicConfig config,
        int seed)
    {
        List<StartingWeaponSlotState> weaponSlots = GetWeaponSlots(row, equipWeaponParam);
        List<StartingWeaponSlotState> convertedWeaponSlots = weaponSlots
            .Where(slot => slot.IsConvertibleWeapon && !slot.IsCatalyst)
            .ToList();

        if (convertedWeaponSlots.Count == 0)
        {
            Console.WriteLine($"[{descriptor.ClassName}] aucune arme de depart convertible a remplacer.");
            return null;
        }

        var baseStats = new StartingClassStats(
            Mag: ReadIntOrDefault(row, "baseMag"),
            Fai: ReadIntOrDefault(row, "baseFai"),
            Luc: ReadIntOrDefault(row, "baseLuc"));
        int oldSoulLevel = ReadIntOrDefault(row, "soulLvl");
        List<int> existingSpellIds = SpellSlotNames
            .Select(slot => ReadIntOrDefault(row, slot))
            .Where(id => id > 0)
            .ToList();

        int desiredSpellCount = Math.Min(
            2,
            convertedWeaponSlots.Count + (config.RebuildStartingSpellLoadout ? existingSpellIds.Count : 0));
        if (desiredSpellCount <= 0)
            desiredSpellCount = Math.Min(2, convertedWeaponSlots.Count);

        List<StartingWeaponSlotState> remainingWeaponSlots = weaponSlots
            .Where(slot => convertedWeaponSlots.All(converted => !string.Equals(converted.SlotName, slot.SlotName, StringComparison.Ordinal)))
            .ToList();

        StartingMagicCategory school = DeterminePreferredSchool(existingSpellIds, remainingWeaponSlots, baseStats, sorceryPool, incantationPool);
        bool canCastSorcery = remainingWeaponSlots.Any(slot => slot.SupportsSorcery);
        bool canCastIncantation = remainingWeaponSlots.Any(slot => slot.SupportsIncantation);

        if (school == StartingMagicCategory.Sorcery && !canCastSorcery)
            school = canCastIncantation ? StartingMagicCategory.Incantation : school;
        else if (school == StartingMagicCategory.Incantation && !canCastIncantation)
            school = canCastSorcery ? StartingMagicCategory.Sorcery : school;

        if (!canCastSorcery && !canCastIncantation)
            school = ChooseFallbackSchool(baseStats, sorceryPool, incantationPool);

        IReadOnlyList<StartingMagicInfo> initialPool = school == StartingMagicCategory.Sorcery ? sorceryPool : incantationPool;
        List<StartingMagicInfo> selectedSpells = SelectSpells(initialPool, baseStats, desiredSpellCount, seed, descriptor.RowId);

        if (selectedSpells.Count == 0)
        {
            StartingMagicCategory fallbackSchool = school == StartingMagicCategory.Sorcery
                ? StartingMagicCategory.Incantation
                : StartingMagicCategory.Sorcery;
            IReadOnlyList<StartingMagicInfo> fallbackPool = fallbackSchool == StartingMagicCategory.Sorcery ? sorceryPool : incantationPool;
            List<StartingMagicInfo> fallbackSpells = SelectSpells(fallbackPool, baseStats, desiredSpellCount, seed, descriptor.RowId + 7000);

            if (fallbackSpells.Count > 0)
            {
                school = fallbackSchool;
                selectedSpells = fallbackSpells;
            }
        }

        if (selectedSpells.Count == 0)
        {
            if (_verbose)
            {
                Console.WriteLine(
                    $"[{descriptor.ClassName}] echec selection | converted={convertedWeaponSlots.Count} | " +
                    $"remaining={remainingWeaponSlots.Count} | school={school} | " +
                    $"baseStats=INT:{baseStats.Mag}/FAI:{baseStats.Fai}/ARC:{baseStats.Luc} | desired={desiredSpellCount}");
                Console.WriteLine($"[{descriptor.ClassName}] sorcery pool ids: {string.Join(", ", sorceryPool.Take(10).Select(info => info.GoodsId))}");
                Console.WriteLine($"[{descriptor.ClassName}] incant pool ids: {string.Join(", ", incantationPool.Take(10).Select(info => info.GoodsId))}");
            }
            Console.WriteLine($"[{descriptor.ClassName}] aucun sort valable trouve pour reconstruire le loadout.");
            return null;
        }

        StartingClassStats adjustedStats = baseStats.AdjustFor(selectedSpells);
        List<StartingCatalystSlotAssignment> catalystAssignments = BuildCatalystSlotCycleAssignments(weaponSlots);
        ApplyCatalystSlotCycle(row, catalystAssignments);

        if (config.RebuildStartingSpellLoadout)
        {
            foreach (string spellSlot in SpellSlotNames)
                SetIntField(row, spellSlot, -1);
        }

        for (int index = 0; index < selectedSpells.Count && index < SpellSlotNames.Length; index++)
            SetIntField(row, SpellSlotNames[index], selectedSpells[index].GoodsId);

        ApplyStatAdjustments(row, baseStats, adjustedStats, oldSoulLevel);

        if (!HasRemainingBow(row, equipWeaponParam))
            ClearAmmo(row);

        CopyToPreviewRows(descriptor, row, charaInitParam);

        Console.WriteLine(
            $"[{descriptor.ClassName}] {convertedWeaponSlots.Count} armes -> {selectedSpells.Count} sorts | " +
            $"ecole={school} | catalysts={FormatCatalystAssignments(catalystAssignments)}");

        StartingCatalystSlotAssignment firstCatalystAssignment = catalystAssignments.FirstOrDefault();

        return new StartingClassWeaponToMagicRunMapping
        {
            RowId = descriptor.RowId,
            ClassName = descriptor.ClassName,
            School = school.ToString(),
            ClearedWeaponSlots = convertedWeaponSlots.Select(slot => slot.SlotName).ToList(),
            ClearedWeaponIds = convertedWeaponSlots.Select(slot => slot.WeaponId).ToList(),
            AssignedSpellIds = selectedSpells.Select(spell => spell.GoodsId).ToList(),
            InjectedCatalystSlot = firstCatalystAssignment?.SlotName ?? string.Empty,
            InjectedCatalystId = firstCatalystAssignment?.WeaponId ?? 0,
            InjectedCatalystCount = catalystAssignments.Count,
            InjectedCatalystSlots = catalystAssignments.Select(assignment => assignment.SlotName).ToList(),
            InjectedCatalystIds = catalystAssignments.Select(assignment => assignment.WeaponId).ToList(),
            OldBaseMag = baseStats.Mag,
            NewBaseMag = adjustedStats.Mag,
            OldBaseFai = baseStats.Fai,
            NewBaseFai = adjustedStats.Fai,
            OldBaseLuc = baseStats.Luc,
            NewBaseLuc = adjustedStats.Luc,
            OldSoulLevel = oldSoulLevel,
            NewSoulLevel = oldSoulLevel + adjustedStats.LevelDelta(baseStats)
        };
    }

    private static List<StartingWeaponSlotState> GetWeaponSlots(PARAM.Row row, PARAM equipWeaponParam)
    {
        var result = new List<StartingWeaponSlotState>();

        foreach (string slotName in WeaponSlotNames)
        {
            int weaponId = ReadIntOrDefault(row, slotName);
            if (weaponId <= 0)
                continue;

            PARAM.Row weaponRow = equipWeaponParam[weaponId];
            if (weaponRow == null)
                continue;

            int wepType = ReadIntOrDefault(weaponRow, "wepType");
            int weaponCategory = ReadIntOrDefault(weaponRow, "weaponCategory");
            bool supportsSorcery = ReadIntOrDefault(weaponRow, "enableSorcery") > 0 || ReadIntOrDefault(weaponRow, "enableMagic") > 0;
            bool supportsIncantation = ReadIntOrDefault(weaponRow, "enableMiracle") > 0;
            bool isCatalyst = ReadIntOrDefault(weaponRow, "isWeaponCatalyst") > 0 || supportsSorcery || supportsIncantation;
            bool isShield = IsShieldType(wepType);
            bool isAmmo = weaponId >= 50_000_000 && weaponId < 60_000_000;
            bool isBow = weaponCategory == 10 || weaponCategory == 11 || ReadIntOrDefault(weaponRow, "arrowSlotEquipable") > 0 || ReadIntOrDefault(weaponRow, "boltSlotEquipable") > 0;

            result.Add(new StartingWeaponSlotState(
                SlotName: slotName,
                WeaponId: weaponId,
                IsConvertibleWeapon: weaponId > 0 && !isShield && !isAmmo,
                IsCatalyst: isCatalyst,
                SupportsSorcery: supportsSorcery,
                SupportsIncantation: supportsIncantation,
                IsBow: isBow));
        }

        return result;
    }

    private static StartingMagicCategory DeterminePreferredSchool(
        IReadOnlyList<int> existingSpellIds,
        IReadOnlyList<StartingWeaponSlotState> remainingWeaponSlots,
        StartingClassStats stats,
        IReadOnlyList<StartingMagicInfo> sorceryPool,
        IReadOnlyList<StartingMagicInfo> incantationPool)
    {
        foreach (int spellId in existingSpellIds)
        {
            ShopMagicCategory category = ShopMagicPoolClassifier.Classify(spellId);
            if (category == ShopMagicCategory.Sorcery)
                return StartingMagicCategory.Sorcery;

            if (category == ShopMagicCategory.Incantation)
                return StartingMagicCategory.Incantation;
        }

        bool explicitSorceryCatalyst = remainingWeaponSlots.Any(slot => slot.IsCatalyst && slot.SupportsSorcery && !slot.SupportsIncantation);
        bool explicitIncantationCatalyst = remainingWeaponSlots.Any(slot => slot.IsCatalyst && slot.SupportsIncantation && !slot.SupportsSorcery);

        if (explicitSorceryCatalyst && !explicitIncantationCatalyst)
            return StartingMagicCategory.Sorcery;

        if (explicitIncantationCatalyst && !explicitSorceryCatalyst)
            return StartingMagicCategory.Incantation;

        bool canCastSorcery = remainingWeaponSlots.Any(slot => slot.SupportsSorcery);
        bool canCastIncantation = remainingWeaponSlots.Any(slot => slot.SupportsIncantation);

        if (canCastSorcery && !canCastIncantation)
            return StartingMagicCategory.Sorcery;

        if (canCastIncantation && !canCastSorcery)
            return StartingMagicCategory.Incantation;

        if (stats.Mag > stats.Fai)
            return StartingMagicCategory.Sorcery;

        if (stats.Fai > stats.Mag)
            return StartingMagicCategory.Incantation;

        return ChooseFallbackSchool(stats, sorceryPool, incantationPool);
    }

    private static StartingMagicCategory ChooseFallbackSchool(
        StartingClassStats stats,
        IReadOnlyList<StartingMagicInfo> sorceryPool,
        IReadOnlyList<StartingMagicInfo> incantationPool)
    {
        int sorceryDelta = GetBestCandidateDelta(stats, sorceryPool);
        int incantationDelta = GetBestCandidateDelta(stats, incantationPool);

        return sorceryDelta <= incantationDelta
            ? StartingMagicCategory.Sorcery
            : StartingMagicCategory.Incantation;
    }

    private static int GetBestCandidateDelta(StartingClassStats stats, IReadOnlyList<StartingMagicInfo> pool)
    {
        if (pool == null || pool.Count == 0)
            return int.MaxValue;

        return pool
            .Where(info => info.SlotLength > 0 && info.SlotLength <= 2)
            .Select(info => info.RequiredDelta(stats))
            .DefaultIfEmpty(int.MaxValue)
            .Min();
    }

    private static List<StartingMagicInfo> SelectSpells(
        IReadOnlyList<StartingMagicInfo> pool,
        StartingClassStats baseStats,
        int desiredSpellCount,
        int seed,
        int rowIdSalt)
    {
        if (pool == null || pool.Count == 0 || desiredSpellCount <= 0)
            return new List<StartingMagicInfo>();

        var random = new Random(MagicSelectionSeedMixer.Mix(seed, rowIdSalt));
        var selected = new List<StartingMagicInfo>();
        StartingClassStats currentStats = baseStats;
        int usedSlots = 0;

        while (selected.Count < desiredSpellCount)
        {
            List<StartingMagicInfo> candidates = pool
                .Where(info =>
                    selected.All(existing => existing.GoodsId != info.GoodsId) &&
                    info.SlotLength > 0 &&
                    usedSlots + info.SlotLength <= 2)
                .ToList();

            if (candidates.Count == 0)
                break;

            int minDelta = candidates.Min(info => info.RequiredDelta(currentStats));
            int tolerance = minDelta <= 1 ? 1 : 2;

            List<StartingMagicInfo> shortlist = candidates
                .Where(info => info.RequiredDelta(currentStats) <= minDelta + tolerance)
                .OrderBy(info => info.RequiredDelta(currentStats))
                .ThenBy(info => info.GoodsId)
                .Take(8)
                .ToList();

            StartingMagicInfo chosen = shortlist[random.Next(shortlist.Count)];
            selected.Add(chosen);
            currentStats = currentStats.AdjustFor(new[] { chosen });
            usedSlots += chosen.SlotLength;
        }

        return selected;
    }

    private static void ApplyStatAdjustments(
        PARAM.Row row,
        StartingClassStats oldStats,
        StartingClassStats newStats,
        int oldSoulLevel)
    {
        if (newStats.Mag != oldStats.Mag)
            SetIntField(row, "baseMag", newStats.Mag);

        if (newStats.Fai != oldStats.Fai)
            SetIntField(row, "baseFai", newStats.Fai);

        if (newStats.Luc != oldStats.Luc)
            SetIntField(row, "baseLuc", newStats.Luc);

        int levelDelta = newStats.LevelDelta(oldStats);
        if (levelDelta > 0)
            SetIntField(row, "soulLvl", oldSoulLevel + levelDelta);
    }

    private static int CountSchoolCatalysts(
        IReadOnlyList<StartingWeaponSlotState> remainingWeaponSlots,
        StartingMagicCategory school)
    {
        return school == StartingMagicCategory.Sorcery
            ? remainingWeaponSlots.Count(slot => slot.SupportsSorcery)
            : remainingWeaponSlots.Count(slot => slot.SupportsIncantation);
    }

    private static List<string> FindPreferredInjectionSlots(
        IReadOnlyList<StartingWeaponSlotState> convertedWeaponSlots,
        PARAM.Row row,
        int desiredCount)
    {
        var result = new List<string>();

        if (desiredCount <= 0)
            return result;

        foreach (StartingWeaponSlotState slot in convertedWeaponSlots)
        {
            result.Add(slot.SlotName);
            if (result.Count >= desiredCount)
                return result;
        }

        foreach (string slotName in WeaponSlotNames)
        {
            if (ReadIntOrDefault(row, slotName) > 0)
                continue;

            if (result.Contains(slotName, StringComparer.Ordinal))
                continue;

            result.Add(slotName);

            if (result.Count >= desiredCount)
                return result;
        }

        return result;
    }

    private static List<StartingCatalystSlotAssignment> BuildCatalystSlotCycleAssignments(
        IReadOnlyList<StartingWeaponSlotState> weaponSlots)
    {
        List<int> staffIds = BuildCatalystIdCycle(
            weaponSlots,
            StartingCatalystKind.Staff,
            SorcerySupportCatalystId);
        List<int> sealIds = BuildCatalystIdCycle(
            weaponSlots,
            StartingCatalystKind.Seal,
            IncantationSupportCatalystId);

        int staffIndex = 0;
        int sealIndex = 0;
        var result = new List<StartingCatalystSlotAssignment>(CatalystSlotCycle.Length);

        foreach (StartingCatalystSlotDescriptor slot in CatalystSlotCycle)
        {
            int weaponId;
            if (slot.Kind == StartingCatalystKind.Staff)
            {
                weaponId = staffIds[staffIndex % staffIds.Count];
                staffIndex++;
            }
            else
            {
                weaponId = sealIds[sealIndex % sealIds.Count];
                sealIndex++;
            }

            result.Add(new StartingCatalystSlotAssignment(slot.SlotName, slot.Kind, weaponId));
        }

        return result;
    }

    private static List<int> BuildCatalystIdCycle(
        IReadOnlyList<StartingWeaponSlotState> weaponSlots,
        StartingCatalystKind kind,
        int fallbackCatalystId)
    {
        List<int> ids = weaponSlots
            .Where(slot => IsCatalystKind(slot, kind))
            .Select(slot => slot.WeaponId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            ids.Add(fallbackCatalystId);

        return ids;
    }

    private static bool IsCatalystKind(StartingWeaponSlotState slot, StartingCatalystKind kind)
    {
        return kind == StartingCatalystKind.Staff
            ? slot.SupportsSorcery
            : slot.SupportsIncantation;
    }

    private static void ApplyCatalystSlotCycle(
        PARAM.Row row,
        IReadOnlyList<StartingCatalystSlotAssignment> catalystAssignments)
    {
        foreach (StartingCatalystSlotAssignment assignment in catalystAssignments)
        {
            SetIntField(row, assignment.SlotName, assignment.WeaponId);
            SetRelatedGenIdToDefault(row, assignment.SlotName);
        }
    }

    private static string FormatCatalystAssignments(
        IReadOnlyList<StartingCatalystSlotAssignment> catalystAssignments)
    {
        return string.Join(
            ", ",
            catalystAssignments.Select(assignment => $"{assignment.SlotName}={assignment.WeaponId}"));
    }

    private static void CopyToPreviewRows(StartingClassDescriptor descriptor, PARAM.Row sourceRow, PARAM charaInitParam)
    {
        for (int offset = 0; offset < 2; offset++)
        {
            PARAM.Row previewRow = charaInitParam[3100 + descriptor.Index * 2 + offset];
            if (previewRow == null)
                continue;

            foreach (string fieldName in WeaponSlotNames
                         .Concat(SpellSlotNames)
                         .Concat(AmmoSlotNames)
                         .Concat(AmmoCountNames)
                         .Concat(new[] { "baseMag", "baseFai", "baseLuc", "soulLvl" }))
            {
                SetIntField(previewRow, fieldName, ReadIntOrDefault(sourceRow, fieldName));
            }
        }
    }

    private static bool HasRemainingBow(PARAM.Row row, PARAM equipWeaponParam)
    {
        foreach (string slotName in WeaponSlotNames)
        {
            int weaponId = ReadIntOrDefault(row, slotName);
            if (weaponId <= 0)
                continue;

            PARAM.Row weaponRow = equipWeaponParam[weaponId];
            if (weaponRow == null)
                continue;

            int weaponCategory = ReadIntOrDefault(weaponRow, "weaponCategory");
            if (weaponCategory == 10 || weaponCategory == 11)
                return true;

            if (ReadIntOrDefault(weaponRow, "arrowSlotEquipable") > 0 || ReadIntOrDefault(weaponRow, "boltSlotEquipable") > 0)
                return true;
        }

        return false;
    }

    private static void ClearAmmo(PARAM.Row row)
    {
        foreach (string slotName in AmmoSlotNames)
            SetIntField(row, slotName, -1);

        foreach (string countName in AmmoCountNames)
            SetIntField(row, countName, 0);
    }

    private static void TrimWeaponSlotsToMaximum(
        PARAM.Row row,
        PARAM equipWeaponParam,
        StartingMagicCategory school,
        int maxWeaponCount)
    {
        if (maxWeaponCount <= 0)
            return;

        List<StartingWeaponSlotState> currentSlots = GetWeaponSlots(row, equipWeaponParam);
        List<string> filledSlotNames = WeaponSlotNames
            .Where(slotName => ReadIntOrDefault(row, slotName) > 0)
            .ToList();

        if (filledSlotNames.Count <= maxWeaponCount)
            return;

        List<string> preferredSlotNames = currentSlots
            .OrderBy(slot => GetWeaponSlotPriority(slot, school))
            .ThenBy(slot => GetWeaponSlotOrder(slot.SlotName))
            .Select(slot => slot.SlotName)
            .ToList();

        HashSet<string> slotsToKeep = preferredSlotNames
            .Concat(filledSlotNames)
            .Distinct(StringComparer.Ordinal)
            .Take(maxWeaponCount)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string slotName in filledSlotNames)
        {
            if (slotsToKeep.Contains(slotName))
                continue;

            SetIntField(row, slotName, -1);
            SetRelatedGenIdToDefault(row, slotName);
        }
    }

    private static int GetWeaponSlotPriority(StartingWeaponSlotState slot, StartingMagicCategory school)
    {
        bool supportsPreferredSchool = school == StartingMagicCategory.Sorcery
            ? slot.SupportsSorcery
            : slot.SupportsIncantation;

        if (supportsPreferredSchool)
            return 0;

        if (slot.IsCatalyst)
            return 1;

        return 2;
    }

    private static int GetWeaponSlotOrder(string slotName)
    {
        for (int index = 0; index < WeaponSlotNames.Length; index++)
        {
            if (string.Equals(WeaponSlotNames[index], slotName, StringComparison.Ordinal))
                return index;
        }

        return int.MaxValue;
    }

    private static void SetRelatedGenIdToDefault(PARAM.Row row, string weaponSlotName)
    {
        string genIdField = weaponSlotName switch
        {
            "equip_Wep_Right" => "equip_Wep_Right_GenId",
            "equip_Subwep_Right" => "equip_Subwep_Right_GenId",
            "equip_Wep_Left" => "equip_Wep_Left_GenId",
            "equip_Subwep_Left" => "equip_Subwep_Left_GenId",
            _ => null
        };

        if (genIdField != null)
            SetIntField(row, genIdField, -1);
    }

    private static Dictionary<int, StartingMagicInfo> BuildStarterSafeMagicInfoLookup(PARAM charaInitParam, ShopMagicPoolMode poolMode)
    {
        var result = new Dictionary<int, StartingMagicInfo>();

        foreach (StartingClassDescriptor descriptor in StartingClasses)
        {
            PARAM.Row classRow = charaInitParam[descriptor.RowId];
            if (classRow == null)
                continue;

            int baseMag = ReadIntOrDefault(classRow, "baseMag");
            int baseFai = ReadIntOrDefault(classRow, "baseFai");
            int baseLuc = ReadIntOrDefault(classRow, "baseLuc");

            foreach (string spellSlot in SpellSlotNames)
            {
                int goodsId = ReadIntOrDefault(classRow, spellSlot);
                if (goodsId <= 0)
                    continue;

                ShopMagicCategory category = ShopMagicPoolClassifier.Classify(goodsId);
                if (!IsAllowedStartingMagicInMode(category, poolMode))
                    continue;

                StartingMagicCategory startingCategory = category == ShopMagicCategory.Sorcery
                    ? StartingMagicCategory.Sorcery
                    : StartingMagicCategory.Incantation;

                int requirementInt = startingCategory == StartingMagicCategory.Sorcery ? baseMag : 0;
                int requirementFai = startingCategory == StartingMagicCategory.Incantation ? baseFai : 0;
                int requirementArc = startingCategory == StartingMagicCategory.Incantation ? baseLuc : 0;

                if (result.TryGetValue(goodsId, out StartingMagicInfo existing))
                {
                    result[goodsId] = existing with
                    {
                        RequirementInt = requirementInt > 0 && existing.RequirementInt > 0
                            ? System.Math.Min(existing.RequirementInt, requirementInt)
                            : System.Math.Max(existing.RequirementInt, requirementInt),
                        RequirementFai = requirementFai > 0 && existing.RequirementFai > 0
                            ? System.Math.Min(existing.RequirementFai, requirementFai)
                            : System.Math.Max(existing.RequirementFai, requirementFai),
                        RequirementArc = requirementArc > 0 && existing.RequirementArc > 0
                            ? System.Math.Min(existing.RequirementArc, requirementArc)
                            : System.Math.Max(existing.RequirementArc, requirementArc)
                    };
                    continue;
                }

                result[goodsId] = new StartingMagicInfo(
                    GoodsId: goodsId,
                    Category: startingCategory,
                    SlotLength: 1,
                    RequirementInt: requirementInt,
                    RequirementFai: requirementFai,
                    RequirementArc: requirementArc);
            }
        }

        return result;
    }

    private static bool IsAllowedStartingMagicInMode(ShopMagicCategory category, ShopMagicPoolMode poolMode)
    {
        return poolMode switch
        {
            ShopMagicPoolMode.Both => category == ShopMagicCategory.Sorcery || category == ShopMagicCategory.Incantation,
            ShopMagicPoolMode.SorceryOnly => category == ShopMagicCategory.Sorcery,
            ShopMagicPoolMode.IncantationOnly => category == ShopMagicCategory.Incantation,
            _ => false
        };
    }

    private static bool IsShieldType(int wepType)
    {
        return wepType == 48
            || wepType == 65
            || wepType == 67
            || wepType == 69
            || wepType == 90;
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

    private PARAM ReadParam(BND4 bnd, string paramName)
    {
        BinderFile file = FindParamFile(bnd, paramName);
        PARAM param = PARAM.Read(file.Bytes);

        if (!param.ApplyParamdefCarefully(_paramdefs))
            throw new InvalidOperationException($"Paramdef non applique sur {paramName} (ParamType: {param.ParamType}).");

        return param;
    }

    private static PARAM ReadParamFromXml(BND4 bnd, string paramName, string xmlPath)
    {
        BinderFile file = FindParamFile(bnd, paramName);
        PARAM param = PARAM.Read(file.Bytes);

        try
        {
            param.ApplyParamdef(PARAMDEF.XmlDeserialize(xmlPath));
            return param;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Impossible d'appliquer la def XML sur {paramName}. ParamType brut = {param.ParamType}, def = {xmlPath}",
                ex);
        }
    }

    private static BinderFile FindParamFile(BND4 bnd, string paramName)
    {
        BinderFile file = bnd.Files.FirstOrDefault(candidate =>
            string.Equals(Path.GetFileNameWithoutExtension(candidate.Name), paramName, StringComparison.OrdinalIgnoreCase));

        return file ?? throw new InvalidOperationException($"Param introuvable dans le BND : {paramName}");
    }

    private static void SaveRunMapping(string outputPath, StartingClassWeaponToMagicRunResult result)
    {
        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        File.WriteAllText(outputPath, JsonSerializer.Serialize(result, options));
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
                // ignore invalid / unrelated XML files
            }
        }

        return defs;
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

    private sealed record StartingClassDescriptor(int RowId, string ClassName, int Index);

    private sealed record StartingCatalystSlotDescriptor(
        string SlotName,
        StartingCatalystKind Kind);

    private sealed record StartingCatalystSlotAssignment(
        string SlotName,
        StartingCatalystKind Kind,
        int WeaponId);

    private sealed record StartingWeaponSlotState(
        string SlotName,
        int WeaponId,
        bool IsConvertibleWeapon,
        bool IsCatalyst,
        bool SupportsSorcery,
        bool SupportsIncantation,
        bool IsBow);

    private sealed record StartingMagicInfo(
        int GoodsId,
        StartingMagicCategory Category,
        int SlotLength,
        int RequirementInt,
        int RequirementFai,
        int RequirementArc)
    {
        public int RequiredDelta(StartingClassStats stats)
        {
            return System.Math.Max(0, RequirementInt - stats.Mag)
                 + System.Math.Max(0, RequirementFai - stats.Fai)
                 + System.Math.Max(0, RequirementArc - stats.Luc);
        }
    }

    private readonly record struct StartingClassStats(int Mag, int Fai, int Luc)
    {
        public StartingClassStats AdjustFor(IEnumerable<StartingMagicInfo> spells)
        {
            int mag = Mag;
            int fai = Fai;
            int luc = Luc;

            foreach (StartingMagicInfo spell in spells)
            {
                if (spell.RequirementInt > mag)
                    mag = spell.RequirementInt;

                if (spell.RequirementFai > fai)
                    fai = spell.RequirementFai;

                if (spell.RequirementArc > luc)
                    luc = spell.RequirementArc;
            }

            return new StartingClassStats(mag, fai, luc);
        }

        public int LevelDelta(StartingClassStats previous)
        {
            return System.Math.Max(0, Mag - previous.Mag)
                 + System.Math.Max(0, Fai - previous.Fai)
                 + System.Math.Max(0, Luc - previous.Luc);
        }
    }

    private enum StartingMagicCategory
    {
        Sorcery,
        Incantation
    }

    private enum StartingCatalystKind
    {
        Staff,
        Seal
    }
}
