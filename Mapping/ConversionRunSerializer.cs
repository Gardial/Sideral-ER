using System;
using System.IO;
using System.Text.Json;

namespace RandomMagicConversion;

public static class ConversionRunSerializer
{
    public static void Save(string path, ConversionRun run)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin de sauvegarde est vide.", nameof(path));

        if (run == null)
            throw new ArgumentNullException(nameof(run));

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(run, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }

    public static ConversionRun Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Le chemin de chargement est vide.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException("Fichier de mapping introuvable.", path);

        string json = File.ReadAllText(path);

        ConversionRun run = JsonSerializer.Deserialize<ConversionRun>(json);
        if (run == null)
            throw new InvalidOperationException("Impossible de désérialiser le ConversionRun.");

        return run;
    }
}