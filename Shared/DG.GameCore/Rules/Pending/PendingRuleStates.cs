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

public enum PushContactBatchStatus
{
    Active = 1,
    Succeeded = 2,
    Failed = 3
}

public enum PendingChildSubjectDecision
{
    Add = 1,
    Skip = 2,
    Fail = 3
}

public readonly struct PendingCycleDiagnostic
{
    public PendingCycleDiagnostic(long stateId, long conflictEntityId, IReadOnlyList<long> candidateSubjectEntityIds, IReadOnlyList<long> chainEntityIds, bool conflictInChain, bool conflictInAdding)
    {
        StateId = stateId;
        ConflictEntityId = conflictEntityId;
        CandidateSubjectEntityIds = candidateSubjectEntityIds?.ToArray() ?? Array.Empty<long>();
        ChainEntityIds = chainEntityIds?.ToArray() ?? Array.Empty<long>();
        ConflictInChain = conflictInChain;
        ConflictInAdding = conflictInAdding;
    }

    public long StateId { get; }
    public long ConflictEntityId { get; }
    public IReadOnlyList<long> CandidateSubjectEntityIds { get; }
    public IReadOnlyList<long> ChainEntityIds { get; }
    public bool ConflictInChain { get; }
    public bool ConflictInAdding { get; }
}

public sealed class PushContactBatch
{
    private readonly List<long> childUnitIds = new();

    public PushContactBatch(long batchId, long parentUnitId, long createdTick)
    {
        BatchId = batchId;
        ParentUnitId = parentUnitId;
        CreatedTick = createdTick;
        Status = PushContactBatchStatus.Active;
        FailureReason = string.Empty;
    }

    public long BatchId { get; private set; }
    public long ParentUnitId { get; }
    public IReadOnlyList<long> ChildUnitIds => childUnitIds.ToArray();
    public long CreatedTick { get; }
    public PushContactBatchStatus Status { get; private set; }
    public string FailureReason { get; private set; }

    public void AddChild(long actionUnitId)
    {
        if (!childUnitIds.Contains(actionUnitId))
        {
            childUnitIds.Add(actionUnitId);
        }
    }

    public bool ContainsChild(long actionUnitId)
    {
        return childUnitIds.Contains(actionUnitId);
    }

    public void Succeed()
    {
        Status = PushContactBatchStatus.Succeeded;
        FailureReason = string.Empty;
    }

    public void Fail(string reason)
    {
        Status = PushContactBatchStatus.Failed;
        FailureReason = reason ?? string.Empty;
    }
}

public sealed class PendingActionUnit
{
    public PendingActionUnit(long actionUnitId, long ownerActionId, long derivedFromUnitId, long retryOfUnitId, long batchId, ActionSpecId specId, long entityId, IReadOnlyList<long> subjectEntityIds, GridCoord? targetCoord, Direction direction, WorldActionPriority priority, ActionRuntimeParams runtimeParams, long createdTick, int costTicks, long clientTick)
    {
        ActionUnitId = actionUnitId;
        OwnerActionId = ownerActionId;
        DerivedFromUnitId = derivedFromUnitId;
        RetryOfUnitId = retryOfUnitId;
        BatchId = batchId;
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
    public long BatchId { get; private set; }
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

    public void AssignBatch(long batchId)
    {
        BatchId = batchId;
    }
}

public sealed class PendingActionState
{
    public PendingActionState(long stateId, ActionRequest sourceRequest, long targetEntityId, Direction direction, long createdTick, int stepCostTicks, bool reportsOwnerResult, ActionSpecId specId, IReadOnlyList<long> subjectEntityIds)
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
        var unit = new PendingActionUnit(stateId, OwnerActionId, sourceRequest.ActionId, 0, 0, specId, targetEntityId, SubjectEntities(targetEntityId, subjectEntityIds ?? Array.Empty<long>()), null, direction, sourceRequest.Priority, sourceRequest.RuntimeParams, createdTick, StepCostTicks, sourceRequest.ClientTick);
        units.Add(unit);
        nextActionUnitId = stateId * 1000;
        ActiveHandoffActionUnitId = unit.ActionUnitId;
        ActiveHandoffEntityId = targetEntityId;
        consumedSubjectKeys.Add(BuildSubjectKey(new[] { sourceRequest.EntityId }));
        consumedSubjectKeys.Add(BuildSubjectKey(unit.SubjectEntityIds));
    }

