using System;

namespace DG.GameCore
{
public readonly struct BehaviorId : IEquatable<BehaviorId>
{
    public BehaviorId(string value)
        : this(RuntimeKeyUtility.StableRuntimeKey(value), value)
    {
    }

    public BehaviorId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(BehaviorId other) => RuntimeKey == other.RuntimeKey;
    public override bool Equals(object obj) => obj is BehaviorId other && Equals(other);
    public override int GetHashCode() => RuntimeKey;
    public override string ToString() => Value;
    public static implicit operator BehaviorId(string value) => new BehaviorId(value);
}

public readonly struct RunnerId : IEquatable<RunnerId>
{
    public RunnerId(string value)
        : this(RuntimeKeyUtility.StableRuntimeKey(value), value)
    {
    }

    public RunnerId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(RunnerId other) => RuntimeKey == other.RuntimeKey;
    public override bool Equals(object obj) => obj is RunnerId other && Equals(other);
    public override int GetHashCode() => RuntimeKey;
    public override string ToString() => Value;
    public static implicit operator RunnerId(string value) => new RunnerId(value);
}

public readonly struct BehaviorDefinition : IEquatable<BehaviorDefinition>
{
    public BehaviorDefinition(BehaviorId behaviorId, RunnerId runnerId, ActionPrimitive primitive, BehaviorClaimChannel channelHint, BehaviorClaimMode claimMode, int baseCostTicks)
    {
        if (!behaviorId.IsValid)
        {
            throw new ArgumentException("Behavior id is empty.", nameof(behaviorId));
        }

        if (!runnerId.IsValid)
        {
            throw new ArgumentException("Runner id is empty on " + behaviorId + ".", nameof(runnerId));
        }

        if (baseCostTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(baseCostTicks), baseCostTicks, "Behavior base cost ticks must be at least 1.");
        }

        BehaviorId = behaviorId;
        RunnerId = runnerId;
        Primitive = primitive;
        ChannelHint = channelHint;
        ClaimMode = claimMode;
        BaseCostTicks = baseCostTicks;
    }

    public BehaviorId BehaviorId { get; }
    public RunnerId RunnerId { get; }
    public ActionPrimitive Primitive { get; }
    public BehaviorClaimChannel ChannelHint { get; }
    public BehaviorClaimMode ClaimMode { get; }
    public int BaseCostTicks { get; }

    public bool IsValid => BehaviorId.IsValid && RunnerId.IsValid;

    public bool Equals(BehaviorDefinition other) => BehaviorId.Equals(other.BehaviorId);
    public override bool Equals(object obj) => obj is BehaviorDefinition other && Equals(other);
    public override int GetHashCode() => BehaviorId.GetHashCode();
    public override string ToString() => BehaviorId.Value + "->" + RunnerId.Value;
}
}
