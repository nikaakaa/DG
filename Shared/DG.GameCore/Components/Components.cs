using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
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

