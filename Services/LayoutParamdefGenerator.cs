using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace RandomMagicConversion;

internal static class LayoutParamdefGenerator
{
    public static string EnsureGeneratedParamdef(
        string defsDirectory,
        string outputFileName,
        string layoutPath,
        string paramType)
    {
        if (string.IsNullOrWhiteSpace(defsDirectory))
            throw new ArgumentException("Le dossier Defs est vide.", nameof(defsDirectory));

        if (string.IsNullOrWhiteSpace(outputFileName))
            throw new ArgumentException("Le nom de fichier de sortie est vide.", nameof(outputFileName));

        if (string.IsNullOrWhiteSpace(layoutPath))
            throw new ArgumentException("Le chemin du layout est vide.", nameof(layoutPath));

        if (string.IsNullOrWhiteSpace(paramType))
            throw new ArgumentException("Le ParamType est vide.", nameof(paramType));

        if (!Directory.Exists(defsDirectory))
            throw new DirectoryNotFoundException($"Defs introuvable : {defsDirectory}");

        if (!File.Exists(layoutPath))
            throw new FileNotFoundException("Layout introuvable.", layoutPath);

        string generatedDir = Path.Combine(defsDirectory, "Generated");
        Directory.CreateDirectory(generatedDir);

        string outputPath = Path.Combine(generatedDir, outputFileName);

        XDocument layout = XDocument.Load(layoutPath);

        XElement[] fields = layout.Root?
            .Elements("entry")
            .Select((entry, index) => CreateFieldElement(entry, index))
            .ToArray()
            ?? throw new InvalidOperationException($"Layout invalide : {layoutPath}");

        var paramdef = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("PARAMDEF",
                new XAttribute("XmlVersion", "2"),
                new XElement("ParamType", paramType),
                new XElement("DataVersion", "1"),
                new XElement("BigEndian", "False"),
                new XElement("Unicode", "True"),
                new XElement("FormatVersion", "203"),
                new XElement("Fields", fields)));

        paramdef.Save(outputPath);
        return outputPath;
    }

    private static XElement CreateFieldElement(XElement entry, int index)
    {
        string name = entry.Element("name")?.Value?.Trim();
        string type = entry.Element("type")?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            throw new InvalidOperationException("Entry de layout invalide : nom ou type manquant.");

        string sanitizedName = SanitizeFieldName(name);
        string def = type switch
        {
            "dummy8" => $"dummy8 {sanitizedName}[{ReadDummySize(entry)}]",
            "b8" => $"u8 {sanitizedName}:1",
            _ => $"{type} {sanitizedName}"
        };

        return new XElement("Field",
            new XAttribute("Def", def),
            new XElement("DisplayName", name),
            new XElement("Description", name),
            new XElement("EditFlags", "None"),
            new XElement("SortID", index + 1));
    }

    private static int ReadDummySize(XElement entry)
    {
        string raw = entry.Element("size")?.Value?.Trim();
        return int.TryParse(raw, out int size) && size > 0 ? size : 1;
    }

    private static string SanitizeFieldName(string name)
    {
        var builder = new StringBuilder(name.Length);

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                builder.Append(c);
            else
                builder.Append('_');
        }

        if (builder.Length == 0)
            builder.Append("field");

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
            builder.Insert(0, '_');

        return builder.ToString();
    }
}
