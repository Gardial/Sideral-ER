using System;

namespace RandomMagicConversion;

internal static class ShopMagicPoolModeParser
{
    public static ShopMagicPoolMode Parse(string rawMode)
    {
        if (string.IsNullOrWhiteSpace(rawMode))
            return ShopMagicPoolMode.Both;

        string normalized = rawMode
            .Trim()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

        return normalized switch
        {
            "both" => ShopMagicPoolMode.Both,
            "all" => ShopMagicPoolMode.Both,
            "mixed" => ShopMagicPoolMode.Both,
            "sorceryandincantation" => ShopMagicPoolMode.Both,
            "sorceriesandincantations" => ShopMagicPoolMode.Both,
            "sorceryonly" => ShopMagicPoolMode.SorceryOnly,
            "sorceriesonly" => ShopMagicPoolMode.SorceryOnly,
            "sorcery" => ShopMagicPoolMode.SorceryOnly,
            "incantationonly" => ShopMagicPoolMode.IncantationOnly,
            "incantationsonly" => ShopMagicPoolMode.IncantationOnly,
            "incantation" => ShopMagicPoolMode.IncantationOnly,
            _ => throw new InvalidOperationException(
                $"Mode de pool shop inconnu : '{rawMode}'. Valeurs autorisees : Both, SorceryOnly, IncantationOnly.")
        };
    }
}
