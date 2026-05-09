using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum WorldActionPriority
{
    Debug = 1,
    Player = 2,
    Mechanism = 3,
    Auto = 4
}

public sealed class WorldAction
{
    public WorldAction(long actionId, WorldActionPriority priority, ActionSpecId specId, long entityId, GridCoord? targetCoord, Direction direction, long clientTick, long createdTick, long readyTick, int costTicks)
    {
        ActionId = actionId;
        Priority = priority;
        SpecId = specId;
        EntityId = entityId;
        TargetCoord = targetCoord;
        Direction = direction;
        ClientTick = clientTick;
        CreatedTick = createdTick;
        ReadyTick = readyTick;
        CostTicks = Math.Max(1, costTicks);
    }

    public long ActionId { get; }
    public WorldActionPriority Priority { get; }
    public ActionSpecId SpecId { get; }
    public long EntityId { get; }
    public GridCoord? TargetCoord { get; }
    public Direction Direction { get; }
    public long ClientTick { get; }
    public long CreatedTick { get; private set; }
    public long ReadyTick { get; private set; }
    public int CostTicks { get; private set; }
    public int ConfigId { get; private set; }
    public long PlayerId { get; private set; }
    public int AutoMoveIntervalTicks { get; private set; }
    public long CausalityId { get; private set; }
    public string DedupeKey { get; private set; } = string.Empty;

    public WorldAction WithSpawn(int configId, long playerId, int autoMoveIntervalTicks)
    {
        ConfigId = configId;
        PlayerId = playerId;
        AutoMoveIntervalTicks = autoMoveIntervalTicks;
        return this;
    }

    public WorldAction WithSchedule(long createdTick, int costTicks)
    {
        CreatedTick = createdTick;
        CostTicks = Math.Max(1, costTicks);
        ReadyTick = CreatedTick + CostTicks;
        return this;
    }

    public WorldAction WithDeferredSource(long causalityId, string dedupeKey)
    {
        CausalityId = causalityId;
        DedupeKey = dedupeKey ?? string.Empty;
        return this;
    }
}

public readonly struct DeferredAction
{
    public DeferredAction(ActionSpecId specId, long entityId, IReadOnlyList<long> subjectEntityIds, Direction direction, long createdTick, long readyTick, int costTicks, long causalityId, string dedupeKey)
    {
        SpecId = specId;
        EntityId = entityId;
        SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? new[] { entityId } : subjectEntityIds.ToArray();
        Direction = direction;
        CreatedTick = createdTick;
        ReadyTick = readyTick;
        CostTicks = Math.Max(1, costTicks);
        CausalityId = causalityId;
        DedupeKey = dedupeKey ?? string.Empty;
    }

    public ActionSpecId SpecId { get; }
    public long EntityId { get; }
    public IReadOnlyList<long> SubjectEntityIds { get; }
    public Direction Direction { get; }
    public long CreatedTick { get; }
    public long ReadyTick { get; }
    public int CostTicks { get; }
    public long CausalityId { get; }
    public string DedupeKey { get; }
}

public sealed class WorldActionQueue
{
    private readonly List<WorldAction> actions = new();
    private readonly ActionSpecRegistry registry;
    private long nextActionId = 1;

    public WorldActionQueue() : this(ActionSpecRegistry.Default)
    {
    }

    public WorldActionQueue(ActionSpecRegistry registry)
    {
        this.registry = registry;
    }

    public int Count => actions.Count;

