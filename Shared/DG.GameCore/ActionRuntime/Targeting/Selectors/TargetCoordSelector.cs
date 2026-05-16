namespace DG.GameCore
{
public sealed class TargetCoordSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("target_coord");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = default;
        if (!context.TargetHint.TargetCoord.HasValue)
        {
            errorCode = MoveErrorCode.InvalidDirection;
            reason = "missing target";
            return false;
        }

        GridCoord target = context.TargetHint.TargetCoord.Value;
        if (targeting.Range > 0 && position.Coord.ManhattanDistance(target) > targeting.Range)
        {
            errorCode = MoveErrorCode.TooFar;
            reason = "target too far";
            return false;
        }

        Direction direction = TargetingSystem.DirectionFromDelta(position.Coord, target);
        if (targeting.Range == 1 && direction == Direction.None && position.Coord != target)
        {
            errorCode = MoveErrorCode.InvalidDirection;
            reason = "invalid direction";
            return false;
        }

        result = new TargetingResult(new[] { ActionTargetData.Cell("target", target, direction, 0) }, direction, target);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }
}
}
