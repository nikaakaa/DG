using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class BounceBlockedOutcomeHandler : IBlockedOutcomeHandler
{
    public void Apply(BlockedOutcomeContext context, BlockedResultDecision decision, ActionArbitrationResult result)
    {
        BlockedResultBranch branch = decision.Branch;
        GameEntity blocking = BlockedOutcomeUtility.FirstBlocking(context.World, context.Contacts);
        bool targetPlayerControlled = context.World.HasComponent<PlayerControlComponent>(blocking);
        string reason = targetPlayerControlled ? "occupied by player" : BlockedOutcomeUtility.BranchReason(branch, "blocked cell");
        MoveErrorCode errorCode = targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked;
        Direction finalDirection = context.Direction;
        bool bounced = false;
        if ((branch.CommitRules & ActionCommitRule.SetDirectionOnBounce) != 0 &&
            context.World.TryGetEntity(context.Request.EntityId, out GameEntity entity) &&
            context.World.HasComponent<BouncableComponent>(entity))
        {
            finalDirection = context.Direction.Opposite();
            bounced = true;
        }

        var moveResult = new MoveResult(false, context.Request.EntityId, context.Current, finalDirection, errorCode, reason, bounced, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), context.Request.ClientTick);
        IReadOnlyList<CommitProposal> proposals = bounced ? new[] { CommitProposal.SetDirection(context.Request.Priority, context.Request.ActionId, 0, context.Request.EntityId, finalDirection, context.ServerTick) } : Array.Empty<CommitProposal>();
        ActionExecutionOutput output = BlockedOutcomeUtility.BuildBlockedExecutionOutput(context.World, context.Request, context.Spec, branch, context.Current, context.Direction, blocking, context.ServerTick, Array.Empty<DeferredAction>(), proposals, moveResult);
        BlockedOutcomeUtility.ApplyExecutionOutput(output, result);
        MoveResult blockedResult = output.BlockedResult ?? moveResult;
        result.Reject(new RejectedAction(context.Request, context.Spec, blockedResult, reason));
    }
}
}
