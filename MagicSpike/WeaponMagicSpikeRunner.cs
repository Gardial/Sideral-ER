using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SoulsFormats;

namespace RandomMagicConversion
{
    public sealed class WeaponMagicSpikeRunner
    {
        private readonly List<PARAMDEF> _paramdefs;
        private readonly bool _verbose;

        public WeaponMagicSpikeRunner(string defsDirectory, bool verbose = false)
        {
            if (string.IsNullOrWhiteSpace(defsDirectory))
                throw new ArgumentException("Le chemin du dossier Defs est vide.", nameof(defsDirectory));

            if (!Directory.Exists(defsDirectory))
                throw new DirectoryNotFoundException($"Dossier Defs introuvable : {defsDirectory}");

            _paramdefs = LoadParamdefs(defsDirectory);
            _verbose = verbose;
        }

        public WeaponMagicSpikeResult ScanItemLots(
            string inputRegulationPath,
            int targetWeaponId,
            string logOutputPath = null)
        {
            if (string.IsNullOrWhiteSpace(inputRegulationPath))
                throw new ArgumentException("Le chemin du regulation.bin est vide.", nameof(inputRegulationPath));

            if (!File.Exists(inputRegulationPath))
                throw new FileNotFoundException("regulation.bin introuvable.", inputRegulationPath);

            Console.WriteLine("==================================================");
            Console.WriteLine("🔎 Weapon -> Magic spike : scan ItemLot");
            Console.WriteLine("==================================================");
            Console.WriteLine($"🎯 TargetWeaponId : {targetWeaponId}");
            Console.WriteLine($"📥 Input regulation : {inputRegulationPath}");

            BND4 bnd = LoadRegulation(inputRegulationPath);

            var result = new WeaponMagicSpikeResult
            {
                TargetWeaponId = targetWeaponId
            };

            var itemLotFiles = bnd.Files
                .Where(f =>
                {
                    string name = Path.GetFileNameWithoutExtension(f.Name);
                    return name.StartsWith("ItemLotParam", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(f => Path.GetFileNameWithoutExtension(f.Name), StringComparer.OrdinalIgnoreCase)
                .ToList();

            Console.WriteLine($"📚 Params ItemLot détectés : {itemLotFiles.Count}");

            foreach (BinderFile file in itemLotFiles)
            {
                string paramName = Path.GetFileNameWithoutExtension(file.Name);

                if (_verbose)
                    Console.WriteLine($"\n🔍 Scan du param : {paramName}");

                PARAM param;
                try
                {
                    param = PARAM.Read(file.Bytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Impossible de lire {paramName} : {ex.Message}");
                    continue;
                }

                bool applied = false;
                try
                {
                    applied = param.ApplyParamdefCarefully(_paramdefs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ ApplyParamdefCarefully a échoué pour {paramName} : {ex.Message}");
                }

                if (!applied)
                {
                    Console.WriteLine($"⚠️ Paramdef non appliqué pour {paramName} (ParamType: {param.ParamType})");
                    continue;
                }

                int hitsBefore = result.Occurrences.Count;

                foreach (PARAM.Row row in param.Rows)
                {
                    ScanRow(paramName, row, targetWeaponId, result);
                }

                int hitsAfter = result.Occurrences.Count;
                int foundInParam = hitsAfter - hitsBefore;

                if (_verbose)
                    Console.WriteLine($"   ↳ Occurrences trouvées dans {paramName} : {foundInParam}");
            }

            Console.WriteLine($"\n✅ Occurrences totales trouvées : {result.Occurrences.Count}");

            if (!string.IsNullOrWhiteSpace(logOutputPath))
            {
                WriteReport(logOutputPath, result);
                Console.WriteLine($"📝 Rapport écrit : {logOutputPath}");
            }

            return result;
        }

        public void ApplySingleItemLotReplacementTest(
            string inputRegulationPath,
            string outputRegulationPath,
            ItemLotSingleReplacementTest test)
        {
            if (string.IsNullOrWhiteSpace(inputRegulationPath))
                throw new ArgumentException("Le chemin du regulation.bin d'entrée est vide.", nameof(inputRegulationPath));

            if (!File.Exists(inputRegulationPath))
                throw new FileNotFoundException("regulation.bin d'entrée introuvable.", inputRegulationPath);

            if (string.IsNullOrWhiteSpace(outputRegulationPath))
                throw new ArgumentException("Le chemin du regulation.bin de sortie est vide.", nameof(outputRegulationPath));

            if (test == null)
                throw new ArgumentNullException(nameof(test));

            if (string.IsNullOrWhiteSpace(test.ParamName))
                throw new ArgumentException("ParamName ne peut pas être vide.", nameof(test));

            if (test.SlotIndex < 1 || test.SlotIndex > 8)
                throw new ArgumentOutOfRangeException(nameof(test), "SlotIndex doit être compris entre 1 et 8.");

            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("🧪 Weapon -> Magic spike : single ItemLot replacement");
            Console.WriteLine("==================================================");
            Console.WriteLine($"📥 Input regulation  : {inputRegulationPath}");
            Console.WriteLine($"📤 Output regulation : {outputRegulationPath}");
            Console.WriteLine($"📦 Param             : {test.ParamName}");
            Console.WriteLine($"🧾 RowID             : {test.RowId}");
            Console.WriteLine($"🎰 Slot              : {test.SlotIndex:00}");
            Console.WriteLine($"🔁 Old Item ID       : {test.ExpectedOldItemId}");
            Console.WriteLine($"✨ New Item ID       : {test.NewItemId}");
            Console.WriteLine($"🏷️ New Category      : {test.NewCategoryValue}");

            BND4 bnd = LoadRegulation(inputRegulationPath);

            BinderFile targetFile = bnd.Files.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f.Name), test.ParamName, StringComparison.OrdinalIgnoreCase));

            if (targetFile == null)
                throw new InvalidOperationException($"Param introuvable dans le BND : {test.ParamName}");

            PARAM param = PARAM.Read(targetFile.Bytes);

            bool applied = false;
            try
            {
                applied = param.ApplyParamdefCarefully(_paramdefs);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Échec ApplyParamdefCarefully sur {test.ParamName} (ParamType: {param.ParamType}). {ex.Message}", ex);
            }

            if (!applied)
                throw new InvalidOperationException(
                    $"Paramdef non appliqué sur {test.ParamName} (ParamType: {param.ParamType}).");

            PARAM.Row row = param.Rows.FirstOrDefault(r => r.ID == test.RowId);
            if (row == null)
                throw new InvalidOperationException($"Row introuvable : {test.RowId} dans {test.ParamName}");

            string suffix = test.SlotIndex.ToString("00");
            string itemIdField = $"lotItemId{suffix}";
            string categoryField = $"lotItemCategory{suffix}";

            if (!TryReadInt(row, itemIdField, out int oldItemId))
                throw new InvalidOperationException($"Impossible de lire le champ {itemIdField} sur RowID={row.ID}");

            if (!TryReadInt(row, categoryField, out int oldCategoryValue))
                throw new InvalidOperationException($"Impossible de lire le champ {categoryField} sur RowID={row.ID}");

            Console.WriteLine($"🔍 Avant remplacement : {itemIdField}={oldItemId} | {categoryField}={oldCategoryValue}");

            if (oldItemId != test.ExpectedOldItemId)
            {
                throw new InvalidOperationException(
                    $"La row {row.ID} ne contient pas l'item attendu dans {itemIdField}. " +
                    $"Attendu={test.ExpectedOldItemId}, lu={oldItemId}");
            }

            SetIntField(row, itemIdField, test.NewItemId);
            SetIntField(row, categoryField, test.NewCategoryValue);

            if (!TryReadInt(row, itemIdField, out int newItemId))
                throw new InvalidOperationException($"Impossible de relire le champ {itemIdField} après écriture.");

            if (!TryReadInt(row, categoryField, out int newCategoryValue))
                throw new InvalidOperationException($"Impossible de relire le champ {categoryField} après écriture.");

            Console.WriteLine($"✅ Après remplacement : {itemIdField}={newItemId} | {categoryField}={newCategoryValue}");

            targetFile.Bytes = param.Write();

            WriteRegulation(outputRegulationPath, bnd);

            Console.WriteLine("🏁 Single replacement terminé.");
        }

        public ShopLineupScanResult ScanShopLineup(
            string inputRegulationPath,
            int targetEquipId,
            string logOutputPath = null)
        {
            if (string.IsNullOrWhiteSpace(inputRegulationPath))
                throw new ArgumentException("Le chemin du regulation.bin est vide.", nameof(inputRegulationPath));

            if (!File.Exists(inputRegulationPath))
                throw new FileNotFoundException("regulation.bin introuvable.", inputRegulationPath);

            Console.WriteLine("==================================================");
            Console.WriteLine("🛒 Weapon -> Magic spike : scan ShopLineupParam");
            Console.WriteLine("==================================================");
            Console.WriteLine($"🎯 TargetEquipId     : {targetEquipId}");
            Console.WriteLine($"📥 Input regulation  : {inputRegulationPath}");

            BND4 bnd = LoadRegulation(inputRegulationPath);

            BinderFile shopFile = bnd.Files.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f.Name), "ShopLineupParam", StringComparison.OrdinalIgnoreCase));

