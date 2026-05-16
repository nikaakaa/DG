using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
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
