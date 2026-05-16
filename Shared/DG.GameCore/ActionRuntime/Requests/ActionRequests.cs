using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct ActionRuntimeParams
{
    public ActionRuntimeParams(int configId, long playerId, int autoMoveIntervalTicks, int costTicks = 1, long causalityId = 0, string dedupeKey = "")
        : this(configId, playerId, autoMoveIntervalTicks, false, default, string.Empty, costTicks, causalityId, dedupeKey)
    {
    }

    public ActionRuntimeParams(int configId, long playerId, int autoMoveIntervalTicks, EffectSpecId effectSpecId, string stackKey, int costTicks = 1, long causalityId = 0, string dedupeKey = "")
        : this(configId, playerId, autoMoveIntervalTicks, false, effectSpecId, stackKey, costTicks, causalityId, dedupeKey)
    {
    }

    public ActionRuntimeParams(int configId, long playerId, int autoMoveIntervalTicks, bool rotatePivot, EffectSpecId effectSpecId, string stackKey, int costTicks = 1, long causalityId = 0, string dedupeKey = "")
    {
        ConfigId = configId;
        PlayerId = playerId;
        AutoMoveIntervalTicks = autoMoveIntervalTicks;
        RotatePivot = rotatePivot;
        EffectSpecId = effectSpecId;
        StackKey = stackKey ?? string.Empty;
        CostTicks = Math.Max(1, costTicks);
        CausalityId = causalityId;
        DedupeKey = dedupeKey ?? string.Empty;
    }

    public int ConfigId { get; }
    public long PlayerId { get; }
    public int AutoMoveIntervalTicks { get; }
    public bool RotatePivot { get; }
    public EffectSpecId EffectSpecId { get; }
    public string StackKey { get; }
    public int CostTicks { get; }
    public long CausalityId { get; }
    public string DedupeKey { get; }

    public static ActionRuntimeParams FromWorldAction(WorldAction action)
    {
        return new ActionRuntimeParams(action.ConfigId, action.PlayerId, action.AutoMoveIntervalTicks, action.RotatePivot, default, string.Empty, action.CostTicks, action.CausalityId, action.DedupeKey);
    }
}


public readonly struct ActionTarget
{
    public ActionTarget(long targetEntityId, GridCoord? targetCoord, Direction direction)
    {
        TargetEntityId = targetEntityId;
        TargetCoord = targetCoord;
        Direction = direction;
    }

    public long TargetEntityId { get; }
    public GridCoord? TargetCoord { get; }
    public Direction Direction { get; }
}


public readonly struct ActionSourceContext
{
    public ActionSourceContext(ActionSourceKind kind, long sourceEntityId, long sourceStateId, WorldTag sourceTag)
    {
        Kind = kind;
        SourceEntityId = sourceEntityId;
        SourceStateId = sourceStateId;
        SourceTag = sourceTag;
    }

    public ActionSourceKind Kind { get; }
    public long SourceEntityId { get; }
    public long SourceStateId { get; }
    public WorldTag SourceTag { get; }
}


public readonly struct ActionContext
{
    public ActionContext(long actionId, long ownerActionId, ActionSpecId specId, WorldActionPriority priority, ActionSourceContext source, long instigatorEntityId, long sourceEntityId, long causerEntityId, long subjectEntryEntityId, ActionTarget targetHint, Direction direction, long createdTick, long readyTick, int costTicks, long clientTick, long causalityId)
    {
        ActionId = actionId;
        OwnerActionId = ownerActionId == 0 ? actionId : ownerActionId;
        SpecId = specId;
        Priority = priority;
        Source = source;
        InstigatorEntityId = instigatorEntityId;
        SourceEntityId = sourceEntityId;
        CauserEntityId = causerEntityId;
        SubjectEntryEntityId = subjectEntryEntityId;
        TargetHint = targetHint;
        Direction = direction;
        CreatedTick = createdTick;
        ReadyTick = readyTick;
        CostTicks = Math.Max(1, costTicks);
        ClientTick = clientTick;
        CausalityId = causalityId == 0 ? OwnerActionId : causalityId;
    }

    public long ActionId { get; }
    public long OwnerActionId { get; }
    public ActionSpecId SpecId { get; }
    public WorldActionPriority Priority { get; }
    public ActionSourceContext Source { get; }
    public long InstigatorEntityId { get; }
    public long SourceEntityId { get; }
    public long CauserEntityId { get; }
    public long SubjectEntryEntityId { get; }
    public ActionTarget TargetHint { get; }
    public Direction Direction { get; }
    public long CreatedTick { get; }
    public long ReadyTick { get; }
    public int CostTicks { get; }
    public long ClientTick { get; }
    public long CausalityId { get; }

    public static bool TryCreate(ActionRequest request, ActionSpec spec, out ActionContext context, out string reason)
    {
        context = default;
        if (!request.SpecId.IsValid)
        {
            reason = "missing action spec";
            return false;
        }

        if (request.EntityId == 0)
        {
            reason = "missing subject entry";
            return false;
        }

        if (request.Source.Kind == 0)
        {
            reason = "missing source";
            return false;
        }

        Direction direction = request.Target.Direction;
        if (direction == Direction.None)
        {
            direction = DirectionFromTarget(request.Target.TargetCoord);
        }

        bool needsTargetHint = spec.TargetRule != ActionTargetRule.None && spec.TargetRule != ActionTargetRule.Self && spec.TargetRule != ActionTargetRule.DirectionFromComponent;
        if (needsTargetHint && direction == Direction.None && !request.Target.TargetCoord.HasValue)
        {
            reason = "missing target hint";
            return false;
        }

        long sourceEntityId = request.Source.SourceEntityId == 0 ? request.EntityId : request.Source.SourceEntityId;
        long causalityId = request.RuntimeParams.CausalityId == 0 ? request.OwnerActionId : request.RuntimeParams.CausalityId;
        context = new ActionContext(request.ActionId, request.OwnerActionId, request.SpecId, request.Priority, request.Source, sourceEntityId, sourceEntityId, sourceEntityId, request.EntityId, request.Target, direction, request.CreatedTick, request.ReadyTick, request.RuntimeParams.CostTicks, request.ClientTick, causalityId);
        reason = string.Empty;
        return true;
    }

    private static Direction DirectionFromTarget(GridCoord? target)
    {
        return target.HasValue ? Direction.None : Direction.None;
    }
}


