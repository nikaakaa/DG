using System;
using System.Collections.Generic;

namespace DG.GameCore
{
    public sealed class PushRunner
    {
        public const string RunnerId = "push_runner";

        private readonly PushVectorArbiter pushVectorArbiter;

        public PushRunner(ActionSpecRegistry actionSpecs)
        {
            pushVectorArbiter = new PushVectorArbiter(actionSpecs);
        }

        public PushVectorCompositionResult ComposePush(GameWorld world, IReadOnlyList<ActionRequest> requests)
        {
            return pushVectorArbiter.Compose(world, requests);
        }

        public int EnqueuePushOnEnterActions(GameWorld world, WorldActionQueue actionQueue, long serverTick)
        {
            return ExplicitOutputPolicies.EnqueuePushOnEnterActions(world, actionQueue, serverTick);
        }

        public int EnqueuePushOnEnterActions(GameWorld world, WorldActionQueue actionQueue, long serverTick, Func<long, long, bool> hasRunningMovementClaim)
        {
            return ExplicitOutputPolicies.EnqueuePushOnEnterActions(world, actionQueue, serverTick, hasRunningMovementClaim);
        }
    }
}
