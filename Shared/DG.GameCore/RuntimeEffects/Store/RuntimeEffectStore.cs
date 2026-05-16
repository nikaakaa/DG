using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
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

        RuntimeEffectInstance? existing = FindStackMatch(spec);
        if (existing.HasValue)
        {
            if (spec.StackPolicy == EffectStackPolicy.RejectDuplicate)
            {
                return existing.Value;
            }

            if (spec.StackPolicy == EffectStackPolicy.RefreshDuration ||
                spec.StackPolicy == EffectStackPolicy.ReplaceByStackKey)
            {
                effects.Remove(existing.Value.Id);
            }
        }

        var id = new RuntimeEffectId(nextId++);
        var instance = new RuntimeEffectInstance(id, spec);
        effects.Add(id, instance);
        return instance;
    }

    public RuntimeEffectInstance Add(EffectApplication application)
    {
        return Add(application.ToRuntimeSpec());
    }

    public bool Remove(RuntimeEffectId id)
    {
        return effects.Remove(id);
    }

    public IReadOnlyList<RuntimeEffectInstance> RemoveByTarget(long targetEntityId)
    {
        RuntimeEffectId[] ids = effects
            .Where(pair => pair.Value.TargetEntityId == targetEntityId)
            .Select(pair => pair.Key)
            .ToArray();
        var removed = new List<RuntimeEffectInstance>();
        for (int i = 0; i < ids.Length; i++)
        {
            removed.Add(effects[ids[i]]);
            effects.Remove(ids[i]);
        }

        return removed;
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
        RuntimeEffectId[] expired = CollectExpired(tick, CandidateScanMode.Serial).ToArray();
        for (int i = 0; i < expired.Length; i++)
        {
            effects.Remove(expired[i]);
        }

        return expired;
    }

    public IReadOnlyList<RuntimeEffectInstance> ExpireInstances(long tick)
    {
        RuntimeEffectId[] expired = CollectExpired(tick, CandidateScanMode.Serial).ToArray();
        var result = new List<RuntimeEffectInstance>();
        for (int i = 0; i < expired.Length; i++)
        {
            result.Add(effects[expired[i]]);
            effects.Remove(expired[i]);
        }

        return result;
    }

    public IReadOnlyList<RuntimeEffectId> CollectExpired(long tick, CandidateScanMode mode)
    {
        IEnumerable<KeyValuePair<RuntimeEffectId, RuntimeEffectInstance>> query = mode == CandidateScanMode.Parallel
            ? effects.AsParallel().Where(pair => pair.Value.HasExpiredAt(tick))
            : effects.Where(pair => pair.Value.HasExpiredAt(tick));
        return query
            .Select(pair => pair.Key)
            .OrderBy(id => id.Value)
            .ToArray();
    }

    public IReadOnlyList<RuntimeEffectInstance> ActiveAt(long tick)
    {
        return effects.Values
            .Where(effect => effect.IsActiveAt(tick))
            .OrderBy(effect => effect.Id.Value)
            .ToArray();
    }

    public bool TryFind(long targetEntityId, RuntimeEffectKind kind, RuntimeEffectId requestedEffectId, out RuntimeEffectInstance instance)
    {
        foreach (RuntimeEffectInstance effect in effects.Values.OrderByDescending(effect => effect.Id.Value))
        {
            if (effect.TargetEntityId == targetEntityId &&
                effect.Kind == kind &&
                (!requestedEffectId.IsValid || effect.Id.Equals(requestedEffectId)))
            {
                instance = effect;
                return true;
            }
        }

        instance = default;
        return false;
    }

    private RuntimeEffectInstance? FindStackMatch(RuntimeEffectSpec spec)
    {
        if (spec.StackPolicy == EffectStackPolicy.AllowMultiple ||
            string.IsNullOrWhiteSpace(spec.StackKey))
        {
            return null;
        }

        foreach (RuntimeEffectInstance effect in effects.Values
            .Where(effect => effect.Spec.TargetEntityId == spec.TargetEntityId &&
                effect.Spec.EffectSpecId.Equals(spec.EffectSpecId) &&
                effect.Spec.StackKey == spec.StackKey)
            .OrderBy(effect => effect.Id.Value))
        {
            return effect;
        }

        return null;
    }
}

}
