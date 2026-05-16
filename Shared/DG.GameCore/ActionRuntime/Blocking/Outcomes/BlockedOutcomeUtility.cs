using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
internal static class BlockedOutcomeUtility
{
    public static GameEntity FirstBlocking(GameWorld world, IReadOnlyList<ExternalPushContact> contacts)
    {
        return contacts.Count != 0 && world.TryGetEntity(contacts[0].BlockerEntityId, out GameEntity blocking) ? blocking : null!;
    }

    public static void ApplyExecutionOutput(ActionExecutionOutput output, ActionArbitrationResult result)
    {
        for (int i = 0; i < output.CommitProposals.Count; i++)
        {
            result.AddProposal(output.CommitProposals[i]);
        }

        for (int i = 0; i < output.DeferredActions.Count; i++)
        {
            result.AddDeferred(output.DeferredActions[i]);
        }
    }

    public static ActionExecutionOutput BuildBlockedExecutionOutput(BlockedOutcomeContext context, BlockedResultBranch branch, MoveResult moveResult, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<CommitProposal> commitProposals)
    {
        return new ActionExecutionOutput(context.ActionContext, ActionExecutionSuccessPolicy.AllOrNothing, context.TargetData, System.Array.Empty<ActionClaim>(), commitProposals, deferredActions, moveResult);
    }

    public static ActionExecutionOutput BuildBlockedExecutionOutput(GameWorld world, ActionRequest request, ActionSpec spec, BlockedResultBranch branch, GridCoord current, Direction direction, GameEntity blocking, long serverTick, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<CommitProposal> commitProposals, MoveResult moveResult)
    {
        ActionContext.TryCreate(request, spec, out ActionContext context, out _);
        var targetData = new[] { ActionTargetData.Cell(BranchReason(branch, "blocked"), current.Add(direction), direction, 0) };
        return new ActionExecutionOutput(context, ActionExecutionSuccessPolicy.AllOrNothing, targetData, System.Array.Empty<ActionClaim>(), commitProposals, deferredActions, moveResult);
    }

    public static string BuildDeferredDedupeKey(ActionRequest request, long targetEntityId, IReadOnlyList<long> subjectEntityIds, Direction direction, long serverTick)
    {
        return request.OwnerActionId + ":" + serverTick + ":" + direction + ":" + BuildSubjectKey(targetEntityId, subjectEntityIds);
    }

    public static string BuildSubjectKey(long targetEntityId, IReadOnlyList<long> subjectEntityIds)
    {
        IReadOnlyList<long> ids = subjectEntityIds == null || subjectEntityIds.Count == 0 ? new[] { targetEntityId } : subjectEntityIds;
        return string.Join("|", ids.OrderBy(id => id));
    }

    public static string BranchReason(BlockedResultBranch branch, string fallback)
    {
        return string.IsNullOrEmpty(branch.Reason) ? fallback : branch.Reason;
    }

    public static MoveResult BuildDeferredOutputResult(GridCoord current, Direction direction, ActionRequest request, GameEntity blocking)
    {
        return new MoveResult(true, request.EntityId, current, direction, MoveErrorCode.None, "bounded/deferred-output", false, new CollisionInfo(blocking.EntityId, true, false), request.ClientTick);
    }
}
}
