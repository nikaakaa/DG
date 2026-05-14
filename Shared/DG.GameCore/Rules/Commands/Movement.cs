using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum MoveCommandSource
{
    PlayerInput = 1,
    AutoTick = 2,
    PushOnEnter = 3
}

public enum MoveErrorCode
{
    None = 0,
    UnknownEntity = 1,
    MissingPosition = 2,
    TooFar = 3,
    Blocked = 4,
    Occupied = 5,
    InvalidDirection = 6,
    NotJoined = 7,
    UnauthorizedEntity = 8,
    Immune = 9
}

public readonly struct MoveCommand
{
    public MoveCommand(long entityId, MoveCommandSource source, Direction direction, GridCoord? target, long tick, long clientTick)
    {
        EntityId = entityId;
        Source = source;
        Direction = direction;
        Target = target;
        Tick = tick;
        ClientTick = clientTick;
    }

    public long EntityId { get; }
    public MoveCommandSource Source { get; }
    public Direction Direction { get; }
    public GridCoord? Target { get; }
    public long Tick { get; }
    public long ClientTick { get; }

    public static MoveCommand ToTarget(long entityId, GridCoord target, MoveCommandSource source, long tick, long clientTick)
    {
        return new MoveCommand(entityId, source, Direction.None, target, tick, clientTick);
    }

    public static MoveCommand ToDirection(long entityId, Direction direction, MoveCommandSource source, long tick, long clientTick)
    {
        return new MoveCommand(entityId, source, direction, null, tick, clientTick);
    }
}

public readonly struct CollisionInfo
{
    public CollisionInfo(long targetEntityId, bool blocking, bool player)
    {
        TargetEntityId = targetEntityId;
        Blocking = blocking;
        Player = player;
    }

    public long TargetEntityId { get; }
    public bool Blocking { get; }
    public bool Player { get; }
}

public readonly struct MoveResult
{
    public MoveResult(bool success, long entityId, GridCoord finalCoord, Direction finalDirection, MoveErrorCode errorCode, string reason, bool bounced, CollisionInfo collision, long clientTick)
    {
        Success = success;
        EntityId = entityId;
        FinalCoord = finalCoord;
        FinalDirection = finalDirection;
        ErrorCode = errorCode;
        Reason = reason;
        Bounced = bounced;
        Collision = collision;
        ClientTick = clientTick;
    }

    public bool Success { get; }
    public long EntityId { get; }
    public GridCoord FinalCoord { get; }
    public Direction FinalDirection { get; }
    public MoveErrorCode ErrorCode { get; }
    public string Reason { get; }
    public bool Bounced { get; }
    public CollisionInfo Collision { get; }
    public long ClientTick { get; }
}
}

