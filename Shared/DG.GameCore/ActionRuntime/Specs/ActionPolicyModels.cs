using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum ActionPrimitive
{
    Move = 1,
    Spawn = 2,
    Remove = 3,
    SetComponentResult = 4,
    ApplyRuntimeEffect = 5
}


public enum ActionSourceKind
{
    Player = 1,
    Auto = 2,
    Mechanism = 3,
    Debug = 4,
    Runtime = 5,
    Handoff = 6
}


public enum ActionResultBranch
{
    Success = 1,
    Failed = 2,
    Handoff = 3,
    Noop = 4
}


public enum ActionTargetRule
{
    None = 0,
    TargetCoordOneStep = 1,
    TargetCoordAny = 2,
    DirectionFromRequest = 3,
    DirectionFromComponent = 4,
    Self = 5,
    DirectionCell = 6,
    FrontEntities = 7
}


public enum TargetDirectionSource
{
    None = 0,
    Request = 1,
    Component = 2,
    TargetCoord = 3
}


public enum TargetOrderingPolicy
{
    None = 0,
    HitOrderThenCoordThenEntity = 1
}


public enum TargetFilterConditionKind
{
    HasComponent = 1,
    MissingComponent = 2,
    HasTag = 3,
    MissingTag = 4
}


public enum TargetFilterSubject
{
    Target = 1
}


public enum BlockedResultKind
{
    Reject = 1,
    DeriveAction = 2,
    Bounce = 3,
    Noop = 4
}


public enum ActionConditionKind
{
    Always = 1,
    HasTag = 2,
    MissingTag = 3,
    HasComponent = 4,
    MissingComponent = 5,
    CanMove = 6,
    CanBePushed = 7,
    BodyKindIs = 8
}


public enum ActionConditionSubject
{
    Source = 1,
    Blocking = 2,
    SourceBody = 3,
    BlockingBody = 4
}


public enum ActionHandoffPolicy
{
    None = 0,
    Configured = 1
}


public enum ActionConflictPolicy
{
    None = 0,
    ExclusiveTargetCell = 1
}


public enum ActionInterruptPolicy
{
    None = 0,
    HigherPriorityInterruptsLower = 1
}


public enum ActionMergePolicy
{
    None = 0,
    SameClaim = 1
}


public enum ActionPlanRule
{
    None = 0,
    MoveBody = 1,
    SpawnEntity = 2,
    RemoveEntity = 3
}


public enum ActionSubjectKind
{
    HitEntity = 1,
    ConnectedBodyIfAny = 2,
    SingleEntity = HitEntity,
    ConnectedBody = ConnectedBodyIfAny
}

[Flags]

public enum ActionCommitRule
{
    None = 0,
    SetAutoMoveTick = 1,
    SetDirectionOnBounce = 2
}


public enum ActionClaimKind
{
    BodyMove = 1,
    TargetCell = 2
}


public enum ActionClaimMode
{
    Shared = 1,
    Exclusive = 2
}


public enum ActionTargetDataKind
{
    None = 0,
    Self = 1,
    Cell = 2,
    Entity = 3
}


public enum ActionExecutionSuccessPolicy
{
    AllOrNothing = 1,
    PartialAllowed = 2
}


public readonly struct TargetFilterCondition
{
    public TargetFilterCondition(TargetFilterConditionKind kind, TargetFilterSubject subject, ComponentKind componentKind = 0, WorldTag tag = WorldTag.None)
    {
        Kind = kind;
        Subject = subject;
        ComponentKind = componentKind;
        Tag = tag;
    }

    public TargetFilterConditionKind Kind { get; }
    public TargetFilterSubject Subject { get; }
    public ComponentKind ComponentKind { get; }
    public WorldTag Tag { get; }
}


public sealed class TargetFilterSpec
{
    public TargetFilterSpec(TargetFilterSpecId filterId, IReadOnlyList<TargetFilterCondition> conditions)
    {
        FilterId = filterId;
        Conditions = conditions == null ? Array.Empty<TargetFilterCondition>() : conditions.ToArray();
        if (!FilterId.IsValid)
        {
            throw new InvalidOperationException("Target filter id is empty.");
        }
    }

    public TargetFilterSpecId FilterId { get; }
    public IReadOnlyList<TargetFilterCondition> Conditions { get; }

    public static TargetFilterSpec None { get; } = new(new TargetFilterSpecId("target_filter_none"), Array.Empty<TargetFilterCondition>());
}


public sealed class TargetingSpec
{
    public TargetingSpec(TargetingSpecId specId, TargetSelectorId selectorId, TargetDirectionSource directionSource, TargetFilterSpecId filterId, TargetOrderingPolicy orderingPolicy = TargetOrderingPolicy.HitOrderThenCoordThenEntity, int range = 1, int maxTargets = 1, ActionTargetRule legacyRule = ActionTargetRule.None)
    {
        SpecId = specId;
        SelectorId = selectorId;
        DirectionSource = directionSource;
        FilterId = filterId;
        OrderingPolicy = orderingPolicy;
        Range = Math.Max(0, range);
        MaxTargets = Math.Max(0, maxTargets);
        LegacyRule = legacyRule;
        if (!SpecId.IsValid)
        {
            throw new InvalidOperationException("Targeting spec id is empty.");
        }

        if (!SelectorId.IsValid)
        {
            throw new InvalidOperationException("Target selector id is empty on " + SpecId + ".");
        }
    }

