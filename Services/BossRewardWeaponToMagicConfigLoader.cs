using System;
using System.IO;
using System.Text.Json;

namespace RandomMagicConversion;

public static class BossRewardWeaponToMagicConfigLoader
{
    public static BossRewardWeaponToMagicConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin du fichier de config boss reward est vide.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier de configuration boss reward introuvable.", path);

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        BossRewardWeaponToMagicConfig config = JsonSerializer.Deserialize<BossRewardWeaponToMagicConfig>(json, options);

        if (config == null)
            throw new InvalidOperationException($"Impossible de deserialiser la configuration boss reward : {path}");

        if (config.Entries == null)
            throw new InvalidOperationException($"La liste 'entries' est absente ou invalide dans : {path}");

        return config;
    }
}
