using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class CommitResolveContext
{
    public CommitResolveContext(HashSet<long> movedEntities, HashSet<GridCoord> occupiedTargets, IReadOnlyDictionary<long, HashSet<long>> moveGroups)
    {
        MovedEntities = movedEntities;
        OccupiedTargets = occupiedTargets;
        MoveGroups = moveGroups;
    }

    public HashSet<long> MovedEntities { get; }
    public HashSet<GridCoord> OccupiedTargets { get; }
    public IReadOnlyDictionary<long, HashSet<long>> MoveGroups { get; }
}
}