public readonly struct ActionRequest
{
    public ActionRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, ActionSourceContext source, long entityId, ActionTarget target, ActionRuntimeParams runtimeParams, long createdTick, long readyTick, long clientTick)
        : this(actionId, specId, priority, source, entityId, target, runtimeParams, createdTick, readyTick, clientTick, 0, 0, 1, Array.Empty<long>(), Array.Empty<long>())
    {
    }

    public ActionRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, ActionSourceContext source, long entityId, ActionTarget target, ActionRuntimeParams runtimeParams, long createdTick, long readyTick, long clientTick, long ownerActionId, long derivedFromUnitId)
        : this(actionId, specId, priority, source, entityId, target, runtimeParams, createdTick, readyTick, clientTick, ownerActionId, derivedFromUnitId, 1, Array.Empty<long>(), Array.Empty<long>())
    {
    }

    public ActionRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, ActionSourceContext source, long entityId, ActionTarget target, ActionRuntimeParams runtimeParams, long createdTick, long readyTick, long clientTick, long ownerActionId, long derivedFromUnitId, int deferredContributionCount, IReadOnlyList<long> deferredCausalitySamples)
        : this(actionId, specId, priority, source, entityId, target, runtimeParams, createdTick, readyTick, clientTick, ownerActionId, derivedFromUnitId, deferredContributionCount, deferredCausalitySamples, Array.Empty<long>())
    {
    }

    public ActionRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, ActionSourceContext source, long entityId, ActionTarget target, ActionRuntimeParams runtimeParams, long createdTick, long readyTick, long clientTick, long ownerActionId, long derivedFromUnitId, int deferredContributionCount, IReadOnlyList<long> deferredCausalitySamples, IReadOnlyList<long> subjectEntityIds)
        : this(actionId, specId, priority, source, entityId, target, runtimeParams, createdTick, readyTick, clientTick, ownerActionId, derivedFromUnitId, deferredContributionCount, deferredCausalitySamples, subjectEntityIds, Array.Empty<PushOriginContext>())
    {
    }

    public ActionRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, ActionSourceContext source, long entityId, ActionTarget target, ActionRuntimeParams runtimeParams, long createdTick, long readyTick, long clientTick, long ownerActionId, long derivedFromUnitId, int deferredContributionCount, IReadOnlyList<long> deferredCausalitySamples, IReadOnlyList<long> subjectEntityIds, IReadOnlyList<PushOriginContext> pushOriginContexts)
    {
        ActionId = actionId;
        OwnerActionId = ownerActionId == 0 ? actionId : ownerActionId;
        DerivedFromUnitId = derivedFromUnitId;
        SpecId = specId;
        Priority = priority;
        Source = source;
        EntityId = entityId;
        Target = target;
        RuntimeParams = runtimeParams;
        CreatedTick = createdTick;
        ReadyTick = readyTick;
        ClientTick = clientTick;
        DeferredContributionCount = Math.Max(1, deferredContributionCount);
        DeferredCausalitySamples = deferredCausalitySamples == null ? Array.Empty<long>() : deferredCausalitySamples.ToArray();
        SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? Array.Empty<long>() : subjectEntityIds.ToArray();
        PushOriginContexts = pushOriginContexts == null ? Array.Empty<PushOriginContext>() : pushOriginContexts.ToArray();
    }

    public long ActionId { get; }
    public long OwnerActionId { get; }
    public long DerivedFromUnitId { get; }
    public ActionSpecId SpecId { get; }
    public WorldActionPriority Priority { get; }
    public ActionSourceContext Source { get; }
    public long EntityId { get; }
    public ActionTarget Target { get; }
    public ActionRuntimeParams RuntimeParams { get; }
    public long CreatedTick { get; }
    public long ReadyTick { get; }
    public long ClientTick { get; }
    public int DeferredContributionCount { get; }
    public IReadOnlyList<long> DeferredCausalitySamples { get; }
    public IReadOnlyList<long> SubjectEntityIds { get; }
    public IReadOnlyList<PushOriginContext> PushOriginContexts { get; }

    public bool TryCreateContext(ActionSpec spec, out ActionContext context, out string reason)
    {
        return ActionContext.TryCreate(this, spec, out context, out reason);
    }
}

}
