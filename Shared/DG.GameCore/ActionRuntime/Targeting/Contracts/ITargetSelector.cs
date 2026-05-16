namespace DG.GameCore
{
public interface ITargetSelector
{
    TargetSelectorId SelectorId { get; }
    bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason);
}
}
