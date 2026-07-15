using System;
using System.Collections.Generic;

namespace RandomMagicConversion;

public sealed class ConversionRun
{
    public int Seed { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ConversionMapping> Mappings { get; set; } = new();

    public int Count => Mappings.Count;
}