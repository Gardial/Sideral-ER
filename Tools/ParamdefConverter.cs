using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SoulsFormats;

namespace RandomMagicConversion;

public sealed class ParamdefConverter
{
    private readonly string _defsFolder;

    public ParamdefConverter(string defsFolder)
    {
        if (string.IsNullOrWhiteSpace(defsFolder))
            throw new ArgumentException("Le dossier Defs ne peut pas être vide.", nameof(defsFolder));

        _defsFolder = defsFolder;
    }

    public List<object> LoadAll()
    {
        if (!Directory.Exists(_defsFolder))
            throw new DirectoryNotFoundException($"Dossier Defs introuvable : {_defsFolder}");

        Type paramdefType = ResolveParamdefType();

        MethodInfo xmlDeserialize = paramdefType.GetMethod(
            "XmlDeserialize",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);

        if (xmlDeserialize == null)
            throw new InvalidOperationException("Méthode PARAMDEF.XmlDeserialize(string) introuvable.");

        var defs = new List<object>();

        foreach (string path in Directory.GetFiles(_defsFolder, "*.xml"))
        {
            object def = xmlDeserialize.Invoke(null, new object[] { path });
            if (def != null)
                defs.Add(def);
        }

        if (defs.Count == 0)
            throw new InvalidOperationException("Aucun fichier XML de paramdef trouvé.");

        return defs;
    }

    public object LoadByParamType(string paramType)
    {
        if (string.IsNullOrWhiteSpace(paramType))
            throw new ArgumentException("paramType vide.", nameof(paramType));

        foreach (object def in LoadAll())
        {
            PropertyInfo prop = def.GetType().GetProperty("ParamType", BindingFlags.Public | BindingFlags.Instance);
            string value = prop?.GetValue(def)?.ToString();

            if (string.Equals(value, paramType, StringComparison.OrdinalIgnoreCase))
                return def;
        }

        throw new InvalidOperationException($"Aucun PARAMDEF trouvé pour {paramType}.");
    }

    public PARAM ApplyParamdef(PARAM param)
    {
        foreach (object def in LoadAll())
        {
            if (TryApplyParamdefCarefully(param, def))
                return param;
        }

        throw new InvalidOperationException(
            $"Impossible d'appliquer un PARAMDEF à ce param.");
    }

    private static bool TryApplyParamdefCarefully(PARAM param, object def)
    {
        foreach (MethodInfo method in typeof(PARAM).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.Name != "ApplyParamdefCarefully")
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1)
                continue;

            Type parameterType = parameters[0].ParameterType;
            if (!parameterType.IsAssignableFrom(def.GetType()))
                continue;

            object result = method.Invoke(param, new[] { def });
            if (result is bool applied)
                return applied;
        }

        return false;
    }

    private static Type ResolveParamdefType()
    {
        Assembly asm = typeof(PARAM).Assembly;

        Type type = asm.GetType("SoulsFormats.PARAMDEF", throwOnError: false);
        if (type != null)
            return type;

        foreach (Type t in asm.GetTypes())
        {
            if (t.Name == "PARAMDEF")
                return t;
        }

        throw new InvalidOperationException("Type PARAMDEF introuvable dans SoulsFormats.dll.");
    }
}