    public TargetingSpecId SpecId { get; }
    public TargetSelectorId SelectorId { get; }
    public TargetDirectionSource DirectionSource { get; }
    public TargetFilterSpecId FilterId { get; }
    public TargetOrderingPolicy OrderingPolicy { get; }
    public int Range { get; }
    public int MaxTargets { get; }
    public ActionTargetRule LegacyRule { get; }

    public static TargetingSpec FromLegacyRule(ActionTargetRule rule)
    {
        switch (rule)
        {
            case ActionTargetRule.None:
                return new TargetingSpec("legacy_none", "none", TargetDirectionSource.None, TargetFilterSpec.None.FilterId, TargetOrderingPolicy.None, 0, 0, rule);
            case ActionTargetRule.TargetCoordOneStep:
                return new TargetingSpec("legacy_target_coord_one_step", "target_coord", TargetDirectionSource.TargetCoord, TargetFilterSpec.None.FilterId, TargetOrderingPolicy.HitOrderThenCoordThenEntity, 1, 1, rule);
            case ActionTargetRule.TargetCoordAny:
                return new TargetingSpec("legacy_target_coord_any", "target_coord", TargetDirectionSource.TargetCoord, TargetFilterSpec.None.FilterId, TargetOrderingPolicy.HitOrderThenCoordThenEntity, 0, 1, rule);
            case ActionTargetRule.DirectionFromRequest:
            case ActionTargetRule.DirectionCell:
                return new TargetingSpec("legacy_direction_cell", "direction_cell", TargetDirectionSource.Request, TargetFilterSpec.None.FilterId, TargetOrderingPolicy.HitOrderThenCoordThenEntity, 1, 1, rule);
            case ActionTargetRule.DirectionFromComponent:
                return new TargetingSpec("legacy_direction_component_cell", "direction_cell", TargetDirectionSource.Component, TargetFilterSpec.None.FilterId, TargetOrderingPolicy.HitOrderThenCoordThenEntity, 1, 1, rule);
            case ActionTargetRule.Self:
                return new TargetingSpec("legacy_self", "self", TargetDirectionSource.Request, TargetFilterSpec.None.FilterId, TargetOrderingPolicy.HitOrderThenCoordThenEntity, 0, 1, rule);
            case ActionTargetRule.FrontEntities:
                return new TargetingSpec("legacy_front_entities", "front_entities", TargetDirectionSource.Request, TargetFilterSpec.None.FilterId, TargetOrderingPolicy.HitOrderThenCoordThenEntity, 32, 0, rule);
            default:
                throw new InvalidOperationException("Unknown action target rule: " + rule);
        }
    }
}


public readonly struct ActionHandoffSpec
{
    public ActionHandoffSpec(ActionHandoffPolicy policy, ActionSpecId specId, ActionSubjectKind subjectKind)
    {
        Policy = policy;
        SpecId = specId;
        SubjectKind = subjectKind;
    }

    public ActionHandoffPolicy Policy { get; }
    public ActionSpecId SpecId { get; }
    public ActionSubjectKind SubjectKind { get; }
    public bool IsEnabled => Policy != ActionHandoffPolicy.None && SpecId.IsValid;

    public static ActionHandoffSpec None => new(ActionHandoffPolicy.None, default, ActionSubjectKind.HitEntity);
}


public readonly struct ActionCondition
{
    public ActionCondition(ActionConditionKind kind, ActionConditionSubject subject, WorldTag tag = WorldTag.None, ComponentKind componentKind = 0, BehaviorBodyKind bodyKind = 0)
    {
        Kind = kind;
        Subject = subject;
        Tag = tag;
        ComponentKind = componentKind;
        BodyKind = bodyKind;
    }

    public ActionConditionKind Kind { get; }
    public ActionConditionSubject Subject { get; }
    public WorldTag Tag { get; }
    public ComponentKind ComponentKind { get; }
    public BehaviorBodyKind BodyKind { get; }
}


public sealed class BlockedResultBranch
{
    public BlockedResultBranch(int order, IReadOnlyList<ActionCondition> conditions, BlockedResultKind resultKind, ActionSpecId resultSpecId, ActionSubjectKind subjectKind, string reason, MoveErrorCode errorCode, ActionCommitRule commitRules)
    {
        Order = order;
        Conditions = conditions == null ? Array.Empty<ActionCondition>() : conditions.ToArray();
        ResultKind = resultKind;
        ResultSpecId = resultSpecId;
        SubjectKind = subjectKind;
        Reason = reason ?? string.Empty;
        ErrorCode = errorCode;
        CommitRules = commitRules;
    }

