using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class Chunk
{
    private readonly Dictionary<int, Cell> cells = new();

    public Chunk(GridCoord coord)
    {
        Coord = coord;
    }

    public GridCoord Coord { get; }

    public bool ContainsLocal(GridCoord localCoord)
    {
        return localCoord.X >= 0 && localCoord.X < MapCoordinate.ChunkSize && localCoord.Y >= 0 && localCoord.Y < MapCoordinate.ChunkSize;
    }

    public Cell GetCell(GridCoord localCoord)
    {
        int key = ToLocalKey(localCoord);
        if (cells.TryGetValue(key, out Cell cell))
        {
            return cell;
        }

        cell = new Cell(MapCoordinate.ToWorldCoord(Coord, localCoord), Coord, localCoord);
        cells.Add(key, cell);
        return cell;
    }

    public bool TryGetCell(GridCoord localCoord, out Cell cell)
    {
        if (!ContainsLocal(localCoord))
        {
            cell = default!;
            return false;
        }

        cell = GetCell(localCoord);
        return true;
    }

    private static int ToLocalKey(GridCoord localCoord)
    {
        return localCoord.Y * MapCoordinate.ChunkSize + localCoord.X;
    }
}
}

