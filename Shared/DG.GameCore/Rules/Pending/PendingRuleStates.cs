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

public sealed class PushPropagationState
{
    public PushPropagationState(long stateId, long ownerActionId, long rootEntityId, long firstTargetEntityId, Direction direction, long createdTick, int stepCostTicks)
    {
        StateId = stateId;
        OwnerActionId = ownerActionId;
        RootEntityId = rootEntityId;
        Direction = direction;
        CreatedTick = createdTick;
        LastUpdatedTick = createdTick;
        StepCostTicks = Math.Max(1, stepCostTicks);
        NextStepTick = createdTick + StepCostTicks;
        Chain.Add(rootEntityId);
        Chain.Add(firstTargetEntityId);
        CurrentFrontEntityId = firstTargetEntityId;
        Status = PendingRuleStatus.Active;
        Reason = string.Empty;
    }

    public long StateId { get; }
    public long OwnerActionId { get; }
    public long RootEntityId { get; }
    public Direction Direction { get; }
    public long CreatedTick { get; }
    public long LastUpdatedTick { get; private set; }
    public long NextStepTick { get; private set; }
    public int StepCostTicks { get; }
    public long CurrentFrontEntityId { get; private set; }
    public PendingRuleStatus Status { get; private set; }
    public string Reason { get; private set; }
    public List<long> Chain { get; } = new();

    public void AddFront(long entityId, long tick)
    {
        Chain.Add(entityId);
        CurrentFrontEntityId = entityId;
        LastUpdatedTick = tick;
        NextStepTick = tick + StepCostTicks;
    }

    public void MarkMoveAccepted(long tick)
    {
        LastUpdatedTick = tick;
        Status = PendingRuleStatus.Completed;
        Reason = string.Empty;
        CurrentFrontEntityId = 0;
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

    public void Delay(long tick)
    {
        LastUpdatedTick = tick;
        NextStepTick = tick + StepCostTicks;
    }
}

public sealed class PendingRuleStateStore
{
    private readonly List<PushPropagationState> pushStates = new();
    private long nextStateId = 1;

    public int ActiveCount => pushStates.Count(state => state.Status == PendingRuleStatus.Active);
    public IReadOnlyList<PushPropagationState> PushStates => pushStates.OrderBy(state => state.StateId).ToArray();

    public PushPropagationState AddPush(long ownerActionId, long rootEntityId, long firstTargetEntityId, Direction direction, long tick, int stepCostTicks = 1)
    {
        var state = new PushPropagationState(nextStateId++, ownerActionId, rootEntityId, firstTargetEntityId, direction, tick, stepCostTicks);
        pushStates.Add(state);
        return state;
    }

    public bool HasActivePushInvolving(long entityId)
    {
        for (int i = 0; i < pushStates.Count; i++)
        {
            PushPropagationState state = pushStates[i];
            if (state.Status == PendingRuleStatus.Active && state.Chain.Contains(entityId))
            {
                return true;
            }
        }

        return false;
    }

    public void CleanupInactive()
    {
        pushStates.RemoveAll(state => state.Status != PendingRuleStatus.Active);
    }
}
}
