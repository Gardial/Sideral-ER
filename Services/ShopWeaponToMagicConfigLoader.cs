using System;
using System.IO;
using System.Text.Json;

namespace RandomMagicConversion;

public static class ShopWeaponToMagicConfigLoader
{
    public static ShopWeaponToMagicConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin du fichier de config shop est vide.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier de configuration shop introuvable.", path);

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        ShopWeaponToMagicConfig config = JsonSerializer.Deserialize<ShopWeaponToMagicConfig>(json, options);

        if (config == null)
            throw new InvalidOperationException($"Impossible de désérialiser la configuration shop : {path}");

        if (config.Entries == null)
            throw new InvalidOperationException($"La liste 'entries' est absente ou invalide dans : {path}");

        return config;
    }
}