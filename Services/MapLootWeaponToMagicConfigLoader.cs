using System;
using System.IO;
using System.Text.Json;

namespace RandomMagicConversion;

public static class MapLootWeaponToMagicConfigLoader
{
    public static MapLootWeaponToMagicConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin du fichier de config map loot est vide.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier de configuration map loot introuvable.", path);

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        MapLootWeaponToMagicConfig config = JsonSerializer.Deserialize<MapLootWeaponToMagicConfig>(json, options);

        if (config == null)
            throw new InvalidOperationException($"Impossible de deserialiser la configuration map loot : {path}");

        if (config.Entries == null)
            throw new InvalidOperationException($"La liste 'entries' est absente ou invalide dans : {path}");

        return config;
    }
}
