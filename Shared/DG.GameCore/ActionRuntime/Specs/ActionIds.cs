using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct ActionSpecId : IEquatable<ActionSpecId>
{
    public ActionSpecId(string value)
        : this(StableRuntimeKey(value), value)
    {
    }

    public ActionSpecId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(ActionSpecId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is ActionSpecId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator ActionSpecId(string value)
    {
        return new ActionSpecId(value);
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


public readonly struct BlockedResultPolicyId : IEquatable<BlockedResultPolicyId>
{
    public BlockedResultPolicyId(string value)
        : this(StableRuntimeKey(value), value)
    {
    }

    public BlockedResultPolicyId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(BlockedResultPolicyId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is BlockedResultPolicyId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator BlockedResultPolicyId(string value)
    {
        return new BlockedResultPolicyId(value);
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


public readonly struct TargetingSpecId : IEquatable<TargetingSpecId>
{
    public TargetingSpecId(string value)
        : this(StableRuntimeKey(value), value)
    {
    }

    public TargetingSpecId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(TargetingSpecId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is TargetingSpecId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator TargetingSpecId(string value)
    {
        return new TargetingSpecId(value);
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


public readonly struct TargetSelectorId : IEquatable<TargetSelectorId>
{
    public TargetSelectorId(string value)
        : this(StableRuntimeKey(value), value)
    {
    }

    public TargetSelectorId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(TargetSelectorId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is TargetSelectorId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator TargetSelectorId(string value)
    {
        return new TargetSelectorId(value);
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


public readonly struct TargetFilterSpecId : IEquatable<TargetFilterSpecId>
{
    public TargetFilterSpecId(string value)
        : this(StableRuntimeKey(value), value)
    {
    }

    public TargetFilterSpecId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(TargetFilterSpecId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is TargetFilterSpecId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator TargetFilterSpecId(string value)
    {
        return new TargetFilterSpecId(value);
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

public readonly struct ActionStrategyId : IEquatable<ActionStrategyId>
{
    public ActionStrategyId(string value)
        : this(RuntimeKeyUtility.StableRuntimeKey(value), value)
    {
    }

    public ActionStrategyId(ActionPrimitive primitive)
        : this(PrimitiveName(primitive))
    {
    }

    public ActionStrategyId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(ActionStrategyId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is ActionStrategyId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator ActionStrategyId(string value)
    {
        return new ActionStrategyId(value);
    }

    private static string PrimitiveName(ActionPrimitive primitive)
    {
        switch (primitive)
        {
            case ActionPrimitive.Move:
                return "move";
            case ActionPrimitive.Spawn:
                return "spawn";
            case ActionPrimitive.Remove:
                return "remove";
            case ActionPrimitive.SetComponentResult:
                return "component_result";
            case ActionPrimitive.ApplyRuntimeEffect:
                return "runtime_effect";
            default:
                return primitive.ToString();
        }
    }
}

internal static class RuntimeKeyUtility
{
    public static int StableRuntimeKey(string value)
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

}
