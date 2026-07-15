using System;

namespace RandomMagicConversion;

public enum MagicProgressionBand
{
    All,
    EarlyOnly,
    EarlyMid,
    MidOnly,
    MidLate,
    LateOnly
}

internal static class MagicProgressionBandParser
{
    public static MagicProgressionBand Parse(string rawBand)
    {
        if (string.IsNullOrWhiteSpace(rawBand))
            return MagicProgressionBand.All;

        string normalized = rawBand
            .Trim()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

        return normalized switch
        {
            "all" => MagicProgressionBand.All,
            "full" => MagicProgressionBand.All,
            "any" => MagicProgressionBand.All,
            "earlyonly" => MagicProgressionBand.EarlyOnly,
            "early" => MagicProgressionBand.EarlyOnly,
            "earlymid" => MagicProgressionBand.EarlyMid,
            "earlytomid" => MagicProgressionBand.EarlyMid,
            "midonly" => MagicProgressionBand.MidOnly,
            "mid" => MagicProgressionBand.MidOnly,
            "midlate" => MagicProgressionBand.MidLate,
            "midtolate" => MagicProgressionBand.MidLate,
            "lateonly" => MagicProgressionBand.LateOnly,
            "late" => MagicProgressionBand.LateOnly,
            _ => throw new InvalidOperationException(
                $"Bande de progression inconnue : '{rawBand}'. Valeurs autorisees : All, EarlyOnly, EarlyMid, MidOnly, MidLate, LateOnly.")
        };
    }
}

