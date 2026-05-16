using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum ComponentKind
{
    Position = 1,
    Direction = 2,
    Collider = 3,
    Blocking = 4,
    Bouncable = 5,
    AutoMove = 6,
    PlayerControl = 7,
    PushOnEnter = 8,
    Pushable = 9,
    PortConnector = 10,
}

public readonly struct ComponentId : IEquatable<ComponentId>
{
    public ComponentId(string value)
        : this(RuntimeKeyUtility.StableRuntimeKey(value), value)
    {
    }

    public ComponentId(ComponentKind kind)
        : this(kind.ToString())
    {
    }

    public ComponentId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(ComponentId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is ComponentId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator ComponentId(string value)
    {
        return new ComponentId(value);
    }
}
}
