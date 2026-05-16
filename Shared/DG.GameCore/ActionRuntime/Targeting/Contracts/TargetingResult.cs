using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct TargetingResult
{
    public TargetingResult(IReadOnlyList<ActionTargetData> targetData, Direction direction, GridCoord primaryTargetCoord)
    {
        TargetData = targetData == null ? Array.Empty<ActionTargetData>() : targetData.ToArray();
        Direction = direction;
        PrimaryTargetCoord = primaryTargetCoord;
    }

    public IReadOnlyList<ActionTargetData> TargetData { get; }
    public Direction Direction { get; }
    public GridCoord PrimaryTargetCoord { get; }
}
}
