using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class SpatialEntityIndex
{
    public const int AllTarget = 0;

    private static readonly IReadOnlyList<long> EmptyEntities = Array.Empty<long>();
    private readonly Dictionary<long, GridCoord> coords = new();
    private readonly Dictionary<long, int> targets = new();
    private readonly Dictionary<int, TargetIndex> targetIndexes = new();

    public IReadOnlyDictionary<long, GridCoord> EntityCoords => coords;

    public bool HasEntity(long entityId)
    {
        return coords.ContainsKey(entityId);
    }

    public bool Register(long entityId, GridCoord coord, int target)
    {
        if (coords.ContainsKey(entityId))
        {
            return Move(entityId, coord, target);
        }

        coords.Add(entityId, coord);
        targets.Add(entityId, target);
        AddToIndexes(entityId, coord, target);
        return true;
    }

    public bool Move(long entityId, GridCoord targetCoord)
    {
        if (!targets.TryGetValue(entityId, out int target))
        {
            return false;
        }

        return Move(entityId, targetCoord, target);
    }

    public bool Move(long entityId, GridCoord targetCoord, int target)
    {
        if (!coords.TryGetValue(entityId, out GridCoord sourceCoord))
        {
            return false;
        }

        if (sourceCoord == targetCoord && targets.TryGetValue(entityId, out int oldTarget) && oldTarget == target)
        {
            return true;
        }

        RemoveFromIndexes(entityId, sourceCoord);
        coords[entityId] = targetCoord;
        targets[entityId] = target;
        AddToIndexes(entityId, targetCoord, target);
        return true;
    }

    public bool Unregister(long entityId)
    {
        if (!coords.TryGetValue(entityId, out GridCoord coord))
        {
            return false;
        }

        RemoveFromIndexes(entityId, coord);
        coords.Remove(entityId);
        targets.Remove(entityId);
        return true;
    }

    public bool TryGetCoord(long entityId, out GridCoord coord)
    {
        return coords.TryGetValue(entityId, out coord);
    }

    public IReadOnlyList<long> GetEntitiesAt(GridCoord coord)
    {
        return GetEntitiesAt(coord, AllTarget);
    }

    public IReadOnlyList<long> GetEntitiesAt(GridCoord coord, int target)
    {
        TargetIndex index = GetTargetIndex(target);
        if (index.CellEntities.TryGetValue(MapCoordinate.ToCellKey(coord), out List<long> entities))
        {
            return entities.ToArray();
        }

        return EmptyEntities;
    }

    public bool TryGetChunkEntities(GridCoord chunkCoord, out IReadOnlyList<long> entities)
    {
        return TryGetChunkEntities(chunkCoord, AllTarget, out entities);
    }

    public bool TryGetChunkEntities(GridCoord chunkCoord, int target, out IReadOnlyList<long> entities)
    {
        TargetIndex index = GetTargetIndex(target);
        if (index.ChunkEntities.TryGetValue(MapCoordinate.ToChunkKey(chunkCoord), out List<long> found))
        {
            entities = found.ToArray();
            return true;
        }

        entities = EmptyEntities;
        return false;
    }

    public IReadOnlyList<long> GetEntitiesAround(GridCoord centerWorldCoord, int cellRange, int target)
    {
        var result = new List<long>();
        CollectEntitiesInRect(centerWorldCoord, cellRange, target, 0, result);
        return result;
    }

    public SpatialVisibilityDelta DiffVisibility(GridCoord oldWorldCoord, GridCoord newWorldCoord, int cellRange, int target, long excludedEntityId = 0)
    {
        var oldVisibleIds = new HashSet<long>();
        var oldVisible = new List<long>();
        CollectEntitiesInRect(oldWorldCoord, cellRange, target, excludedEntityId, oldVisible);
        for (int i = 0; i < oldVisible.Count; i++)
        {
            oldVisibleIds.Add(oldVisible[i]);
        }

        var newVisibleIds = new HashSet<long>();
        var newVisible = new List<long>();
        CollectEntitiesInRect(newWorldCoord, cellRange, target, excludedEntityId, newVisible);
        for (int i = 0; i < newVisible.Count; i++)
        {
            newVisibleIds.Add(newVisible[i]);
        }

        var entered = new List<long>();
        for (int i = 0; i < newVisible.Count; i++)
        {
            long entityId = newVisible[i];
            if (!oldVisibleIds.Contains(entityId))
            {
                entered.Add(entityId);
            }
        }

        var left = new List<long>();
        for (int i = 0; i < oldVisible.Count; i++)
        {
            long entityId = oldVisible[i];
            if (!newVisibleIds.Contains(entityId))
            {
                left.Add(entityId);
            }
        }

        return new SpatialVisibilityDelta(entered, left);
    }

    private void CollectEntitiesInRect(GridCoord centerWorldCoord, int cellRange, int target, long excludedEntityId, List<long> result)
    {
        int minX = centerWorldCoord.X - cellRange;
        int maxX = centerWorldCoord.X + cellRange;
        int minY = centerWorldCoord.Y - cellRange;
        int maxY = centerWorldCoord.Y + cellRange;
        GridCoord minChunk = MapCoordinate.ToChunkCoord(new GridCoord(minX, minY));
        GridCoord maxChunk = MapCoordinate.ToChunkCoord(new GridCoord(maxX, maxY));
        TargetIndex index = GetTargetIndex(target);

        for (int chunkX = minChunk.X; chunkX <= maxChunk.X; chunkX++)
        {
            for (int chunkY = minChunk.Y; chunkY <= maxChunk.Y; chunkY++)
            {
                if (!index.ChunkEntities.TryGetValue(MapCoordinate.ToChunkKey(chunkX, chunkY), out List<long> entities))
                {
                    continue;
                }

                for (int i = 0; i < entities.Count; i++)
                {
                    long entityId = entities[i];
                    if (entityId == excludedEntityId)
                    {
                        continue;
                    }

                    GridCoord coord = coords[entityId];
                    if (coord.X >= minX && coord.X <= maxX && coord.Y >= minY && coord.Y <= maxY)
                    {
                        result.Add(entityId);
                    }
                }
            }
        }
    }

    private void AddToIndexes(long entityId, GridCoord coord, int target)
    {
        AddToTargetIndex(AllTarget, entityId, coord);
        if (target != AllTarget)
        {
            AddToTargetIndex(target, entityId, coord);
        }
    }

    private void RemoveFromIndexes(long entityId, GridCoord coord)
    {
        foreach (TargetIndex index in targetIndexes.Values)
        {
            index.Entities.Remove(entityId);
            RemoveFromIndex(index.CellEntities, MapCoordinate.ToCellKey(coord), entityId);
            RemoveFromIndex(index.ChunkEntities, MapCoordinate.ToChunkKey(MapCoordinate.ToChunkCoord(coord)), entityId);
        }
    }

    private void AddToTargetIndex(int target, long entityId, GridCoord coord)
    {
        TargetIndex index = GetTargetIndex(target);
        index.Entities.Add(entityId);
        AddToIndex(index.CellEntities, MapCoordinate.ToCellKey(coord), entityId);
        AddToIndex(index.ChunkEntities, MapCoordinate.ToChunkKey(MapCoordinate.ToChunkCoord(coord)), entityId);
    }

    private TargetIndex GetTargetIndex(int target)
    {
        if (!targetIndexes.TryGetValue(target, out TargetIndex index))
        {
            index = new TargetIndex();
            targetIndexes.Add(target, index);
        }

        return index;
    }

    private static void AddToIndex(Dictionary<long, List<long>> index, long key, long entityId)
    {
        if (!index.TryGetValue(key, out List<long> entities))
        {
            entities = new List<long>();
            index.Add(key, entities);
        }

        if (!entities.Contains(entityId))
        {
            entities.Add(entityId);
        }
    }

    private static void RemoveFromIndex(Dictionary<long, List<long>> index, long key, long entityId)
    {
        if (!index.TryGetValue(key, out List<long> entities))
        {
            return;
        }

        entities.Remove(entityId);
        if (entities.Count == 0)
        {
            index.Remove(key);
        }
    }

    private sealed class TargetIndex
    {
        public readonly HashSet<long> Entities = new();
        public readonly Dictionary<long, List<long>> CellEntities = new();
        public readonly Dictionary<long, List<long>> ChunkEntities = new();
    }
}
}

