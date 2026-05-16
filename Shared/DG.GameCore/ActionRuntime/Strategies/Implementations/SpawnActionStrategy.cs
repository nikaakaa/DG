namespace DG.GameCore
{
[ActionStrategy("spawn")]
public sealed class SpawnActionStrategy : IActionStrategy
{
    public ActionStrategyId StrategyId => "spawn";

    public void Process(ActionStrategyContext context)
    {
        ActionRequest request = context.Request;
        if (!request.Target.TargetCoord.HasValue)
        {
            context.ActionResults[request.ActionId] = new MoveResult(false, request.EntityId, default, Direction.None, MoveErrorCode.InvalidDirection, "missing target", false, default, request.ClientTick);
            context.Reasons.Add("missing target");
            return;
        }

        context.Proposals.Add(CommitProposal.Create(request.Priority, request.ActionId, request.EntityId, request.RuntimeParams.ConfigId, request.Target.TargetCoord.Value, request.Target.Direction, request.RuntimeParams.PlayerId, request.RuntimeParams.AutoMoveIntervalTicks, request.RuntimeParams.RotatePivot, context.ServerTick));
        context.ActionResults[request.ActionId] = new MoveResult(true, request.EntityId, request.Target.TargetCoord.Value, request.Target.Direction, MoveErrorCode.None, string.Empty, false, default, request.ClientTick);
    }
}
}
