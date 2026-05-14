using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public enum EntityIterationOrder
{
    Unordered = 0,
    EntityId = 1
}

public readonly struct EntityLocation
{
    public EntityLocation(long entityId, GridCoord? coord, int target, bool hasSpatialLocation)
    {
        EntityId = entityId;
        Coord = coord;
        Target = target;
        HasSpatialLocation = hasSpatialLocation;
    }

    public long EntityId { get; }
    public GridCoord? Coord { get; }
    public int Target { get; }
    public bool HasSpatialLocation { get; }
}

public readonly struct GameWorldObservation
{
    public GameWorldObservation(long entityEnumerationCount, long entityEnumerationSortCount, long componentQueryCount, long tryGetComponentCount, long hasComponentCount, long spatialQueryCount, long touchedSnapshotBuildCount, long fullSnapshotBuildCount, long deltaFlushCount, long touchedDiagnosticCount)
    {
        EntityEnumerationCount = entityEnumerationCount;
        EntityEnumerationSortCount = entityEnumerationSortCount;
        ComponentQueryCount = componentQueryCount;
        TryGetComponentCount = tryGetComponentCount;
        HasComponentCount = hasComponentCount;
        SpatialQueryCount = spatialQueryCount;
        TouchedSnapshotBuildCount = touchedSnapshotBuildCount;
        FullSnapshotBuildCount = fullSnapshotBuildCount;
        DeltaFlushCount = deltaFlushCount;
        TouchedDiagnosticCount = touchedDiagnosticCount;
    }

    public long EntityEnumerationCount { get; }
    public long EntityEnumerationSortCount { get; }
    public long ComponentQueryCount { get; }
    public long TryGetComponentCount { get; }
    public long HasComponentCount { get; }
    public long SpatialQueryCount { get; }
    public long TouchedSnapshotBuildCount { get; }
    public long FullSnapshotBuildCount { get; }
    public long SnapshotBuildCount => TouchedSnapshotBuildCount + FullSnapshotBuildCount;
    public long DeltaFlushCount { get; }
    public long TouchedDiagnosticCount { get; }
}

internal sealed class GameWorldObservationCounters
{
    public long EntityEnumerationCount;
    public long EntityEnumerationSortCount;
    public long ComponentQueryCount;
    public long TryGetComponentCount;
    public long HasComponentCount;
    public long SpatialQueryCount;
    public long TouchedSnapshotBuildCount;
    public long FullSnapshotBuildCount;
    public long DeltaFlushCount;
    public long TouchedDiagnosticCount;

    public GameWorldObservation Snapshot()
    {
        return new GameWorldObservation(
            EntityEnumerationCount,
            EntityEnumerationSortCount,
            ComponentQueryCount,
            TryGetComponentCount,
            HasComponentCount,
            SpatialQueryCount,
            TouchedSnapshotBuildCount,
            FullSnapshotBuildCount,
            DeltaFlushCount,
            TouchedDiagnosticCount);
    }

    public void Reset()
    {
        EntityEnumerationCount = 0;
        EntityEnumerationSortCount = 0;
        ComponentQueryCount = 0;
        TryGetComponentCount = 0;
        HasComponentCount = 0;
        SpatialQueryCount = 0;
        TouchedSnapshotBuildCount = 0;
        FullSnapshotBuildCount = 0;
        DeltaFlushCount = 0;
        TouchedDiagnosticCount = 0;
    }
}

public readonly struct AutoMoveQueryResult
{
    public AutoMoveQueryResult(long entityId, GridCoord position, Direction direction, AutoMoveComponent autoMove)
    {
        EntityId = entityId;
        Position = position;
        Direction = direction;
        AutoMove = autoMove;
    }

    public long EntityId { get; }
    public GridCoord Position { get; }
    public Direction Direction { get; }
    public AutoMoveComponent AutoMove { get; }
}

public readonly struct PushOnEnterQueryResult
{
    public PushOnEnterQueryResult(long entityId, GridCoord position, Direction direction, PushOnEnterComponent pushOnEnter)
    {
        EntityId = entityId;
        Position = position;
        Direction = direction;
        PushOnEnter = pushOnEnter;
    }

    public long EntityId { get; }
    public GridCoord Position { get; }
    public Direction Direction { get; }
    public PushOnEnterComponent PushOnEnter { get; }
}

public readonly struct ColliderSpatialQueryResult
{
    public ColliderSpatialQueryResult(long entityId, GridCoord position, int target)
    {
        EntityId = entityId;
        Position = position;
        Target = target;
    }

    public long EntityId { get; }
    public GridCoord Position { get; }
    public int Target { get; }
}

public readonly struct BlockingSpatialQueryResult
{
    public BlockingSpatialQueryResult(long entityId, GridCoord position, int target)
    {
        EntityId = entityId;
        Position = position;
        Target = target;
    }

    public long EntityId { get; }
    public GridCoord Position { get; }
    public int Target { get; }
}

