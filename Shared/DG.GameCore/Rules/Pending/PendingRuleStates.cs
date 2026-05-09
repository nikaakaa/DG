using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum PendingRuleStatus
{
    Active = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public enum ActionUnitLifecycleStatus
{
    Ready = 1,
    Succeeded = 2,
    Failed = 3,
    Handoff = 4
}

public sealed class PendingActionUnit
{
    public PendingActionUnit(long actionUnitId, long ownerActionId, long derivedFromUnitId, long retryOfUnitId, ActionSpecId specId, long entityId, IReadOnlyList<long> subjectEntityIds, GridCoord? targetCoord, Direction direction, WorldActionPriority priority, ActionRuntimeParams runtimeParams, long createdTick, int costTicks, long clientTick)
    {
        ActionUnitId = actionUnitId;
        OwnerActionId = ownerActionId;
        DerivedFromUnitId = derivedFromUnitId;
        RetryOfUnitId = retryOfUnitId;
        SpecId = specId;
        EntityId = entityId;
        SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? new[] { entityId } : subjectEntityIds.ToArray();
        TargetCoord = targetCoord;
        Direction = direction;
        Priority = priority;
        RuntimeParams = runtimeParams;
        CreatedTick = createdTick;
        CostTicks = Math.Max(1, costTicks);
        ReadyTick = createdTick + CostTicks;
        ClientTick = clientTick;
        Status = ActionUnitLifecycleStatus.Ready;
        FailureReason = string.Empty;
    }

    public long ActionUnitId { get; }
    public long OwnerActionId { get; }
    public long DerivedFromUnitId { get; }
    public long RetryOfUnitId { get; }
    public ActionSpecId SpecId { get; }
    public long EntityId { get; }
    public IReadOnlyList<long> SubjectEntityIds { get; }
    public GridCoord? TargetCoord { get; }
    public Direction Direction { get; }
    public WorldActionPriority Priority { get; }
    public ActionRuntimeParams RuntimeParams { get; }
    public long CreatedTick { get; }
    public long ReadyTick { get; }
    public int CostTicks { get; }
    public long ClientTick { get; }
    public ActionUnitLifecycleStatus Status { get; private set; }
    public string FailureReason { get; private set; }

    public void Succeed()
    {
        Status = ActionUnitLifecycleStatus.Succeeded;
    }

    public void Handoff()
    {
        Status = ActionUnitLifecycleStatus.Handoff;
    }

    public void Fail(string reason)
    {
        FailureReason = reason ?? string.Empty;
        Status = ActionUnitLifecycleStatus.Failed;
    }
}

public sealed class PendingActionState
{
    public const int MaxChainDepth = 8;

    public PendingActionState(long stateId, ActionRequest sourceRequest, long targetEntityId, Direction direction, long createdTick, int stepCostTicks, bool reportsOwnerResult, ActionSpecId specId, IReadOnlyList<long>? subjectEntityIds)
    {
        StateId = stateId;
        SourceRequest = sourceRequest;
        OwnerActionId = sourceRequest.OwnerActionId;
        RootEntityId = sourceRequest.EntityId;
        Priority = sourceRequest.Priority;
        RuntimeParams = sourceRequest.RuntimeParams;
        ClientTick = sourceRequest.ClientTick;
        Direction = direction;
        CreatedTick = createdTick;
        LastUpdatedTick = createdTick;
        StepCostTicks = Math.Max(1, stepCostTicks);
        TimeoutTick = createdTick + 128;
        Chain.Add(sourceRequest.EntityId);
        if (subjectEntityIds == null || subjectEntityIds.Count == 0)
        {
            Chain.Add(targetEntityId);
        }
        else
        {
            for (int i = 0; i < subjectEntityIds.Count; i++)
            {
                Chain.Add(subjectEntityIds[i]);
            }
        }
        Status = PendingRuleStatus.Active;
        Reason = string.Empty;
        ReportsOwnerResult = reportsOwnerResult;
        var unit = new PendingActionUnit(stateId, OwnerActionId, sourceRequest.ActionId, 0, specId, targetEntityId, SubjectEntities(targetEntityId, subjectEntityIds), null, direction, sourceRequest.Priority, sourceRequest.RuntimeParams, createdTick, StepCostTicks, sourceRequest.ClientTick);
        units.Add(unit);
        nextActionUnitId = stateId * 1000;
        ActiveHandoffActionUnitId = unit.ActionUnitId;
        ActiveHandoffEntityId = targetEntityId;
    }

    private readonly List<PendingActionUnit> units = new();
    private long nextActionUnitId;

    public long StateId { get; }
    public ActionRequest SourceRequest { get; }
    public long OwnerActionId { get; }
    public long RootEntityId { get; }
    public WorldActionPriority Priority { get; }
    public ActionRuntimeParams RuntimeParams { get; }
    public long ClientTick { get; }
    public Direction Direction { get; }
    public long CreatedTick { get; }
    public long LastUpdatedTick { get; private set; }
    public long NextStepTick => ReadyUnit != null ? ReadyUnit.ReadyTick : long.MaxValue;
    public int StepCostTicks { get; }
    public long TimeoutTick { get; }
    public long ActiveHandoffActionUnitId { get; private set; }
    public long ActiveHandoffEntityId { get; private set; }
    public bool ReportsOwnerResult { get; }
    public PendingActionUnit ReadyUnit => units
        .Where(unit => unit.Status == ActionUnitLifecycleStatus.Ready)
        .OrderBy(unit => unit.ReadyTick)
        .FirstOrDefault();
    public IReadOnlyList<PendingActionUnit> Units => units.OrderBy(unit => unit.ActionUnitId).ToArray();
    public PendingRuleStatus Status { get; private set; }
    public string Reason { get; private set; }
    public List<long> Chain { get; } = new();

    public bool HasReadyUnit(long tick)
    {
        return Status == PendingRuleStatus.Active &&
            tick <= TimeoutTick &&
            ReadyUnit != null &&
            ReadyUnit.ReadyTick <= tick;
    }

    public bool MarkUnitHandoff(long actionUnitId, long tick)
    {
        PendingActionUnit unit = FindUnit(actionUnitId);
        if (unit == null || unit.Status != ActionUnitLifecycleStatus.Ready)
        {
            return false;
        }

        unit.Handoff();
        Reason = "handoff";
        ActiveHandoffActionUnitId = 0;
        ActiveHandoffEntityId = 0;
        LastUpdatedTick = tick;
        return true;
    }

    public bool TryAddChildUnit(ActionRequest parentRequest, long targetEntityId, Direction direction, long tick, ActionSpecId specId, IReadOnlyList<long>? subjectEntityIds, out string reason)
    {
        reason = string.Empty;
        if (!CanAddChild(targetEntityId, subjectEntityIds, out reason))
        {
            return false;
        }

        PendingActionUnit parent = FindUnit(parentRequest.ActionId);
        if (parent == null || parent.Status != ActionUnitLifecycleStatus.Ready)
        {
            reason = "push parent missing";
            return false;
        }

        if (!MarkUnitHandoff(parentRequest.ActionId, tick))
        {
            reason = "push parent missing";
            return false;
        }

        AddUnit(parentRequest.OwnerActionId, parentRequest.ActionId, 0, specId, targetEntityId, SubjectEntities(targetEntityId, subjectEntityIds), null, direction, parentRequest.Priority, parentRequest.RuntimeParams, tick, parentRequest.ClientTick);
        AddChainEntities(targetEntityId, subjectEntityIds);
        return true;
    }

    public bool MarkUnitAccepted(long actionUnitId, long tick, out bool ownerCompleted)
    {
        ownerCompleted = false;
        PendingActionUnit unit = FindUnit(actionUnitId);
        if (unit == null || unit.Status != ActionUnitLifecycleStatus.Ready)
        {
            return false;
        }

        unit.Succeed();
        Status = PendingRuleStatus.Completed;
        Reason = string.Empty;
        ActiveHandoffActionUnitId = 0;
        ActiveHandoffEntityId = 0;
        LastUpdatedTick = tick;
        ownerCompleted = ReportsOwnerResult;
        return true;
    }

    public bool FailUnit(long actionUnitId, string reason, long tick)
    {
        PendingActionUnit unit = FindUnit(actionUnitId);
        if (unit != null)
        {
            unit.Fail(reason);
        }

        Fail(reason, tick);
        return unit != null;
    }

    public void Fail(string reason, long tick)
    {
        Status = PendingRuleStatus.Failed;
        Reason = reason;
        LastUpdatedTick = tick;
    }

    public void Cancel(string reason, long tick)
    {
        Status = PendingRuleStatus.Cancelled;
        Reason = reason;
        LastUpdatedTick = tick;
    }

    private PendingActionUnit FindUnit(long actionUnitId)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].ActionUnitId == actionUnitId)
            {
                return units[i];
            }
        }

        return null!;
    }

    private bool CanAddChild(long targetEntityId, IReadOnlyList<long>? subjectEntityIds, out string reason)
    {
        reason = string.Empty;
        if (units.Count >= MaxChainDepth)
        {
            reason = "push chain depth exceeded";
            return false;
        }

        IReadOnlyList<long> ids = SubjectEntities(targetEntityId, subjectEntityIds);
        for (int i = 0; i < ids.Count; i++)
        {
            if (Chain.Contains(ids[i]))
            {
                reason = "push chain cycle";
                return false;
            }
        }

        return true;
    }

    private void AddUnit(long ownerActionId, long derivedFromUnitId, long retryOfUnitId, ActionSpecId specId, long entityId, IReadOnlyList<long> subjectEntityIds, GridCoord? targetCoord, Direction direction, WorldActionPriority priority, ActionRuntimeParams runtimeParams, long tick, long clientTick)
    {
        var unit = new PendingActionUnit(++nextActionUnitId, ownerActionId, derivedFromUnitId, retryOfUnitId, specId, entityId, subjectEntityIds, targetCoord, direction, priority, runtimeParams, tick, StepCostTicks, clientTick);
        units.Add(unit);
        ActiveHandoffActionUnitId = unit.ActionUnitId;
        ActiveHandoffEntityId = entityId;
        LastUpdatedTick = tick;
    }

    private void AddChainEntities(long targetEntityId, IReadOnlyList<long>? subjectEntityIds)
    {
        IReadOnlyList<long> ids = SubjectEntities(targetEntityId, subjectEntityIds);
        for (int i = 0; i < ids.Count; i++)
        {
            if (!Chain.Contains(ids[i]))
            {
                Chain.Add(ids[i]);
            }
        }
    }

    private static IReadOnlyList<long> SubjectEntities(long targetEntityId, IReadOnlyList<long>? subjectEntityIds)
    {
        return subjectEntityIds == null || subjectEntityIds.Count == 0 ? new[] { targetEntityId } : subjectEntityIds.ToArray();
    }
}

