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

public enum WorldDeltaMotionKind
{
    Unknown = 0,
    PlayerMove = 1,
    MechanismPush = 2,
    AutoMove = 3,
    DebugDrag = 4,
    Spawn = 5,
    Remove = 6,
    RotatePivot = 7,
    RotatePivotBounce = 8
}

public readonly struct WorldDeltaAnimationMetadata
{
    public WorldDeltaAnimationMetadata(long entityId, long serverTick, WorldDeltaMotionKind motionKind, string styleKey)
        : this(entityId, serverTick, motionKind, styleKey, Direction.None)
    {
    }

    public WorldDeltaAnimationMetadata(long entityId, long serverTick, WorldDeltaMotionKind motionKind, string styleKey, Direction direction)
        : this(entityId, serverTick, motionKind, styleKey, direction, 0, default, default, default, RotatePivotDirection.None, false, default)
    {
    }

    public WorldDeltaAnimationMetadata(long entityId, long serverTick, WorldDeltaMotionKind motionKind, string styleKey, Direction direction, long pivotEntityId, GridCoord pivotCoord, GridCoord fromCoord, GridCoord toCoord, RotatePivotDirection rotateDirection, bool bounce, GridCoord impactCoord)
    {
        EntityId = entityId;
        ServerTick = serverTick;
        MotionKind = motionKind;
        StyleKey = styleKey ?? string.Empty;
        Direction = direction;
        PivotEntityId = pivotEntityId;
        PivotCoord = pivotCoord;
        FromCoord = fromCoord;
        ToCoord = toCoord;
        RotateDirection = rotateDirection;
        Bounce = bounce;
        ImpactCoord = impactCoord;
    }

    public long EntityId { get; }
    public long ServerTick { get; }
    public WorldDeltaMotionKind MotionKind { get; }
    public string StyleKey { get; }
    public Direction Direction { get; }
    public long PivotEntityId { get; }
    public GridCoord PivotCoord { get; }
    public GridCoord FromCoord { get; }
    public GridCoord ToCoord { get; }
    public RotatePivotDirection RotateDirection { get; }
    public bool Bounce { get; }
    public GridCoord ImpactCoord { get; }
}

public readonly struct EntitySnapshot
{
    public EntitySnapshot(long entityId, int configId, int archetypeId, int entityTarget, int x, int y, Direction direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, bool playerControlled, long serverTick)
        : this(entityId, configId, archetypeId, entityTarget, x, y, direction, hasCollider, blocking, bouncable, autoMove, autoMove ? 1 : 0, playerControlled, false, DirectionMask.None, false, true, true, serverTick)
    {
    }

    public EntitySnapshot(long entityId, int configId, int archetypeId, int entityTarget, int x, int y, Direction direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, int autoMoveIntervalTicks, bool playerControlled, bool pushable, DirectionMask portLocalPorts, bool hasMovementPermission, bool canMove, bool canBePushed, long serverTick)
        : this(entityId, configId, archetypeId, entityTarget, x, y, direction, hasCollider, blocking, bouncable, autoMove, autoMoveIntervalTicks, playerControlled, pushable, portLocalPorts, false, hasMovementPermission, canMove, canBePushed, serverTick)
    {
    }

    public EntitySnapshot(long entityId, int configId, int archetypeId, int entityTarget, int x, int y, Direction direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, int autoMoveIntervalTicks, bool playerControlled, bool pushable, DirectionMask portLocalPorts, bool rotatePivot, bool hasMovementPermission, bool canMove, bool canBePushed, long serverTick)
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
        AutoMoveIntervalTicks = autoMove ? Math.Max(1, autoMoveIntervalTicks) : 0;
        PlayerControlled = playerControlled;
        Pushable = pushable;
        PortLocalPorts = portLocalPorts;
        RotatePivot = rotatePivot;
        HasMovementPermission = hasMovementPermission;
        CanMove = !hasMovementPermission || canMove;
        CanBePushed = !hasMovementPermission || canBePushed;
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
    public int AutoMoveIntervalTicks { get; }
    public bool PlayerControlled { get; }
    public bool Pushable { get; }
    public DirectionMask PortLocalPorts { get; }
    public bool RotatePivot { get; }
    public bool HasMovementPermission { get; }
    public bool CanMove { get; }
    public bool CanBePushed { get; }
    public long ServerTick { get; }
}

public readonly struct WorldDelta
{
    public WorldDelta(long serverTick, IReadOnlyList<EntitySnapshot> changedEntities)
        : this(serverTick, changedEntities, Array.Empty<long>())
    {
    }

    public WorldDelta(long serverTick, IReadOnlyList<EntitySnapshot> changedEntities, IReadOnlyList<long> removedEntityIds)
        : this(serverTick, changedEntities, removedEntityIds, Array.Empty<WorldDeltaAnimationMetadata>())
    {
    }

    public WorldDelta(long serverTick, IReadOnlyList<EntitySnapshot> changedEntities, IReadOnlyList<long> removedEntityIds, IReadOnlyList<WorldDeltaAnimationMetadata> animationMetadata)
    {
        ServerTick = serverTick;
        ChangedEntities = changedEntities;
        RemovedEntityIds = removedEntityIds;
        AnimationMetadata = animationMetadata;
    }

    public long ServerTick { get; }
    public IReadOnlyList<EntitySnapshot> ChangedEntities { get; }
    public IReadOnlyList<long> RemovedEntityIds { get; }
    public IReadOnlyList<WorldDeltaAnimationMetadata> AnimationMetadata { get; }
}
}

