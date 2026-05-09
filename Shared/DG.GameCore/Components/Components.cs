using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
[Flags]
public enum WorldTag
{
    None = 0,
    SourcePlayer = 1 << 0,
    SourceMechanism = 1 << 1,
    SourceAuto = 1 << 2,
    SourceDebug = 1 << 3,
    SourceSkill = 1 << 4,
    AbilityMove = 1 << 5,
    AbilityPlayerPush = 1 << 6,
    AbilityMechanismPush = 1 << 7,
    AbilityAutoMove = 1 << 8,
    StateStunned = 1 << 9,
    StateRooted = 1 << 10,
    StateSuperArmor = 1 << 11,
    ImmuneMechanismPush = 1 << 12,
    BlockPlayerMove = 1 << 13
}

public struct TagSetComponent
{
    public TagSetComponent(WorldTag tags)
    {
        Tags = tags;
    }

    public WorldTag Tags { get; set; }

    public bool Has(WorldTag tag)
    {
        return tag != WorldTag.None && (Tags & tag) == tag;
    }

    public TagSetComponent Add(WorldTag tag)
    {
        return new TagSetComponent(Tags | tag);
    }

    public TagSetComponent Remove(WorldTag tag)
    {
        return new TagSetComponent(Tags & ~tag);
    }
}

public struct PositionComponent
{
    public PositionComponent(GridCoord coord)
    {
        Coord = coord;
    }

    public GridCoord Coord { get; set; }
}

public struct DirectionComponent
{
    public DirectionComponent(Direction direction)
    {
        Direction = direction;
    }

    public Direction Direction { get; set; }
}

public struct ColliderComponent
{
}

public struct BlockingComponent
{
}

public struct BouncableComponent
{
}

public struct PushOnEnterComponent
{
}

public struct PushableComponent
{
}

public struct PortConnectorComponent
{
    public PortConnectorComponent(DirectionMask localPorts)
    {
        LocalPorts = localPorts;
    }

    public DirectionMask LocalPorts { get; }
}

public struct MovementPermissionComponent
{
    public MovementPermissionComponent(bool canMove, bool canBePushed)
    {
        CanMove = canMove;
        CanBePushed = canBePushed;
    }

    public bool CanMove { get; }
    public bool CanBePushed { get; }
}

public struct AutoMoveComponent
{
    public AutoMoveComponent(int intervalTicks)
    {
        IntervalTicks = Math.Max(1, intervalTicks);
        LastMoveTick = 0;
    }

    public int IntervalTicks { get; }
    public long LastMoveTick { get; set; }
}

public struct PlayerControlComponent
{
    public PlayerControlComponent(long playerId)
    {
        PlayerId = playerId;
    }

    public long PlayerId { get; }
}
}

