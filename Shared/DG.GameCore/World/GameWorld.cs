using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class GameWorld
{
    private readonly Dictionary<long, GameEntity> entities = new();
    private readonly Dictionary<Type, IComponentStore> componentStores = new();
    private readonly ChunkStore chunks = new();
    private readonly SpatialEntityIndex spatial = new();
    private readonly SpatialDirtyTracker spatialDirty = new();
    private readonly List<DirtyChange> dirtyChanges = new();
    private readonly List<long> removedEntityIds = new();
    private readonly IGameConfigProvider configProvider;

    public GameWorld() : this(FallbackGameConfigProvider.Instance)
    {
    }

    public GameWorld(IGameConfigProvider configProvider)
    {
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    }

    public long ServerTick { get; private set; }
    public int EntityCount => entities.Count;
    public IReadOnlyDictionary<long, Chunk> LoadedChunks => chunks.LoadedChunks;
    public IReadOnlyCollection<long> ChangedCells => spatialDirty.ChangedCells;
    public IReadOnlyCollection<long> ChangedChunks => spatialDirty.ChangedChunks;

    public long NextTick()
    {
        return ++ServerTick;
    }

    public bool AddEntity(GameEntity entity)
    {
        if (entities.ContainsKey(entity.EntityId))
        {
            return false;
        }

        entities.Add(entity.EntityId, entity);
        MarkDirty(entity.EntityId);
        return true;
    }

    public bool ApplySnapshot(EntitySnapshot snapshot)
    {
        if (snapshot.ServerTick > ServerTick)
        {
            ServerTick = snapshot.ServerTick;
        }

        return AddOrUpdateEntity(EntitySpawnSpec.FromSnapshot(snapshot));
    }

    public bool AddEntity(EntitySpawnSpec spawn)
    {
        return EntityBuilder.AddEntity(this, configProvider, spawn);
    }

    public bool AddOrUpdateEntity(EntitySpawnSpec spawn)
    {
        return EntityBuilder.AddOrUpdateEntity(this, configProvider, spawn);
    }

    public bool RemoveEntity(long entityId)
    {
        if (!entities.Remove(entityId, out GameEntity entity))
        {
            return false;
        }

        if (spatial.TryGetCoord(entity.EntityId, out GridCoord spatialCoord))
        {
            RemoveSpatial(entity.EntityId, spatialCoord);
        }

        foreach (IComponentStore store in componentStores.Values)
        {
            store.Remove(entityId);
        }

        MarkRemoved(entityId);
        return true;
    }

    public bool TryGetEntity(long entityId, out GameEntity entity)
    {
        if (entities.TryGetValue(entityId, out GameEntity found))
        {
            entity = found;
            return true;
        }

        entity = default!;
        return false;
    }

    public IReadOnlyList<GameEntity> EnumerateEntities()
    {
        return entities.Values.OrderBy(entity => entity.EntityId).ToArray();
    }

    public IReadOnlyList<GameEntity> GetEntitiesAt(GridCoord coord)
    {
        var result = new List<GameEntity>();
        IReadOnlyList<long> ids = spatial.GetEntitiesAt(coord);
        foreach (long entityId in ids)
        {
            if (entities.TryGetValue(entityId, out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        return result;
    }

    public IReadOnlyList<GameEntity> GetEntitiesAt(GridCoord coord, int target)
    {
        var result = new List<GameEntity>();
        IReadOnlyList<long> ids = spatial.GetEntitiesAt(coord, target);
        foreach (long entityId in ids)
        {
            if (entities.TryGetValue(entityId, out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        return result;
    }

    public bool TryGetChunkEntities(GridCoord chunkCoord, out IReadOnlyList<GameEntity> entitiesInChunk)
    {
        return TryGetChunkEntities(chunkCoord, SpatialEntityIndex.AllTarget, out entitiesInChunk);
    }

    public bool TryGetChunkEntities(GridCoord chunkCoord, int target, out IReadOnlyList<GameEntity> entitiesInChunk)
    {
        bool found = spatial.TryGetChunkEntities(chunkCoord, target, out IReadOnlyList<long> ids);
        var result = new List<GameEntity>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (entities.TryGetValue(ids[i], out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        entitiesInChunk = result;
        return found;
    }

    public IReadOnlyList<GameEntity> GetEntitiesAround(GridCoord centerWorldCoord, int cellRange, int target)
    {
        IReadOnlyList<long> ids = spatial.GetEntitiesAround(centerWorldCoord, cellRange, target);
        var result = new List<GameEntity>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (entities.TryGetValue(ids[i], out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        return result;
    }

    public SpatialVisibilityDelta DiffVisibility(GridCoord oldWorldCoord, GridCoord newWorldCoord, int cellRange, int target, long excludedEntityId = 0)
    {
        return spatial.DiffVisibility(oldWorldCoord, newWorldCoord, cellRange, target, excludedEntityId);
    }

    public Chunk GetOrCreateChunk(GridCoord chunkCoord)
    {
        return chunks.GetOrCreateChunk(chunkCoord);
    }

    public bool TryGetChunk(GridCoord chunkCoord, out Chunk chunk)
    {
        return chunks.TryGetChunk(chunkCoord, out chunk);
    }

    public bool HasChunk(GridCoord chunkCoord)
    {
        return chunks.HasChunk(chunkCoord);
    }

    public Cell GetOrCreateCell(GridCoord worldCoord)
    {
        return chunks.GetOrCreateCell(worldCoord);
    }

    public bool TryGetCell(GridCoord worldCoord, out Cell cell)
    {
        return chunks.TryGetCell(worldCoord, out cell);
    }

    public IReadOnlyList<Chunk> GetChunksAround(GridCoord centerWorldCoord, int radiusInChunks)
    {
        return chunks.GetChunksAround(centerWorldCoord, radiusInChunks);
    }

    public bool ApplyCellBatch(IReadOnlyList<GridCoord> cells)
    {
        if (cells == null)
        {
            return false;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            GetOrCreateCell(cells[i]);
        }

        for (int i = 0; i < cells.Count; i++)
        {
            spatialDirty.MarkCell(cells[i]);
        }

        return true;
    }

    public void ClearSpatialDirty()
    {
        spatialDirty.Clear();
    }

    public bool CanEnterNewEntity(GridCoord coord)
    {
        IReadOnlyList<GameEntity> targets = GetEntitiesAt(coord);
        for (int i = 0; i < targets.Count; i++)
        {
            if (HasComponent<BlockingComponent>(targets[i]))
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<DirtyChange> PeekDirtyChanges()
    {
        return dirtyChanges.ToArray();
    }

    public WorldDelta FlushDelta()
    {
        var snapshots = new List<EntitySnapshot>();
        var removed = new List<long>();
        var seen = new HashSet<long>();
        for (int i = 0; i < dirtyChanges.Count; i++)
        {
            long entityId = dirtyChanges[i].EntityId;
            if (!seen.Add(entityId))
            {
                continue;
            }

            if (entities.TryGetValue(entityId, out GameEntity entity))
            {
                snapshots.Add(CreateSnapshot(entity));
            }
        }

        for (int i = 0; i < removedEntityIds.Count; i++)
        {
            long entityId = removedEntityIds[i];
            if (seen.Add(entityId))
            {
                removed.Add(entityId);
            }
        }

        dirtyChanges.Clear();
        removedEntityIds.Clear();
        return new WorldDelta(ServerTick, snapshots, removed);
    }

    public IReadOnlyList<EntitySnapshot> CreateSnapshot()
    {
        var result = new List<EntitySnapshot>();
        foreach (GameEntity entity in entities.Values.OrderBy(entity => entity.EntityId))
        {
            result.Add(CreateSnapshot(entity));
        }

        return result;
    }

    public EntitySnapshot CreateSnapshot(GameEntity entity)
    {
        GridCoord coord = TryGetComponent(entity, out PositionComponent position) ? position.Coord : default;
        Direction direction = TryGetComponent(entity, out DirectionComponent directionComponent) ? directionComponent.Direction : Direction.None;
        return new EntitySnapshot(
            entity.EntityId,
            entity.ConfigId,
            entity.ArchetypeId,
            entity.EntityTarget,
            coord.X,
            coord.Y,
            direction,
            HasComponent<ColliderComponent>(entity),
            HasComponent<BlockingComponent>(entity),
            HasComponent<BouncableComponent>(entity),
            HasComponent<AutoMoveComponent>(entity),
            HasComponent<PlayerControlComponent>(entity),
            ServerTick);
    }

    public void MoveEntity(GameEntity entity, GridCoord target)
    {
        SetComponent(entity, new PositionComponent(target));
        MarkDirty(entity.EntityId);
    }

    public void SetDirection(GameEntity entity, Direction direction)
    {
        SetComponent(entity, new DirectionComponent(direction));
        MarkDirty(entity.EntityId);
    }

    public void SetAutoMove(GameEntity entity, AutoMoveComponent component)
    {
        SetComponent(entity, component);
    }

    public bool HasTag(GameEntity entity, WorldTag tag)
    {
        return TryGetComponent(entity, out TagSetComponent component) && component.Has(tag);
    }

    public void AddTag(GameEntity entity, WorldTag tag)
    {
        TagSetComponent component = TryGetComponent(entity, out TagSetComponent existing) ? existing : new TagSetComponent(WorldTag.None);
        SetComponent(entity, component.Add(tag));
    }

    public bool RemoveTag(GameEntity entity, WorldTag tag)
    {
        if (!TryGetComponent(entity, out TagSetComponent component) || !component.Has(tag))
        {
            return false;
        }

        SetComponent(entity, component.Remove(tag));
        return true;
    }

    public void SetComponent(GameEntity entity, PositionComponent component)
    {
        if (TryGetComponent(entity, out PositionComponent oldPosition) &&
            HasComponent<ColliderComponent>(entity))
        {
            RemoveSpatial(entity.EntityId, oldPosition.Coord);
        }

        GetStore<PositionComponent>().Set(entity.EntityId, component);
        GetOrCreateCell(component.Coord);

        if (HasComponent<ColliderComponent>(entity))
        {
            AddSpatial(entity.EntityId, component.Coord);
        }
    }

    public void SetComponent(GameEntity entity, ColliderComponent component)
    {
        bool hadComponent = HasComponent<ColliderComponent>(entity);
        GetStore<ColliderComponent>().Set(entity.EntityId, component);

        if (!hadComponent &&
            TryGetComponent(entity, out PositionComponent position))
        {
            AddSpatial(entity.EntityId, position.Coord);
        }
    }

    public void SetComponent<TComponent>(GameEntity entity, TComponent component) where TComponent : struct
    {
        GetStore<TComponent>().Set(entity.EntityId, component);
    }

    public bool HasComponent<TComponent>(GameEntity entity) where TComponent : struct
    {
        return TryGetStore<TComponent>(out ComponentStore<TComponent> store) && store.Has(entity.EntityId);
    }

    public bool TryGetComponent<TComponent>(GameEntity entity, out TComponent component) where TComponent : struct
    {
        if (TryGetStore<TComponent>(out ComponentStore<TComponent> store))
        {
            return store.TryGet(entity.EntityId, out component);
        }

        component = default;
        return false;
    }

    public TComponent GetComponent<TComponent>(GameEntity entity) where TComponent : struct
    {
        return GetStore<TComponent>().Get(entity.EntityId);
    }

    public bool RemoveComponent<TComponent>(GameEntity entity) where TComponent : struct
    {
        if (!TryGetStore<TComponent>(out ComponentStore<TComponent> store))
        {
            return false;
        }

        if (typeof(TComponent) == typeof(PositionComponent) &&
            TryGetComponent(entity, out PositionComponent oldPosition) &&
            HasComponent<ColliderComponent>(entity))
        {
            RemoveSpatial(entity.EntityId, oldPosition.Coord);
        }

        if (typeof(TComponent) == typeof(ColliderComponent) &&
            TryGetComponent(entity, out PositionComponent position))
        {
            RemoveSpatial(entity.EntityId, position.Coord);
        }

        bool removed = store.Remove(entity.EntityId);
        return removed;
    }

    public void MarkDirty(long entityId)
    {
        dirtyChanges.Add(new DirtyChange(entityId, ServerTick));
    }

    private void MarkRemoved(long entityId)
    {
        removedEntityIds.Add(entityId);
        dirtyChanges.RemoveAll(change => change.EntityId == entityId);
    }

    private void AddSpatial(long entityId, GridCoord coord)
    {
        if (entities.TryGetValue(entityId, out GameEntity entity))
        {
            if (spatial.HasEntity(entityId))
            {
                spatial.Move(entityId, coord, entity.EntityTarget);
            }
            else
            {
                spatial.Register(entityId, coord, entity.EntityTarget);
            }

            GetOrCreateCell(coord);
            spatialDirty.MarkCell(coord);
        }
    }

    private void RemoveSpatial(long entityId, GridCoord coord)
    {
        if (spatial.Unregister(entityId))
        {
            spatialDirty.MarkCell(coord);
        }
    }

    private ComponentStore<TComponent> GetStore<TComponent>() where TComponent : struct
    {
        Type type = typeof(TComponent);
        if (!componentStores.TryGetValue(type, out IComponentStore store))
        {
            var typedStore = new ComponentStore<TComponent>();
            componentStores.Add(type, typedStore);
            return typedStore;
        }

        return (ComponentStore<TComponent>)store;
    }

    private bool TryGetStore<TComponent>(out ComponentStore<TComponent> store) where TComponent : struct
    {
        if (componentStores.TryGetValue(typeof(TComponent), out IComponentStore found))
        {
            store = (ComponentStore<TComponent>)found;
            return true;
        }

        store = default!;
        return false;
    }
}
}

