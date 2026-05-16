using System;

namespace DG.GameCore
{
public sealed class NoopBlockedOutcomeHandler : IBlockedOutcomeHandler
{
    public void Apply(BlockedOutcomeContext context, BlockedResultDecision decision, ActionArbitrationResult result)
    {
        GameEntity blocking = BlockedOutcomeUtility.FirstBlocking(context.World, context.Contacts);
        BlockedResultBranch branch = decision.Branch;
        ActionExecutionOutput output = BlockedOutcomeUtility.BuildBlockedExecutionOutput(context, branch, BlockedOutcomeUtility.BuildDeferredOutputResult(context.Current, context.Direction, context.Request, blocking), Array.Empty<DeferredAction>(), Array.Empty<CommitProposal>());
        BlockedOutcomeUtility.ApplyExecutionOutput(output, result);
        result.Derive(new DerivedAction(context.Request, context.Spec, ActionResultBranch.Noop, branch.Reason), output.BlockedResult);
    }
}
}
