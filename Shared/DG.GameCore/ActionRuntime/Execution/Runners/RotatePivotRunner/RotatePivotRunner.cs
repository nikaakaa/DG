using System.Collections.Generic;

namespace DG.GameCore
{
    public sealed class RotatePivotRunner : IBatchBehaviorRunner
    {
        public const string RunnerId = "rotate_pivot_runner";

        public const BehaviorClaimChannel DefaultClaimChannel = BehaviorClaimChannel.Movement;
        public const BehaviorClaimMode DefaultClaimMode = BehaviorClaimMode.Exclusive;
        public const int DefaultCostTicks = 1;

        private readonly RotatePivotResponseProcessor processor;

        public RotatePivotRunner(ActionSpecRegistry registry)
        {
            processor = new RotatePivotResponseProcessor(registry);
        }

        public string BehaviorId => "rotate-pivot";

        public RotatePivotResponseResult Resolve(GameWorld world, IReadOnlyList<ActionRequest> requests, long serverTick)
        {
            return processor.Process(world, requests, serverTick);
        }

        public bool TryResolve(ActionBehaviorContext context, out ActionBehaviorResult result)
        {
            RotatePivotResponseResult rotate = Resolve(context.World, context.Requests, context.ServerTick);
            bool handled = rotate.BehaviorInstances.Count != 0 ||
                rotate.MovePlans.Count != 0 ||
                rotate.ActionResults.Count != 0 ||
                rotate.DeferredActions.Count != 0 ||
                rotate.ActionFacts.Count != 0 ||
                rotate.RemainingRequests.Count != context.Requests.Count;

            if (!handled)
            {
                result = ActionBehaviorResult.PassThrough(context.Requests);
                return false;
            }

            result = new ActionBehaviorResult(
                rotate.RemainingRequests,
                rotate.MovePlans,
                rotate.ActionResults,
                rotate.DeferredActions,
                rotate.ActionFacts,
                rotate.BehaviorInstances,
                rotate.Reasons);
            return true;
        }
    }
}
