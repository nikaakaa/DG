using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class Cell
{
    public Cell(GridCoord coord, GridCoord chunkCoord, GridCoord localCoord)
    {
        Coord = coord;
        ChunkCoord = chunkCoord;
        LocalCoord = localCoord;
    }

    public GridCoord Coord { get; }
    public GridCoord ChunkCoord { get; }
    public GridCoord LocalCoord { get; }
}
}

