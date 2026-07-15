namespace RandomMagicConversion;

internal static class VersionInfo
{
    public const string ProductName = "SIDERAL";
    public const string Version = "1.0.3";
    public const string DisplayVersion = "V1.0.3";
    public const string ReleaseDate = "2026-07-14";

    public static string BuildWindowTitle(string localizedTitle)
    {
        return $"{localizedTitle} - {DisplayVersion}";
    }
}
