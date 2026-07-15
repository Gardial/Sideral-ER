using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomMagicConversion;

public sealed class ConversionMappingBuilder
{
    private readonly Random _rng;

    public int Seed { get; }

    public ConversionMappingBuilder(int seed)
    {
        Seed = seed;
        _rng = new Random(seed);
    }

    public ConversionRun BuildShieldRun(
        IReadOnlyDictionary<int, IReadOnlyList<int>> targetShieldFamilies,
        IReadOnlyList<int> staffIds,
        IReadOnlyList<int> sealIds)
    {
        if (targetShieldFamilies == null || targetShieldFamilies.Count == 0)
            throw new InvalidOperationException("Aucune famille de boucliers cible.");

        if (staffIds == null || staffIds.Count == 0)
            throw new InvalidOperationException("Aucun staff source.");

        if (sealIds == null || sealIds.Count == 0)
            throw new InvalidOperationException("Aucun seal source.");

        var run = new ConversionRun
        {
            Seed = Seed
        };

        foreach (KeyValuePair<int, IReadOnlyList<int>> family in targetShieldFamilies
                     .OrderBy(pair => pair.Key))
        {
            if (family.Value == null || family.Value.Count == 0)
                continue;

            bool toStaff = _rng.Next(2) == 0;
            int sourceId = toStaff
                ? staffIds[_rng.Next(staffIds.Count)]
                : sealIds[_rng.Next(sealIds.Count)];
            ConversionKind kind = toStaff
                ? ConversionKind.ShieldToStaff
                : ConversionKind.ShieldToSeal;
            string sourceCategory = toStaff ? "Staff" : "Seal";

            foreach (int targetId in family.Value.OrderBy(id => id))
            {
                run.Mappings.Add(new ConversionMapping
                {
                    TargetId = targetId,
                    SourceId = sourceId,
                    Kind = kind,
                    TargetTextRootId = family.Key,
                    TargetCategory = "Shield",
                    SourceCategory = sourceCategory
                });
            }
        }

        return run;
    }

    public ConversionRun BuildSingleShieldRun(
        int targetShieldId,
        int sourceId,
        bool useStaff)
    {
        var run = new ConversionRun
        {
            Seed = Seed
        };

        run.Mappings.Add(new ConversionMapping
        {
            TargetId = targetShieldId,
            SourceId = sourceId,
            Kind = useStaff ? ConversionKind.ShieldToStaff : ConversionKind.ShieldToSeal,
            TargetTextRootId = targetShieldId,
            TargetCategory = "Shield",
            SourceCategory = useStaff ? "Staff" : "Seal"
        });

        return run;
    }
}
