using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct DirtyChange
{
    public DirtyChange(long entityId, long serverTick)
    {
        EntityId = entityId;
        ServerTick = serverTick;
    }

    public long EntityId { get; }
    public long ServerTick { get; }
}

public readonly struct EntitySnapshot
{
    public EntitySnapshot(long entityId, int configId, int archetypeId, int entityTarget, int x, int y, Direction direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, bool playerControlled, long serverTick)
    {
        EntityId = entityId;
        ConfigId = configId;
        ArchetypeId = archetypeId;
        EntityTarget = entityTarget;
        X = x;
        Y = y;
        Direction = direction;
        HasCollider = hasCollider;
        Blocking = blocking;
        Bouncable = bouncable;
        AutoMove = autoMove;
        PlayerControlled = playerControlled;
        ServerTick = serverTick;
    }

    public long EntityId { get; }
    public int ConfigId { get; }
    public int ArchetypeId { get; }
    public int EntityTarget { get; }
    public int X { get; }
    public int Y { get; }
    public Direction Direction { get; }
    public bool HasCollider { get; }
    public bool Blocking { get; }
    public bool Bouncable { get; }
    public bool AutoMove { get; }
    public bool PlayerControlled { get; }
    public long ServerTick { get; }
}

public readonly struct WorldDelta
{
    public WorldDelta(long serverTick, IReadOnlyList<EntitySnapshot> changedEntities)
        : this(serverTick, changedEntities, Array.Empty<long>())
    {
    }

    public WorldDelta(long serverTick, IReadOnlyList<EntitySnapshot> changedEntities, IReadOnlyList<long> removedEntityIds)
    {
        ServerTick = serverTick;
        ChangedEntities = changedEntities;
        RemovedEntityIds = removedEntityIds;
    }

    public long ServerTick { get; }
    public IReadOnlyList<EntitySnapshot> ChangedEntities { get; }
    public IReadOnlyList<long> RemovedEntityIds { get; }
}
}

