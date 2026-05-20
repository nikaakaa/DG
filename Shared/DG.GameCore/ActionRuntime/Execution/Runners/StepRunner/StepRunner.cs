using System.Collections.Generic;

namespace DG.GameCore
{
    public sealed class StepRunner
    {
        public const string RunnerId = "step_runner";

        private readonly CommitResolver commitResolver = new();
        private readonly RulePlanner rulePlanner;
        private readonly ConflictResolver conflictResolver = new();
        private readonly ActionArbiter actionArbiter;

        public StepRunner(ActionSpecRegistry actionSpecs)
            : this(actionSpecs, ActionPresentationRegistry.Default)
        {
        }

        public StepRunner(ActionSpecRegistry actionSpecs, ActionPresentationRegistry presentationRegistry)
        {
            rulePlanner = new RulePlanner(actionSpecs, presentationRegistry);
            actionArbiter = new ActionArbiter(actionSpecs);
        }

        public ActionArbitrationResult Arbitrate(GameWorld world, IReadOnlyList<ActionRequest> requests, long serverTick)
        {
            return actionArbiter.ArbitrateMoves(world, requests, serverTick);
        }

        public bool TryPlanMove(GameWorld world, AcceptedAction action, out MovePlan plan, out PlanResult result)
        {
            return rulePlanner.TryPlanMove(world, action, out plan, out result);
        }

        public IReadOnlyList<CommitProposalResult> ResolveCommitProposals(GameWorld world, IReadOnlyList<CommitProposal> proposals)
        {
            return commitResolver.Resolve(world, proposals);
        }

        public IReadOnlyList<CommitProposalResult> ResolveMovePlans(GameWorld world, IReadOnlyList<MovePlan> plans, BehaviorInstanceRunner runningBehaviors)
        {
            return conflictResolver.Resolve(world, plans, runningBehaviors);
        }
    }
}