public readonly struct PushableSpatialQueryResult
{
    public PushableSpatialQueryResult(long entityId, GridCoord position, int target)
    {
        EntityId = entityId;
        Position = position;
        Target = target;
    }

    public long EntityId { get; }
    public GridCoord Position { get; }
    public int Target { get; }
}

public readonly struct ComponentQueryDescriptor
{
    private readonly Type[] componentTypes;

    private ComponentQueryDescriptor(Type[] componentTypes)
    {
        this.componentTypes = componentTypes;
    }

    public static ComponentQueryDescriptor With<TComponent>() where TComponent : struct
    {
        return new ComponentQueryDescriptor(new[] { typeof(TComponent) });
    }

    public static ComponentQueryDescriptor With<TFirst, TSecond>()
        where TFirst : struct
        where TSecond : struct
    {
        return new ComponentQueryDescriptor(new[] { typeof(TFirst), typeof(TSecond) });
    }

    public static ComponentQueryDescriptor With<TFirst, TSecond, TThird>()
        where TFirst : struct
        where TSecond : struct
        where TThird : struct
    {
        return new ComponentQueryDescriptor(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) });
    }

    public IReadOnlyList<Type> ComponentTypes
    {
        get
        {
            return componentTypes ?? Array.Empty<Type>();
        }
    }
}

internal sealed class DirtyWorldJournal
{
    private readonly List<DirtyChange> changed = new();
    private readonly List<long> removed = new();
    private readonly List<WorldDeltaAnimationMetadata> animationMetadata = new();

    public void MarkChanged(long entityId, long serverTick)
    {
        if (removed.Contains(entityId))
        {
            return;
        }

        for (int i = 0; i < changed.Count; i++)
        {
            if (changed[i].EntityId == entityId)
            {
                changed[i] = new DirtyChange(entityId, serverTick);
                return;
            }
        }

        changed.Add(new DirtyChange(entityId, serverTick));
    }

    public void MarkRemoved(long entityId)
    {
        for (int i = changed.Count - 1; i >= 0; i--)
        {
            if (changed[i].EntityId == entityId)
            {
                changed.RemoveAt(i);
            }
        }

        if (!removed.Contains(entityId))
        {
            removed.Add(entityId);
        }
    }

    public void AddAnimationMetadata(WorldDeltaAnimationMetadata metadata)
    {
        animationMetadata.Add(metadata);
    }

    public IReadOnlyList<DirtyChange> PeekChanges()
    {
        return changed.ToArray();
    }

    public IReadOnlyList<long> PeekRemoved()
    {
        return removed.ToArray();
    }

    public IReadOnlyList<WorldDeltaAnimationMetadata> PeekAnimationMetadata()
    {
        return animationMetadata.ToArray();
    }

    public void Clear()
    {
        changed.Clear();
        removed.Clear();
        animationMetadata.Clear();
    }
}

internal sealed class EntityLocationStore
{
    private readonly List<EntityLocationRecord> records = new();

    public void Register(long entityId, int target)
    {
        int index = FindIndex(entityId);
        if (index >= 0)
        {
            EntityLocationRecord found = records[index];
            found.Target = target;
            records[index] = found;
            return;
        }

        records.Add(new EntityLocationRecord(entityId, null, target, false));
    }

    public void SetCoord(long entityId, GridCoord coord)
    {
        int index = FindIndex(entityId);
        if (index < 0)
        {
            records.Add(new EntityLocationRecord(entityId, coord, 0, true));
            return;
        }

        EntityLocationRecord found = records[index];
        found.Coord = coord;
        found.HasSpatialLocation = true;
        records[index] = found;
    }

    public void ClearCoord(long entityId)
    {
        int index = FindIndex(entityId);
        if (index < 0)
        {
            return;
        }

        EntityLocationRecord found = records[index];
        found.Coord = null;
        found.HasSpatialLocation = false;
        records[index] = found;
    }

    public bool Remove(long entityId)
    {
        int index = FindIndex(entityId);
        if (index < 0)
        {
            return false;
        }

        int last = records.Count - 1;
        records[index] = records[last];
        records.RemoveAt(last);
        return true;
    }

    public bool TryGet(long entityId, out EntityLocation location)
    {
        int index = FindIndex(entityId);
        if (index >= 0)
        {
            EntityLocationRecord found = records[index];
            location = new EntityLocation(found.EntityId, found.Coord, found.Target, found.HasSpatialLocation);
            return true;
        }

        location = default;
        return false;
    }

    private int FindIndex(long entityId)
    {
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].EntityId == entityId)
            {
                return i;
            }
        }

        return -1;
    }
}

internal struct EntityLocationRecord
{
    public EntityLocationRecord(long entityId, GridCoord? coord, int target, bool hasSpatialLocation)
    {
        EntityId = entityId;
        Coord = coord;
        Target = target;
        HasSpatialLocation = hasSpatialLocation;
    }

    public long EntityId;
    public GridCoord? Coord;
    public int Target;
    public bool HasSpatialLocation;
}
}
