using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct GridCoord : IEquatable<GridCoord>
{
    public GridCoord(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }

    public GridCoord Add(Direction direction)
    {
        return direction switch
        {
            Direction.Left => new GridCoord(X - 1, Y),
            Direction.Right => new GridCoord(X + 1, Y),
            Direction.Up => new GridCoord(X, Y + 1),
            Direction.Down => new GridCoord(X, Y - 1),
            _ => this
        };
    }

    public int ManhattanDistance(GridCoord other)
    {
        return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    }

    public bool Equals(GridCoord other)
    {
        return X == other.X && Y == other.Y;
    }

    public override bool Equals(object obj)
    {
        return obj is GridCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (X * 397) ^ Y;
        }
    }

    public static bool operator ==(GridCoord left, GridCoord right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GridCoord left, GridCoord right)
    {
        return !left.Equals(right);
    }
}
}

