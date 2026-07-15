using System;
using System.IO;
using System.Text.Json;

namespace RandomMagicConversion;

public static class StartingClassWeaponToMagicConfigLoader
{
    public static StartingClassWeaponToMagicConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin du fichier de config starting classes est vide.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier de configuration starting classes introuvable.", path);

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        StartingClassWeaponToMagicConfig config = JsonSerializer.Deserialize<StartingClassWeaponToMagicConfig>(json, options);

        if (config == null)
            throw new InvalidOperationException($"Impossible de deserialiser la configuration starting classes : {path}");

        return config;
    }
}
