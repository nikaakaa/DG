using System;
using System.Collections.Generic;

namespace DG.GameCore
{
    public sealed class PrimitiveRequestDispatcher
    {
        private readonly ActionSpecRegistry actionSpecs;
        private readonly PrimitiveRunnerRegistry primitiveRunners;
        private readonly BehaviorResolver behaviorResolver;
        private readonly IGameConfigProvider configProvider;
        private readonly BehaviorInstanceRunner behaviorRunner;

        public PrimitiveRequestDispatcher(ActionSpecRegistry actionSpecs, PrimitiveRunnerRegistry primitiveRunners, BehaviorResolver behaviorResolver, IGameConfigProvider configProvider, BehaviorInstanceRunner behaviorRunner)
        {
            this.actionSpecs = actionSpecs ?? throw new ArgumentNullException(nameof(actionSpecs));
            this.primitiveRunners = primitiveRunners ?? throw new ArgumentNullException(nameof(primitiveRunners));
            this.behaviorResolver = behaviorResolver ?? throw new ArgumentNullException(nameof(behaviorResolver));
            this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            this.behaviorRunner = behaviorRunner ?? throw new ArgumentNullException(nameof(behaviorRunner));
        }

        public void Route(GameWorld world, ActionRequest request, List<CommitProposal> proposals, List<ActionRequest> moveRequests, Dictionary<long, MoveResult> actionResults, List<string> reasons, List<ActionFact> actionFacts, List<ActionBehaviorInstance> behaviorInstances, long serverTick)
        {
            ActionSpec spec = actionSpecs.Get(request.SpecId);
            BehaviorDefinition definition = behaviorResolver.Resolve(spec, world, request);
            Action<PrimitiveRunnerContext> dispatcher = primitiveRunners.Get(definition);
            IReadOnlyList<long> subjectIds = request.SubjectEntityIds.Count == 0 ? new[] { request.EntityId } : request.SubjectEntityIds;
            BehaviorStepOutput output = ExecutePrimitive(world, request, spec, dispatcher, serverTick);
            proposals.AddRange(output.CommitProposals);
            foreach (KeyValuePair<long, MoveResult> pair in output.ActionResults)
            {
                actionResults[pair.Key] = pair.Value;
            }
            moveRequests.AddRange(output.RoutedRequests);
            for (int i = 0; i < output.Reasons.Count; i++)
            {
                reasons.Add(output.Reasons[i]);
            }

            for (int i = 0; i < output.RoutedRequests.Count; i++)
            {
                if (output.RoutedRequests[i].ActionId == request.ActionId)
                {
                    return;
                }
            }

            var reservation = BehaviorClaimSet.FromSubjects(subjectIds, BehaviorIncomingPolicy.RejectIncoming);
            ActionBehaviorInstance completedInstance = ActionBehaviorInstance.Completed(request, spec, subjectIds, reservation, serverTick, "primitive-completed");
            BehaviorStep step = behaviorRunner.RunTerminal(completedInstance, world);
            actionFacts.AddRange(step.Output.ActionFacts);
            behaviorInstances.Add(completedInstance);
        }

        private BehaviorStepOutput ExecutePrimitive(GameWorld world, ActionRequest request, ActionSpec spec, Action<PrimitiveRunnerContext> dispatcher, long serverTick)
        {
            var runnerProposals = new List<CommitProposal>();
            var runnerMoveRequests = new List<ActionRequest>();
            var runnerActionResults = new Dictionary<long, MoveResult>();
            var runnerReasons = new List<string>();
            dispatcher(new PrimitiveRunnerContext(world, request, spec, runnerProposals, runnerMoveRequests, runnerActionResults, runnerReasons, serverTick, configProvider, actionSpecs.TargetFilters));
            return new BehaviorStepOutput(
                Array.Empty<MovePlan>(),
                runnerProposals,
                runnerMoveRequests,
                runnerActionResults,
                Array.Empty<DeferredAction>(),
                Array.Empty<ActionFact>(),
                runnerReasons);
        }
    }
}
