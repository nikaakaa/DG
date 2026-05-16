using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class ChunkStore
{
    private readonly Dictionary<long, Chunk> chunks = new();

    public IReadOnlyDictionary<long, Chunk> LoadedChunks => chunks;

    public Chunk GetOrCreateChunk(GridCoord chunkCoord)
    {
        long key = MapCoordinate.ToChunkKey(chunkCoord);
        if (chunks.TryGetValue(key, out Chunk chunk))
        {
            return chunk;
        }

        chunk = new Chunk(chunkCoord);
        chunks.Add(key, chunk);
        return chunk;
    }

    public bool TryGetChunk(GridCoord chunkCoord, out Chunk chunk)
    {
        if (chunks.TryGetValue(MapCoordinate.ToChunkKey(chunkCoord), out Chunk found))
        {
            chunk = found;
            return true;
        }

        chunk = default!;
        return false;
    }

    public bool HasChunk(GridCoord chunkCoord)
    {
        return chunks.ContainsKey(MapCoordinate.ToChunkKey(chunkCoord));
    }

    public Cell GetOrCreateCell(GridCoord worldCoord)
    {
        GridCoord chunkCoord = MapCoordinate.ToChunkCoord(worldCoord);
        GridCoord localCoord = MapCoordinate.ToLocalCoord(worldCoord);
        return GetOrCreateChunk(chunkCoord).GetCell(localCoord);
    }

    public bool TryGetCell(GridCoord worldCoord, out Cell cell)
    {
        GridCoord chunkCoord = MapCoordinate.ToChunkCoord(worldCoord);
        GridCoord localCoord = MapCoordinate.ToLocalCoord(worldCoord);
        if (!TryGetChunk(chunkCoord, out Chunk chunk))
        {
            cell = default!;
            return false;
        }

        return chunk.TryGetCell(localCoord, out cell);
    }

    public IReadOnlyList<Chunk> GetChunksAround(GridCoord centerWorldCoord, int radiusInChunks)
    {
        GridCoord centerChunkCoord = MapCoordinate.ToChunkCoord(centerWorldCoord);
        var result = new List<Chunk>();
        for (int x = -radiusInChunks; x <= radiusInChunks; x++)
        {
            for (int y = -radiusInChunks; y <= radiusInChunks; y++)
            {
                result.Add(GetOrCreateChunk(new GridCoord(centerChunkCoord.X + x, centerChunkCoord.Y + y)));
            }
        }

        return result;
    }
}
}

