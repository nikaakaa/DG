using System;
using System.Collections.Generic;
using System.Linq;
using DG.GameCore;

namespace DG.Map
{
    public sealed class DirtyFlushSystem : IClientSystem
    {
        public DirtyFlushResult LastFlush { get; private set; } = new DirtyFlushResult(Array.Empty<long>(), Array.Empty<long>(), new WorldDelta(0, Array.Empty<EntitySnapshot>()));

        public void Tick(ClientWorldContext context, float deltaTime)
        {
            List<long> changedCells = context.ClientMapWorld.ChangedCells.ToList();
            List<long> changedChunks = context.ClientMapWorld.ChangedChunks.ToList();
            WorldDelta delta = context.ClientMapWorld.FlushDelta();
            LastFlush = new DirtyFlushResult(changedCells, changedChunks, delta);
            context.ClientMapWorld.CoreWorld.ClearSpatialDirty();
        }
    }
}
