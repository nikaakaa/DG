using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public static class MapCoordinate
{
    public const int ChunkSize = 32;

    public static long ToChunkKey(GridCoord chunkCoord)
    {
        return ToKey(chunkCoord.X, chunkCoord.Y);
    }

    public static long ToChunkKey(int chunkX, int chunkY)
    {
        return ToKey(chunkX, chunkY);
    }

    public static long ToCellKey(GridCoord worldCoord)
    {
        return ToKey(worldCoord.X, worldCoord.Y);
    }

    public static long ToCellKey(int worldX, int worldY)
    {
        return ToKey(worldX, worldY);
    }

    public static GridCoord ToChunkCoord(GridCoord worldCoord)
    {
        return new GridCoord(FloorDiv(worldCoord.X, ChunkSize), FloorDiv(worldCoord.Y, ChunkSize));
    }

    public static GridCoord ToLocalCoord(GridCoord worldCoord)
    {
        return new GridCoord(FloorMod(worldCoord.X, ChunkSize), FloorMod(worldCoord.Y, ChunkSize));
    }

    public static GridCoord ToWorldCoord(GridCoord chunkCoord, GridCoord localCoord)
    {
        return new GridCoord(chunkCoord.X * ChunkSize + localCoord.X, chunkCoord.Y * ChunkSize + localCoord.Y);
    }

    private static int FloorDiv(int value, int size)
    {
        int result = value / size;
        int remainder = value % size;
        if (remainder != 0 && value < 0)
        {
            result--;
        }

        return result;
    }

    private static int FloorMod(int value, int size)
    {
        int result = value % size;
        if (result < 0)
        {
            result += size;
        }

        return result;
    }

    private static long ToKey(int x, int y)
    {
        return ((long)x << 32) | (uint)y;
    }
}
}