    private readonly List<PendingActionUnit> units = new();
    private readonly List<PushContactBatch> batches = new();
    private readonly HashSet<string> consumedSubjectKeys = new();
    private long nextActionUnitId;
    private long nextBatchId;

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
    public PendingCycleDiagnostic LastCycleDiagnostic { get; private set; }
    public PendingActionUnit ReadyUnit => units
        .Where(unit => unit.Status == ActionUnitLifecycleStatus.Ready)
        .OrderBy(unit => unit.ReadyTick)
        .FirstOrDefault();
    public IReadOnlyList<PendingActionUnit> ReadyUnits => units
        .Where(unit => unit.Status == ActionUnitLifecycleStatus.Ready)
        .OrderBy(unit => unit.ReadyTick)
        .ThenBy(unit => unit.ActionUnitId)
        .ToArray();
    public IReadOnlyList<PendingActionUnit> Units => units.OrderBy(unit => unit.ActionUnitId).ToArray();
    public IReadOnlyList<PushContactBatch> PushContactBatches => batches.OrderBy(batch => batch.BatchId).ToArray();
    public PendingRuleStatus Status { get; private set; }
    public string Reason { get; private set; }
    public List<long> Chain { get; } = new();

    public bool HasReadyUnit(long tick)
    {
        return Status == PendingRuleStatus.Active &&
            tick <= TimeoutTick &&
            ReadyUnits.Any(unit => unit.ReadyTick <= tick);
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

    public bool TryAddChildUnit(ActionRequest parentRequest, long targetEntityId, Direction direction, long tick, ActionSpecId specId, IReadOnlyList<long> subjectEntityIds, out string reason)
    {
        if (!TryAddChildUnits(parentRequest, new[]
            {
                new PendingChildUnitRequest(targetEntityId, specId, subjectEntityIds)
            }, direction, tick, out reason))
        {
            return false;
        }

        return true;
    }

    public bool TryAddChildUnits(ActionRequest parentRequest, IReadOnlyList<PendingChildUnitRequest> children, Direction direction, long tick, out string reason)
    {
        reason = string.Empty;
        if (children == null || children.Count == 0)
        {
            reason = "push child missing";
            return false;
        }

        PendingActionUnit parent = FindUnit(parentRequest.ActionId);
        if (parent == null || parent.Status != ActionUnitLifecycleStatus.Ready)
        {
            reason = "push parent missing";
            return false;
        }

        var pendingChildren = new List<PendingChildUnitRequest>();
        var addingEntities = new HashSet<long>();
        var addingSubjects = new HashSet<string>();
        bool boundedNoOutput = false;
        bool skippedReadySubject = false;
        for (int i = 0; i < children.Count; i++)
        {
            IReadOnlyList<long> ids = SubjectEntities(children[i].TargetEntityId, children[i].SubjectEntityIds);
            if (HasReadyUnitWithSubject(ids, parent.ActionUnitId))
            {
                skippedReadySubject = true;
                continue;
            }

            PendingChildSubjectDecision decision = DecideChildSubject(ids, addingEntities, addingSubjects, out reason);
            if (decision == PendingChildSubjectDecision.Skip)
            {
                boundedNoOutput = true;
                continue;
            }

            if (decision == PendingChildSubjectDecision.Fail)
            {
                return false;
            }

            pendingChildren.Add(children[i]);
        }

        if (pendingChildren.Count == 0 && boundedNoOutput && !skippedReadySubject)
        {
            reason = "bounded/no-output";
            CompleteBoundedNoOutput(parentRequest.ActionId, tick, reason);
            return true;
        }

        long batchId = pendingChildren.Count > 1 ? ++nextBatchId : 0;
        PushContactBatch batch = batchId == 0 ? null! : new PushContactBatch(batchId, parent.ActionUnitId, tick);
        if (!MarkUnitHandoff(parentRequest.ActionId, tick))
        {
            reason = "push parent missing";
            return false;
        }

        for (int i = 0; i < pendingChildren.Count; i++)
        {
            PendingChildUnitRequest child = pendingChildren[i];
            PendingActionUnit unit = AddUnit(parentRequest.OwnerActionId, parentRequest.ActionId, 0, batchId, child.SpecId, child.TargetEntityId, SubjectEntities(child.TargetEntityId, child.SubjectEntityIds), null, direction, parentRequest.Priority, parentRequest.RuntimeParams, tick, parentRequest.ClientTick);
            if (batch != null)
            {
                batch.AddChild(unit.ActionUnitId);
            }

            AddChainEntities(child.TargetEntityId, child.SubjectEntityIds);
        }

        if (batch != null)
        {
            batches.Add(batch);
        }

        return true;
    }

    public bool TryAddSiblingUnitsToReadyBatch(IReadOnlyList<PendingChildUnitRequest> children, Direction direction, long tick, out string reason)
    {
        reason = string.Empty;
        PendingActionUnit first = ReadyUnit;
        if (first == null || children == null || children.Count <= 1)
        {
            return true;
        }

        var pendingChildren = new List<PendingChildUnitRequest>();
        var addingEntities = new HashSet<long>();
        var addingSubjects = new HashSet<string>();
        string firstSubjectKey = BuildSubjectKey(first.SubjectEntityIds);
        for (int i = 1; i < children.Count; i++)
        {
            IReadOnlyList<long> ids = SubjectEntities(children[i].TargetEntityId, children[i].SubjectEntityIds);
            if (BuildSubjectKey(ids) == firstSubjectKey || HasReadyUnitWithSubject(ids, first.ActionUnitId))
            {
                continue;
            }

            PendingChildSubjectDecision decision = DecideChildSubject(ids, addingEntities, addingSubjects, out reason);
            if (decision == PendingChildSubjectDecision.Skip)
            {
                continue;
            }

            if (decision == PendingChildSubjectDecision.Fail)
            {
                return false;
            }

            pendingChildren.Add(children[i]);
        }

        if (pendingChildren.Count == 0)
        {
            return true;
        }

        long batchId = ++nextBatchId;
        first.AssignBatch(batchId);
        var batch = new PushContactBatch(batchId, first.DerivedFromUnitId, tick);
        batch.AddChild(first.ActionUnitId);
        for (int i = 0; i < pendingChildren.Count; i++)
        {
            PendingChildUnitRequest child = pendingChildren[i];
            PendingActionUnit unit = AddUnit(first.OwnerActionId, first.DerivedFromUnitId, 0, batchId, child.SpecId, child.TargetEntityId, SubjectEntities(child.TargetEntityId, child.SubjectEntityIds), null, direction, first.Priority, first.RuntimeParams, tick, first.ClientTick);
            batch.AddChild(unit.ActionUnitId);
            AddChainEntities(child.TargetEntityId, child.SubjectEntityIds);
        }

        batches.Add(batch);
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
        if (unit.BatchId != 0)
        {
            PushContactBatch batch = FindBatch(unit.BatchId);
            if (batch != null && batch.Status == PushContactBatchStatus.Active && BatchChildrenCompleted(batch))
            {
                batch.Succeed();
                Complete(tick, out ownerCompleted);
            }
            else
            {
                ActiveHandoffActionUnitId = ReadyUnit != null ? ReadyUnit.ActionUnitId : 0;
                ActiveHandoffEntityId = ReadyUnit != null ? ReadyUnit.EntityId : 0;
                LastUpdatedTick = tick;
            }

            return true;
        }

        Complete(tick, out ownerCompleted);
        return true;
    }

    public bool FailUnit(long actionUnitId, string reason, long tick)
    {
        PendingActionUnit unit = FindUnit(actionUnitId);
        if (unit != null)
        {
            unit.Fail(reason);
            if (unit.BatchId != 0)
            {
                PushContactBatch batch = FindBatch(unit.BatchId);
                if (batch != null && batch.Status == PushContactBatchStatus.Active)
                {
                    batch.Fail(reason);
                }
            }
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

    private PushContactBatch FindBatch(long batchId)
    {
        for (int i = 0; i < batches.Count; i++)
        {
            if (batches[i].BatchId == batchId)
            {
                return batches[i];
            }
        }

        return null!;
    }

    private bool BatchChildrenCompleted(PushContactBatch batch)
    {
        IReadOnlyList<long> childIds = batch.ChildUnitIds;
        for (int i = 0; i < childIds.Count; i++)
        {
            PendingActionUnit child = FindUnit(childIds[i]);
            if (child == null || child.Status != ActionUnitLifecycleStatus.Succeeded)
            {
                return false;
            }
        }

        return true;
    }

    private void Complete(long tick, out bool ownerCompleted)
    {
        Status = PendingRuleStatus.Completed;
        Reason = string.Empty;
        ActiveHandoffActionUnitId = 0;
        ActiveHandoffEntityId = 0;
        LastUpdatedTick = tick;
        ownerCompleted = ReportsOwnerResult;
    }

    private void CompleteBoundedNoOutput(long actionUnitId, long tick, string reason)
    {
        PendingActionUnit unit = FindUnit(actionUnitId);
        if (unit != null && unit.Status == ActionUnitLifecycleStatus.Ready)
        {
            unit.Succeed();
        }

        Status = PendingRuleStatus.Completed;
        Reason = reason ?? string.Empty;
        ActiveHandoffActionUnitId = 0;
        ActiveHandoffEntityId = 0;
        LastUpdatedTick = tick;
    }

    private PendingChildSubjectDecision DecideChildSubject(IReadOnlyList<long> ids, HashSet<long> addingEntities, HashSet<string> addingSubjects, out string reason)
    {
        reason = string.Empty;
        string subjectKey = BuildSubjectKey(ids);
        if (consumedSubjectKeys.Contains(subjectKey) || addingSubjects.Contains(subjectKey))
        {
            return PendingChildSubjectDecision.Skip;
        }

        for (int i = 0; i < ids.Count; i++)
        {
            bool conflictInChain = Chain.Contains(ids[i]);
            bool conflictInAdding = addingEntities.Contains(ids[i]);
            if (conflictInChain || conflictInAdding)
            {
                LastCycleDiagnostic = new PendingCycleDiagnostic(StateId, ids[i], ids, Chain, conflictInChain, conflictInAdding);
                reason = "push chain cycle";
                return PendingChildSubjectDecision.Fail;
            }

            addingEntities.Add(ids[i]);
        }

        addingSubjects.Add(subjectKey);
        return PendingChildSubjectDecision.Add;
    }

    private bool HasReadyUnitWithSubject(IReadOnlyList<long> subjectEntityIds, long excludedActionUnitId)
    {
        string key = BuildSubjectKey(subjectEntityIds);
        for (int i = 0; i < units.Count; i++)
        {
            PendingActionUnit unit = units[i];
            if (unit.ActionUnitId != excludedActionUnitId &&
                unit.Status == ActionUnitLifecycleStatus.Ready &&
                BuildSubjectKey(unit.SubjectEntityIds) == key)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildSubjectKey(IReadOnlyList<long> subjectEntityIds)
    {
        return string.Join("|", subjectEntityIds.OrderBy(id => id));
    }

    private PendingActionUnit AddUnit(long ownerActionId, long derivedFromUnitId, long retryOfUnitId, long batchId, ActionSpecId specId, long entityId, IReadOnlyList<long> subjectEntityIds, GridCoord? targetCoord, Direction direction, WorldActionPriority priority, ActionRuntimeParams runtimeParams, long tick, long clientTick)
    {
        var unit = new PendingActionUnit(++nextActionUnitId, ownerActionId, derivedFromUnitId, retryOfUnitId, batchId, specId, entityId, subjectEntityIds, targetCoord, direction, priority, runtimeParams, tick, StepCostTicks, clientTick);
        units.Add(unit);
        consumedSubjectKeys.Add(BuildSubjectKey(unit.SubjectEntityIds));
        ActiveHandoffActionUnitId = unit.ActionUnitId;
        ActiveHandoffEntityId = entityId;
        LastUpdatedTick = tick;
        return unit;
    }

    private void AddChainEntities(long targetEntityId, IReadOnlyList<long> subjectEntityIds)
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

    private static IReadOnlyList<long> SubjectEntities(long targetEntityId, IReadOnlyList<long> subjectEntityIds)
    {
        return subjectEntityIds == null || subjectEntityIds.Count == 0 ? new[] { targetEntityId } : subjectEntityIds.ToArray();
    }
}

public readonly struct PendingChildUnitRequest
{
    public PendingChildUnitRequest(long targetEntityId, ActionSpecId specId, IReadOnlyList<long> subjectEntityIds)
    {
        TargetEntityId = targetEntityId;
        SpecId = specId;
        SubjectEntityIds = subjectEntityIds;
    }

    public long TargetEntityId { get; }
    public ActionSpecId SpecId { get; }
    public IReadOnlyList<long> SubjectEntityIds { get; }
}

public sealed class PendingRuleStateStore
{
    private readonly List<PendingActionState> actionStates = new();
    private long nextStateId = 1;

    public int ActiveCount => actionStates.Count(state => state.Status == PendingRuleStatus.Active);
    public IReadOnlyList<PendingActionState> ActionStates => actionStates.OrderBy(state => state.StateId).ToArray();
    public PendingCycleDiagnostic LastCycleDiagnostic { get; private set; }

    public PendingActionState AddHandoffActionState(ActionRequest sourceRequest, long targetEntityId, Direction direction, long tick, int stepCostTicks = 1, ActionSpecId specId = default)
    {
        return AddHandoffActionState(sourceRequest, targetEntityId, direction, tick, stepCostTicks, specId, Array.Empty<long>());
    }

    public PendingActionState AddHandoffActionState(ActionRequest sourceRequest, long targetEntityId, Direction direction, long tick, int stepCostTicks, ActionSpecId specId, IReadOnlyList<long> subjectEntityIds)
    {
        if (!specId.IsValid)
        {
            throw new ArgumentException("Handoff action spec is required.", nameof(specId));
        }

        var state = new PendingActionState(nextStateId++, sourceRequest, targetEntityId, direction, tick, stepCostTicks, sourceRequest.Source.SourceStateId == 0, specId, subjectEntityIds ?? Array.Empty<long>());
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
        for (int i = 0; i < actionStates.Count; i++)
        {
            PendingCycleDiagnostic diagnostic = actionStates[i].LastCycleDiagnostic;
            if (diagnostic.ConflictEntityId != 0)
            {
                LastCycleDiagnostic = diagnostic;
            }
        }

        actionStates.RemoveAll(state => state.Status != PendingRuleStatus.Active);
    }
}
}
