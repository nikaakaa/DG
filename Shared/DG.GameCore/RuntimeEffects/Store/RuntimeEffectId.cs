using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct RuntimeEffectId : IEquatable<RuntimeEffectId>
{
    public RuntimeEffectId(long value)
    {
        Value = value;
    }

    public long Value { get; }
    public bool IsValid => Value > 0;

    public bool Equals(RuntimeEffectId other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is RuntimeEffectId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}

}
