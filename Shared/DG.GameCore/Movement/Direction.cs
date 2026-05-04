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
}
}

