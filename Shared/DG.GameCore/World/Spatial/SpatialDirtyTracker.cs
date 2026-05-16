using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class SpatialDirtyTracker
{
    private readonly HashSet<long> dirtyCells = new();
    private readonly HashSet<long> dirtyChunks = new();

    public IReadOnlyCollection<long> ChangedCells => dirtyCells;
    public IReadOnlyCollection<long> ChangedChunks => dirtyChunks;

    public void MarkCell(GridCoord worldCoord)
    {
        dirtyCells.Add(MapCoordinate.ToCellKey(worldCoord));
        dirtyChunks.Add(MapCoordinate.ToChunkKey(MapCoordinate.ToChunkCoord(worldCoord)));
    }

    public void MarkChunk(GridCoord chunkCoord)
    {
        dirtyChunks.Add(MapCoordinate.ToChunkKey(chunkCoord));
    }

    public void Clear()
    {
        dirtyCells.Clear();
        dirtyChunks.Clear();
    }
}
}

