using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomMagicConversion;

internal sealed class MagicShuffleBag
{
    private readonly List<int> _sourceIds;
    private readonly Random _rng;
    private List<int> _bag;
    private int _nextIndex;

    public MagicShuffleBag(IEnumerable<int> sourceIds, Random rng)
    {
        _sourceIds = sourceIds?.ToList() ?? throw new ArgumentNullException(nameof(sourceIds));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        if (_sourceIds.Count == 0)
            throw new InvalidOperationException("Impossible de construire un shuffle bag avec un pool vide.");

        _bag = new List<int>(_sourceIds.Count);
        Refill();
    }

    public int Next()
    {
        if (_nextIndex >= _bag.Count)
            Refill();

        return _bag[_nextIndex++];
    }

    private void Refill()
    {
        _bag = new List<int>(_sourceIds);

        for (int index = _bag.Count - 1; index > 0; index--)
        {
            int swapIndex = _rng.Next(index + 1);
            (_bag[index], _bag[swapIndex]) = (_bag[swapIndex], _bag[index]);
        }

        _nextIndex = 0;
    }
}
