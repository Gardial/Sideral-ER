using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RandomMagicConversion;

public sealed class WeaponRandomizer
{
    private readonly bool _verbose;

    public List<int> TargetWeaponIds { get; }
    public List<int> SourceWeaponIds { get; }

    public WeaponRandomizer(string targetWeaponsCsvPath, string sourceWeaponsCsvPath, bool verbose = false)
    {
        _verbose = verbose;

        if (!File.Exists(targetWeaponsCsvPath))
            throw new FileNotFoundException("CSV cible introuvable.", targetWeaponsCsvPath);

        if (!File.Exists(sourceWeaponsCsvPath))
            throw new FileNotFoundException("CSV source introuvable.", sourceWeaponsCsvPath);

        TargetWeaponIds = LoadWeaponIds(targetWeaponsCsvPath);
        SourceWeaponIds = LoadWeaponIds(sourceWeaponsCsvPath);

        if (TargetWeaponIds.Count == 0)
            throw new InvalidOperationException("Aucune arme cible chargée depuis le CSV.");

        if (SourceWeaponIds.Count == 0)
            throw new InvalidOperationException("Aucune arme source chargée depuis le CSV.");

        if (_verbose)
        {
            Console.WriteLine($"🎯 Target weapons : {TargetWeaponIds.Count}");
            Console.WriteLine($"✨ Source weapons : {SourceWeaponIds.Count}");
        }
    }

    public Dictionary<int, int> BuildRandomMap(int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var result = new Dictionary<int, int>();

        foreach (int targetId in TargetWeaponIds)
        {
            int sourceId = SourceWeaponIds[rng.Next(SourceWeaponIds.Count)];
            result[targetId] = sourceId;

            if (_verbose)
                Console.WriteLine($"🔄 {targetId} <= {sourceId}");
        }

        return result;
    }

    public int PickRandomSource(Random rng)
    {
        if (SourceWeaponIds.Count == 0)
            throw new InvalidOperationException("Aucune arme source disponible.");

        return SourceWeaponIds[rng.Next(SourceWeaponIds.Count)];
    }

    private static List<int> LoadWeaponIds(string path)
    {
        var result = new List<int>();

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("#"))
                continue;

            string firstCell = line.Split(';', ',', '\t')[0].Trim();

            if (int.TryParse(firstCell, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                result.Add(id);
        }

        return result.Distinct().ToList();
    }
}