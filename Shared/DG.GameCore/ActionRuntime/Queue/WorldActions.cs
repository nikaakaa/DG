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
    public bool RotatePivot { get; private set; }
    public long CausalityId { get; private set; }
    public string DedupeKey { get; private set; } = string.Empty;
    public string DeferredEquivalenceKey { get; private set; } = string.Empty;
    public int DeferredContributionCount { get; private set; } = 1;
    public IReadOnlyList<long> SubjectEntityIds { get; private set; } = Array.Empty<long>();
    public IReadOnlyList<PushOriginContext> PushOriginContexts => pushOriginContexts;
    public IReadOnlyList<long> DeferredCausalitySamples => deferredCausalitySamples;
    private readonly List<long> deferredCausalitySamples = new();
    private readonly List<PushOriginContext> pushOriginContexts = new();

    public WorldAction WithSpawn(int configId, long playerId, int autoMoveIntervalTicks)
        => WithSpawn(configId, playerId, autoMoveIntervalTicks, false);

    public WorldAction WithSpawn(int configId, long playerId, int autoMoveIntervalTicks, bool rotatePivot)
    {
        ConfigId = configId;
        PlayerId = playerId;
        AutoMoveIntervalTicks = autoMoveIntervalTicks;
        RotatePivot = rotatePivot;
        return this;
    }

    public WorldAction WithSchedule(long createdTick, int costTicks)
    {
        CreatedTick = createdTick;
        CostTicks = Math.Max(1, costTicks);
        ReadyTick = CreatedTick + CostTicks;
        return this;
    }

    public WorldAction WithDeferredSource(long causalityId, string dedupeKey, string equivalenceKey)
        => WithDeferredSource(causalityId, dedupeKey, equivalenceKey, Array.Empty<PushOriginContext>());

    public WorldAction WithDeferredSource(long causalityId, string dedupeKey, string equivalenceKey, IReadOnlyList<PushOriginContext> contexts)
    {
        CausalityId = causalityId;
        DedupeKey = dedupeKey ?? string.Empty;
        DeferredEquivalenceKey = equivalenceKey ?? string.Empty;
        DeferredContributionCount = 1;
        deferredCausalitySamples.Clear();
        deferredCausalitySamples.Add(causalityId);
        pushOriginContexts.Clear();
        if (contexts != null)
        {
            for (int i = 0; i < contexts.Count && pushOriginContexts.Count < 8; i++)
            {
                pushOriginContexts.Add(contexts[i]);
            }
        }

        return this;
    }

    public WorldAction WithSubjectEntityIds(IReadOnlyList<long> subjectEntityIds)
    {
        SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? Array.Empty<long>() : subjectEntityIds.ToArray();
        return this;
    }

    public void MergeDeferredContribution(long causalityId)
        => MergeDeferredContribution(causalityId, Array.Empty<PushOriginContext>());

    public void MergeDeferredContribution(long causalityId, IReadOnlyList<PushOriginContext> contexts)
    {
        DeferredContributionCount++;
        if (deferredCausalitySamples.Count < 8 && !deferredCausalitySamples.Contains(causalityId))
        {
            deferredCausalitySamples.Add(causalityId);
        }

        if (contexts == null)
        {
            return;
        }

        for (int i = 0; i < contexts.Count && pushOriginContexts.Count < 8; i++)
        {
            PushOriginContext context = contexts[i];
            if (!pushOriginContexts.Contains(context))
            {
                pushOriginContexts.Add(context);
            }
        }
    }
}

public enum PushOriginKind
{
    Ordinary = 0,
    RotatePivotImpact = 1
}

public readonly struct PushOriginContext : IEquatable<PushOriginContext>
{
    public PushOriginContext(PushOriginKind kind, long impactMemberId, GridCoord impactFrom, GridCoord impactTo, long blockerEntityId, long pivotEntityId, Direction rotateDirection, long sourceActionId)
    {
        Kind = kind;
        ImpactMemberId = impactMemberId;
        ImpactFrom = impactFrom;
        ImpactTo = impactTo;
        BlockerEntityId = blockerEntityId;
        PivotEntityId = pivotEntityId;
        RotateDirection = rotateDirection;
        SourceActionId = sourceActionId;
    }

    public PushOriginKind Kind { get; }
    public long ImpactMemberId { get; }
    public GridCoord ImpactFrom { get; }
    public GridCoord ImpactTo { get; }
    public long BlockerEntityId { get; }
    public long PivotEntityId { get; }
    public Direction RotateDirection { get; }
    public long SourceActionId { get; }
    public bool IsValid => Kind != PushOriginKind.Ordinary;

    public bool Equals(PushOriginContext other)
    {
        return Kind == other.Kind &&
            ImpactMemberId == other.ImpactMemberId &&
            ImpactFrom.Equals(other.ImpactFrom) &&
            ImpactTo.Equals(other.ImpactTo) &&
            BlockerEntityId == other.BlockerEntityId &&
            PivotEntityId == other.PivotEntityId &&
            RotateDirection == other.RotateDirection &&
            SourceActionId == other.SourceActionId;
    }

    public override bool Equals(object obj)
    {
        return obj is PushOriginContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Kind;
            hash = (hash * 397) ^ ImpactMemberId.GetHashCode();
            hash = (hash * 397) ^ ImpactFrom.GetHashCode();
            hash = (hash * 397) ^ ImpactTo.GetHashCode();
            hash = (hash * 397) ^ BlockerEntityId.GetHashCode();
            hash = (hash * 397) ^ PivotEntityId.GetHashCode();
            hash = (hash * 397) ^ RotateDirection.GetHashCode();
            hash = (hash * 397) ^ SourceActionId.GetHashCode();
            return hash;
        }
    }
}

