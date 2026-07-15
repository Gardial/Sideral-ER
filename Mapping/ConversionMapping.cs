namespace RandomMagicConversion;

public sealed class ConversionMapping
{
    public int TargetId { get; set; }
    public int SourceId { get; set; }
    public ConversionKind Kind { get; set; }
    public int TargetTextRootId { get; set; }

    // Champs utiles pour logs/debug
    public string TargetCategory { get; set; } = string.Empty;
    public string SourceCategory { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{TargetId} <= {SourceId} ({Kind})";
    }
}
