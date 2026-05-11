using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class StateDrivenRuleExecutionSystem
{
    private readonly CommitResolver commitResolver = new();
    private readonly RulePlanner rulePlanner;
    private readonly ConflictResolver conflictResolver = new();
    private readonly ActionPrimitiveProcessor primitiveProcessor = new();
    private readonly ActionSpecRegistry actionSpecs;
    private readonly ActionRequestAdapter actionAdapter;
    private readonly ActionArbiter actionArbiter;
    private readonly PushVectorArbiter pushVectorArbiter;

    public StateDrivenRuleExecutionSystem() : this(ActionSpecRegistry.Default)
    {
    }

    public StateDrivenRuleExecutionSystem(ActionSpecRegistry actionSpecs)
    {
        this.actionSpecs = actionSpecs;
        rulePlanner = new RulePlanner(actionSpecs);
        actionAdapter = new ActionRequestAdapter(actionSpecs);
        actionArbiter = new ActionArbiter(actionSpecs);
        pushVectorArbiter = new PushVectorArbiter(actionSpecs);
    }

    public StateDrivenRuleExecutionResult Tick(GameWorld world, IReadOnlyList<WorldAction> actions, long serverTick)
    {
        return TickCore(world, actions, null, serverTick);
    }

    public StateDrivenRuleExecutionResult Tick(GameWorld world, IReadOnlyList<WorldAction> actions, PendingRuleStateStore pendingStates, long serverTick)
    {
        return TickCore(world, actions, pendingStates, serverTick);
    }

    private StateDrivenRuleExecutionResult TickCore(GameWorld world, IReadOnlyList<WorldAction> actions, PendingRuleStateStore? pendingStates, long serverTick)
    {
        var proposals = new List<CommitProposal>();
        var moveRequests = new List<ActionRequest>();
        var actionResults = new Dictionary<long, MoveResult>();
        var reasons = new List<string>();
        var deferredActions = new List<DeferredAction>();

        for (int i = 0; i < actions.Count; i++)
        {
            ActionRequest request = actionAdapter.FromWorldAction(actions[i]);
            RouteRequest(world, request, pendingStates, proposals, moveRequests, actionResults, reasons, serverTick);
        }

        if (pendingStates != null)
        {
            AddPendingActionRequests(world, pendingStates, moveRequests, actionResults, reasons, serverTick);
        }

        PushVectorCompositionResult pushComposition = pushVectorArbiter.Compose(world, moveRequests);
        ApplyPushComposition(world, pushComposition, actionResults, reasons);
        ActionArbitrationResult arbitration = actionArbiter.ArbitrateMoves(world, pushComposition.Requests, serverTick);
        MergeArbitrationResult(arbitration, proposals, actionResults, reasons, deferredActions);
        if (pendingStates != null)
        {
            ApplyCompletedPendingStates(world, pendingStates, actionResults, reasons);
            ApplyRejectedArbitrationToPendingStates(world, pendingStates, arbitration.RejectedActions, actionResults, reasons, serverTick);
        }

        ApplyDerivedArbitrationToReasons(arbitration.DerivedActions, reasons);
        IReadOnlyList<MovePlan> movePlans = CreateMovePlans(world, pendingStates, arbitration.AcceptedActions, actionResults, reasons, serverTick);
        var proposalResults = new List<CommitProposalResult>();
        proposalResults.AddRange(commitResolver.Resolve(world, proposals));
        proposalResults.AddRange(conflictResolver.Resolve(world, movePlans));
        ApplyProposalResultsToActions(actionResults, proposalResults, reasons);
        ApplyComposedPushResults(actionResults, pushComposition);
        if (pendingStates != null)
        {
            ApplyProposalResultsToPendingStates(world, pendingStates, proposalResults, actionResults, reasons, serverTick);
            pendingStates.CleanupInactive();
        }

        return new StateDrivenRuleExecutionResult(actionResults, proposalResults, pendingStates?.ActiveCount ?? 0, reasons, deferredActions);
    }

    private static void ApplyCompletedPendingStates(GameWorld world, PendingRuleStateStore pendingStates, Dictionary<long, MoveResult> actionResults, List<string> reasons)
    {
        IReadOnlyList<PendingActionState> states = pendingStates.ActionStates;
        for (int i = 0; i < states.Count; i++)
        {
            PendingActionState state = states[i];
            if (state.Status != PendingRuleStatus.Completed || !state.ReportsOwnerResult || actionResults.ContainsKey(state.OwnerActionId))
            {
                continue;
            }

            actionResults[state.OwnerActionId] = new MoveResult(true, state.RootEntityId, CurrentCoord(world, state.RootEntityId), state.Direction, MoveErrorCode.None, state.Reason, false, default, state.ClientTick);
            if (!string.IsNullOrEmpty(state.Reason))
            {
                reasons.Add(state.Reason);
            }
        }
    }

    private static void ApplyPushComposition(GameWorld world, PushVectorCompositionResult composition, Dictionary<long, MoveResult> actionResults, List<string> reasons)
    {
        for (int i = 0; i < composition.CancelledRequests.Count; i++)
        {
            ActionRequest request = composition.CancelledRequests[i];
            actionResults[request.ActionId] = new MoveResult(false, request.EntityId, CurrentCoord(world, request.EntityId), Direction.None, MoveErrorCode.Blocked, "push-vector-cancelled", false, default, request.ClientTick);
        }

        for (int i = 0; i < composition.Reasons.Count; i++)
        {
            reasons.Add(composition.Reasons[i]);
        }
    }

    private void RouteRequest(GameWorld world, ActionRequest request, PendingRuleStateStore? pendingStates, List<CommitProposal> proposals, List<ActionRequest> moveRequests, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        ActionSpec spec = actionSpecs.Get(request.SpecId);
        if (spec.Primitive == ActionPrimitive.Move)
        {
            moveRequests.Add(request);
            return;
        }

        primitiveProcessor.Process(world, request, spec, pendingStates, proposals, actionResults, reasons, serverTick);
    }

    private void AddPendingActionRequests(GameWorld world, PendingRuleStateStore pendingStates, List<ActionRequest> moveRequests, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        IReadOnlyList<PendingActionState> states = pendingStates.ActionStates;
        for (int i = 0; i < states.Count; i++)
        {
            PendingActionState state = states[i];
            if (state.Status != PendingRuleStatus.Active)
            {
                continue;
            }

            if (serverTick > state.TimeoutTick)
            {
                state.Fail("pending timeout", serverTick);
                if (state.ReportsOwnerResult)
                {
                    actionResults[state.OwnerActionId] = new MoveResult(false, state.RootEntityId, CurrentCoord(world, state.RootEntityId), state.Direction, MoveErrorCode.Blocked, "pending timeout", false, default, state.ClientTick);
                }

                reasons.Add("pending timeout");
                continue;
            }

            if (state.HasReadyUnit(serverTick))
            {
                IReadOnlyList<PendingActionUnit> readyUnits = state.ReadyUnits;
                for (int unitIndex = 0; unitIndex < readyUnits.Count; unitIndex++)
                {
                    if (readyUnits[unitIndex].ReadyTick <= serverTick)
                    {
                        moveRequests.Add(actionAdapter.FromPendingActionUnit(state, readyUnits[unitIndex], serverTick));
                    }
                }
            }
        }
    }

    private static void MergeArbitrationResult(ActionArbitrationResult arbitration, List<CommitProposal> proposals, Dictionary<long, MoveResult> actionResults, List<string> reasons, List<DeferredAction> deferredActions)
    {
        proposals.AddRange(arbitration.CommitProposals);
        deferredActions.AddRange(arbitration.DeferredActions);
        foreach (KeyValuePair<long, MoveResult> pair in arbitration.ActionResults)
        {
            actionResults[pair.Key] = pair.Value;
        }

        for (int i = 0; i < arbitration.Reasons.Count; i++)
        {
            reasons.Add(arbitration.Reasons[i]);
        }
    }

    private static void ApplyRejectedArbitrationToPendingStates(GameWorld world, PendingRuleStateStore pendingStates, IReadOnlyList<RejectedAction> rejectedActions, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        for (int i = 0; i < rejectedActions.Count; i++)
        {
            RejectedAction rejected = rejectedActions[i];
            if (rejected.Request.Source.SourceStateId == 0)
            {
                continue;
            }

            if (TryGetActiveActionState(pendingStates, rejected.Request.Source.SourceStateId, out PendingActionState state))
            {
                state.FailUnit(rejected.Request.ActionId, rejected.Reason, serverTick);
                if (state.ReportsOwnerResult)
                {
                    actionResults[state.OwnerActionId] = new MoveResult(false, state.RootEntityId, CurrentCoord(world, state.RootEntityId), state.Direction, rejected.Result.ErrorCode, rejected.Reason, false, rejected.Result.Collision, state.ClientTick);
                }

                if (!string.IsNullOrEmpty(rejected.Reason))
                {
                    reasons.Add(rejected.Reason);
                }
            }
        }
    }

    private static void ApplyDerivedArbitrationToReasons(IReadOnlyList<DerivedAction> derivedActions, List<string> reasons)
    {
        for (int i = 0; i < derivedActions.Count; i++)
        {
            string reason = derivedActions[i].Reason;
            if (!string.IsNullOrEmpty(reason))
            {
                reasons.Add(reason);
            }
        }
    }

    private IReadOnlyList<MovePlan> CreateMovePlans(GameWorld world, PendingRuleStateStore? pendingStates, IReadOnlyList<AcceptedAction> acceptedActions, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        var movePlans = new List<MovePlan>();
        for (int i = 0; i < acceptedActions.Count; i++)
        {
            AcceptedAction action = acceptedActions[i];
            if (!rulePlanner.TryPlanMove(world, action, out MovePlan plan, out PlanResult result))
            {
                if (pendingStates != null && action.Request.Source.SourceStateId != 0)
                {
                    FailPendingState(pendingStates, action.Request.Source.SourceStateId, action.Request.ActionId, result.Message, serverTick);
                    reasons.Add(result.Message);
                    continue;
                }

                if (actionResults.TryGetValue(action.Request.ActionId, out MoveResult actionResult))
                {
                    actionResults[action.Request.ActionId] = new MoveResult(false, action.Request.EntityId, CurrentCoord(world, action.Request.EntityId), action.Direction, ErrorCodeFromPlanReason(result.Reason), result.Message, false, default, actionResult.ClientTick);
                }

                if (!string.IsNullOrEmpty(result.Message))
                {
                    reasons.Add(result.Message);
                }

                continue;
            }

            movePlans.Add(plan);
        }

        return movePlans;
    }

    private static void ApplyProposalResultsToPendingStates(GameWorld world, PendingRuleStateStore pendingStates, IReadOnlyList<CommitProposalResult> proposalResults, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        IReadOnlyList<PendingActionState> states = pendingStates.ActionStates;
        var groups = proposalResults
            .Where(result => result.Proposal.SourceStateId != 0)
            .GroupBy(result => (result.Proposal.SourceStateId, result.Proposal.SourceActionId))
            .ToArray();
        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            IGrouping<(long SourceStateId, long SourceActionId), CommitProposalResult> group = groups[groupIndex];
            PendingActionState state = FindState(states, group.Key.SourceStateId);
            if (state == null || state.Status != PendingRuleStatus.Active)
            {
                continue;
            }

            IReadOnlyList<CommitProposalResult> unitResults = group.ToArray();
            CommitProposalResult failed = unitResults.FirstOrDefault(result => !result.Accepted);
            if (!failed.Equals(default(CommitProposalResult)) && !failed.Accepted)
            {
                string reason = failed.Reason;
                state.FailUnit(group.Key.SourceActionId, reason, serverTick);
                if (!string.IsNullOrEmpty(reason))
                {
                    reasons.Add(reason);
                }

                if (state.ReportsOwnerResult)
                {
                    actionResults[state.OwnerActionId] = BuildFailedOwnerResult(state, failed, reason);
                }

                continue;
            }

            if (state.MarkUnitAccepted(group.Key.SourceActionId, serverTick, out bool ownerCompleted) && ownerCompleted && state.ReportsOwnerResult)
            {
                actionResults[state.OwnerActionId] = BuildSuccessfulOwnerResult(world, state);
            }
        }

        for (int resultIndex = 0; resultIndex < proposalResults.Count; resultIndex++)
        {
            CommitProposalResult result = proposalResults[resultIndex];
            if (result.Proposal.SourceStateId == 0 &&
                !result.Accepted &&
                !string.IsNullOrEmpty(result.Reason))
            {
                reasons.Add(result.Reason);
            }
        }
    }

    private static void ApplyProposalResultsToActions(Dictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, List<string> reasons)
    {
        for (int i = 0; i < proposalResults.Count; i++)
        {
            CommitProposalResult result = proposalResults[i];
            if (result.Proposal.SourceStateId != 0 ||
                !actionResults.TryGetValue(result.Proposal.SourceActionId, out MoveResult actionResult))
            {
                continue;
            }

            if (result.Accepted)
            {
                continue;
            }

            actionResults[result.Proposal.SourceActionId] = new MoveResult(false, actionResult.EntityId, result.Proposal.From, actionResult.FinalDirection, MoveErrorCode.Blocked, result.Reason, false, default, actionResult.ClientTick);
            if (!string.IsNullOrEmpty(result.Reason))
            {
                reasons.Add(result.Reason);
            }
        }
    }

    private static void ApplyComposedPushResults(Dictionary<long, MoveResult> actionResults, PushVectorCompositionResult composition)
    {
        foreach (KeyValuePair<long, IReadOnlyList<ActionRequest>> pair in composition.MergedRequestsByRepresentative)
        {
            if (!actionResults.TryGetValue(pair.Key, out MoveResult representativeResult))
            {
                continue;
            }

            IReadOnlyList<ActionRequest> mergedRequests = pair.Value;
            for (int i = 0; i < mergedRequests.Count; i++)
            {
                ActionRequest request = mergedRequests[i];
                actionResults[request.ActionId] = new MoveResult(
                    representativeResult.Success,
                    request.EntityId,
                    representativeResult.FinalCoord,
                    representativeResult.FinalDirection,
                    representativeResult.ErrorCode,
                    representativeResult.Reason,
                    representativeResult.Bounced,
                    representativeResult.Collision,
                    request.ClientTick);
            }
        }
    }

    private static void FailPendingState(PendingRuleStateStore pendingStates, long stateId, long actionUnitId, string reason, long serverTick)
    {
        if (TryGetActiveActionState(pendingStates, stateId, out PendingActionState state))
        {
            state.FailUnit(actionUnitId, reason, serverTick);
        }
    }

    private static PendingActionState FindState(IReadOnlyList<PendingActionState> states, long stateId)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].StateId == stateId)
            {
                return states[i];
            }
        }

        return null!;
    }

    private static MoveResult BuildSuccessfulOwnerResult(GameWorld world, PendingActionState state)
    {
        return new MoveResult(true, state.RootEntityId, CurrentCoord(world, state.RootEntityId), state.Direction, MoveErrorCode.None, string.Empty, false, default, state.ClientTick);
    }

    private static MoveResult BuildFailedOwnerResult(PendingActionState state, CommitProposalResult result, string reason)
    {
        return new MoveResult(false, state.RootEntityId, result.Proposal.From, state.Direction, MoveErrorCode.Blocked, reason, false, default, state.ClientTick);
    }

    private static bool TryGetActiveActionState(PendingRuleStateStore pendingStates, long stateId, out PendingActionState state)
    {
        IReadOnlyList<PendingActionState> states = pendingStates.ActionStates;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].StateId == stateId && states[i].Status == PendingRuleStatus.Active)
            {
                state = states[i];
                return true;
            }
        }

        state = null!;
        return false;
    }

    private static MoveErrorCode ErrorCodeFromPlanReason(PlanFailureReason reason)
    {
        return reason == PlanFailureReason.OccupiedByPlayer ? MoveErrorCode.Occupied :
            reason == PlanFailureReason.InvalidDirection ? MoveErrorCode.InvalidDirection :
            reason == PlanFailureReason.MissingPosition ? MoveErrorCode.MissingPosition :
            reason == PlanFailureReason.UnknownEntity ? MoveErrorCode.UnknownEntity :
            MoveErrorCode.Blocked;
    }

    private static GridCoord CurrentCoord(GameWorld world, long entityId)
    {
        if (world.TryGetEntity(entityId, out GameEntity entity) &&
            world.TryGetComponent(entity, out PositionComponent position))
        {
            return position.Coord;
        }

        return default;
    }
}

