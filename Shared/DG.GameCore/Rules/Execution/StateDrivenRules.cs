using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class StateDrivenRuleExecutionSystem
{
    private readonly CommitResolver commitResolver = new();
    private readonly RulePlanner rulePlanner;
    private readonly ConflictResolver conflictResolver = new();
    private readonly ActionStrategyRegistry strategyRegistry;
    private readonly ActionSpecRegistry actionSpecs;
    private readonly ActionRequestAdapter actionAdapter;
    private readonly ActionArbiter actionArbiter;
    private readonly PushVectorArbiter pushVectorArbiter;

    public StateDrivenRuleExecutionSystem() : this(ActionSpecRegistry.Default)
    {
    }

    public StateDrivenRuleExecutionSystem(ActionSpecRegistry actionSpecs)
        : this(actionSpecs, ActionStrategyRegistry.Default)
    {
    }

    public StateDrivenRuleExecutionSystem(ActionSpecRegistry actionSpecs, ActionStrategyRegistry strategyRegistry)
    {
        this.actionSpecs = actionSpecs;
        this.strategyRegistry = strategyRegistry ?? throw new System.ArgumentNullException(nameof(strategyRegistry));
        rulePlanner = new RulePlanner(actionSpecs);
        actionAdapter = new ActionRequestAdapter(actionSpecs);
        actionArbiter = new ActionArbiter(actionSpecs);
        pushVectorArbiter = new PushVectorArbiter(actionSpecs);
    }

    public StateDrivenRuleExecutionResult Tick(GameWorld world, IReadOnlyList<WorldAction> actions, long serverTick)
    {
        var proposals = new List<CommitProposal>();
        var moveRequests = new List<ActionRequest>();
        var actionResults = new Dictionary<long, MoveResult>();
        var reasons = new List<string>();
        var deferredActions = new List<DeferredAction>();
        var transitions = new List<ActionUnitTransition>();

        for (int i = 0; i < actions.Count; i++)
        {
            ActionRequest request = actionAdapter.FromWorldAction(actions[i]);
            RouteRequest(world, request, proposals, moveRequests, actionResults, reasons, serverTick);
        }

        PushVectorCompositionResult pushComposition = pushVectorArbiter.Compose(world, moveRequests);
        ApplyPushComposition(world, pushComposition, actionResults, reasons);
        ActionArbitrationResult arbitration = actionArbiter.ArbitrateMoves(world, pushComposition.Requests, serverTick);
        transitions.AddRange(arbitration.Transitions);
        MergeArbitrationResult(arbitration, proposals, actionResults, reasons, deferredActions);
        ApplyDerivedArbitrationToReasons(arbitration.DerivedActions, reasons);
        IReadOnlyList<MovePlan> movePlans = CreateMovePlans(world, arbitration.AcceptedActions, actionResults, reasons, transitions);
        var proposalResults = new List<CommitProposalResult>();
        proposalResults.AddRange(commitResolver.Resolve(world, proposals));
        proposalResults.AddRange(conflictResolver.Resolve(world, movePlans));
        ApplyProposalResultsToActions(actionResults, proposalResults, reasons, transitions);
        ApplyComposedPushResults(actionResults, pushComposition);

        return new StateDrivenRuleExecutionResult(actionResults, proposalResults, reasons, deferredActions, transitions);
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

    private void RouteRequest(GameWorld world, ActionRequest request, List<CommitProposal> proposals, List<ActionRequest> moveRequests, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        ActionSpec spec = actionSpecs.Get(request.SpecId);
        IActionStrategy strategy = strategyRegistry.Get(spec);
        strategy.Process(new ActionStrategyContext(world, request, spec, proposals, moveRequests, actionResults, reasons, serverTick));
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

    private IReadOnlyList<MovePlan> CreateMovePlans(GameWorld world, IReadOnlyList<AcceptedAction> acceptedActions, Dictionary<long, MoveResult> actionResults, List<string> reasons, List<ActionUnitTransition> transitions)
    {
        var movePlans = new List<MovePlan>();
        for (int i = 0; i < acceptedActions.Count; i++)
        {
            AcceptedAction action = acceptedActions[i];
            if (!rulePlanner.TryPlanMove(world, action, out MovePlan plan, out PlanResult result))
            {
                transitions.Add(new ActionUnitTransition(action.Request.ActionId, ActionUnitLifecycleState.Accepted, ActionUnitLifecycleState.Failed, result.Message));
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

            transitions.Add(new ActionUnitTransition(action.Request.ActionId, ActionUnitLifecycleState.Accepted, ActionUnitLifecycleState.Planned, string.Empty));
            movePlans.Add(plan);
        }

        return movePlans;
    }

    private static void ApplyProposalResultsToActions(Dictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, List<string> reasons, List<ActionUnitTransition> transitions)
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
                transitions.Add(new ActionUnitTransition(result.Proposal.SourceActionId, ActionUnitLifecycleState.Planned, ActionUnitLifecycleState.Committed, string.Empty));
                continue;
            }

            transitions.Add(new ActionUnitTransition(result.Proposal.SourceActionId, ActionUnitLifecycleState.Planned, ActionUnitLifecycleState.Failed, result.Reason));
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
}
