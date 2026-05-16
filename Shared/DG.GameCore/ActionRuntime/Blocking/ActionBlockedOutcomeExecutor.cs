using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class ActionBlockedOutcomeExecutor
{
    private readonly BlockedResultResolver blockedResultResolver = new();
    private readonly Dictionary<BlockedResultKind, IBlockedOutcomeHandler> handlers;

    public ActionBlockedOutcomeExecutor()
    {
        handlers = new Dictionary<BlockedResultKind, IBlockedOutcomeHandler>
        {
            [BlockedResultKind.DeriveAction] = new DeriveActionBlockedOutcomeHandler(),
            [BlockedResultKind.Bounce] = new BounceBlockedOutcomeHandler(),
            [BlockedResultKind.Noop] = new NoopBlockedOutcomeHandler(),
            [BlockedResultKind.Reject] = new RejectBlockedOutcomeHandler()
        };
    }

    public void Resolve(BlockedResultPolicy policy, BlockedOutcomeContext context, ActionArbitrationResult result)
    {
        GameEntity blocking = BlockedOutcomeUtility.FirstBlocking(context.World, context.Contacts);
        if (!blockedResultResolver.TryResolve(policy, new BlockedResultContext(context.World, context.Request, context.Spec, context.Body, context.Contacts, context.Direction), out BlockedResultDecision decision))
        {
            RejectBlockedOutcomeHandler.RejectBlocked(context.World, context.Request, context.Spec, result, context.Current, context.Direction, blocking);
            return;
        }

        BlockedResultBranch branch = decision.Branch;
        if (!handlers.TryGetValue(branch.ResultKind, out IBlockedOutcomeHandler handler))
        {
            handler = handlers[BlockedResultKind.Reject];
        }

        handler.Apply(context, decision, result);
    }
}
}