            if (shopFile == null)
                throw new InvalidOperationException("Param introuvable dans le BND : ShopLineupParam");

            PARAM param = PARAM.Read(shopFile.Bytes);

            bool applied = false;
            try
            {
                applied = param.ApplyParamdefCarefully(_paramdefs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ ApplyParamdefCarefully a échoué pour ShopLineupParam : {ex.Message}");
            }

            if (!applied)
                throw new InvalidOperationException($"Paramdef non appliqué pour ShopLineupParam (ParamType: {param.ParamType})");

            Console.WriteLine($"📚 Rows ShopLineupParam : {param.Rows.Count}");

            var result = new ShopLineupScanResult
            {
                TargetEquipId = targetEquipId
            };

            foreach (PARAM.Row row in param.Rows)
            {
                if (!TryReadInt(row, "equipId", out int equipId))
                    continue;

                if (equipId != targetEquipId)
                    continue;

                int equipType = -1;
                int value = -1;
                int sellQuantity = -1;
                int eventFlagForStock = -1;
                int eventFlagForRelease = -1;

                TryReadInt(row, "equipType", out equipType);
                TryReadInt(row, "value", out value);
                TryReadInt(row, "sellQuantity", out sellQuantity);
                TryReadInt(row, "eventFlag_forStock", out eventFlagForStock);
                TryReadInt(row, "eventFlag_forRelease", out eventFlagForRelease);

                var hit = new ShopLineupOccurrence
                {
                    RowId = row.ID,
                    RowName = row.Name ?? string.Empty,
                    EquipId = equipId,
                    EquipType = equipType,
                    Value = value,
                    SellQuantity = sellQuantity,
                    EventFlagForStock = eventFlagForStock,
                    EventFlagForRelease = eventFlagForRelease
                };

                result.Occurrences.Add(hit);

                Console.WriteLine(
                    $"🎯 HIT | RowID={hit.RowId} | EquipId={hit.EquipId} | EquipType={hit.EquipType} | " +
                    $"Price={hit.Value} | Qty={hit.SellQuantity} | StockFlag={hit.EventFlagForStock} | ReleaseFlag={hit.EventFlagForRelease}");
            }

            Console.WriteLine($"\n✅ Occurrences totales trouvées : {result.Occurrences.Count}");

            if (!string.IsNullOrWhiteSpace(logOutputPath))
            {
                WriteShopLineupReport(logOutputPath, result);
                Console.WriteLine($"📝 Rapport écrit : {logOutputPath}");
            }

            return result;
        }