    public WorldAction EnqueuePlayerMove(long entityId, GridCoord targetCoord, long clientTick)
    {
        var spec = registry.Get("player_move");
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, spec.SpecId, entityId, targetCoord, Direction.None, clientTick, 0, 0, spec.DefaultCostTicks);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueDebugMove(long entityId, GridCoord targetCoord)
    {
        var spec = registry.Get("debug_move");
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, spec.SpecId, entityId, targetCoord, Direction.None, 0, 0, 0, spec.DefaultCostTicks);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueDebugSpawn(long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks)
    {
        var spec = registry.Get("debug_spawn");
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, spec.SpecId, entityId, coord, direction, 0, 0, 0, spec.DefaultCostTicks)
            .WithSpawn(configId, playerId, autoMoveIntervalTicks);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueDebugRemove(long entityId)
    {
        var spec = registry.Get("debug_remove");
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, spec.SpecId, entityId, null, Direction.None, 0, 0, 0, spec.DefaultCostTicks);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueAutoMove(long entityId, long createdTick, int costTicks)
    {
        var spec = registry.Get("auto_move");
        int resolvedCost = costTicks > 0 ? costTicks : spec.DefaultCostTicks;
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, spec.SpecId, entityId, null, Direction.None, 0, createdTick, createdTick + Math.Max(1, resolvedCost), resolvedCost);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueConfiguredMove(ActionSpecId specId, long entityId, Direction direction, long createdTick, int costTicks)
    {
        var spec = registry.Get(specId);
        int resolvedCost = costTicks > 0 ? costTicks : spec.DefaultCostTicks;
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, specId, entityId, null, direction, 0, createdTick, createdTick + Math.Max(1, resolvedCost), resolvedCost);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueDeferred(DeferredAction deferred)
    {
        var spec = registry.Get(deferred.SpecId);
        int resolvedCost = deferred.CostTicks > 0 ? deferred.CostTicks : spec.DefaultCostTicks;
        long readyTick = deferred.ReadyTick > deferred.CreatedTick ? deferred.ReadyTick : deferred.CreatedTick + Math.Max(1, resolvedCost);
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, deferred.SpecId, deferred.EntityId, null, deferred.Direction, 0, deferred.CreatedTick, readyTick, resolvedCost)
            .WithDeferredSource(deferred.CausalityId, deferred.DedupeKey);
        actions.Add(action);
        return action;
    }

    public IReadOnlyList<WorldAction> Drain()
    {
        return DrainReady(long.MaxValue);
    }

    public IReadOnlyList<WorldAction> DrainReady(long currentTick)
    {
        if (actions.Count == 0)
        {
            return Array.Empty<WorldAction>();
        }

        var result = new List<WorldAction>();
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            WorldAction action = actions[i];
            if (action.ReadyTick == 0)
            {
                action.WithSchedule(Math.Max(0, currentTick - 1), 1);
            }

            if (action.ReadyTick > currentTick)
            {
                continue;
            }

            actions.RemoveAt(i);
            result.Add(action);
        }

        return result
            .OrderBy(action => action.Priority)
            .ThenBy(action => action.ReadyTick)
            .ThenBy(action => action.ActionId)
            .ThenBy(action => action.EntityId)
            .ToArray();
    }
}

public static class ExplicitOutputPolicies
{
    public static int EnqueuePushOnEnterActions(GameWorld world, WorldActionQueue actionQueue, long serverTick)
    {
        int count = 0;
        var moved = new HashSet<long>();
        IReadOnlyList<GameEntity> triggers = world.EnumerateEntities();
        for (int i = 0; i < triggers.Count; i++)
        {
            GameEntity trigger = triggers[i];
            if (!world.TryGetComponent(trigger, out PositionComponent triggerPosition) ||
                !world.TryGetComponent(trigger, out DirectionComponent triggerDirection) ||
                !world.TryGetComponent(trigger, out PushOnEnterComponent output) ||
                !output.OutputSpecId.IsValid)
            {
                continue;
            }

            IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(triggerPosition.Coord);
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                GameEntity target = targets[targetIndex];
                if (target.EntityId == trigger.EntityId ||
                    moved.Contains(target.EntityId) ||
                    !world.TryGetComponent(target, out PositionComponent _))
                {
                    continue;
                }

                moved.Add(target.EntityId);
                actionQueue.EnqueueConfiguredMove(output.OutputSpecId, target.EntityId, triggerDirection.Direction, serverTick, output.OutputCostTicks);
                count++;
            }
        }

        return count;
    }
}
}
