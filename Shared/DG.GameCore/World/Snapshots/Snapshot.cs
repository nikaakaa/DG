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

public enum PresentationFactType
{
    Unknown = 0,
    EntityMoved = 1,
    EntityPushed = 2,
    EntitySpawned = 3,
    EntityRemoved = 4,
    RotatePivotGroup = 5,
    RotatePivotImpact = 6,
    BodyMoved = 7
}

public enum PresentationFactResultKind
{
    None = 0,
    Success = 1,
    Bounce = 2,
    Impact = 3
}

public readonly struct PresentationFactMember
{
    public PresentationFactMember(long entityId, GridCoord from, GridCoord to, Direction fromDirection, Direction toDirection, DirectionMask fromPortLocalPorts, DirectionMask toPortLocalPorts)
    {
        EntityId = entityId;
        From = from;
        To = to;
        FromDirection = fromDirection;
        ToDirection = toDirection;
        FromPortLocalPorts = fromPortLocalPorts;
        ToPortLocalPorts = toPortLocalPorts;
    }

    public long EntityId { get; }
    public GridCoord From { get; }
    public GridCoord To { get; }
    public Direction FromDirection { get; }
    public Direction ToDirection { get; }
    public DirectionMask FromPortLocalPorts { get; }
    public DirectionMask ToPortLocalPorts { get; }
}

public readonly struct PresentationFactImpact
{
    public PresentationFactImpact(long blockerEntityId, long impactMemberId, GridCoord impactFrom, GridCoord impactTo, Direction pushDirection)
    {
        BlockerEntityId = blockerEntityId;
        ImpactMemberId = impactMemberId;
        ImpactFrom = impactFrom;
        ImpactTo = impactTo;
        PushDirection = pushDirection;
    }

    public long BlockerEntityId { get; }
    public long ImpactMemberId { get; }
    public GridCoord ImpactFrom { get; }
    public GridCoord ImpactTo { get; }
    public Direction PushDirection { get; }
}

public readonly struct PresentationFact
{
    public PresentationFact(long factId, long serverTick, PresentationFactType factType, PresentationFactResultKind resultKind, long sourceActionId, long clientInputId, long sourceEntityId, IReadOnlyList<long> subjectEntityIds, GridCoord from, GridCoord to, Direction direction, long startTick, long endTick, long pivotEntityId, GridCoord pivotCoord, RotatePivotDirection rotateDirection, IReadOnlyList<PresentationFactMember> members, IReadOnlyList<PresentationFactImpact> impacts)
        : this(factId, serverTick, factType, resultKind, sourceActionId, clientInputId, sourceEntityId, subjectEntityIds, from, to, direction, startTick, 0, endTick, 0d, Math.Max(0, (int)(endTick - startTick)), pivotEntityId, pivotCoord, rotateDirection, members, impacts)
    {
    }

    public PresentationFact(long factId, long serverTick, PresentationFactType factType, PresentationFactResultKind resultKind, long sourceActionId, long clientInputId, long sourceEntityId, IReadOnlyList<long> subjectEntityIds, GridCoord from, GridCoord to, Direction direction, long startTick, long contactTick, long endTick, double contactProgress, int effectiveCostTicks, long pivotEntityId, GridCoord pivotCoord, RotatePivotDirection rotateDirection, IReadOnlyList<PresentationFactMember> members, IReadOnlyList<PresentationFactImpact> impacts)
    {
        FactId = factId;
        ServerTick = serverTick;
        FactType = factType;
        ResultKind = resultKind;
        SourceActionId = sourceActionId;
        ClientInputId = clientInputId;
        SourceEntityId = sourceEntityId;
        SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? Array.Empty<long>() : subjectEntityIds.ToArray();
        From = from;
        To = to;
        Direction = direction;
        StartTick = startTick;
        ContactTick = contactTick;
        EndTick = endTick;
        ContactProgress = contactProgress;
        EffectiveCostTicks = effectiveCostTicks;
        PivotEntityId = pivotEntityId;
        PivotCoord = pivotCoord;
        RotateDirection = rotateDirection;
        Members = members == null || members.Count == 0 ? Array.Empty<PresentationFactMember>() : members.ToArray();
        Impacts = impacts == null || impacts.Count == 0 ? Array.Empty<PresentationFactImpact>() : impacts.ToArray();
    }

    public long FactId { get; }
    public long ServerTick { get; }
    public PresentationFactType FactType { get; }
    public PresentationFactResultKind ResultKind { get; }
    public long SourceActionId { get; }
    public long ClientInputId { get; }
    public long SourceEntityId { get; }
    public IReadOnlyList<long> SubjectEntityIds { get; }
    public GridCoord From { get; }
    public GridCoord To { get; }
    public Direction Direction { get; }
    public long StartTick { get; }
    public long ContactTick { get; }
    public long EndTick { get; }
    public double ContactProgress { get; }
    public int EffectiveCostTicks { get; }
    public long PivotEntityId { get; }
    public GridCoord PivotCoord { get; }
    public RotatePivotDirection RotateDirection { get; }
    public IReadOnlyList<PresentationFactMember> Members { get; }
    public IReadOnlyList<PresentationFactImpact> Impacts { get; }

    public long PrimarySubjectEntityId => SubjectEntityIds.Count == 0 ? 0 : SubjectEntityIds[0];
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
        : this(serverTick, changedEntities, removedEntityIds, Array.Empty<PresentationFact>())
    {
    }

    public WorldDelta(long serverTick, IReadOnlyList<EntitySnapshot> changedEntities, IReadOnlyList<long> removedEntityIds, IReadOnlyList<PresentationFact> presentationFacts)
    {
        ServerTick = serverTick;
        ChangedEntities = changedEntities;
        RemovedEntityIds = removedEntityIds;
        PresentationFacts = presentationFacts;
    }

    public long ServerTick { get; }
    public IReadOnlyList<EntitySnapshot> ChangedEntities { get; }
    public IReadOnlyList<long> RemovedEntityIds { get; }
    public IReadOnlyList<PresentationFact> PresentationFacts { get; }
}
}

