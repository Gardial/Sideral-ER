using System;
using System.IO;
using System.Text.Json;

namespace RandomMagicConversion;

public static class FarmableEnemyLootSuppressionConfigLoader
{
    public static FarmableEnemyLootSuppressionConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin du fichier de config farmable enemy loot est vide.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier de configuration farmable enemy loot introuvable.", path);

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        FarmableEnemyLootSuppressionConfig config = JsonSerializer.Deserialize<FarmableEnemyLootSuppressionConfig>(json, options);

        if (config == null)
            throw new InvalidOperationException($"Impossible de deserialiser la configuration farmable enemy loot : {path}");

        return config;
    }
}