public readonly struct DeferredAction
{
    public DeferredAction(ActionSpecId specId, long entityId, IReadOnlyList<long> subjectEntityIds, Direction direction, long createdTick, long readyTick, int costTicks, long causalityId, string dedupeKey)
        : this(specId, entityId, subjectEntityIds, direction, createdTick, readyTick, costTicks, causalityId, dedupeKey, Array.Empty<PushOriginContext>())
    {
    }

    public DeferredAction(ActionSpecId specId, long entityId, IReadOnlyList<long> subjectEntityIds, Direction direction, long createdTick, long readyTick, int costTicks, long causalityId, string dedupeKey, IReadOnlyList<PushOriginContext> originContexts)
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
        OriginContexts = originContexts == null ? Array.Empty<PushOriginContext>() : originContexts.ToArray();
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
    public IReadOnlyList<PushOriginContext> OriginContexts { get; }
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
        => EnqueueDebugSpawn(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks, false);

    public WorldAction EnqueueDebugSpawn(long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, bool rotatePivot)
    {
        var spec = registry.Get("debug_spawn");
        var action = new WorldAction(nextActionId++, spec.DefaultPriority, spec.SpecId, entityId, coord, direction, 0, 0, 0, spec.DefaultCostTicks)
            .WithSpawn(configId, playerId, autoMoveIntervalTicks, rotatePivot);
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

    public DeferredEnqueueResult EnqueueDeferred(DeferredAction deferred)
    {
        var spec = registry.Get(deferred.SpecId);
        int resolvedCost = deferred.CostTicks > 0 ? deferred.CostTicks : spec.DefaultCostTicks;
        long readyTick = deferred.ReadyTick > deferred.CreatedTick ? deferred.ReadyTick : deferred.CreatedTick + Math.Max(1, resolvedCost);
        string equivalenceKey = BuildDeferredEquivalenceKey(deferred, readyTick);
        for (int i = 0; i < actions.Count; i++)
        {
            WorldAction existing = actions[i];
            if (existing.DeferredEquivalenceKey == equivalenceKey)
            {
                existing.MergeDeferredContribution(deferred.CausalityId, deferred.OriginContexts);
                return new DeferredEnqueueResult(existing, false, equivalenceKey, existing.DeferredContributionCount);
            }
        }

        var action = new WorldAction(nextActionId++, spec.DefaultPriority, deferred.SpecId, deferred.EntityId, null, deferred.Direction, 0, deferred.CreatedTick, readyTick, resolvedCost)
            .WithDeferredSource(deferred.CausalityId, deferred.DedupeKey, equivalenceKey, deferred.OriginContexts)
            .WithSubjectEntityIds(deferred.SubjectEntityIds);
        actions.Add(action);
        return new DeferredEnqueueResult(action, true, equivalenceKey, action.DeferredContributionCount);
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

    private static string BuildDeferredEquivalenceKey(DeferredAction deferred, long readyTick)
    {
        IReadOnlyList<long> subjects = deferred.SubjectEntityIds.Count == 0 ? new[] { deferred.EntityId } : deferred.SubjectEntityIds;
        return readyTick + "|" + deferred.SpecId + "|" + deferred.Direction + "|" + string.Join(",", subjects.OrderBy(entityId => entityId));
    }
}

public readonly struct DeferredEnqueueResult
{
    public DeferredEnqueueResult(WorldAction action, bool enqueued, string equivalenceKey, int contributionCount)
    {
        Action = action;
        Enqueued = enqueued;
        EquivalenceKey = equivalenceKey ?? string.Empty;
        ContributionCount = Math.Max(1, contributionCount);
    }

    public WorldAction Action { get; }
    public bool Enqueued { get; }
    public bool Merged => !Enqueued;
    public string EquivalenceKey { get; }
    public int ContributionCount { get; }
}

public static class ExplicitOutputPolicies
{
    public static int EnqueuePushOnEnterActions(GameWorld world, WorldActionQueue actionQueue, long serverTick)
    {
        int count = 0;
        var moved = new HashSet<long>();
        IReadOnlyList<PushOnEnterQueryResult> triggers = world.QueryPushOnEnter(EntityIterationOrder.EntityId);
        for (int i = 0; i < triggers.Count; i++)
        {
            PushOnEnterQueryResult trigger = triggers[i];
            PushOnEnterComponent output = trigger.PushOnEnter;
            if (!output.OutputSpecId.IsValid)
            {
                continue;
            }

            IReadOnlyList<GameEntity> targets = world.GetPositionedEntitiesAt(trigger.Position);
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                GameEntity target = targets[targetIndex];
                if (target.EntityId == trigger.EntityId ||
                    moved.Contains(target.EntityId))
                {
                    continue;
                }

                moved.Add(target.EntityId);
                actionQueue.EnqueueConfiguredMove(output.OutputSpecId, target.EntityId, trigger.Direction, serverTick, output.OutputCostTicks);
                count++;
            }
        }

        return count;
    }
}
}
