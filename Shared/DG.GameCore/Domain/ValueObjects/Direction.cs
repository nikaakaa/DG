using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum Direction
{
    None = 0,
    Left = 1,
    Right = 2,
    Up = 3,
    Down = 4
}

[Flags]
public enum DirectionMask
{
    None = 0,
    Left = 1,
    Right = 2,
    Up = 4,
    Down = 8,
    All = Left | Right | Up | Down
}

public static class DirectionExtensions
{
    public static Direction Opposite(this Direction direction)
    {
        return direction switch
        {
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            _ => Direction.None
        };
    }

    public static DirectionMask ToMask(this Direction direction)
    {
        return direction switch
        {
            Direction.Left => DirectionMask.Left,
            Direction.Right => DirectionMask.Right,
            Direction.Up => DirectionMask.Up,
            Direction.Down => DirectionMask.Down,
            _ => DirectionMask.None
        };
    }

    public static bool Contains(this DirectionMask mask, Direction direction)
    {
        DirectionMask directionMask = direction.ToMask();
        return directionMask != DirectionMask.None && (mask & directionMask) == directionMask;
    }

    public static Direction RotateBy(this Direction localDirection, Direction orientation)
    {
        if (localDirection == Direction.None)
        {
            return Direction.None;
        }

        return orientation switch
        {
            Direction.Right or Direction.None => localDirection,
            Direction.Down => localDirection.RotateClockwise(),
            Direction.Left => localDirection.Opposite(),
            Direction.Up => localDirection.RotateCounterClockwise(),
            _ => localDirection
        };
    }

    public static DirectionMask RotateBy(this DirectionMask localPorts, Direction orientation)
    {
        DirectionMask result = DirectionMask.None;
        if (localPorts.Contains(Direction.Left))
        {
            result |= Direction.Left.RotateBy(orientation).ToMask();
        }

        if (localPorts.Contains(Direction.Right))
        {
            result |= Direction.Right.RotateBy(orientation).ToMask();
        }

        if (localPorts.Contains(Direction.Up))
        {
            result |= Direction.Up.RotateBy(orientation).ToMask();
        }

        if (localPorts.Contains(Direction.Down))
        {
            result |= Direction.Down.RotateBy(orientation).ToMask();
        }

        return result;
    }

    private static Direction RotateClockwise(this Direction direction)
    {
        return direction switch
        {
            Direction.Left => Direction.Up,
            Direction.Up => Direction.Right,
            Direction.Right => Direction.Down,
            Direction.Down => Direction.Left,
            _ => Direction.None
        };
    }

    private static Direction RotateCounterClockwise(this Direction direction)
    {
        return direction switch
        {
            Direction.Left => Direction.Down,
            Direction.Down => Direction.Right,
            Direction.Right => Direction.Up,
            Direction.Up => Direction.Left,
            _ => Direction.None
        };
    }
}
}

