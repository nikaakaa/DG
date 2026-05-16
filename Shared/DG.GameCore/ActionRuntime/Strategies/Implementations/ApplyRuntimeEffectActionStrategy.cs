using System.Collections.Generic;

namespace DG.GameCore
{
[ActionStrategy("runtime_effect")]
public sealed class ApplyRuntimeEffectActionStrategy : IActionStrategy
{
    public ActionStrategyId StrategyId => "runtime_effect";

    public void Process(ActionStrategyContext context)
    {
        if (!context.Request.TryCreateContext(context.Spec, out ActionContext actionContext, out string contextReason))
        {
            Fail(context, MoveErrorCode.InvalidDirection, contextReason);
            return;
        }

        EffectSpecId effectSpecId = context.Request.RuntimeParams.EffectSpecId.IsValid ? context.Request.RuntimeParams.EffectSpecId : context.Spec.EffectSpecId;
        if (!effectSpecId.IsValid ||
            context.ConfigProvider == null ||
            !context.ConfigProvider.TryGetEffectSpec(effectSpecId, out EffectSpec spec))
        {
            Fail(context, MoveErrorCode.Blocked, "unknown effect spec");
            return;
        }

        if (!context.World.TryGetEntity(context.Request.EntityId, out GameEntity entity) ||
            !context.World.TryGetComponent(entity, out PositionComponent position))
        {
            Fail(context, MoveErrorCode.UnknownEntity, "entity not found");
            return;
        }

        var targetingSystem = new TargetingSystem(TargetSelectorRegistry.CreateDefault(), context.TargetFilters);
        if (!targetingSystem.TryResolveTargetData(context.World, actionContext, context.Spec, entity, position, out IReadOnlyList<ActionTargetData> targetData, out Direction direction, out _, out MoveErrorCode errorCode, out string reason))
        {
            Fail(context, errorCode, reason);
            return;
        }

        if (targetData.Count == 0)
        {
            targetData = new[] { ActionTargetData.Self(context.Request.EntityId, position.Coord, direction) };
        }

        var applications = new List<EffectApplication>();
        for (int i = 0; i < targetData.Count; i++)
        {
            EffectApplication application = new EffectApplication(actionContext, spec, targetData[i], context.ServerTick, context.Request.RuntimeParams.StackKey);
            applications.Add(application);
            context.Proposals.Add(CommitProposal.AddRuntimeEffect(context.Request.Priority, context.Request.ActionId, application, context.ServerTick));
        }

        GridCoord finalCoord = position.Coord;
        context.ActionResults[context.Request.ActionId] = new MoveResult(true, context.Request.EntityId, finalCoord, direction, MoveErrorCode.None, "effect-application", false, default, context.Request.ClientTick);
        context.Reasons.Add("effect-application:" + applications.Count);
    }

    private static void Fail(ActionStrategyContext context, MoveErrorCode errorCode, string reason)
    {
        GridCoord coord = default;
        if (context.World.TryGetEntity(context.Request.EntityId, out GameEntity entity) &&
            context.World.TryGetComponent(entity, out PositionComponent position))
        {
            coord = position.Coord;
        }

        context.ActionResults[context.Request.ActionId] = new MoveResult(false, context.Request.EntityId, coord, Direction.None, errorCode, reason, false, default, context.Request.ClientTick);
        context.Reasons.Add(reason);
    }
}
}
