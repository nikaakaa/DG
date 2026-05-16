using System;

namespace DG.GameCore
{
public sealed class NoneTargetSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("none");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = new TargetingResult(Array.Empty<ActionTargetData>(), Direction.None, position.Coord);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }
}
}
