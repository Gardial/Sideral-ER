using System;
using System.IO;
using System.Text.Json;

namespace RandomMagicConversion;

public static class EnemyLotWeaponToMagicConfigLoader
{
    public static EnemyLotWeaponToMagicConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin du fichier de config enemy lot est vide.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier de configuration enemy lot introuvable.", path);

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        EnemyLotWeaponToMagicConfig config = JsonSerializer.Deserialize<EnemyLotWeaponToMagicConfig>(json, options);

        if (config == null)
            throw new InvalidOperationException($"Impossible de deserialiser la configuration enemy lot : {path}");

        if (config.Entries == null)
            throw new InvalidOperationException($"La liste 'entries' est absente ou invalide dans : {path}");

        return config;
    }
}