public sealed class PendingRuleStateStore
{
    private readonly List<PendingActionState> actionStates = new();
    private long nextStateId = 1;

    public int ActiveCount => actionStates.Count(state => state.Status == PendingRuleStatus.Active);
    public IReadOnlyList<PendingActionState> ActionStates => actionStates.OrderBy(state => state.StateId).ToArray();

    public PendingActionState AddHandoffActionState(ActionRequest sourceRequest, long targetEntityId, Direction direction, long tick, int stepCostTicks = 1, ActionSpecId specId = default, IReadOnlyList<long>? subjectEntityIds = null)
    {
        ActionSpecId resolvedSpecId = specId.IsValid ? specId : "player_push";
        var state = new PendingActionState(nextStateId++, sourceRequest, targetEntityId, direction, tick, stepCostTicks, sourceRequest.Source.SourceStateId == 0, resolvedSpecId, subjectEntityIds);
        actionStates.Add(state);
        return state;
    }

    public bool HasActiveActionInvolving(long entityId)
    {
        for (int i = 0; i < actionStates.Count; i++)
        {
            PendingActionState state = actionStates[i];
            if (state.Status == PendingRuleStatus.Active && state.Chain.Contains(entityId))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasActiveActionInvolvingAny(IReadOnlyList<long> entityIds)
    {
        return HasActiveActionInvolvingAny(entityIds, 0);
    }

    public bool HasActiveActionInvolvingAny(IReadOnlyList<long> entityIds, long excludedStateId)
    {
        for (int i = 0; i < entityIds.Count; i++)
        {
            if (HasActiveActionInvolving(entityIds[i], excludedStateId))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasActiveActionInvolving(long entityId, long excludedStateId)
    {
        for (int i = 0; i < actionStates.Count; i++)
        {
            PendingActionState state = actionStates[i];
            if (state.StateId != excludedStateId && state.Status == PendingRuleStatus.Active && state.Chain.Contains(entityId))
            {
                return true;
            }
        }

        return false;
    }

    public void CleanupInactive()
    {
        actionStates.RemoveAll(state => state.Status != PendingRuleStatus.Active);
    }
}
}
