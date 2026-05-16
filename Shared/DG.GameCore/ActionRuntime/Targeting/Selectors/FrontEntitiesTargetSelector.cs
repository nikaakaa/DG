using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class FrontEntitiesTargetSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("front_entities");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = default;
        Direction direction = context.TargetHint.Direction;
        if (direction == Direction.None)
        {
            errorCode = MoveErrorCode.InvalidDirection;
            reason = "invalid direction";
            return false;
        }

        GridCoord target = position.Coord.Add(direction);
        GridCoord scan = target;
        int order = 0;
        int range = targeting.Range <= 0 ? 32 : targeting.Range;
        int maxTargets = targeting.MaxTargets;
        var results = new List<ActionTargetData>();
        for (int step = 0; step < range; step++)
        {
            IReadOnlyList<GameEntity> entities = world.GetEntitiesAt(scan)
                .OrderBy(item => item.EntityId)
                .ToArray();
            if (entities.Count == 0)
            {
                break;
            }

            bool added = false;
            for (int i = 0; i < entities.Count; i++)
            {
                if (!world.HasComponent<PushableComponent>(entities[i]) ||
                    !world.TryGetComponent(entities[i], out PositionComponent targetPosition) ||
                    !TargetingSystem.PassesFilter(world, entities[i], filter))
                {
                    continue;
                }

                results.Add(ActionTargetData.Entity("front_entities", entities[i].EntityId, targetPosition.Coord, direction, order++));
                added = true;
                if (maxTargets > 0 && results.Count >= maxTargets)
                {
                    break;
                }
            }

            if (!added || maxTargets > 0 && results.Count >= maxTargets)
            {
                break;
            }

            scan = scan.Add(direction);
        }

        if (results.Count == 0)
        {
            results.Add(ActionTargetData.Cell("front_entities", target, direction, 0));
        }

        result = new TargetingResult(results, direction, target);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }
}
}
