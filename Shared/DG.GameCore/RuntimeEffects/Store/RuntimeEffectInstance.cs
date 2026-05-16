using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
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

}
