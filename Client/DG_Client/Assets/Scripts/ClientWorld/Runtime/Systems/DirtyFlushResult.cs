using System.Collections.Generic;
using DG.GameCore;

namespace DG.Map
{
    public readonly struct DirtyFlushResult
    {
        public readonly IReadOnlyList<long> ChangedCells;
        public readonly IReadOnlyList<long> ChangedChunks;
        public readonly WorldDelta Delta;

        public DirtyFlushResult(IReadOnlyList<long> changedCells, IReadOnlyList<long> changedChunks, WorldDelta delta)
        {
            ChangedCells = changedCells;
            ChangedChunks = changedChunks;
            Delta = delta;
        }
    }
}
