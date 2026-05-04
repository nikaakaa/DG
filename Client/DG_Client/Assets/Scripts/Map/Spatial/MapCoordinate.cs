using UnityEngine;
using CoreMapCoordinate = DG.GameCore.MapCoordinate;
using GridCoord = DG.GameCore.GridCoord;

namespace DG.Map
{
    public static class MapCoordinate
    {
        public const int ChunkSize = CoreMapCoordinate.ChunkSize;

        public static long ToChunkKey(Vector2Int chunkCoord)
        {
            return CoreMapCoordinate.ToChunkKey(chunkCoord.x, chunkCoord.y);
        }

        public static long ToChunkKey(int chunkX, int chunkY)
        {
            return CoreMapCoordinate.ToChunkKey(chunkX, chunkY);
        }

        public static long ToCellKey(Vector2Int worldCoord)
        {
            return CoreMapCoordinate.ToCellKey(worldCoord.x, worldCoord.y);
        }

        public static long ToCellKey(int worldX, int worldY)
        {
            return CoreMapCoordinate.ToCellKey(worldX, worldY);
        }

        public static Vector2Int ToChunkCoord(Vector2Int worldCoord)
        {
            return ToVector2Int(CoreMapCoordinate.ToChunkCoord(ToGridCoord(worldCoord)));
        }

        public static Vector2Int ToLocalCoord(Vector2Int worldCoord)
        {
            return ToVector2Int(CoreMapCoordinate.ToLocalCoord(ToGridCoord(worldCoord)));
        }

        public static Vector2Int ToWorldCoord(Vector2Int chunkCoord, Vector2Int localCoord)
        {
            return ToVector2Int(CoreMapCoordinate.ToWorldCoord(ToGridCoord(chunkCoord), ToGridCoord(localCoord)));
        }

        public static GridCoord ToGridCoord(Vector2Int coord)
        {
            return new GridCoord(coord.x, coord.y);
        }

        public static Vector2Int ToVector2Int(GridCoord coord)
        {
            return new Vector2Int(coord.X, coord.Y);
        }
    }
}


