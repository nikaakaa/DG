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

public enum WorldActionKind
{
    PlayerMove = 1,
    DebugMove = 2,
    DebugRemove = 3,
    DebugSpawn = 4,
    AutoMove = 5,
    MechanismPush = 6
}

public sealed class WorldAction
{
    public WorldAction(long actionId, WorldActionPriority priority, WorldActionKind kind, long entityId, GridCoord? targetCoord, Direction direction, long clientTick, long createdTick, long readyTick, int costTicks)
        : this(actionId, priority, kind, default, entityId, targetCoord, direction, clientTick, createdTick, readyTick, costTicks)
    {
    }

    public WorldAction(long actionId, WorldActionPriority priority, WorldActionKind kind, ActionSpecId specId, long entityId, GridCoord? targetCoord, Direction direction, long clientTick, long createdTick, long readyTick, int costTicks)
    {
        ActionId = actionId;
        Priority = priority;
        Kind = kind;
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
    public WorldActionKind Kind { get; }
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
}

public sealed class WorldActionQueue
{
    private readonly List<WorldAction> actions = new();
    private long nextActionId = 1;

    public int Count => actions.Count;

    public WorldAction EnqueuePlayerMove(long entityId, GridCoord targetCoord, long clientTick)
    {
        var action = new WorldAction(nextActionId++, WorldActionPriority.Player, WorldActionKind.PlayerMove, entityId, targetCoord, Direction.None, clientTick, 0, 0, 1);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueDebugMove(long entityId, GridCoord targetCoord)
    {
        var action = new WorldAction(nextActionId++, WorldActionPriority.Debug, WorldActionKind.DebugMove, entityId, targetCoord, Direction.None, 0, 0, 0, 1);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueDebugSpawn(long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks)
    {
        var action = new WorldAction(nextActionId++, WorldActionPriority.Debug, WorldActionKind.DebugSpawn, entityId, coord, direction, 0, 0, 0, 1)
            .WithSpawn(configId, playerId, autoMoveIntervalTicks);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueDebugRemove(long entityId)
    {
        var action = new WorldAction(nextActionId++, WorldActionPriority.Debug, WorldActionKind.DebugRemove, entityId, null, Direction.None, 0, 0, 0, 1);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueAutoMove(long entityId, long createdTick, int costTicks)
    {
        var action = new WorldAction(nextActionId++, WorldActionPriority.Auto, WorldActionKind.AutoMove, entityId, null, Direction.None, 0, createdTick, createdTick + Math.Max(1, costTicks), costTicks);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueMechanismPush(long entityId, Direction direction, long createdTick, int costTicks)
    {
        var action = new WorldAction(nextActionId++, WorldActionPriority.Mechanism, WorldActionKind.MechanismPush, entityId, null, direction, 0, createdTick, createdTick + Math.Max(1, costTicks), costTicks);
        actions.Add(action);
        return action;
    }

    public WorldAction EnqueueConfiguredMove(ActionSpecId specId, long entityId, Direction direction, long createdTick, int costTicks)
    {
        var spec = ActionSpecRegistry.Default.Get(specId);
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, WorldActionKind.MechanismPush, specId, entityId, null, direction, 0, createdTick, createdTick + Math.Max(1, costTicks), costTicks);
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
}
