namespace DG.GameCore
{
[ActionStrategy("remove")]
public sealed class RemoveActionStrategy : IActionStrategy
{
    public ActionStrategyId StrategyId => "remove";

    public void Process(ActionStrategyContext context)
    {
        ActionRequest request = context.Request;
        if (!context.World.TryGetEntity(request.EntityId, out GameEntity entity))
        {
            context.ActionResults[request.ActionId] = new MoveResult(false, request.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "entity not found", false, default, request.ClientTick);
            context.Reasons.Add("entity not found");
            return;
        }

        GridCoord coord = context.World.TryGetComponent(entity, out PositionComponent position) ? position.Coord : default;
        context.Proposals.Add(CommitProposal.Delete(request.Priority, request.ActionId, request.EntityId, context.ServerTick));
        context.ActionResults[request.ActionId] = new MoveResult(true, request.EntityId, coord, Direction.None, MoveErrorCode.None, string.Empty, false, default, request.ClientTick);
    }
}
}