public sealed class ActionPrimitiveProcessor
{
    public void Process(GameWorld world, ActionRequest request, ActionSpec spec, PendingRuleStateStore? pendingStates, List<CommitProposal> proposals, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (spec.Primitive == ActionPrimitive.Spawn)
        {
            AddSpawnProposal(request, proposals, actionResults, reasons, serverTick);
            return;
        }

        if (spec.Primitive == ActionPrimitive.Remove)
        {
            AddRemoveProposal(world, request, pendingStates, proposals, actionResults, reasons, serverTick);
        }
    }

    private static void AddSpawnProposal(ActionRequest request, List<CommitProposal> proposals, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (!request.Target.TargetCoord.HasValue)
        {
            actionResults[request.ActionId] = new MoveResult(false, request.EntityId, default, Direction.None, MoveErrorCode.InvalidDirection, "missing target", false, default, request.ClientTick);
            reasons.Add("missing target");
            return;
        }

        proposals.Add(CommitProposal.Create(request.Priority, request.ActionId, request.EntityId, request.RuntimeParams.ConfigId, request.Target.TargetCoord.Value, request.Target.Direction, request.RuntimeParams.PlayerId, request.RuntimeParams.AutoMoveIntervalTicks, serverTick));
        actionResults[request.ActionId] = new MoveResult(true, request.EntityId, request.Target.TargetCoord.Value, request.Target.Direction, MoveErrorCode.None, string.Empty, false, default, request.ClientTick);
    }

