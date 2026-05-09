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

public enum RuntimeEffectKind
{
    TemporaryBlocking = 1,
    TemporaryAutoMove = 2,
    TemporaryPushable = 3,
    TemporaryPort = 4,
    TemporaryImmobile = 5
}

public readonly struct RuntimeEffectSpec
{
    public RuntimeEffectSpec(RuntimeEffectKind kind, long targetEntityId, long sourceEntityId, long startTick, long expireTick, int autoMoveIntervalTicks, DirectionMask portMask, bool canMove, bool canBePushed)
    {
        Kind = kind;
        TargetEntityId = targetEntityId;
        SourceEntityId = sourceEntityId;
        StartTick = startTick;
        ExpireTick = expireTick;
        AutoMoveIntervalTicks = Math.Max(1, autoMoveIntervalTicks);
        PortMask = portMask;
        CanMove = canMove;
        CanBePushed = canBePushed;
    }

    public RuntimeEffectKind Kind { get; }
    public long TargetEntityId { get; }
    public long SourceEntityId { get; }
    public long StartTick { get; }
    public long ExpireTick { get; }
    public int AutoMoveIntervalTicks { get; }
    public DirectionMask PortMask { get; }
    public bool CanMove { get; }
    public bool CanBePushed { get; }

    public static RuntimeEffectSpec Blocking(long targetEntityId, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryBlocking, targetEntityId, sourceEntityId, startTick, expireTick, 1, DirectionMask.None, true, true);
    }

    public static RuntimeEffectSpec AutoMove(long targetEntityId, int intervalTicks, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryAutoMove, targetEntityId, sourceEntityId, startTick, expireTick, intervalTicks, DirectionMask.None, true, true);
    }

    public static RuntimeEffectSpec Pushable(long targetEntityId, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryPushable, targetEntityId, sourceEntityId, startTick, expireTick, 1, DirectionMask.None, true, true);
    }

    public static RuntimeEffectSpec Port(long targetEntityId, DirectionMask portMask, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryPort, targetEntityId, sourceEntityId, startTick, expireTick, 1, portMask, true, true);
    }

    public static RuntimeEffectSpec Immobile(long targetEntityId, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryImmobile, targetEntityId, sourceEntityId, startTick, expireTick, 1, DirectionMask.None, false, false);
    }
}

public readonly struct RuntimeEffectInstance
{
    public RuntimeEffectInstance(RuntimeEffectId id, RuntimeEffectSpec spec)
    {
        Id = id;
        Spec = spec;
    }

    public RuntimeEffectId Id { get; }
    public RuntimeEffectSpec Spec { get; }
    public long TargetEntityId => Spec.TargetEntityId;
    public RuntimeEffectKind Kind => Spec.Kind;

    public bool IsActiveAt(long tick)
    {
        return Spec.StartTick <= tick && !HasExpiredAt(tick);
    }

    public bool HasExpiredAt(long tick)
    {
        return Spec.ExpireTick > 0 && Spec.ExpireTick <= tick;
    }
}

public readonly struct RuntimeEffectLifecycleResult
{
    public RuntimeEffectLifecycleResult(IReadOnlyList<RuntimeEffectInstance> active, IReadOnlyList<RuntimeEffectInstance> removed, IReadOnlyList<RuntimeEffectInstance> expired)
    {
        Active = active;
        Removed = removed;
        Expired = expired;
    }

    public IReadOnlyList<RuntimeEffectInstance> Active { get; }
    public IReadOnlyList<RuntimeEffectInstance> Removed { get; }
    public IReadOnlyList<RuntimeEffectInstance> Expired { get; }
}

public sealed class RuntimeEffectStore
{
    private readonly Dictionary<RuntimeEffectId, RuntimeEffectInstance> effects = new();
    private long nextId = 1;

    public int Count => effects.Count;

    public RuntimeEffectInstance Add(RuntimeEffectSpec spec)
    {
        if (spec.TargetEntityId == 0)
        {
            throw new ArgumentException("target entity id is required", nameof(spec));
        }

        var id = new RuntimeEffectId(nextId++);
        var instance = new RuntimeEffectInstance(id, spec);
        effects.Add(id, instance);
        return instance;
    }

    public bool Remove(RuntimeEffectId id)
    {
        return effects.Remove(id);
    }

    public bool TryRemove(RuntimeEffectId id, out RuntimeEffectInstance instance)
    {
        if (!effects.TryGetValue(id, out instance))
        {
            return false;
        }

        effects.Remove(id);
        return true;
    }

    public IReadOnlyList<RuntimeEffectId> Expire(long tick)
    {
        return ExpireInstances(tick)
            .Select(effect => effect.Id)
            .ToArray();
    }

    public IReadOnlyList<RuntimeEffectInstance> ExpireInstances(long tick)
    {
        RuntimeEffectId[] expired = effects
            .Where(pair => pair.Value.HasExpiredAt(tick))
            .Select(pair => pair.Key)
            .ToArray();
        var result = new List<RuntimeEffectInstance>();
        for (int i = 0; i < expired.Length; i++)
        {
            result.Add(effects[expired[i]]);
            effects.Remove(expired[i]);
        }

        return result;
    }

    public IReadOnlyList<RuntimeEffectInstance> ActiveAt(long tick)
    {
        return effects.Values
            .Where(effect => effect.IsActiveAt(tick))
            .OrderBy(effect => effect.Id.Value)
            .ToArray();
    }
}

public sealed class EffectLifecycleSystem
{
    public RuntimeEffectLifecycleResult Active(RuntimeEffectStore store, long tick)
    {
        return new RuntimeEffectLifecycleResult(store.ActiveAt(tick), Array.Empty<RuntimeEffectInstance>(), Array.Empty<RuntimeEffectInstance>());
    }

    public RuntimeEffectLifecycleResult Remove(RuntimeEffectStore store, RuntimeEffectId id, long tick)
    {
        RuntimeEffectInstance[] removed = store.TryRemove(id, out RuntimeEffectInstance instance)
            ? new[] { instance }
            : Array.Empty<RuntimeEffectInstance>();
        return new RuntimeEffectLifecycleResult(store.ActiveAt(tick), removed, Array.Empty<RuntimeEffectInstance>());
    }

    public RuntimeEffectLifecycleResult Expire(RuntimeEffectStore store, long tick)
    {
        IReadOnlyList<RuntimeEffectInstance> expired = store.ExpireInstances(tick);
        return new RuntimeEffectLifecycleResult(store.ActiveAt(tick), Array.Empty<RuntimeEffectInstance>(), expired);
    }
}
}
