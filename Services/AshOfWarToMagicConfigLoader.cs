using System;
using System.IO;
using System.Text.Json;

namespace RandomMagicConversion;

public static class AshOfWarToMagicConfigLoader
{
    public static AshOfWarToMagicConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin du fichier de config Ashes of War est vide.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier de configuration Ashes of War introuvable.", path);

        string json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        AshOfWarToMagicConfig config = JsonSerializer.Deserialize<AshOfWarToMagicConfig>(json, options);

        if (config == null)
            throw new InvalidOperationException($"Impossible de deserialiser la configuration Ashes of War : {path}");

        return config;
    }
}