        public void ApplySingleShopLineupReplacementTest(
            string inputRegulationPath,
            string outputRegulationPath,
            ShopLineupSingleReplacementTest test)
        {
            if (string.IsNullOrWhiteSpace(inputRegulationPath))
                throw new ArgumentException("Le chemin du regulation.bin d'entrée est vide.", nameof(inputRegulationPath));

            if (!File.Exists(inputRegulationPath))
                throw new FileNotFoundException("regulation.bin d'entrée introuvable.", inputRegulationPath);

            if (string.IsNullOrWhiteSpace(outputRegulationPath))
                throw new ArgumentException("Le chemin du regulation.bin de sortie est vide.", nameof(outputRegulationPath));

            if (test == null)
                throw new ArgumentNullException(nameof(test));

            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("🧪 Weapon -> Magic spike : single ShopLineup replacement");
            Console.WriteLine("==================================================");
            Console.WriteLine($"📥 Input regulation  : {inputRegulationPath}");
            Console.WriteLine($"📤 Output regulation : {outputRegulationPath}");
            Console.WriteLine($"🧾 RowID             : {test.RowId}");
            Console.WriteLine($"🔁 Old EquipId       : {test.ExpectedOldEquipId}");
            Console.WriteLine($"🔁 Old EquipType     : {test.ExpectedOldEquipType}");
            Console.WriteLine($"✨ New EquipId       : {test.NewEquipId}");
            Console.WriteLine($"🏷️ New EquipType     : {test.NewEquipType}");

            BND4 bnd = LoadRegulation(inputRegulationPath);

            BinderFile shopFile = bnd.Files.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f.Name), "ShopLineupParam", StringComparison.OrdinalIgnoreCase));

            if (shopFile == null)
                throw new InvalidOperationException("Param introuvable dans le BND : ShopLineupParam");

            PARAM param = PARAM.Read(shopFile.Bytes);

            bool applied = false;
            try
            {
                applied = param.ApplyParamdefCarefully(_paramdefs);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Échec ApplyParamdefCarefully sur ShopLineupParam (ParamType: {param.ParamType}). {ex.Message}", ex);
            }

            if (!applied)
                throw new InvalidOperationException($"Paramdef non appliqué sur ShopLineupParam (ParamType: {param.ParamType}).");

            PARAM.Row row = param.Rows.FirstOrDefault(r => r.ID == test.RowId);
            if (row == null)
                throw new InvalidOperationException($"Row introuvable : {test.RowId} dans ShopLineupParam");

            if (!TryReadInt(row, "equipId", out int oldEquipId))
                throw new InvalidOperationException($"Impossible de lire equipId sur RowID={row.ID}");

            if (!TryReadInt(row, "equipType", out int oldEquipType))
                throw new InvalidOperationException($"Impossible de lire equipType sur RowID={row.ID}");

            Console.WriteLine($"🔍 Avant remplacement : equipId={oldEquipId} | equipType={oldEquipType}");

            if (oldEquipId != test.ExpectedOldEquipId)
                throw new InvalidOperationException(
                    $"equipId inattendu sur RowID={row.ID}. Attendu={test.ExpectedOldEquipId}, lu={oldEquipId}");

            if (oldEquipType != test.ExpectedOldEquipType)
                throw new InvalidOperationException(
                    $"equipType inattendu sur RowID={row.ID}. Attendu={test.ExpectedOldEquipType}, lu={oldEquipType}");

            SetIntField(row, "equipId", test.NewEquipId);
            SetIntField(row, "equipType", test.NewEquipType);

            if (!TryReadInt(row, "equipId", out int newEquipId))
                throw new InvalidOperationException("Impossible de relire equipId après écriture.");

            if (!TryReadInt(row, "equipType", out int newEquipType))
                throw new InvalidOperationException("Impossible de relire equipType après écriture.");

            Console.WriteLine($"✅ Après remplacement : equipId={newEquipId} | equipType={newEquipType}");

            shopFile.Bytes = param.Write();

            WriteRegulation(outputRegulationPath, bnd);

            Console.WriteLine("🏁 Single ShopLineup replacement terminé.");
        }

        private static BND4 LoadRegulation(string inputRegulationPath)
        {
            Console.WriteLine("🔍 Lecture du regulation.bin...");

            try
            {
                Console.WriteLine("📦 Tentative via SFUtil.DecryptERRegulation(path)...");
                BND4 bnd = SFUtil.DecryptERRegulation(inputRegulationPath);
                Console.WriteLine("✅ Regulation chargé via déchiffrement.");
                return bnd;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ DecryptERRegulation a échoué : {ex.Message}");
                Console.WriteLine("📦 Fallback sur lecture BND4 brute...");
                byte[] raw = File.ReadAllBytes(inputRegulationPath);
                BND4 bnd = BND4.Read(raw);
                Console.WriteLine("✅ Regulation chargé via BND4.Read(raw).");
                return bnd;
            }
        }

        private static void WriteRegulation(string outputRegulationPath, BND4 bnd)
        {
            string outputDir = Path.GetDirectoryName(outputRegulationPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
                Directory.CreateDirectory(outputDir);

            Console.WriteLine("💾 Écriture du regulation de spike...");

            try
            {
                SFUtil.EncryptERRegulation(outputRegulationPath, bnd);
                Console.WriteLine($"✅ Fichier écrit via EncryptERRegulation : {outputRegulationPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ EncryptERRegulation a échoué : {ex.Message}");
                Console.WriteLine("📦 Fallback sur écriture BND4 brute...");
                byte[] raw = bnd.Write();
                File.WriteAllBytes(outputRegulationPath, raw);
                Console.WriteLine($"⚠️ Fichier brut écrit : {outputRegulationPath}");
            }
        }

        private static List<PARAMDEF> LoadParamdefs(string defsDirectory)
        {
            Console.WriteLine($"📂 Chargement des paramdefs depuis : {defsDirectory}");

            var defs = new List<PARAMDEF>();

            foreach (string xmlPath in Directory.GetFiles(defsDirectory, "*.xml", SearchOption.AllDirectories))
            {
                try
                {
                    defs.Add(PARAMDEF.XmlDeserialize(xmlPath));
                }
                catch
                {
                    // On ignore les XML qui ne sont pas des paramdefs valides
                }
            }

            Console.WriteLine($"✅ Paramdefs chargés : {defs.Count}");
            return defs;
        }

        private static void ScanRow(
            string paramName,
            PARAM.Row row,
            int targetWeaponId,
            WeaponMagicSpikeResult result)
        {
            for (int slot = 1; slot <= 8; slot++)
            {
                string suffix = slot.ToString("00");
                string itemIdField = $"lotItemId{suffix}";
                string categoryField = $"lotItemCategory{suffix}";

                if (!TryReadInt(row, itemIdField, out int itemId))
                    continue;

                if (itemId != targetWeaponId)
                    continue;

                int categoryValue = -1;
                TryReadInt(row, categoryField, out categoryValue);

                result.Occurrences.Add(new WeaponMagicSpikeOccurrence
                {
                    ParamName = paramName,
                    RowId = row.ID,
                    RowName = row.Name ?? string.Empty,
                    SlotIndex = slot,
                    ItemIdField = itemIdField,
                    CategoryField = categoryField,
                    ItemId = itemId,
                    CategoryValue = categoryValue
                });

                Console.WriteLine(
                    $"🎯 HIT | Param={paramName} | RowID={row.ID} | Slot={slot:00} | " +
                    $"{itemIdField}={itemId} | {categoryField}={categoryValue}");
            }
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

        private static void SetIntField(PARAM.Row row, string fieldName, int value)
        {
            PARAM.Cell cell = row[fieldName];
            object raw = cell.Value;

            switch (raw)
            {
                case sbyte _:
                    if (value < sbyte.MinValue || value > sbyte.MaxValue)
                        throw new InvalidOperationException($"{fieldName}: valeur hors plage sbyte ({value}).");
                    cell.Value = (sbyte)value;
                    break;

                case byte _:
                    if (value < byte.MinValue || value > byte.MaxValue)
                        throw new InvalidOperationException($"{fieldName}: valeur hors plage byte ({value}).");
                    cell.Value = (byte)value;
                    break;

                case short _:
                    if (value < short.MinValue || value > short.MaxValue)
                        throw new InvalidOperationException($"{fieldName}: valeur hors plage short ({value}).");
                    cell.Value = (short)value;
                    break;

                case ushort _:
                    if (value < ushort.MinValue || value > ushort.MaxValue)
                        throw new InvalidOperationException($"{fieldName}: valeur hors plage ushort ({value}).");
                    cell.Value = (ushort)value;
                    break;

                case int _:
                    cell.Value = value;
                    break;

                case uint _:
                    if (value < 0)
                        throw new InvalidOperationException($"{fieldName}: valeur négative impossible pour uint ({value}).");
                    cell.Value = (uint)value;
                    break;

                case long _:
                    cell.Value = (long)value;
                    break;

                case ulong _:
                    if (value < 0)
                        throw new InvalidOperationException($"{fieldName}: valeur négative impossible pour ulong ({value}).");
                    cell.Value = (ulong)value;
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

        private static void WriteReport(string outputPath, WeaponMagicSpikeResult result)
        {
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();

            sb.AppendLine("==================================================");
            sb.AppendLine("Weapon -> Magic spike : scan ItemLot");
            sb.AppendLine("==================================================");
            sb.AppendLine($"TargetWeaponId : {result.TargetWeaponId}");
            sb.AppendLine($"Occurrences    : {result.Occurrences.Count}");
            sb.AppendLine();

            foreach (var group in result.Occurrences
                         .GroupBy(x => x.ParamName)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"[{group.Key}]");

                foreach (var occ in group.OrderBy(x => x.RowId).ThenBy(x => x.SlotIndex))
                {
                    sb.AppendLine(
                        $" - RowID={occ.RowId} | RowName=\"{occ.RowName}\" | " +
                        $"Slot={occ.SlotIndex:00} | {occ.ItemIdField}={occ.ItemId} | " +
                        $"{occ.CategoryField}={occ.CategoryValue}");
                }

                sb.AppendLine();
            }

            if (result.Occurrences.Count == 0)
                sb.AppendLine("Aucune occurrence trouvée.");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteShopLineupReport(string outputPath, ShopLineupScanResult result)
        {
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();

            sb.AppendLine("==================================================");
            sb.AppendLine("Weapon -> Magic spike : scan ShopLineupParam");
            sb.AppendLine("==================================================");
            sb.AppendLine($"TargetEquipId : {result.TargetEquipId}");
            sb.AppendLine($"Occurrences   : {result.Occurrences.Count}");
            sb.AppendLine();

            foreach (var occ in result.Occurrences.OrderBy(x => x.RowId))
            {
                sb.AppendLine(
                    $" - RowID={occ.RowId} | RowName=\"{occ.RowName}\" | " +
                    $"EquipId={occ.EquipId} | EquipType={occ.EquipType} | " +
                    $"Price={occ.Value} | Qty={occ.SellQuantity} | " +
                    $"StockFlag={occ.EventFlagForStock} | ReleaseFlag={occ.EventFlagForRelease}");
            }

            if (result.Occurrences.Count == 0)
                sb.AppendLine("Aucune occurrence trouvée.");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }
    }

    public sealed class ItemLotSingleReplacementTest
    {
        public string ParamName { get; init; } = string.Empty;
        public int RowId { get; init; }
        public int SlotIndex { get; init; }
        public int ExpectedOldItemId { get; init; }
        public int NewItemId { get; init; }
        public int NewCategoryValue { get; init; }
    }

    public sealed class ShopLineupScanResult
    {
        public int TargetEquipId { get; init; }
        public List<ShopLineupOccurrence> Occurrences { get; } = new();
    }

    public sealed class ShopLineupOccurrence
    {
        public int RowId { get; init; }
        public string RowName { get; init; } = string.Empty;
        public int EquipId { get; init; }
        public int EquipType { get; init; }
        public int Value { get; init; }
        public int SellQuantity { get; init; }
        public int EventFlagForStock { get; init; }
        public int EventFlagForRelease { get; init; }
    }

    public sealed class ShopLineupSingleReplacementTest
    {
        public int RowId { get; init; }
        public int ExpectedOldEquipId { get; init; }
        public int ExpectedOldEquipType { get; init; }
        public int NewEquipId { get; init; }
        public int NewEquipType { get; init; }
    }
}