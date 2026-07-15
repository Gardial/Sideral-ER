using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class RegulationEditor
{
    private static readonly bool UseSingleDiffTest = false;
    private const bool VerboseFieldDump = true;
    private const int PreviewLimit = 25;
    private const int FirstRealWeaponId = 1000000;

    private static readonly string[] CoreFields =
    {
        "wepType",
        "behaviorVariationId",
        "weaponCategory",
        "wepmotionCategory",
        "guardmotionCategory",
        "weaponPoseTypeR",
        "weaponPoseTypeL",
        "spAttribute",
        "rightHandEquipable",
        "leftHandEquipable",
        "bothHandleEquiable",
        "bothHandEquipable",
        "isWeaponCatalyst",
        "enableMagic",
        "enableSorcery",
        "enableMiracle"
    };

    private static readonly string[] ExtendedStatFields =
    {
        "properStrength",
        "properAgility",
        "properMagic",
        "properFaith",
        "properLuck",

        "correctStrength",
        "correctAgility",
        "correctMagic",
        "correctFaith",
        "correctLuck",

        "attackBasePhysics",
        "attackBaseMagic",
        "attackBaseFire",
        "attackBaseThunder",
        "attackBaseDark",
        "attackBaseStamina",

        "attackElementCorrectId",

        "weight",
        "fixPrice",
        "reinforcePrice",

        "physGuardCutRate",
        "magGuardCutRate",
        "fireGuardCutRate",
        "thunGuardCutRate",
        "darkGuardCutRate",

        "reinforceTypeId",
        "materialSetId",
        "originEquipWep",
        "durability",
        "durabilityMax",
        "slotLength",

        "staminaGuardDef",
        "guardAngle",
        "guardBaseRepel",
        "attackBaseRepel",
        "guardLevel",

        "defSeMaterial1",
        "correctType_Physics",
        "correctType_Magic",
        "correctType_Fire",
        "correctType_Thunder",
        "correctType_Dark",

        "poisonGuardResist",
        "diseaseGuardResist",
        "bloodGuardResist",
        "curseGuardResist",
        "sleepGuardResist",
        "madnessGuardResist",
        "freezeGuardResist",

        "enableGuard",

        "wepmotionBothHandId",
        "wepCollidableType0",
        "wepCollidableType1",
        "postureControlId_Right",
        "postureControlId_Left",

        "absorpParamId",
        "toughnessCorrectRate",

        "sortGroupId",
        "swordArtsParamId",

        "reinforceShopCategory",
        "baseChangePrice",
        "levelSyncCorrectId",
        "wepRegainHp",

        "saWeaponDamage",
        "saDurability"
    };

    private static readonly string[] CatalystPassiveFields =
    {
        "spEffectBehaviorId0",
        "spEffectBehaviorId1",
        "spEffectBehaviorId2",
        "residentSpEffectId",
        "residentSpEffectId1",
        "residentSpEffectId2",
        "spEffectMsgId0",
        "spEffectMsgId1",
        "spEffectMsgId2",
        "residentSfxId_1",
        "residentSfxId_2",
        "residentSfxId_3",
        "residentSfxId_4",
        "residentSfx_DmyId_1",
        "residentSfx_DmyId_2",
        "residentSfx_DmyId_3",
        "residentSfx_DmyId_4",
        "residentSfx_1_IsVisibleForHang",
        "residentSfx_2_IsVisibleForHang",
        "residentSfx_3_IsVisibleForHang",
        "residentSfx_4_IsVisibleForHang"
    };

    private static readonly string[] VisualFields =
    {
        "equipModelId",
        "iconId"
    };

    private static readonly string[] RandomizerSafePreservedTargetFields =
    {
        "reinforceTypeId",
        "materialSetId",
        "reinforcePrice",
        "reinforceShopCategory",
        "baseChangePrice",
        "levelSyncCorrectId",
        "originEquipWep"
    };

    private static readonly string[] StaffLeftHandParryFields =
    {
        "guardmotionCategory",
        "parryDamageLife",
        "guardAngle",
        "staminaGuardDef",
        "attackBaseParry",
        "defenseBaseParry",
        "guardBaseRepel",
        "attackBaseRepel",
        "guardCutCancelRate",
        "guardLevel",
        "enableGuard",
        "enableParry",
        "wepCollidableType0",
        "wepCollidableType1",
        "postureControlId_Left",
        "swordArtsParamId"
    };

    private static readonly string[] InterestingFields =
        CoreFields
            .Concat(ExtendedStatFields)
            .Concat(CatalystPassiveFields)
            .Concat(VisualFields)
            .Distinct()
            .ToArray();

    public void ProcessEquipParamWeapon(
        string inputRegulation,
        string outputRegulation,
        string defsFolder,
        ConversionRun run,
        string weaponNamesPath = null,
        bool useRandomizerFriendlyShieldUpgradePath = false)
    {
        BND4 bnd = LoadRegulation(inputRegulation);

        BinderFile equipFile = bnd.Files.FirstOrDefault(f =>
            !string.IsNullOrWhiteSpace(f.Name) &&
            f.Name.Contains("EquipParamWeapon.param", StringComparison.OrdinalIgnoreCase));

        if (equipFile == null)
            throw new FileNotFoundException("EquipParamWeapon.param introuvable dans le regulation.");

        Console.WriteLine($"✅ Param trouvé : {equipFile.Name}");

        List<object> paramdefs = LoadParamdefs(defsFolder);
        Console.WriteLine($"📚 Paramdefs chargés : {paramdefs.Count}");

        PARAM equipParam = PARAM.Read(equipFile.Bytes);

        bool applied = ApplyParamdefsCarefully(equipParam, paramdefs);
        if (!applied)
            throw new InvalidOperationException("Impossible d'appliquer automatiquement un paramdef au param chargé.");

        Console.WriteLine("✅ Paramdef appliqué.");
        Console.WriteLine($"📊 Rows EquipParamWeapon : {equipParam.Rows.Count}");

        EnsureRunMappings(equipParam, run, weaponNamesPath);
        Console.WriteLine($"🗺️ Run mappings reçus/générés : {run.Count}");

        DumpWepTypeDistribution(equipParam);

        PARAM.Row staffParryTemplate = FindStaffParryTemplate(equipParam);
        Dictionary<string, object> staffParryTemplateFields = staffParryTemplate != null
            ? CaptureFieldValues(staffParryTemplate, StaffLeftHandParryFields)
            : null;

        if (UseSingleDiffTest)
            ApplySingleMappingDiffTest(equipParam, run, useRandomizerFriendlyShieldUpgradePath);
        else
            ApplyRunMappings(equipParam, run, useRandomizerFriendlyShieldUpgradePath);

        ApplyStaffLeftHandParryPatch(equipParam, staffParryTemplateFields);

        equipFile.Bytes = equipParam.Write();

        SaveRegulation(outputRegulation, bnd);
    }

    private static BND4 LoadRegulation(string inputRegulation)
    {
        Console.WriteLine("🔍 Lecture du regulation...");

        try
        {
            Console.WriteLine("📦 Tentative de déchiffrement SoulsFormats...");
            BND4 decrypted = TryDecryptRegulation(inputRegulation);
            Console.WriteLine("✅ Regulation chargé via DecryptERRegulation.");
            Console.WriteLine($"📚 Fichiers dans le BND : {decrypted.Files.Count}");
            return decrypted;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Déchiffrement indisponible ou en échec : {DescribeException(ex)}");
            Console.WriteLine("📦 Fallback sur lecture BND4 brute...");

            BND4 bnd = BND4.Read(File.ReadAllBytes(inputRegulation));
            Console.WriteLine("✅ Regulation chargé en BND4 brut.");
            Console.WriteLine($"📚 Fichiers dans le BND : {bnd.Files.Count}");
            return bnd;
        }
    }

    private static List<object> LoadParamdefs(string defsFolder)
    {
        Assembly asm = typeof(PARAM).Assembly;

        var paramdefTypes = asm.GetTypes()
            .Where(t => t.Name == "PARAMDEF")
            .ToList();

        if (paramdefTypes.Count == 0)
            throw new InvalidOperationException("Aucun type PARAMDEF trouvé dans l'assembly chargé.");

        var result = new List<object>();

        foreach (Type paramdefType in paramdefTypes)
        {
            MethodInfo xmlDeserialize1 = paramdefType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (!string.Equals(m.Name, "XmlDeserialize", StringComparison.Ordinal))
                        return false;

                    ParameterInfo[] p = m.GetParameters();
                    return p.Length == 1 && p[0].ParameterType == typeof(string);
                });

            MethodInfo xmlDeserialize2 = paramdefType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (!string.Equals(m.Name, "XmlDeserialize", StringComparison.Ordinal))
                        return false;

                    ParameterInfo[] p = m.GetParameters();
                    return p.Length == 2
                        && p[0].ParameterType == typeof(string)
                        && p[1].ParameterType == typeof(bool);
                });

            foreach (string path in Directory.GetFiles(defsFolder, "*.xml"))
            {
                object def = null;

                if (xmlDeserialize1 != null)
                    def = xmlDeserialize1.Invoke(null, new object[] { path });
                else if (xmlDeserialize2 != null)
                    def = xmlDeserialize2.Invoke(null, new object[] { path, false });

                if (def != null)
                    result.Add(def);
            }

            if (result.Count > 0)
                return result;
        }

        throw new InvalidOperationException("Aucune surcharge compatible de PARAMDEF.XmlDeserialize n'a été trouvée.");
    }

    private static void SaveRegulation(string outputRegulation, BND4 bnd)
    {
        string outputDir = Path.GetDirectoryName(outputRegulation);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        byte[] testBytes = bnd.Write();
        string header = System.Text.Encoding.ASCII.GetString(testBytes, 0, 4).TrimEnd('\0');
        Console.WriteLine($"🧪 Header généré : {header}");

        Console.WriteLine("💾 Écriture du regulation de sortie...");

        object compressionValue = GetCompressionTypeFromBnd(bnd);
        Console.WriteLine($"🗜️ Compression value : {compressionValue}");

        bool encrypted = TryEncryptRegulation(outputRegulation, bnd, compressionValue);
        if (!encrypted)
            throw new InvalidOperationException("Aucune surcharge compatible de EncryptERRegulation n'a été trouvée.");

        Console.WriteLine($"✅ Fichier écrit : {outputRegulation}");
    }

    private static string DescribeException(Exception ex)
    {
        var parts = new List<string>();
        Exception current = ex;

        while (current != null)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
        }

        return string.Join(" --> ", parts);
    }

    private static bool ApplyParamdefsCarefully(PARAM param, IEnumerable<object> paramdefs)
    {
        foreach (object def in paramdefs)
        {
            foreach (MethodInfo method in typeof(PARAM).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != "ApplyParamdefCarefully")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1)
                    continue;

                Type parameterType = parameters[0].ParameterType;
                if (!parameterType.IsAssignableFrom(def.GetType()))
                    continue;

                object result = method.Invoke(param, new[] { def });
                if (result is bool applied && applied)
                    return true;
            }
        }

        return false;
    }

    private static BND4 TryDecryptRegulation(string inputRegulation)
    {
        byte[] raw = File.ReadAllBytes(inputRegulation);

        foreach (MethodInfo method in typeof(SFUtil).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "DecryptERRegulation")
                continue;

            ParameterInfo[] p = method.GetParameters();

            try
            {
                if (p.Length == 1 && p[0].ParameterType == typeof(string))
                {
                    object result = method.Invoke(null, new object[] { inputRegulation });
                    if (result is BND4 bndFromPath)
                        return bndFromPath;
                }

                if (p.Length == 1 && p[0].ParameterType == typeof(byte[]))
                {
                    object result = method.Invoke(null, new object[] { raw });
                    if (result is BND4 bndFromBytes)
                        return bndFromBytes;
                }
            }
            catch (TargetInvocationException tie)
            {
                Console.WriteLine($"⚠️ {method} a échoué : {DescribeException(tie.InnerException ?? tie)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ {method} a échoué : {DescribeException(ex)}");
            }
        }

        throw new MissingMethodException("Aucune méthode DecryptERRegulation compatible n'a pu charger ce regulation.");
    }

    private static object GetCompressionTypeFromBnd(BND4 bnd)
    {
        PropertyInfo compressionProp = typeof(BND4).GetProperty(
            "Compression",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (compressionProp == null)
            throw new InvalidOperationException("Propriété Compression introuvable sur BND4.");

        object value = compressionProp.GetValue(bnd);

        if (value == null)
            throw new InvalidOperationException("La propriété Compression du BND4 est nulle.");

        return value;
    }

    private static bool TryEncryptRegulation(string outputRegulation, BND4 bnd, object compressionValue)
    {
        foreach (MethodInfo method in typeof(SFUtil).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "EncryptERRegulation")
                continue;

            ParameterInfo[] p = method.GetParameters();

            if (p.Length == 3 &&
                p[0].ParameterType == typeof(string) &&
                p[1].ParameterType == typeof(BND4) &&
                p[2].ParameterType.IsInstanceOfType(compressionValue))
            {
                method.Invoke(null, new object[] { outputRegulation, bnd, compressionValue });
                return true;
            }

            if (p.Length == 2 &&
                p[0].ParameterType == typeof(BND4) &&
                p[1].ParameterType.IsInstanceOfType(compressionValue))
            {
                object result = method.Invoke(null, new object[] { bnd, compressionValue });

                if (result is byte[] bytes)
                {
                    File.WriteAllBytes(outputRegulation, bytes);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasField(PARAM.Row row, string field)
    {
        try
        {
            var cell = row[field];
            return cell != null;
        }
        catch
        {
            return false;
        }
    }

    private static int GetInt(PARAM.Row row, string field)
    {
        object value = row[field].Value;
        return Convert.ToInt32(value);
    }

    private static int TryGetInt(PARAM.Row row, string field)
    {
        if (!HasField(row, field))
            return -1;

        return Convert.ToInt32(row[field].Value);
    }

    private static void DumpWepTypeDistribution(PARAM equipParam)
    {
        Console.WriteLine("📋 Répartition des wepType :");

        var groups = equipParam.Rows
            .Where(r => HasField(r, "wepType"))
            .GroupBy(r => GetInt(r, "wepType"))
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            Console.WriteLine($"   wepType {group.Key} : {group.Count()}");
        }
    }

    private static void DumpInterestingFields(PARAM.Row row, string label)
    {
        Console.WriteLine($"--- {label} | ID={row.ID} ---");

        foreach (string field in InterestingFields)
        {
            if (!HasField(row, field))
                continue;

            Console.WriteLine($"{field} = {row[field].Value}");
        }
    }

    private static void DumpDifferentInterestingFields(PARAM.Row source, PARAM.Row target, string label)
    {
        Console.WriteLine($"--- {label} | SOURCE={source.ID} TARGET={target.ID} ---");

        int diffCount = 0;

        foreach (string field in InterestingFields)
        {
            if (!HasField(source, field) || !HasField(target, field))
                continue;

            object sourceValue = source[field].Value;
            object targetValue = target[field].Value;

            if (!Equals(sourceValue, targetValue))
            {
                Console.WriteLine($"{field} | source={sourceValue} | target={targetValue}");
                diffCount++;
            }
        }

        Console.WriteLine($"📌 Différences restantes : {diffCount}");
    }

    private static void DumpCompactSummary(PARAM.Row row, string label)
    {
        Console.WriteLine(
            $"[{label}] ID={row.ID} " +
            $"wepType={TryGetInt(row, "wepType")} " +
            $"behaviorVariationId={TryGetInt(row, "behaviorVariationId")} " +
            $"weaponCategory={TryGetInt(row, "weaponCategory")} " +
            $"wepmotionCategory={TryGetInt(row, "wepmotionCategory")} " +
            $"guardmotionCategory={TryGetInt(row, "guardmotionCategory")} " +
            $"weaponPoseTypeR={TryGetInt(row, "weaponPoseTypeR")} " +
            $"weaponPoseTypeL={TryGetInt(row, "weaponPoseTypeL")} " +
            $"spAttribute={TryGetInt(row, "spAttribute")} " +
            $"rightHandEquipable={TryGetInt(row, "rightHandEquipable")} " +
            $"leftHandEquipable={TryGetInt(row, "leftHandEquipable")} " +
            $"bothHandleEquiable={TryGetInt(row, "bothHandleEquiable")} " +
            $"enableMagic={TryGetInt(row, "enableMagic")} " +
            $"enableMiracle={TryGetInt(row, "enableMiracle")}");
    }

    private static bool IsStaff(PARAM.Row row)
    {
        return HasField(row, "wepType") && GetInt(row, "wepType") == 57;
    }

    private static bool IsSeal(PARAM.Row row)
    {
        return HasField(row, "wepType") && GetInt(row, "wepType") == 61;
    }

    private static bool IsShield(PARAM.Row row)
    {
        if (!HasField(row, "wepType"))
            return false;

        int wepType = GetInt(row, "wepType");

        return wepType == 48
            || wepType == 65
            || wepType == 67
            || wepType == 69
            || wepType == 90;
    }

    private static bool IsConvertibleWeaponTarget(PARAM.Row row)
    {
        if (row.ID < FirstRealWeaponId)
            return false;

        return IsShield(row);
    }

    private static List<PARAM.Row> BuildStaffPool(PARAM equipParam, HashSet<int> validNamedWeaponIds)
    {
        return equipParam.Rows
            .Where(row => IsStaff(row) && IsUsableCatalystSource(row, validNamedWeaponIds))
            .OrderBy(r => r.ID)
            .ToList();
    }

    private static List<PARAM.Row> BuildSealPool(PARAM equipParam, HashSet<int> validNamedWeaponIds)
    {
        return equipParam.Rows
            .Where(row => IsSeal(row) && IsUsableCatalystSource(row, validNamedWeaponIds))
            .OrderBy(r => r.ID)
            .ToList();
    }

    private static List<PARAM.Row> BuildConvertibleWeaponTargets(PARAM equipParam)
    {
        return equipParam.Rows
            .Where(IsConvertibleWeaponTarget)
            .OrderBy(r => r.ID)
            .ToList();
    }

    private static Dictionary<int, IReadOnlyList<int>> BuildConvertibleShieldFamilies(PARAM equipParam)
    {
        Dictionary<int, PARAM.Row> rowLookup = equipParam.Rows.ToDictionary(row => row.ID);
        var families = new Dictionary<int, List<int>>();

        foreach (PARAM.Row target in BuildConvertibleWeaponTargets(equipParam))
        {
            int familyRootId = ResolveShieldFamilyRootId(target, rowLookup);

            if (!families.TryGetValue(familyRootId, out List<int> targetIds))
            {
                targetIds = new List<int>();
                families[familyRootId] = targetIds;
            }

            targetIds.Add(target.ID);
        }

        return families.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<int>)pair.Value.OrderBy(id => id).ToList());
    }

    private static int ResolveShieldFamilyRootId(PARAM.Row target, IReadOnlyDictionary<int, PARAM.Row> rowLookup)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));

        int currentId = target.ID;
        var visited = new HashSet<int>();

        while (currentId > 0 && visited.Add(currentId))
        {
            if (!rowLookup.TryGetValue(currentId, out PARAM.Row currentRow))
                break;

            int originEquipWep = TryGetInt(currentRow, "originEquipWep");
            if (originEquipWep <= 0 || originEquipWep == currentId)
                break;

            currentId = originEquipWep;
        }

        return currentId > 0 ? currentId : target.ID;
    }

    private static void EnsureRunMappings(PARAM equipParam, ConversionRun run, string weaponNamesPath)
    {
        if (run == null)
            throw new ArgumentNullException(nameof(run));

        if (run.Mappings != null && run.Mappings.Count > 0)
            return;

        int seed = run.Seed != 0 ? run.Seed : Environment.TickCount;

        Dictionary<int, IReadOnlyList<int>> targetShieldFamilies = BuildConvertibleShieldFamilies(equipParam);
        HashSet<int> validNamedWeaponIds = LoadValidNamedWeaponIds(weaponNamesPath);

        List<int> staffIds = BuildStaffPool(equipParam, validNamedWeaponIds)
            .Select(r => r.ID)
            .ToList();

        List<int> sealIds = BuildSealPool(equipParam, validNamedWeaponIds)
            .Select(r => r.ID)
            .ToList();

        var builder = new ConversionMappingBuilder(seed);
        ConversionRun generated = builder.BuildShieldRun(targetShieldFamilies, staffIds, sealIds);

        Console.WriteLine($"🛡️ Familles de boucliers  : {targetShieldFamilies.Count}");
        Console.WriteLine($"🪄 Staff sources valides  : {staffIds.Count}");
        Console.WriteLine($"✨ Seal sources valides   : {sealIds.Count}");

        foreach (ConversionMapping mapping in generated.Mappings)
        {
            PARAM.Row target = FindRowById(equipParam, mapping.TargetId, "target");

            if (IsStaff(target) || IsSeal(target))
            {
                throw new InvalidOperationException(
                    $"Mapping invalide : la target {mapping.TargetId} est déjà un catalyst (wepType={GetInt(target, "wepType")}).");
            }
        }

        run.Seed = generated.Seed;
        run.Mappings.Clear();

        foreach (ConversionMapping mapping in generated.Mappings)
            run.Mappings.Add(mapping);
    }

    private static bool IsUsableCatalystSource(PARAM.Row row, HashSet<int> validNamedWeaponIds)
    {
        if (row == null)
            return false;

        int sortId = TryGetInt(row, "sortId");
        if (sortId == 9999999)
            return false;

        if (validNamedWeaponIds != null && validNamedWeaponIds.Count > 0)
            return validNamedWeaponIds.Contains(row.ID);

        return true;
    }

    private static HashSet<int> LoadValidNamedWeaponIds(string weaponNamesPath)
    {
        var result = new HashSet<int>();

        if (string.IsNullOrWhiteSpace(weaponNamesPath) || !File.Exists(weaponNamesPath))
            return result;

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

            if (string.Equals(namePart, "[ERROR]", StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(id);
        }

        return result;
    }

    private static PARAM.Row FindRowById(PARAM equipParam, int id, string label)
    {
        foreach (PARAM.Row row in equipParam.Rows)
        {
            if (row.ID == id)
                return row;
        }

        throw new InvalidOperationException($"{label} introuvable pour l'ID {id}.");
    }

    private static void CopyCatalystBehaviorCore(
        PARAM.Row source,
        PARAM.Row target,
        ConversionKind kind,
        bool logDetails,
        bool preserveTargetUpgradePath)
    {
        Dictionary<string, object> preservedTargetFields = preserveTargetUpgradePath
            ? CaptureFieldValues(target, RandomizerSafePreservedTargetFields)
            : null;
        Dictionary<string, object> preservedStaffParryFields = kind == ConversionKind.ShieldToStaff
            ? CaptureFieldValues(target, StaffLeftHandParryFields)
            : null;

        int changed = 0;

        foreach (string field in InterestingFields)
        {
            if (!HasField(source, field) || !HasField(target, field))
                continue;

            object oldValue = target[field].Value;
            object newValue = source[field].Value;

            if (!Equals(oldValue, newValue))
            {
                target[field].Value = newValue;
                changed++;
            }
        }

        switch (kind)
        {
            case ConversionKind.ShieldToStaff:
                if (HasField(target, "enableMagic"))
                    target["enableMagic"].Value = 1;

                if (HasField(target, "enableMiracle"))
                    target["enableMiracle"].Value = 0;
                break;

            case ConversionKind.ShieldToSeal:
                if (HasField(target, "enableMagic"))
                    target["enableMagic"].Value = 0;

                if (HasField(target, "enableMiracle"))
                    target["enableMiracle"].Value = 1;
                break;
        }

        if (HasField(source, "swordArtsParamId") && HasField(target, "swordArtsParamId"))
            target["swordArtsParamId"].Value = source["swordArtsParamId"].Value;

        if (HasField(target, "enableGuard"))
            target["enableGuard"].Value = 0;

        if (HasField(source, "guardmotionCategory") && HasField(target, "guardmotionCategory"))
            target["guardmotionCategory"].Value = source["guardmotionCategory"].Value;

        if (HasField(source, "wepmotionBothHandId") && HasField(target, "wepmotionBothHandId"))
            target["wepmotionBothHandId"].Value = source["wepmotionBothHandId"].Value;

        if (preservedTargetFields != null)
            RestoreFieldValues(target, preservedTargetFields);

        if (preservedStaffParryFields != null)
        {
            RestoreFieldValues(target, preservedStaffParryFields);
            EnsureStaffLeftHandParryFlags(target);
        }

        if (logDetails)
        {
            string upgradeMode = preserveTargetUpgradePath
                ? " | upgrade path préservé"
                : string.Empty;
            Console.WriteLine($"🔧 {target.ID} <= {source.ID} | {kind} | champs modifiés : {changed}{upgradeMode}");
        }
    }

    private static void ApplySingleMappingDiffTest(PARAM equipParam, ConversionRun run, bool preserveTargetUpgradePath)
    {
        if (run == null)
            throw new ArgumentNullException(nameof(run));

        if (run.Mappings == null || run.Mappings.Count == 0)
            throw new InvalidOperationException("Le run ne contient aucun mapping.");

        ConversionMapping mapping = run.Mappings[0];

        List<PARAM.Row> staffPool = BuildStaffPool(equipParam, validNamedWeaponIds: null);
        List<PARAM.Row> sealPool = BuildSealPool(equipParam, validNamedWeaponIds: null);
        List<PARAM.Row> targets = BuildConvertibleWeaponTargets(equipParam);

        Console.WriteLine($"🪄 Staff pool    : {staffPool.Count}");
        Console.WriteLine($"✨ Seal pool     : {sealPool.Count}");
        Console.WriteLine($"🛡️ Shield targets: {targets.Count}");

        if (targets.Count == 0)
            throw new InvalidOperationException("Aucun bouclier cible détecté.");

        Console.WriteLine($"🎯 Mapping test : {mapping.TargetId} <= {mapping.SourceId} ({mapping.Kind})");

        PARAM.Row source = FindRowById(equipParam, mapping.SourceId, "source");
        PARAM.Row target = FindRowById(equipParam, mapping.TargetId, "target");

        DumpCompactSummary(source, "SOURCE BEFORE");
        DumpCompactSummary(target, "TARGET BEFORE");

        if (VerboseFieldDump)
            DumpDifferentInterestingFields(source, target, "DIFF BEFORE");

        CopyCatalystBehaviorCore(source, target, mapping.Kind, true, preserveTargetUpgradePath);

        DumpCompactSummary(target, "TARGET AFTER");

        if (VerboseFieldDump)
        {
            DumpDifferentInterestingFields(source, target, "DIFF AFTER");
            DumpInterestingFields(source, "SOURCE FULL");
            DumpInterestingFields(target, "TARGET FULL AFTER");
        }
    }

    private static void ApplyRunMappings(PARAM equipParam, ConversionRun run, bool preserveTargetUpgradePath)
    {
        if (run == null)
            throw new ArgumentNullException(nameof(run));

        if (run.Mappings == null || run.Mappings.Count == 0)
            throw new InvalidOperationException("Le run ne contient aucun mapping.");

        Console.WriteLine($"🗺️ Application de {run.Mappings.Count} mappings...");

        int preview = 0;
        int staffCount = 0;
        int sealCount = 0;

        foreach (ConversionMapping mapping in run.Mappings)
        {
            PARAM.Row source = FindRowById(equipParam, mapping.SourceId, "source");
            PARAM.Row target = FindRowById(equipParam, mapping.TargetId, "target");

            CopyCatalystBehaviorCore(source, target, mapping.Kind, false, preserveTargetUpgradePath);

            if (mapping.Kind == ConversionKind.ShieldToStaff)
                staffCount++;
            else if (mapping.Kind == ConversionKind.ShieldToSeal)
                sealCount++;

            if (preview < PreviewLimit)
                Console.WriteLine($"🔄 {mapping.TargetId} <= {mapping.SourceId} ({mapping.Kind})");

            preview++;
        }

        if (run.Mappings.Count > PreviewLimit)
            Console.WriteLine($"… {run.Mappings.Count - PreviewLimit} mappings supplémentaires non affichés.");

        Console.WriteLine($"✅ Convertis en staffs : {staffCount}");
        Console.WriteLine($"✅ Convertis en seals  : {sealCount}");
    }

    private static void ApplyStaffLeftHandParryPatch(
        PARAM equipParam,
        IReadOnlyDictionary<string, object> parryTemplateFields)
    {
        int patchedCount = 0;

        foreach (PARAM.Row row in equipParam.Rows.Where(IsStaff))
        {
            if (parryTemplateFields != null)
                RestoreFieldValues(row, parryTemplateFields);

            EnsureStaffLeftHandParryFlags(row);
            patchedCount++;
        }

        Console.WriteLine($"Staffs compatibles parade main gauche : {patchedCount}");
    }

    private static PARAM.Row FindStaffParryTemplate(PARAM equipParam)
    {
        return equipParam.Rows
            .Where(row => IsShield(row) && TryGetInt(row, "enableParry") > 0)
            .OrderByDescending(row => TryGetInt(row, "swordArtsParamId") > 10)
            .ThenBy(row => row.ID)
            .FirstOrDefault();
    }

    private static void EnsureStaffLeftHandParryFlags(PARAM.Row row)
    {
        SetIntIfFieldExists(row, "leftHandEquipable", 1);
        SetIntIfFieldExists(row, "enableGuard", 1);
        SetIntIfFieldExists(row, "enableParry", 1);

        if (HasField(row, "parryDamageLife") && TryGetInt(row, "parryDamageLife") <= 0)
            row["parryDamageLife"].Value = 10;
    }

    private static void CopyExistingFields(PARAM.Row source, PARAM.Row target, IEnumerable<string> fieldNames)
    {
        foreach (string fieldName in fieldNames)
        {
            if (!HasField(source, fieldName) || !HasField(target, fieldName))
                continue;

            target[fieldName].Value = source[fieldName].Value;
        }
    }

    private static void SetIntIfFieldExists(PARAM.Row row, string fieldName, int value)
    {
        if (HasField(row, fieldName))
            row[fieldName].Value = value;
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
}
