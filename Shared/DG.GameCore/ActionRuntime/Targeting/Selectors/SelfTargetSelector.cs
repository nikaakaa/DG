namespace DG.GameCore
{
public sealed class SelfTargetSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("self");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = new TargetingResult(new[] { ActionTargetData.Self(entity.EntityId, position.Coord, context.TargetHint.Direction) }, context.TargetHint.Direction, position.Coord);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }
}
}
