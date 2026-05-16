using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
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

}
