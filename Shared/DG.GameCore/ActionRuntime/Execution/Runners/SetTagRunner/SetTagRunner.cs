namespace DG.GameCore
{
    public sealed class SetTagRunner
    {
        public const string RunnerId = "set_tag_runner";

        public void Process(PrimitiveRunnerContext context)
        {
            WorldTag tag = context.Request.RuntimeParams.Tag;
            if (tag == WorldTag.None)
            {
                Fail(context, default, MoveErrorCode.Blocked, "invalid tag");
                return;
            }

            if (!context.World.TryGetEntity(context.Request.EntityId, out GameEntity entity) ||
                !context.World.TryGetComponent(entity, out PositionComponent position))
            {
                Fail(context, default, MoveErrorCode.UnknownEntity, "entity not found");
                return;
            }

            bool enabled = context.Request.RuntimeParams.TagEnabled;
            context.Proposals.Add(enabled
                ? CommitProposal.AddTag(context.Request.Priority, context.Request.ActionId, context.Request.EntityId, tag, context.ServerTick)
                : CommitProposal.RemoveTag(context.Request.Priority, context.Request.ActionId, context.Request.EntityId, tag, context.ServerTick));
            context.ActionResults[context.Request.ActionId] = new MoveResult(true, context.Request.EntityId, position.Coord, Direction.None, MoveErrorCode.None, string.Empty, false, default, context.Request.ClientTick);
            context.Reasons.Add((enabled ? "tag-added:" : "tag-removed:") + (int)tag);
        }

        private static void Fail(PrimitiveRunnerContext context, GridCoord coord, MoveErrorCode errorCode, string reason)
        {
            context.ActionResults[context.Request.ActionId] = new MoveResult(false, context.Request.EntityId, coord, Direction.None, errorCode, reason, false, default, context.Request.ClientTick);
            context.Reasons.Add(reason);
        }
    }
}
