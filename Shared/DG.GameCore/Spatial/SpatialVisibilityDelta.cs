using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct SpatialVisibilityDelta
{
    public SpatialVisibilityDelta(IReadOnlyList<long> entered, IReadOnlyList<long> left)
    {
        Entered = entered;
        Left = left;
    }

    public IReadOnlyList<long> Entered { get; }
    public IReadOnlyList<long> Left { get; }
}
}

