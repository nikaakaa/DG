using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DG.GameCore
{
public sealed class GameWorld
{
    private readonly IWorldDataStorage storage;
    private readonly ChunkStore chunks = new();
    private readonly SpatialEntityIndex spatial = new();
    private readonly EntityLocationStore entityLocations = new();
    private readonly SpatialDirtyTracker spatialDirty = new();
    private readonly DirtyWorldJournal dirtyJournal = new();
    private readonly IGameConfigProvider configProvider;
    private readonly RuntimeEffectStore runtimeEffects = new();
    private readonly ComponentStateResolver componentStateResolver = new();
    private readonly SnapshotComponentRegistry snapshotComponents = SnapshotComponentRegistry.Default;
    private readonly GameWorldObservationCounters observations = new();

    public GameWorld() : this(LubanGameConfigProvider.FromDirectory(GameCoreConfigPath.FindGeneratedJsonDirectory()), new IndexedWorldDataStorage())
    {
    }

    public GameWorld(IGameConfigProvider configProvider) : this(configProvider, new IndexedWorldDataStorage())
    {
    }

    public static GameWorld CreateIndexed(IGameConfigProvider configProvider)
    {
        return new GameWorld(configProvider, new IndexedWorldDataStorage());
    }

    public static GameWorld CreateArchBacked(IGameConfigProvider configProvider)
    {
#if UNITY_5_3_OR_NEWER
        return new GameWorld(configProvider, new IndexedWorldDataStorage());
#else
        return new GameWorld(configProvider, new ArchWorldDataStorage());
#endif
    }

    public static GameWorld CreateArchBacked()
    {
        return CreateArchBacked(LubanGameConfigProvider.FromDirectory(GameCoreConfigPath.FindGeneratedJsonDirectory()));
    }

    internal GameWorld(IGameConfigProvider configProvider, IWorldDataStorage storage)
    {
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public long ServerTick { get; private set; }
    public int EntityCount => storage.EntityCount;
    public IReadOnlyDictionary<long, Chunk> LoadedChunks => chunks.LoadedChunks;
    public IReadOnlyCollection<long> ChangedCells => spatialDirty.ChangedCells;
    public IReadOnlyCollection<long> ChangedChunks => spatialDirty.ChangedChunks;
    public RuntimeEffectStore RuntimeEffects => runtimeEffects;
    public GameWorldObservation Observations => observations.Snapshot();

    public void ResetObservations()
    {
        observations.Reset();
    }

    public long NextTick()
    {
        return ++ServerTick;
    }

    public bool AddEntity(GameEntity entity)
    {
        if (!storage.AddEntity(entity))
        {
            return false;
        }

        MarkDirty(entity.EntityId);
        entityLocations.Register(entity.EntityId, entity.EntityTarget);
        return true;
    }

    public bool ApplySnapshot(EntitySnapshot snapshot)
    {
        if (snapshot.ServerTick > ServerTick)
        {
            ServerTick = snapshot.ServerTick;
        }

        if (!AddOrUpdateEntity(EntitySpawnSpec.FromSnapshot(snapshot)) ||
            !TryGetEntity(snapshot.EntityId, out GameEntity entity))
        {
            return false;
        }

        componentStateResolver.RemoveEntity(snapshot.EntityId);
        snapshotComponents.Apply(this, entity, snapshot);
        MarkDirty(snapshot.EntityId);
        return true;
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
        if (!storage.RemoveEntity(entityId, out GameEntity entity))
        {
            return false;
        }

        if (spatial.TryGetCoord(entity.EntityId, out GridCoord spatialCoord))
        {
            RemoveSpatial(entity.EntityId, spatialCoord);
        }

        componentStateResolver.RemoveEntity(entityId);
        entityLocations.Remove(entityId);
        MarkRemoved(entityId);
        return true;
    }

    public bool TryGetEntity(long entityId, out GameEntity entity)
    {
        if (storage.TryGetEntity(entityId, out GameEntity found))
        {
            entity = found;
            return true;
        }

        entity = default!;
        return false;
    }

    public IReadOnlyList<GameEntity> EnumerateEntities()
    {
        return EnumerateEntities(EntityIterationOrder.EntityId);
    }

    public IReadOnlyList<GameEntity> EnumerateEntities(EntityIterationOrder order)
    {
        observations.EntityEnumerationCount++;
        if (order == EntityIterationOrder.EntityId)
        {
            observations.EntityEnumerationSortCount++;
        }

        return storage.EnumerateEntities(order);
    }

    public bool TryGetEntityLocation(long entityId, out EntityLocation location)
    {
        if (!storage.TryGetEntity(entityId, out _))
        {
            location = default;
            return false;
        }

        return entityLocations.TryGet(entityId, out location);
    }

    public IReadOnlyList<GameEntity> QueryEntities(ComponentQueryDescriptor descriptor, EntityIterationOrder order = EntityIterationOrder.Unordered)
    {
        observations.ComponentQueryCount++;
        IReadOnlyList<Type> componentTypes = descriptor.ComponentTypes;
        if (componentTypes.Count == 0)
        {
            return EnumerateEntities(order);
        }

        if (order == EntityIterationOrder.EntityId)
        {
            observations.EntityEnumerationSortCount++;
        }

        return storage.QueryEntities(componentTypes, order);
    }

    public IReadOnlyList<AutoMoveQueryResult> QueryAutoMove(EntityIterationOrder order = EntityIterationOrder.EntityId)
    {
        return QueryAutoMoveSources(order);
    }

    public IReadOnlyList<AutoMoveQueryResult> QueryAutoMoveSources(EntityIterationOrder order = EntityIterationOrder.EntityId)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<GameEntity> entities = QueryEntities(ComponentQueryDescriptor.With<PositionComponent, DirectionComponent, AutoMoveComponent>(), order);
        var result = new List<AutoMoveQueryResult>(entities.Count);
        for (int i = 0; i < entities.Count; i++)
        {
            GameEntity entity = entities[i];
            if (storage.TryGetComponent(entity.EntityId, out PositionComponent position) &&
                storage.TryGetComponent(entity.EntityId, out DirectionComponent direction) &&
                storage.TryGetComponent(entity.EntityId, out AutoMoveComponent autoMove))
            {
                result.Add(new AutoMoveQueryResult(entity.EntityId, position.Coord, direction.Direction, autoMove));
            }
        }

        stopwatch.Stop();
        observations.QueryTimeTicks += stopwatch.ElapsedTicks;
        return result;
    }

    public IReadOnlyList<PushOnEnterQueryResult> QueryPushOnEnter(EntityIterationOrder order = EntityIterationOrder.EntityId)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        IReadOnlyList<GameEntity> entities = QueryEntities(ComponentQueryDescriptor.With<PositionComponent, DirectionComponent, PushOnEnterComponent>(), order);
        var result = new List<PushOnEnterQueryResult>(entities.Count);
        for (int i = 0; i < entities.Count; i++)
        {
            GameEntity entity = entities[i];
            if (storage.TryGetComponent(entity.EntityId, out PositionComponent position) &&
                storage.TryGetComponent(entity.EntityId, out DirectionComponent direction) &&
                storage.TryGetComponent(entity.EntityId, out PushOnEnterComponent pushOnEnter))
            {
                result.Add(new PushOnEnterQueryResult(entity.EntityId, position.Coord, direction.Direction, pushOnEnter));
            }
        }

        stopwatch.Stop();
        observations.QueryTimeTicks += stopwatch.ElapsedTicks;
        return result;
    }

    public IReadOnlyList<AutoMoveActionCandidate> CollectAutoMoveCandidates(long serverTick, CandidateScanMode mode)
    {
        IReadOnlyList<AutoMoveQueryResult> sources = QueryAutoMoveSources(EntityIterationOrder.EntityId);
        AutoMoveActionCandidate[] candidates = mode == CandidateScanMode.Parallel
            ? sources.AsParallel().Where(source => IsAutoMoveReady(source, serverTick)).Select(source => CreateAutoMoveCandidate(source, serverTick)).ToArray()
            : sources.Where(source => IsAutoMoveReady(source, serverTick)).Select(source => CreateAutoMoveCandidate(source, serverTick)).ToArray();
        Array.Sort(candidates, static (left, right) =>
        {
            int ready = left.ReadyTick.CompareTo(right.ReadyTick);
            return ready != 0 ? ready : left.EntityId.CompareTo(right.EntityId);
        });
        observations.CandidateCount += candidates.Length;
        return candidates;
    }

    public IReadOnlyList<PushOnEnterActionCandidate> CollectPushOnEnterCandidates(long serverTick, CandidateScanMode mode)
    {
        IReadOnlyList<PushOnEnterQueryResult> triggers = QueryPushOnEnter(EntityIterationOrder.EntityId);
        IEnumerable<PushOnEnterActionCandidate> query = mode == CandidateScanMode.Parallel
            ? triggers.AsParallel().SelectMany(trigger => BuildPushOnEnterCandidates(trigger, serverTick))
            : triggers.SelectMany(trigger => BuildPushOnEnterCandidates(trigger, serverTick));
        PushOnEnterActionCandidate[] candidates = query.ToArray();
        Array.Sort(candidates, static (left, right) =>
        {
            int created = left.CreatedTick.CompareTo(right.CreatedTick);
            if (created != 0)
            {
                return created;
            }

            int source = left.SourceEntityId.CompareTo(right.SourceEntityId);
            return source != 0 ? source : left.SubjectEntityId.CompareTo(right.SubjectEntityId);
        });
        observations.CandidateCount += candidates.Length;
        return candidates;
    }

    public IReadOnlyList<RuntimeEffectId> CollectExpiredRuntimeEffectCandidates(long tick, CandidateScanMode mode)
    {
        IReadOnlyList<RuntimeEffectId> expired = runtimeEffects.CollectExpired(tick, mode);
        observations.CandidateCount += expired.Count;
        return expired;
    }

    public void RecordCandidateCommits(int count)
    {
        observations.CommitCount += Math.Max(0, count);
    }

    public IReadOnlyList<GameEntity> GetEntitiesAt(GridCoord coord)
    {
        observations.SpatialQueryCount++;
        var result = new List<GameEntity>();
        IReadOnlyList<long> ids = spatial.GetEntitiesAt(coord);
        foreach (long entityId in ids)
        {
            if (storage.TryGetEntity(entityId, out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        return result;
    }

    public IReadOnlyList<GameEntity> GetEntitiesAt(GridCoord coord, int target)
    {
        observations.SpatialQueryCount++;
        var result = new List<GameEntity>();
        IReadOnlyList<long> ids = spatial.GetEntitiesAt(coord, target);
        foreach (long entityId in ids)
        {
            if (storage.TryGetEntity(entityId, out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        return result;
    }

    public bool TryGetFirstBlockingAt(GridCoord coord, HashSet<long> excludedEntityIds, out BlockingSpatialQueryResult result)
    {
        observations.SpatialQueryCount++;
        IReadOnlyList<long> ids = spatial.GetEntitiesAt(coord);
        for (int i = 0; i < ids.Count; i++)
        {
            long entityId = ids[i];
            if ((excludedEntityIds != null && excludedEntityIds.Contains(entityId)) ||
                !storage.TryGetEntity(entityId, out GameEntity entity) ||
                !storage.HasComponent<BlockingComponent>(entityId) ||
                !storage.TryGetComponent(entityId, out PositionComponent position))
            {
                continue;
            }

            result = new BlockingSpatialQueryResult(entityId, position.Coord, entity.EntityTarget);
            return true;
        }

        result = default;
        return false;
    }

    public IReadOnlyList<ColliderSpatialQueryResult> GetColliderEntitiesAt(GridCoord coord, int target)
    {
        observations.SpatialQueryCount++;
        IReadOnlyList<long> ids = spatial.GetEntitiesAt(coord, target);
        var result = new List<ColliderSpatialQueryResult>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            long entityId = ids[i];
            if (storage.TryGetEntity(entityId, out GameEntity entity) &&
                storage.HasComponent<ColliderComponent>(entityId) &&
                storage.TryGetComponent(entityId, out PositionComponent position))
            {
                result.Add(new ColliderSpatialQueryResult(entityId, position.Coord, entity.EntityTarget));
            }
        }

        return result;
    }

    public IReadOnlyList<PushableSpatialQueryResult> GetPushableEntitiesAt(GridCoord coord, int target)
    {
        observations.SpatialQueryCount++;
        IReadOnlyList<long> ids = spatial.GetEntitiesAt(coord, target);
        var result = new List<PushableSpatialQueryResult>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            long entityId = ids[i];
            if (storage.TryGetEntity(entityId, out GameEntity entity) &&
                storage.HasComponent<PushableComponent>(entityId) &&
                storage.TryGetComponent(entityId, out PositionComponent position))
            {
                result.Add(new PushableSpatialQueryResult(entityId, position.Coord, entity.EntityTarget));
            }
        }

        return result;
    }

    public IReadOnlyList<GameEntity> GetPositionedEntitiesAt(GridCoord coord)
    {
        observations.SpatialQueryCount++;
        IReadOnlyList<long> ids = spatial.GetEntitiesAt(coord);
        var result = new List<GameEntity>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            if (storage.TryGetEntity(ids[i], out GameEntity entity) &&
                storage.HasComponent<PositionComponent>(entity.EntityId))
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
        observations.SpatialQueryCount++;
        bool found = spatial.TryGetChunkEntities(chunkCoord, target, out IReadOnlyList<long> ids);
        var result = new List<GameEntity>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (storage.TryGetEntity(ids[i], out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        entitiesInChunk = result;
        return found;
    }

    public IReadOnlyList<GameEntity> GetEntitiesAround(GridCoord centerWorldCoord, int cellRange, int target)
    {
        observations.SpatialQueryCount++;
        IReadOnlyList<long> ids = spatial.GetEntitiesAround(centerWorldCoord, cellRange, target);
        var result = new List<GameEntity>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (storage.TryGetEntity(ids[i], out GameEntity entity))
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
        return !TryGetFirstBlockingAt(coord, null!, out _);
    }

    public void CaptureStaticComponentSources(GameEntity entity)
    {
        componentStateResolver.CaptureStaticSources(this, entity);
    }

    public void AddStaticComponentSource(ComponentSourceContribution contribution)
    {
        componentStateResolver.AddStaticSource(contribution);
    }

    public void AddRuntimeComponentSource(ComponentSourceContribution contribution)
    {
        componentStateResolver.AddRuntimeSource(contribution);
    }

    public bool RemoveRuntimeComponentSource(long entityId, ComponentSourceKey source)
    {
        return componentStateResolver.RemoveRuntimeSource(entityId, source);
    }

    public void AddStaticTagSource(long entityId, ComponentSourceKey source, WorldTag tag)
    {
        componentStateResolver.AddStaticTagSource(entityId, source, tag);
    }

    public void AddRuntimeTagSource(long entityId, ComponentSourceKey source, WorldTag tag)
    {
        componentStateResolver.AddRuntimeTagSource(entityId, source, tag);
    }

    public bool RemoveRuntimeTagSource(long entityId, ComponentSourceKey source, WorldTag tag)
    {
        return componentStateResolver.RemoveRuntimeTagSource(entityId, source, tag);
    }

    public bool RemoveEntityRuntimeSources(long entityId)
    {
        bool removed = componentStateResolver.RemoveRuntimeSources(entityId);
        removed |= runtimeEffects.RemoveByTarget(entityId).Count > 0;
        if (removed)
        {
            ResolveComponentResults();
        }

        return removed;
    }

    public void ResolveComponentResults()
    {
        componentStateResolver.Resolve(this, runtimeEffects, ServerTick);
    }

    internal RuntimeEffectInstance AddRuntimeEffectSource(RuntimeEffectSpec spec)
    {
        return runtimeEffects.Add(spec);
    }

    internal bool RemoveRuntimeEffectSource(RuntimeEffectId id)
    {
        return runtimeEffects.Remove(id);
    }

    public IReadOnlyList<RuntimeEffectId> ExpireRuntimeEffects(long tick)
    {
        IReadOnlyList<RuntimeEffectId> expired = runtimeEffects.Expire(tick);
        if (expired.Count > 0)
        {
            ResolveComponentResults();
        }

        return expired;
    }

    public IReadOnlyList<DirtyChange> PeekDirtyChanges()
    {
        return dirtyJournal.PeekChanges();
    }

    public void RecordTouchedDiagnostics(IReadOnlyCollection<long> entityIds)
    {
        if (entityIds != null)
        {
            observations.TouchedDiagnosticCount += entityIds.Count;
        }
    }

    public void AddPresentationFact(PresentationFact presentationFact)
    {
        dirtyJournal.AddPresentationFact(presentationFact);
    }

    public WorldDelta FlushDelta()
    {
        observations.DeltaFlushCount++;
        observations.DeltaMaterializationCount++;
        var snapshots = new List<EntitySnapshot>();
        var removed = new List<long>();
        IReadOnlyList<DirtyChange> changes = dirtyJournal.PeekChanges();
        IReadOnlyList<long> removedEntityIds = dirtyJournal.PeekRemoved();
        for (int i = 0; i < changes.Count; i++)
        {
            long entityId = changes[i].EntityId;

            if (storage.TryGetEntity(entityId, out GameEntity entity))
            {
                snapshots.Add(CreateSnapshot(entity));
            }
        }

        for (int i = 0; i < removedEntityIds.Count; i++)
        {
            removed.Add(removedEntityIds[i]);
        }

        IReadOnlyList<PresentationFact> presentationFacts = dirtyJournal.PeekPresentationFacts();
        dirtyJournal.Clear();
        return new WorldDelta(ServerTick, snapshots, removed, presentationFacts);
    }

    public IReadOnlyList<EntitySnapshot> CreateSnapshot()
    {
        observations.FullSnapshotBuildCount++;
        var result = new List<EntitySnapshot>();
        foreach (GameEntity entity in storage.EnumerateEntities(EntityIterationOrder.EntityId))
        {
            result.Add(CreateSnapshot(entity));
        }

        return result;
    }

    public EntitySnapshot CreateSnapshot(GameEntity entity)
    {
        observations.TouchedSnapshotBuildCount++;
        return snapshotComponents.Project(this, entity, ServerTick);
    }

    public void MoveEntity(GameEntity entity, GridCoord target)
    {
        SetComponent(entity, new PositionComponent(target));
    }

    public void SetDirection(GameEntity entity, Direction direction)
    {
        SetComponent(entity, new DirectionComponent(direction));
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

        storage.SetComponent(entity.EntityId, component);
        entityLocations.SetCoord(entity.EntityId, component.Coord);
        GetOrCreateCell(component.Coord);

        if (HasComponent<ColliderComponent>(entity))
        {
            AddSpatial(entity.EntityId, component.Coord);
        }

        MarkDirty(entity.EntityId);
    }

    public void SetComponent(GameEntity entity, ColliderComponent component)
    {
        bool hadComponent = HasComponent<ColliderComponent>(entity);
        storage.SetComponent(entity.EntityId, component);

        if (!hadComponent &&
            TryGetComponent(entity, out PositionComponent position))
        {
            AddSpatial(entity.EntityId, position.Coord);
        }

        MarkDirty(entity.EntityId);
    }

    public void SetComponent<TComponent>(GameEntity entity, TComponent component) where TComponent : struct
    {
        storage.SetComponent(entity.EntityId, component);
        MarkDirty(entity.EntityId);
    }

    public bool HasComponent<TComponent>(GameEntity entity) where TComponent : struct
    {
        observations.HasComponentCount++;
        return storage.HasComponent<TComponent>(entity.EntityId);
    }

    public bool TryGetComponent<TComponent>(GameEntity entity, out TComponent component) where TComponent : struct
    {
        observations.TryGetComponentCount++;
        return storage.TryGetComponent(entity.EntityId, out component);
    }

    public TComponent GetComponent<TComponent>(GameEntity entity) where TComponent : struct
    {
        return storage.GetComponent<TComponent>(entity.EntityId);
    }

    public bool RemoveComponent<TComponent>(GameEntity entity) where TComponent : struct
    {
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

        bool removed = storage.RemoveComponent<TComponent>(entity.EntityId);
        if (removed && typeof(TComponent) == typeof(PositionComponent))
        {
            entityLocations.ClearCoord(entity.EntityId);
        }

        if (removed)
        {
            MarkDirty(entity.EntityId);
        }

        return removed;
    }

    public void MarkDirty(long entityId)
    {
        dirtyJournal.MarkChanged(entityId, ServerTick);
    }

    private void MarkRemoved(long entityId)
    {
        dirtyJournal.MarkRemoved(entityId);
    }

    private void AddSpatial(long entityId, GridCoord coord)
    {
        if (storage.TryGetEntity(entityId, out GameEntity entity))
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

    private static bool IsAutoMoveReady(AutoMoveQueryResult source, long serverTick)
    {
        return serverTick - source.AutoMove.LastMoveTick >= source.AutoMove.IntervalTicks;
    }

    private static AutoMoveActionCandidate CreateAutoMoveCandidate(AutoMoveQueryResult source, long serverTick)
    {
        return new AutoMoveActionCandidate(source.EntityId, serverTick - 1, 1, serverTick);
    }

    private IReadOnlyList<PushOnEnterActionCandidate> BuildPushOnEnterCandidates(PushOnEnterQueryResult trigger, long serverTick)
    {
        PushOnEnterComponent output = trigger.PushOnEnter;
        if (!output.OutputSpecId.IsValid)
        {
            return Array.Empty<PushOnEnterActionCandidate>();
        }

        IReadOnlyList<GameEntity> targets = GetPositionedEntitiesAt(trigger.Position);
        var candidates = new List<PushOnEnterActionCandidate>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            GameEntity target = targets[i];
            if (target.EntityId == trigger.EntityId)
            {
                continue;
            }

            candidates.Add(new PushOnEnterActionCandidate(trigger.EntityId, target.EntityId, output.OutputSpecId, trigger.Direction, serverTick, output.OutputCostTicks));
        }

        return candidates;
    }

}
}

