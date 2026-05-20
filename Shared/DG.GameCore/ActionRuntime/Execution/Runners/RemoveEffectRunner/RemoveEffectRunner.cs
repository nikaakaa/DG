using System.Collections.Generic;

namespace DG.GameCore
{
    public sealed class RemoveEffectRunner
    {
        public const string RunnerId = "remove_effect_runner";

        private readonly CommitResolver commitResolver = new CommitResolver();

        public CommitProposal CreateExpirationProposal(WorldActionPriority priority, long actionId, long entityId, RuntimeEffectId effectId, long serverTick)
        {
            return CommitProposal.RemoveRuntimeEffect(priority, actionId, entityId, effectId, serverTick);
        }

        public IReadOnlyList<CommitProposalResult> Commit(GameWorld world, CommitProposal proposal)
        {
            return commitResolver.Resolve(world, new[] { proposal });
        }

        public void Process(PrimitiveRunnerContext context)
        {
            if (!context.World.TryGetEntity(context.Request.EntityId, out GameEntity entity) ||
                !context.World.TryGetComponent(entity, out PositionComponent position))
            {
                Fail(context, default, MoveErrorCode.UnknownEntity, "entity not found");
                return;
            }

            RuntimeEffectId effectId = context.Request.RuntimeParams.RuntimeEffectId;
            if (!effectId.IsValid)
            {
                Fail(context, position.Coord, MoveErrorCode.Blocked, "runtime effect not found");
                return;
            }

            context.Proposals.Add(CommitProposal.RemoveRuntimeEffect(context.Request.Priority, context.Request.ActionId, context.Request.EntityId, effectId, context.ServerTick));
            context.ActionResults[context.Request.ActionId] = new MoveResult(true, context.Request.EntityId, position.Coord, Direction.None, MoveErrorCode.None, string.Empty, false, default, context.Request.ClientTick);
            context.Reasons.Add("effect-removed:" + effectId.Value);
        }

        private static void Fail(PrimitiveRunnerContext context, GridCoord coord, MoveErrorCode errorCode, string reason)
        {
            context.ActionResults[context.Request.ActionId] = new MoveResult(false, context.Request.EntityId, coord, Direction.None, errorCode, reason, false, default, context.Request.ClientTick);
            context.Reasons.Add(reason);
        }
    }
}
