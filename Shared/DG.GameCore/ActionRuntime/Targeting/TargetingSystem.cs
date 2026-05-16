using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public class TargetingSystem
{
    private readonly TargetSelectorRegistry selectors;
    private readonly Dictionary<TargetFilterSpecId, TargetFilterSpec> filters;
    private static readonly ComponentFactQueryRegistry ComponentQueries = ComponentFactQueryRegistry.Default;

    public TargetingSystem() : this(TargetSelectorRegistry.CreateDefault(), new[] { TargetFilterSpec.None })
    {
    }

    public TargetingSystem(TargetSelectorRegistry selectors, IEnumerable<TargetFilterSpec> filters)
    {
        this.selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
        this.filters = new Dictionary<TargetFilterSpecId, TargetFilterSpec>();
        if (filters != null)
        {
            foreach (TargetFilterSpec filter in filters)
            {
                if (this.filters.ContainsKey(filter.FilterId))
                {
                    throw new InvalidOperationException("Duplicate target filter id: " + filter.FilterId);
                }

                this.filters.Add(filter.FilterId, filter);
            }
        }

        if (!this.filters.ContainsKey(TargetFilterSpec.None.FilterId))
        {
            this.filters.Add(TargetFilterSpec.None.FilterId, TargetFilterSpec.None);
        }
    }

    public bool TryResolveTargetData(GameWorld world, ActionContext context, ActionSpec spec, GameEntity entity, PositionComponent position, out IReadOnlyList<ActionTargetData> targetData, out Direction direction, out GridCoord target, out MoveErrorCode errorCode, out string reason)
    {
        targetData = Array.Empty<ActionTargetData>();
        direction = Direction.None;
        target = position.Coord;
        TargetingSpec targeting = spec.Targeting;
        if (!filters.TryGetValue(targeting.FilterId, out TargetFilterSpec filter))
        {
            errorCode = MoveErrorCode.InvalidDirection;
            reason = "unknown target filter";
            return false;
        }

        if (!selectors.Get(targeting.SelectorId).TrySelect(world, context, spec, targeting, entity, position, filter, out TargetingResult result, out errorCode, out reason))
        {
            return false;
        }

        targetData = ApplyOrdering(result.TargetData, targeting.OrderingPolicy);
        direction = result.Direction;
        target = result.PrimaryTargetCoord;
        return true;
    }

    public bool TryResolveMoveTarget(GameWorld world, ActionRequest request, ActionSpec spec, GameEntity entity, PositionComponent position, out Direction direction, out GridCoord target, out MoveErrorCode errorCode, out string reason)
    {
        if (!request.TryCreateContext(spec, out ActionContext context, out reason))
        {
            direction = Direction.None;
            target = position.Coord;
            errorCode = MoveErrorCode.InvalidDirection;
            return false;
        }

        return TryResolveTargetData(world, context, spec, entity, position, out _, out direction, out target, out errorCode, out reason);
    }

    internal static bool PassesFilter(GameWorld world, GameEntity target, TargetFilterSpec filter)
    {
        for (int i = 0; i < filter.Conditions.Count; i++)
        {
            TargetFilterCondition condition = filter.Conditions[i];
            bool result = condition.Kind switch
            {
                TargetFilterConditionKind.HasComponent => ComponentQueries.Has(world, target, condition.ComponentKind),
                TargetFilterConditionKind.MissingComponent => !ComponentQueries.Has(world, target, condition.ComponentKind),
                TargetFilterConditionKind.HasTag => HasTag(world, target, condition.Tag),
                TargetFilterConditionKind.MissingTag => !HasTag(world, target, condition.Tag),
                _ => false
            };

            if (!result)
            {
                return false;
            }
        }

        return true;
    }

    internal static Direction DirectionFromDelta(GridCoord current, GridCoord target)
    {
        if (target.X == current.X - 1 && target.Y == current.Y)
        {
            return Direction.Left;
        }

        if (target.X == current.X + 1 && target.Y == current.Y)
        {
            return Direction.Right;
        }

        if (target.X == current.X && target.Y == current.Y + 1)
        {
            return Direction.Up;
        }

        if (target.X == current.X && target.Y == current.Y - 1)
        {
            return Direction.Down;
        }

        return Direction.None;
    }

    private static IReadOnlyList<ActionTargetData> ApplyOrdering(IReadOnlyList<ActionTargetData> targetData, TargetOrderingPolicy ordering)
    {
        if (targetData == null || targetData.Count <= 1 || ordering == TargetOrderingPolicy.None)
        {
            return targetData ?? Array.Empty<ActionTargetData>();
        }

        return targetData
            .OrderBy(item => item.HitOrder)
            .ThenBy(item => item.HitCell.X)
            .ThenBy(item => item.HitCell.Y)
            .ThenBy(item => item.TargetEntityId)
            .ThenBy(item => item.QueryId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasTag(GameWorld world, GameEntity target, WorldTag tag)
    {
        return tag == WorldTag.None ||
            world.TryGetComponent(target, out TagSetComponent component) &&
            (component.Tags & tag) == tag;
    }
}

public sealed class ActionTargetSelector : TargetingSystem
{
}
}
