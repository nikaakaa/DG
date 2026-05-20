using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
    public sealed class MoveBatchOrchestrator
    {
        private readonly ActionSpecRegistry actionSpecs;
        private readonly BatchBehaviorRunnerRegistry batchRunners;
        private readonly PushRunner pushRunner;
        private readonly StepRunner stepRunner;
        private readonly BehaviorInstanceRunner behaviorRunner;

        public MoveBatchOrchestrator(ActionSpecRegistry actionSpecs, BatchBehaviorRunnerRegistry batchRunners, PushRunner pushRunner, StepRunner stepRunner, BehaviorInstanceRunner behaviorRunner)
        {
            this.actionSpecs = actionSpecs ?? throw new ArgumentNullException(nameof(actionSpecs));
            this.batchRunners = batchRunners ?? throw new ArgumentNullException(nameof(batchRunners));
            this.pushRunner = pushRunner ?? throw new ArgumentNullException(nameof(pushRunner));
            this.stepRunner = stepRunner ?? throw new ArgumentNullException(nameof(stepRunner));
            this.behaviorRunner = behaviorRunner ?? throw new ArgumentNullException(nameof(behaviorRunner));
        }

        public MoveBatchPlanResult Plan(
            GameWorld world,
            IReadOnlyList<ActionRequest> moveRequests,
            long serverTick,
            List<CommitProposal> proposals,
            Dictionary<long, MoveResult> actionResults,
            List<string> reasons,
            List<DeferredAction> deferredActions,
            List<ActionFact> actionFacts,
            List<ActionBehaviorInstance> behaviorInstances)
        {
            ActionBehaviorResult behaviorResult = batchRunners.Resolve(world, moveRequests, serverTick);
            MergeBehaviorResult(behaviorResult, actionResults, reasons, deferredActions);
            QueueBehaviorInstances(world, behaviorResult, actionFacts);
            PushVectorCompositionResult pushComposition = pushRunner.ComposePush(world, behaviorResult.RemainingRequests);
            ApplyPushComposition(world, pushComposition, actionResults, reasons, actionFacts, behaviorInstances, serverTick);
            ActionArbitrationResult arbitration = stepRunner.Arbitrate(world, pushComposition.Requests, serverTick);
            MergeArbitrationResult(world, arbitration, proposals, actionResults, reasons, deferredActions, actionFacts, behaviorInstances, serverTick);
            AddDerivedNoopActionFacts(world, arbitration.DerivedActions, actionResults, actionFacts, serverTick);
            ApplyDerivedArbitrationToReasons(arbitration.DerivedActions, reasons);
            var delayedCommitActionIds = new HashSet<long>();
            IReadOnlyList<MovePlan> movePlans = CreateMovePlans(world, arbitration.AcceptedActions, actionResults, reasons, actionFacts, behaviorInstances, serverTick, delayedCommitActionIds);
            if (behaviorResult.MovePlans.Count != 0)
            {
                movePlans = behaviorResult.MovePlans.Concat(movePlans).ToArray();
            }

            return new MoveBatchPlanResult(behaviorResult, movePlans, delayedCommitActionIds, pushComposition);
        }

        public void Execute(
            GameWorld world,
            IReadOnlyList<ActionRequest> moveRequests,
            long serverTick,
            List<CommitProposal> proposals,
            Dictionary<long, MoveResult> actionResults,
            List<string> reasons,
            List<DeferredAction> deferredActions,
            List<ActionFact> actionFacts,
            List<ActionBehaviorInstance> behaviorInstances,
            List<CommitProposalResult> proposalResults,
            IReadOnlyDictionary<long, ActionSpecId> specIdsByActionId,
            IReadOnlyDictionary<long, ActionRequest> requestsByActionId)
        {
            MoveBatchPlanResult planResult = Plan(world, moveRequests, serverTick, proposals, actionResults, reasons, deferredActions, actionFacts, behaviorInstances);
            proposalResults.AddRange(stepRunner.ResolveCommitProposals(world, FilterDelayedCommitProposals(proposals, planResult.DelayedCommitActionIds)));
            proposalResults.AddRange(stepRunner.ResolveMovePlans(world, planResult.MovePlans, behaviorRunner));
            ApplyProposalResultsToActions(actionResults, proposalResults, reasons, actionFacts, behaviorInstances, serverTick, specIdsByActionId, requestsByActionId);
            ApplyComposedPushResults(actionResults, planResult.PushComposition);

            actionFacts.AddRange(planResult.BehaviorResult.ActionFacts);
            behaviorInstances.AddRange(planResult.BehaviorResult.BehaviorInstances);
        }

        public static IReadOnlyList<CommitProposal> FilterDelayedCommitProposals(IReadOnlyList<CommitProposal> proposals, HashSet<long> delayedCommitActionIds)
        {
            if (delayedCommitActionIds.Count == 0)
            {
                return proposals;
            }

            return proposals
                .Where(proposal => proposal.Kind != CommitProposalKind.SetAutoMoveTick || !delayedCommitActionIds.Contains(proposal.SourceActionId))
                .ToArray();
        }

        public static void ApplyComposedPushResults(Dictionary<long, MoveResult> actionResults, PushVectorCompositionResult composition)
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

        public void ApplyProposalResultsToActions(Dictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, List<string> reasons, List<ActionFact> actionFacts, List<ActionBehaviorInstance> behaviorInstances, long serverTick, IReadOnlyDictionary<long, ActionSpecId> specIdsByActionId, IReadOnlyDictionary<long, ActionRequest> requestsByActionId)
        {
            var groupedMoveActionIds = AddGroupedMoveActionFacts(proposalResults, actionFacts, serverTick, specIdsByActionId);
            var acceptedSourceActionIds = proposalResults
                .Where(result => result.Accepted)
                .Select(result => result.Proposal.SourceActionId)
                .ToHashSet();
            var coveredEntityIds = actionFacts
                .SelectMany(fact => fact.SubjectEntityIds)
                .ToHashSet();
            for (int i = 0; i < proposalResults.Count; i++)
            {
                CommitProposalResult result = proposalResults[i];
                if (result.Proposal.SourceStateId != 0)
                {
                    continue;
                }

                if (result.Accepted)
                {
                    if (!groupedMoveActionIds.Contains(result.Proposal.SourceActionId) &&
                        !coveredEntityIds.Contains(result.Proposal.EntityId) &&
                        CommitFactProjector.TryProject(result.Proposal, serverTick, out ActionFact fact))
                    {
                        actionFacts.Add(fact);
                    }

                    continue;
                }

                if (acceptedSourceActionIds.Contains(result.Proposal.SourceActionId) ||
                    !actionResults.TryGetValue(result.Proposal.SourceActionId, out MoveResult actionResult))
                {
                    continue;
                }

                actionResults[result.Proposal.SourceActionId] = new MoveResult(false, actionResult.EntityId, result.Proposal.From, actionResult.FinalDirection, MoveErrorCode.Blocked, result.Reason, false, default, actionResult.ClientTick);
                if (requestsByActionId.TryGetValue(result.Proposal.SourceActionId, out ActionRequest failedRequest))
                {
                    ActionSpec failedSpec = actionSpecs.Get(failedRequest.SpecId);
                    IReadOnlyList<long> failedSubjectIds = failedRequest.SubjectEntityIds.Count == 0 ? new[] { failedRequest.EntityId } : failedRequest.SubjectEntityIds;
                    ActionBehaviorInstance failedInstance = ActionBehaviorInstance.Failed(failedRequest, failedSpec, failedSubjectIds, serverTick, result.Reason, "commit-conflict");
                    BehaviorStep failedStep = behaviorRunner.RunTerminal(failedInstance, null!);
                    actionFacts.AddRange(failedStep.Output.ActionFacts);
                    behaviorInstances.Add(failedInstance);
                }
                if (!string.IsNullOrEmpty(result.Reason))
                {
                    reasons.Add(result.Reason);
                }
            }
        }

        private static void MergeBehaviorResult(ActionBehaviorResult behaviorResult, Dictionary<long, MoveResult> actionResults, List<string> reasons, List<DeferredAction> deferredActions)
        {
            foreach (KeyValuePair<long, MoveResult> pair in behaviorResult.ActionResults)
            {
                actionResults[pair.Key] = pair.Value;
            }

            deferredActions.AddRange(behaviorResult.DeferredActions);
            for (int i = 0; i < behaviorResult.Reasons.Count; i++)
            {
                reasons.Add(behaviorResult.Reasons[i]);
            }
        }

        private void QueueBehaviorInstances(GameWorld world, ActionBehaviorResult behaviorResult, List<ActionFact> actionFacts)
        {
            for (int i = 0; i < behaviorResult.BehaviorInstances.Count; i++)
            {
                ActionBehaviorInstance instance = behaviorResult.BehaviorInstances[i];
                BehaviorStep enterStep = behaviorRunner.Start(instance, world);
                BehaviorStepOutput enterOutput = enterStep.Output;
                actionFacts.AddRange(enterOutput.ActionFacts);
            }
        }

        private void ApplyPushComposition(GameWorld world, PushVectorCompositionResult composition, Dictionary<long, MoveResult> actionResults, List<string> reasons, List<ActionFact> actionFacts, List<ActionBehaviorInstance> behaviorInstances, long serverTick)
        {
            for (int i = 0; i < composition.CancelledRequests.Count; i++)
            {
                ActionRequest request = composition.CancelledRequests[i];
                ActionSpec spec = actionSpecs.Get(request.SpecId);
                IReadOnlyList<long> subjectIds = request.SubjectEntityIds.Count == 0 ? new[] { request.EntityId } : request.SubjectEntityIds;
                ActionBehaviorInstance cancelledInstance = ActionBehaviorInstance.Cancelled(request, spec, subjectIds, serverTick, "push-vector-cancelled", "push-composition");
                BehaviorStep cancelStep = behaviorRunner.RunTerminal(cancelledInstance, world);
                actionFacts.AddRange(cancelStep.Output.ActionFacts);
                behaviorInstances.Add(cancelledInstance);
                actionResults[request.ActionId] = new MoveResult(false, request.EntityId, CurrentCoord(world, request.EntityId), Direction.None, MoveErrorCode.Blocked, "push-vector-cancelled", false, default, request.ClientTick);
            }

            for (int i = 0; i < composition.Reasons.Count; i++)
            {
                reasons.Add(composition.Reasons[i]);
            }
        }

        private void MergeArbitrationResult(GameWorld world, ActionArbitrationResult arbitration, List<CommitProposal> proposals, Dictionary<long, MoveResult> actionResults, List<string> reasons, List<DeferredAction> deferredActions, List<ActionFact> actionFacts, List<ActionBehaviorInstance> behaviorInstances, long serverTick)
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

            for (int i = 0; i < arbitration.RejectedActions.Count; i++)
            {
                RejectedAction rejected = arbitration.RejectedActions[i];
                IReadOnlyList<long> subjectIds = rejected.Request.SubjectEntityIds.Count == 0 ? new[] { rejected.Request.EntityId } : rejected.Request.SubjectEntityIds;
                ActionBehaviorInstance rejectedInstance = ActionBehaviorInstance.Rejected(rejected.Request, rejected.Spec, subjectIds, serverTick, rejected.Reason, "arbitration-reject");
                BehaviorStep rejectStep = behaviorRunner.RunTerminal(rejectedInstance, world);
                actionFacts.AddRange(rejectStep.Output.ActionFacts);
                behaviorInstances.Add(rejectedInstance);
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

        private static void AddDerivedNoopActionFacts(GameWorld world, IReadOnlyList<DerivedAction> derivedActions, IReadOnlyDictionary<long, MoveResult> actionResults, List<ActionFact> actionFacts, long serverTick)
        {
            for (int i = 0; i < derivedActions.Count; i++)
            {
                DerivedAction action = derivedActions[i];
                if (action.Branch != ActionResultBranch.Noop ||
                    action.Request.Source.SourceStateId != 0 ||
                    !actionResults.TryGetValue(action.Request.ActionId, out MoveResult result) ||
                    !result.Success ||
                    result.FinalDirection == Direction.None)
                {
                    continue;
                }

                IReadOnlyList<long> subjectIds = action.Request.SubjectEntityIds.Count == 0
                    ? new[] { action.Request.EntityId }
                    : action.Request.SubjectEntityIds;
                if (subjectIds.Count > 1)
                {
                    var members = new PresentationFactMember[subjectIds.Count];
                    for (int subjectIndex = 0; subjectIndex < subjectIds.Count; subjectIndex++)
                    {
                        long subjectId = subjectIds[subjectIndex];
                        GridCoord coord = CurrentCoord(world, subjectId);
                        Direction direction = CurrentDirection(world, subjectId);
                        DirectionMask ports = CurrentPortLocalPorts(world, subjectId);
                        members[subjectIndex] = new PresentationFactMember(subjectId, coord, coord, direction, direction, ports, ports);
                    }

                    actionFacts.Add(ActionFact.WithProjection(
                        ActionFactType.BodyMoved,
                        serverTick,
                        PresentationFactType.BodyMoved,
                        PresentationFactResultKind.Success,
                        action.Request.ActionId,
                        action.Request.ClientTick,
                        action.Request.EntityId,
                        subjectIds,
                        members[0].From,
                        members[0].To,
                        result.FinalDirection,
                        serverTick,
                        0,
                        serverTick,
                        0d,
                        action.Request.RuntimeParams.CostTicks,
                        0,
                        default,
                        RotatePivotDirection.None,
                        members,
                        Array.Empty<PresentationFactImpact>()));
                    continue;
                }

                actionFacts.Add(ActionFact.WithProjection(
                    ActionFactType.EntityPushed,
                    serverTick,
                    PresentationFactType.EntityPushed,
                    PresentationFactResultKind.Success,
                    action.Request.ActionId,
                    action.Request.ClientTick,
                    action.Request.EntityId,
                    subjectIds,
                    result.FinalCoord,
                    result.FinalCoord,
                    result.FinalDirection,
                    serverTick,
                    0,
                    serverTick,
                    0d,
                    action.Request.RuntimeParams.CostTicks,
                    0,
                    default,
                    RotatePivotDirection.None,
                    Array.Empty<PresentationFactMember>(),
                    Array.Empty<PresentationFactImpact>()));
            }
        }

        private IReadOnlyList<MovePlan> CreateMovePlans(GameWorld world, IReadOnlyList<AcceptedAction> acceptedActions, Dictionary<long, MoveResult> actionResults, List<string> reasons, List<ActionFact> actionFacts, List<ActionBehaviorInstance> behaviorInstances, long serverTick, HashSet<long> delayedCommitActionIds)
        {
            var movePlans = new List<MovePlan>();
            for (int i = 0; i < acceptedActions.Count; i++)
            {
                AcceptedAction action = acceptedActions[i];
                if (!stepRunner.TryPlanMove(world, action, out MovePlan plan, out PlanResult result))
                {
                    if (actionResults.TryGetValue(action.Request.ActionId, out MoveResult actionResult))
                    {
                        actionResults[action.Request.ActionId] = new MoveResult(false, action.Request.EntityId, CurrentCoord(world, action.Request.EntityId), action.Direction, ErrorCodeFromPlanReason(result.Reason), result.Message, false, default, actionResult.ClientTick);
                    }

                    IReadOnlyList<long> failedSubjectIds = action.Request.SubjectEntityIds.Count == 0 ? new[] { action.Request.EntityId } : action.Request.SubjectEntityIds;
                    ActionBehaviorInstance failedInstance = ActionBehaviorInstance.Failed(action.Request, action.Spec, failedSubjectIds, serverTick, result.Message, "plan-failure");
                    BehaviorStep failedStep = behaviorRunner.RunTerminal(failedInstance, world);
                    actionFacts.AddRange(failedStep.Output.ActionFacts);
                    behaviorInstances.Add(failedInstance);

                    if (!string.IsNullOrEmpty(result.Message))
                    {
                        reasons.Add(result.Message);
                    }

                    continue;
                }

                if (action.Request.RuntimeParams.CostTicks > 0 && action.Request.Source.SourceStateId == 0)
                {
                    QueueMovePlanInstance(world, action, plan, actionFacts, behaviorInstances, serverTick);
                    delayedCommitActionIds.Add(action.Request.ActionId);
                    actionResults.Remove(action.Request.ActionId);
                }
                else
                {
                    movePlans.Add(plan);
                }
            }

            return movePlans;
        }

        private void QueueMovePlanInstance(GameWorld world, AcceptedAction action, MovePlan plan, List<ActionFact> actionFacts, List<ActionBehaviorInstance> behaviorInstances, long serverTick)
        {
            var actionResults = new Dictionary<long, MoveResult>();
            GridCoord finalCoord = plan.Members.Count == 0 ? CurrentCoord(world, action.Request.EntityId) : plan.Members[0].To;
            actionResults[action.Request.ActionId] = new MoveResult(true, action.Request.EntityId, finalCoord, action.Direction, MoveErrorCode.None, string.Empty, false, default, action.Request.ClientTick);
            long completionTick = serverTick + Math.Max(1, action.Request.RuntimeParams.CostTicks);
            var output = new BehaviorStepOutput(
                new[] { plan },
                CreateMovePlanCompletionProposals(action, completionTick),
                Array.Empty<ActionRequest>(),
                actionResults,
                Array.Empty<DeferredAction>(),
                CreateMovePlanCompletionFacts(plan, action, serverTick),
                Array.Empty<string>());
            IReadOnlyList<long> subjectIds = plan.Members.Select(member => member.EntityId).ToArray();
            var reservation = new BehaviorClaimSet(subjectIds, plan.Members.Select(member => member.To).Concat(plan.Members.Select(member => member.From)).ToArray(), Array.Empty<ResourceKey>(), BehaviorIncomingPolicy.RejectIncoming);
            ActionBehaviorInstance instance = ActionBehaviorInstance.Running(action.Request, action.Spec, subjectIds, reservation, serverTick, output, "move-plan");
            BehaviorStep step = behaviorRunner.Start(instance, world);
            actionFacts.AddRange(step.Output.ActionFacts);
            behaviorInstances.Add(instance);
        }

        private static IReadOnlyList<CommitProposal> CreateMovePlanCompletionProposals(AcceptedAction action, long completionTick)
        {
            return (action.Spec.CommitRules & ActionCommitRule.SetAutoMoveTick) != 0
                ? new[] { CommitProposal.SetAutoMoveTick(action.Request.Priority, action.Request.ActionId, action.Request.EntityId, completionTick) }
                : Array.Empty<CommitProposal>();
        }

        private static IReadOnlyList<ActionFact> CreateMovePlanCompletionFacts(MovePlan plan, AcceptedAction action, long serverTick)
        {
            if (plan.Members.Count == 0)
            {
                return Array.Empty<ActionFact>();
            }

            if (plan.Members.Count > 1)
            {
                Direction groupDirection = ResolveDirection(plan.Members[0].From, plan.Members[0].To);
                var subjectIds = plan.Members.Select(member => member.EntityId).ToArray();
                var members = new PresentationFactMember[plan.Members.Count];
                for (int i = 0; i < plan.Members.Count; i++)
                {
                    BodyMember member = plan.Members[i];
                    Direction direction = member.Direction != Direction.None ? member.Direction : ResolveDirection(member.From, member.To);
                    members[i] = new PresentationFactMember(member.EntityId, member.From, member.To, Direction.None, direction, DirectionMask.None, DirectionMask.None);
                }

                return new[]
                {
                    ActionFact.WithProjection(
                        ActionFactType.BodyMoved,
                        serverTick,
                        PresentationFactType.BodyMoved,
                        PresentationFactResultKind.Success,
                        action.Request.ActionId,
                        action.Request.ClientTick,
                        plan.Members[0].EntityId,
                        subjectIds,
                        plan.Members[0].From,
                        plan.Members[0].To,
                        groupDirection,
                        action.Request.CreatedTick,
                        0,
                        serverTick,
                        0d,
                        action.Request.RuntimeParams.CostTicks,
                        0,
                        default,
                        RotatePivotDirection.None,
                        members,
                        Array.Empty<PresentationFactImpact>())
                };
            }

            BodyMember single = plan.Members[0];
            PresentationFactType presentationType = plan.PresentationHint;
            ActionFactType factType = MovePresentationResolver.ToActionFactType(presentationType);
            if (factType == ActionFactType.Unknown)
            {
                return Array.Empty<ActionFact>();
            }

            return new[]
            {
                ActionFact.WithProjection(
                    factType,
                    serverTick,
                    presentationType,
                    PresentationFactResultKind.Success,
                    action.Request.ActionId,
                    action.Request.ClientTick,
                    action.Request.EntityId,
                    new[] { single.EntityId },
                    single.From,
                    single.To,
                    single.Direction != Direction.None ? single.Direction : ResolveDirection(single.From, single.To),
                    action.Request.CreatedTick,
                    0,
                    serverTick,
                    0d,
                    action.Request.RuntimeParams.CostTicks,
                    0,
                    default,
                    RotatePivotDirection.None,
                    Array.Empty<PresentationFactMember>(),
                    Array.Empty<PresentationFactImpact>())
            };
        }

        private HashSet<long> AddGroupedMoveActionFacts(IReadOnlyList<CommitProposalResult> proposalResults, List<ActionFact> actionFacts, long serverTick, IReadOnlyDictionary<long, ActionSpecId> specIdsByActionId)
        {
            var groupedActionIds = new HashSet<long>();
            var coveredEntityIds = actionFacts
                .SelectMany(fact => fact.SubjectEntityIds)
                .ToHashSet();
            foreach (IGrouping<long, CommitProposalResult> group in proposalResults
                .Where(result => result.Accepted &&
                    result.Proposal.SourceStateId == 0 &&
                    result.Proposal.Kind == CommitProposalKind.MoveEntity)
                .GroupBy(result => result.Proposal.SourceActionId))
            {
                if (group.Any(result => coveredEntityIds.Contains(result.Proposal.EntityId)))
                {
                    continue;
                }

                IReadOnlyList<CommitProposal> proposals = group.Select(item => item.Proposal).ToArray();
                if (proposals.Count <= 1)
                {
                    continue;
                }

                if (!specIdsByActionId.TryGetValue(group.Key, out ActionSpecId specId))
                {
                    continue;
                }

                Direction direction = ResolveDirection(proposals[0].From, proposals[0].To);
                var subjectIds = proposals.Select(proposal => proposal.EntityId).ToArray();
                var members = new PresentationFactMember[proposals.Count];
                for (int i = 0; i < proposals.Count; i++)
                {
                    CommitProposal proposal = proposals[i];
                    members[i] = new PresentationFactMember(proposal.EntityId, proposal.From, proposal.To, Direction.None, proposal.Direction != Direction.None ? proposal.Direction : ResolveDirection(proposal.From, proposal.To), DirectionMask.None, DirectionMask.None);
                }

                actionFacts.Add(ActionFact.WithProjection(
                    ActionFactType.BodyMoved,
                    serverTick,
                    PresentationFactType.BodyMoved,
                    PresentationFactResultKind.Success,
                    group.Key,
                    0,
                    proposals[0].EntityId,
                    subjectIds,
                    proposals[0].From,
                    proposals[0].To,
                    direction,
                    serverTick,
                    0,
                    serverTick,
                    0d,
                    0,
                    0,
                    default,
                    RotatePivotDirection.None,
                    members,
                    Array.Empty<PresentationFactImpact>()));
                groupedActionIds.Add(group.Key);
            }

            return groupedActionIds;
        }

        internal static MoveErrorCode ErrorCodeFromPlanReason(PlanFailureReason reason)
        {
            return reason == PlanFailureReason.OccupiedByPlayer ? MoveErrorCode.Occupied :
                reason == PlanFailureReason.InvalidDirection ? MoveErrorCode.InvalidDirection :
                reason == PlanFailureReason.MissingPosition ? MoveErrorCode.MissingPosition :
                reason == PlanFailureReason.UnknownEntity ? MoveErrorCode.UnknownEntity :
                MoveErrorCode.Blocked;
        }

        internal static Direction ResolveDirection(GridCoord from, GridCoord to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dx < 0) return Direction.Left;
            if (dx > 0) return Direction.Right;
            if (dy < 0) return Direction.Down;
            if (dy > 0) return Direction.Up;
            return Direction.None;
        }

        internal static GridCoord CurrentCoord(GameWorld world, long entityId)
        {
            if (world.TryGetEntity(entityId, out GameEntity entity) &&
                world.TryGetComponent(entity, out PositionComponent position))
            {
                return position.Coord;
            }

            return default;
        }

        internal static Direction CurrentDirection(GameWorld world, long entityId)
        {
            return world.TryGetEntity(entityId, out GameEntity entity) &&
                world.TryGetComponent(entity, out DirectionComponent direction)
                ? direction.Direction
                : Direction.None;
        }

        internal static DirectionMask CurrentPortLocalPorts(GameWorld world, long entityId)
        {
            return world.TryGetEntity(entityId, out GameEntity entity) &&
                world.TryGetComponent(entity, out PortConnectorComponent connector)
                ? connector.LocalPorts
                : DirectionMask.None;
        }
    }

    public readonly struct MoveBatchPlanResult
    {
        public MoveBatchPlanResult(ActionBehaviorResult behaviorResult, IReadOnlyList<MovePlan> movePlans, HashSet<long> delayedCommitActionIds, PushVectorCompositionResult pushComposition)
        {
            BehaviorResult = behaviorResult;
            MovePlans = movePlans;
            DelayedCommitActionIds = delayedCommitActionIds;
            PushComposition = pushComposition;
        }

        public ActionBehaviorResult BehaviorResult { get; }
        public IReadOnlyList<MovePlan> MovePlans { get; }
        public HashSet<long> DelayedCommitActionIds { get; }
        public PushVectorCompositionResult PushComposition { get; }
    }
}
