using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct EffectApplication
{
    public EffectApplication(ActionContext context, EffectSpec spec, ActionTargetData targetData, long startTick, string stackKey, long? expireTickOverride = null)
    {
        Context = context;
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        TargetData = targetData;
        TargetEntityId = targetData.TargetEntityId != 0 ? targetData.TargetEntityId : context.SubjectEntryEntityId;
        TargetBodyId = targetData.BodyId;
        TargetCell = targetData.TargetCoord;
        StartTick = startTick;
        ExpireTick = expireTickOverride ?? spec.ResolveExpireTick(startTick);
        StackKey = string.IsNullOrWhiteSpace(stackKey) ? BuildDefaultStackKey(context, spec, TargetEntityId) : stackKey;
        CausalityId = context.CausalityId;
    }

    public ActionContext Context { get; }
    public EffectSpec Spec { get; }
    public ActionTargetData TargetData { get; }
    public long TargetEntityId { get; }
    public long TargetBodyId { get; }
    public GridCoord TargetCell { get; }
    public long StartTick { get; }
    public long ExpireTick { get; }
    public string StackKey { get; }
    public long CausalityId { get; }

    public RuntimeEffectSpec ToRuntimeSpec()
    {
        return new RuntimeEffectSpec(
            Spec.SpecId,
            Spec.RuntimeKind,
            TargetEntityId,
            Context.SourceEntityId,
            StartTick,
            ExpireTick,
            StackKey,
            CausalityId,
            Context.SpecId,
            Context.ActionId,
            Spec.AutoMoveIntervalTicks,
            Spec.PortMask,
            Spec.CanMove,
            Spec.CanBePushed,
            Spec.Tag,
            Spec.StackPolicy,
            Spec.CueId);
    }

    private static string BuildDefaultStackKey(ActionContext context, EffectSpec spec, long targetEntityId)
    {
        return context.SourceEntityId + ":" + targetEntityId + ":" + spec.SpecId.RuntimeKey;
    }
}

}
