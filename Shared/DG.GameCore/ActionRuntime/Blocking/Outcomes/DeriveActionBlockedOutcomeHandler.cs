using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class DeriveActionBlockedOutcomeHandler : IBlockedOutcomeHandler
{
    private readonly BodyResolver bodyResolver = new();
    private readonly BodyCapabilityResolver bodyCapabilities = new();

    public void Apply(BlockedOutcomeContext context, BlockedResultDecision decision, ActionArbitrationResult result)
    {
        BlockedResultBranch branch = decision.Branch;
        if (!TryResolvePushContacts(context.World, context.Spec, branch, decision.MatchedContacts, out IReadOnlyList<DeferredAction> deferredActions, out GameEntity blocking, out bool playerControlled, context.Request, context.Direction, context.ServerTick))
        {
            RejectPushBlocked(context.World, context.Request, context.Spec, result, context.Current, context.Direction, blocking, playerControlled);
            return;
        }

        if (context.Direction == Direction.None || !context.Spec.Handoff.IsEnabled)
        {
            RejectPushBlocked(context.World, context.Request, context.Spec, result, context.Current, context.Direction, blocking, playerControlled);
            return;
        }

        ActionExecutionOutput output = BlockedOutcomeUtility.BuildBlockedExecutionOutput(context.World, context.Request, context.Spec, branch, context.Current, context.Direction, blocking, context.ServerTick, deferredActions, Array.Empty<CommitProposal>(), BlockedOutcomeUtility.BuildDeferredOutputResult(context.Current, context.Direction, context.Request, blocking));
        BlockedOutcomeUtility.ApplyExecutionOutput(output, result);
        result.Derive(new DerivedAction(context.Request, context.Spec, ActionResultBranch.Noop, "bounded/deferred-output"), output.BlockedResult);
    }

    private bool TryResolvePushContacts(GameWorld world, ActionSpec spec, BlockedResultBranch branch, IReadOnlyList<ExternalPushContact> contacts, out IReadOnlyList<DeferredAction> deferredActions, out GameEntity firstBlocking, out bool firstBlockingPlayerControlled, ActionRequest request, Direction direction, long serverTick)
    {
        var result = new List<DeferredAction>();
        var seenSubjects = new HashSet<string>();
        firstBlocking = null!;
        firstBlockingPlayerControlled = false;
        for (int i = 0; i < contacts.Count; i++)
        {
            if (!world.TryGetEntity(contacts[i].BlockerEntityId, out GameEntity blocking))
            {
                deferredActions = Array.Empty<DeferredAction>();
                return false;
            }

            if (firstBlocking == null)
            {
                firstBlocking = blocking;
                firstBlockingPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
            }

            if (!bodyCapabilities.CanPushEntry(world, blocking))
            {
                firstBlocking = blocking;
                firstBlockingPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
                deferredActions = Array.Empty<DeferredAction>();
                return false;
            }

            ResolveHandoffSubject(world, branch, blocking, out ActionSpecId handoffSpecId, out IReadOnlyList<long> subjectEntityIds);
            if (!seenSubjects.Add(BlockedOutcomeUtility.BuildSubjectKey(blocking.EntityId, subjectEntityIds)))
            {
                continue;
            }

            result.Add(new DeferredAction(handoffSpecId, blocking.EntityId, subjectEntityIds, direction, serverTick, serverTick + spec.DefaultCostTicks, spec.DefaultCostTicks, request.OwnerActionId, BlockedOutcomeUtility.BuildDeferredDedupeKey(request, blocking.EntityId, subjectEntityIds, direction, serverTick)));
        }

        deferredActions = result;
        return result.Count != 0;
    }

    private void ResolveHandoffSubject(GameWorld world, BlockedResultBranch branch, GameEntity blocking, out ActionSpecId specId, out IReadOnlyList<long> subjectEntityIds)
    {
        specId = branch.ResultSpecId;
        if (branch.SubjectKind != ActionSubjectKind.ConnectedBodyIfAny ||
            !bodyResolver.TryResolve(world, blocking, out BehaviorBody body, out _) ||
            body.Kind != BehaviorBodyKind.PortConnected)
        {
            subjectEntityIds = new[] { blocking.EntityId };
            return;
        }

        subjectEntityIds = body.Entities.Select(entity => entity.EntityId).ToArray();
    }

    private static void RejectPushBlocked(GameWorld world, ActionRequest request, ActionSpec spec, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking, bool playerControlled)
    {
        if (request.Source.SourceStateId != 0)
        {
            RejectBlockedOutcomeHandler.Reject(request, current, direction, playerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked, playerControlled ? "push occupied by player" : "push blocked", false, new CollisionInfo(blocking.EntityId, true, playerControlled), result, spec);
            return;
        }

        RejectBlockedOutcomeHandler.RejectBlocked(world, request, spec, result, current, direction, blocking);
    }
}
}