    private static void AddRemoveProposal(GameWorld world, ActionRequest request, PendingRuleStateStore? pendingStates, List<CommitProposal> proposals, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (!world.TryGetEntity(request.EntityId, out GameEntity entity))
        {
            actionResults[request.ActionId] = new MoveResult(false, request.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "entity not found", false, default, request.ClientTick);
            reasons.Add("entity not found");
            return;
        }

        if (pendingStates != null)
        {
            CancelRelatedPendingActionStates(pendingStates, request.EntityId, serverTick);
        }

        GridCoord coord = world.TryGetComponent(entity, out PositionComponent position) ? position.Coord : default;
        proposals.Add(CommitProposal.Delete(request.Priority, request.ActionId, request.EntityId, serverTick));
        actionResults[request.ActionId] = new MoveResult(true, request.EntityId, coord, Direction.None, MoveErrorCode.None, string.Empty, false, default, request.ClientTick);
    }

    private static void CancelRelatedPendingActionStates(PendingRuleStateStore pendingStates, long entityId, long serverTick)
    {
        IReadOnlyList<PendingActionState> states = pendingStates.ActionStates;
        for (int i = 0; i < states.Count; i++)
        {
            PendingActionState state = states[i];
            if (state.Status == PendingRuleStatus.Active && state.Chain.Contains(entityId))
            {
                state.Cancel("related entity removed", serverTick);
            }
        }
    }
}
}
