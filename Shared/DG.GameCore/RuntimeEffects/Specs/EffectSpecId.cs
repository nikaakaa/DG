using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct EffectSpecId : IEquatable<EffectSpecId>
{
    public EffectSpecId(string value)
        : this(StableRuntimeKey(value), value)
    {
    }

    public EffectSpecId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(EffectSpecId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is EffectSpecId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator EffectSpecId(string value)
    {
        return new EffectSpecId(value);
    }

    private static int StableRuntimeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619;
            }

            int key = (int)(hash & 0x7fffffff);
            return key == 0 ? 1 : key;
        }
    }
}

public readonly struct EffectPayloadId : IEquatable<EffectPayloadId>
{
    public EffectPayloadId(string value)
        : this(RuntimeKeyUtility.StableRuntimeKey(value), value)
    {
    }

    public EffectPayloadId(EffectKind kind)
        : this(kind.ToString())
    {
    }

    public EffectPayloadId(RuntimeEffectKind kind)
        : this(kind.ToString())
    {
    }

    public EffectPayloadId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(EffectPayloadId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is EffectPayloadId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator EffectPayloadId(string value)
    {
        return new EffectPayloadId(value);
    }
}

}
