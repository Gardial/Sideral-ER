namespace RandomMagicConversion;

internal static class MagicSelectionSeedMixer
{
    public static int Mix(int seed, int salt)
    {
        unchecked
        {
            return (seed * 397) ^ salt;
        }
    }
}