    public int Order { get; }
    public IReadOnlyList<ActionCondition> Conditions { get; }
    public BlockedResultKind ResultKind { get; }
    public ActionSpecId ResultSpecId { get; }
    public ActionSubjectKind SubjectKind { get; }
    public string Reason { get; }
    public MoveErrorCode ErrorCode { get; }
    public ActionCommitRule CommitRules { get; }
}


public sealed class BlockedResultPolicy
{
    public BlockedResultPolicy(BlockedResultPolicyId policyId, IReadOnlyList<BlockedResultBranch> branches)
    {
        PolicyId = policyId;
        Branches = branches == null ? Array.Empty<BlockedResultBranch>() : branches.OrderBy(branch => branch.Order).ToArray();
        Validate();
    }

    public BlockedResultPolicyId PolicyId { get; }
    public IReadOnlyList<BlockedResultBranch> Branches { get; }

    private void Validate()
    {
        if (!PolicyId.IsValid)
        {
            throw new InvalidOperationException("Blocked result policy id is empty.");
        }

        var orders = new HashSet<int>();
        for (int i = 0; i < Branches.Count; i++)
        {
            BlockedResultBranch branch = Branches[i];
            if (!orders.Add(branch.Order))
            {
                throw new InvalidOperationException("Duplicate blocked result branch order on " + PolicyId + ": " + branch.Order);
            }

            if (branch.ResultKind == BlockedResultKind.DeriveAction && !branch.ResultSpecId.IsValid)
            {
                throw new InvalidOperationException("Blocked result derive branch missing result spec id on " + PolicyId + ".");
            }
        }
    }
}


public static class BlockedResultPolicyFactory
{
    public static BlockedResultPolicy PushPolicy(BlockedResultPolicyId policyId, ActionSpecId resultSpecId, ActionSubjectKind subjectKind)
    {
        return new BlockedResultPolicy(policyId, new[]
        {
            new BlockedResultBranch(10, new[]
            {
                new ActionCondition(ActionConditionKind.CanBePushed, ActionConditionSubject.Blocking)
            }, BlockedResultKind.DeriveAction, resultSpecId, subjectKind, "bounded/deferred-output", MoveErrorCode.None, ActionCommitRule.None),
            new BlockedResultBranch(100, Array.Empty<ActionCondition>(), BlockedResultKind.Reject, default, ActionSubjectKind.HitEntity, "blocked cell", MoveErrorCode.Blocked, ActionCommitRule.None)
        });
    }

    public static BlockedResultPolicy ImmuneThenPushPolicy(BlockedResultPolicyId policyId, ActionSpecId resultSpecId, ActionSubjectKind subjectKind)
    {
        return new BlockedResultPolicy(policyId, new[]
        {
            new BlockedResultBranch(5, new[]
            {
                new ActionCondition(ActionConditionKind.HasTag, ActionConditionSubject.Blocking, WorldTag.ImmuneMechanismPush)
            }, BlockedResultKind.Reject, default, ActionSubjectKind.HitEntity, "immune", MoveErrorCode.Immune, ActionCommitRule.None),
            new BlockedResultBranch(10, new[]
            {
                new ActionCondition(ActionConditionKind.CanBePushed, ActionConditionSubject.Blocking)
            }, BlockedResultKind.DeriveAction, resultSpecId, subjectKind, "bounded/deferred-output", MoveErrorCode.None, ActionCommitRule.None),
            new BlockedResultBranch(100, Array.Empty<ActionCondition>(), BlockedResultKind.Reject, default, ActionSubjectKind.HitEntity, "blocked cell", MoveErrorCode.Blocked, ActionCommitRule.None)
        });
    }

    public static BlockedResultPolicy BouncePolicy(BlockedResultPolicyId policyId)
    {
        return new BlockedResultPolicy(policyId, new[]
        {
            new BlockedResultBranch(10, new[]
            {
                new ActionCondition(ActionConditionKind.HasComponent, ActionConditionSubject.Source, componentKind: ComponentKind.Bouncable)
            }, BlockedResultKind.Bounce, default, ActionSubjectKind.HitEntity, "blocked cell", MoveErrorCode.Blocked, ActionCommitRule.SetDirectionOnBounce),
            new BlockedResultBranch(100, Array.Empty<ActionCondition>(), BlockedResultKind.Reject, default, ActionSubjectKind.HitEntity, "blocked cell", MoveErrorCode.Blocked, ActionCommitRule.None)
        });
    }

    public static BlockedResultPolicy RejectPolicy(BlockedResultPolicyId policyId)
    {
        return new BlockedResultPolicy(policyId, new[]
        {
            new BlockedResultBranch(100, Array.Empty<ActionCondition>(), BlockedResultKind.Reject, default, ActionSubjectKind.HitEntity, "blocked cell", MoveErrorCode.Blocked, ActionCommitRule.None)
        });
    }
}

}
