namespace DG.GameCore
{
public sealed class DirectionCellTargetSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("direction_cell");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = default;
        bool found = true;
        Direction direction = targeting.DirectionSource == TargetDirectionSource.Component ? DirectionFromComponent(world, entity, out found) : context.TargetHint.Direction;
        if (targeting.DirectionSource == TargetDirectionSource.Component && !found)
        {
            errorCode = MoveErrorCode.MissingPosition;
            reason = "missing auto move state";
            return false;
        }

        if (direction == Direction.None)
        {
            errorCode = MoveErrorCode.InvalidDirection;
            reason = "invalid direction";
            return false;
        }

        GridCoord target = position.Coord.Add(direction);
        result = new TargetingResult(new[] { ActionTargetData.Cell(targeting.DirectionSource == TargetDirectionSource.Component ? "direction_component" : "direction", target, direction, 0) }, direction, target);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }

    private static Direction DirectionFromComponent(GameWorld world, GameEntity entity, out bool found)
    {
        found = world.TryGetComponent(entity, out DirectionComponent directionComponent) &&
            world.TryGetComponent(entity, out AutoMoveComponent _);
        return found ? directionComponent.Direction : Direction.None;
    }
}
}
