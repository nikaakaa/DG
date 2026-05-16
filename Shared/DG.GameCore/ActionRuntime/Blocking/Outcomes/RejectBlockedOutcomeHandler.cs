namespace DG.GameCore
{
public sealed class RejectBlockedOutcomeHandler : IBlockedOutcomeHandler
{
    public void Apply(BlockedOutcomeContext context, BlockedResultDecision decision, ActionArbitrationResult result)
    {
        GameEntity blocking = BlockedOutcomeUtility.FirstBlocking(context.World, context.Contacts);
        RejectBlocked(context.World, context.Request, context.Spec, decision.Branch, result, context.Current, context.Direction, blocking);
    }

    public static void RejectBlocked(GameWorld world, ActionRequest request, ActionSpec spec, BlockedResultBranch branch, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking)
    {
        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        MoveErrorCode errorCode = targetPlayerControlled && branch.ErrorCode != MoveErrorCode.Immune ? MoveErrorCode.Occupied : branch.ErrorCode;
        if (errorCode == MoveErrorCode.None)
        {
            errorCode = MoveErrorCode.Blocked;
        }

        string reason = targetPlayerControlled && errorCode == MoveErrorCode.Occupied ? "occupied by player" : BlockedOutcomeUtility.BranchReason(branch, "blocked cell");
        Reject(request, current, direction, errorCode, reason, false, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), result, spec);
    }

    public static void RejectBlocked(GameWorld world, ActionRequest request, ActionSpec spec, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking)
    {
        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        Reject(request, current, direction, targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked, targetPlayerControlled ? "occupied by player" : "blocked cell", false, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), result, spec);
    }

    public static void Reject(ActionRequest request, GridCoord coord, Direction direction, MoveErrorCode errorCode, string reason, bool bounced, CollisionInfo collision, ActionArbitrationResult result, ActionSpec spec)
    {
        var moveResult = new MoveResult(false, request.EntityId, coord, direction, errorCode, reason, bounced, collision, request.ClientTick);
        result.Reject(new RejectedAction(request, spec, moveResult, reason));
    }
}
}
