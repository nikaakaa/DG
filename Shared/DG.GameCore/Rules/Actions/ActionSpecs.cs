using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct ActionSpecId : IEquatable<ActionSpecId>
{
    public ActionSpecId(string value)
    {
        Value = value ?? string.Empty;
    }

    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);

    public bool Equals(ActionSpecId other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is ActionSpecId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value != null ? Value.GetHashCode() : 0;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator ActionSpecId(string value)
    {
        return new ActionSpecId(value);
    }
}

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
    DirectionFromComponent = 4
}

public enum ActionBlockedPolicy
{
    Reject = 1,
    StartPushIfPushable = 2,
    BounceIfBouncable = 3
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

public readonly struct ActionRuntimeParams
{
    public ActionRuntimeParams(int configId, long playerId, int autoMoveIntervalTicks)
    {
        ConfigId = configId;
        PlayerId = playerId;
        AutoMoveIntervalTicks = autoMoveIntervalTicks;
    }

    public int ConfigId { get; }
    public long PlayerId { get; }
    public int AutoMoveIntervalTicks { get; }

    public static ActionRuntimeParams FromWorldAction(WorldAction action)
    {
        return new ActionRuntimeParams(action.ConfigId, action.PlayerId, action.AutoMoveIntervalTicks);
    }
}

public readonly struct ActionTarget
{
    public ActionTarget(long targetEntityId, GridCoord? targetCoord, Direction direction)
    {
        TargetEntityId = targetEntityId;
        TargetCoord = targetCoord;
        Direction = direction;
    }

    public long TargetEntityId { get; }
    public GridCoord? TargetCoord { get; }
    public Direction Direction { get; }
}

public readonly struct ActionSourceContext
{
    public ActionSourceContext(ActionSourceKind kind, long sourceEntityId, long sourceStateId, WorldTag sourceTag)
    {
        Kind = kind;
        SourceEntityId = sourceEntityId;
        SourceStateId = sourceStateId;
        SourceTag = sourceTag;
    }

    public ActionSourceKind Kind { get; }
    public long SourceEntityId { get; }
    public long SourceStateId { get; }
    public WorldTag SourceTag { get; }
}

public sealed class ActionSpec
{
    public ActionSpec(
        ActionSpecId specId,
        ActionPrimitive primitive,
        ActionSourceKind defaultSource,
        WorldActionPriority defaultPriority,
        WorldTag sourceTag,
        WorldTag abilityTag,
        WorldTag requiredTags,
        WorldTag blockedTags,
        ActionTargetRule targetRule,
        ActionBlockedPolicy blockedPolicy,
        ActionConflictPolicy conflictPolicy,
        ActionInterruptPolicy interruptPolicy,
        ActionMergePolicy mergePolicy,
        ActionPlanRule planRule,
        ActionCommitRule commitRules,
        ActionSubjectKind subjectKind = ActionSubjectKind.HitEntity,
        ActionHandoffSpec handoff = default,
        int defaultCostTicks = 1)
    {
        SpecId = specId;
        Primitive = primitive;
        DefaultSource = defaultSource;
        DefaultPriority = defaultPriority;
        SourceTag = sourceTag;
        AbilityTag = abilityTag;
        RequiredTags = requiredTags;
        BlockedTags = blockedTags;
        TargetRule = targetRule;
        BlockedPolicy = blockedPolicy;
        ConflictPolicy = conflictPolicy;
        InterruptPolicy = interruptPolicy;
        MergePolicy = mergePolicy;
        PlanRule = planRule;
        CommitRules = commitRules;
        SubjectKind = subjectKind;
        Handoff = handoff.Policy == ActionHandoffPolicy.None && !handoff.SpecId.IsValid ? ActionHandoffSpec.None : handoff;
        DefaultCostTicks = Math.Max(1, defaultCostTicks);
    }

    public ActionSpecId SpecId { get; }
    public ActionPrimitive Primitive { get; }
    public ActionSourceKind DefaultSource { get; }
    public WorldActionPriority DefaultPriority { get; }
    public WorldTag SourceTag { get; }
    public WorldTag AbilityTag { get; }
    public WorldTag RequiredTags { get; }
    public WorldTag BlockedTags { get; }
    public ActionTargetRule TargetRule { get; }
    public ActionBlockedPolicy BlockedPolicy { get; }
    public ActionConflictPolicy ConflictPolicy { get; }
    public ActionInterruptPolicy InterruptPolicy { get; }
    public ActionMergePolicy MergePolicy { get; }
    public ActionPlanRule PlanRule { get; }
    public ActionCommitRule CommitRules { get; }
    public ActionSubjectKind SubjectKind { get; }
    public ActionHandoffSpec Handoff { get; }
    public int DefaultCostTicks { get; }
    public bool AllowsConnectedBodySubject => SubjectKind == ActionSubjectKind.ConnectedBodyIfAny;
}

public readonly struct ActionRequest
{
    public ActionRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, ActionSourceContext source, long entityId, ActionTarget target, ActionRuntimeParams runtimeParams, long createdTick, long readyTick, long clientTick, long ownerActionId = 0, long derivedFromUnitId = 0)
    {
        ActionId = actionId;
        OwnerActionId = ownerActionId == 0 ? actionId : ownerActionId;
        DerivedFromUnitId = derivedFromUnitId;
        SpecId = specId;
        Priority = priority;
        Source = source;
        EntityId = entityId;
        Target = target;
        RuntimeParams = runtimeParams;
        CreatedTick = createdTick;
        ReadyTick = readyTick;
        ClientTick = clientTick;
    }

    public long ActionId { get; }
    public long OwnerActionId { get; }
    public long DerivedFromUnitId { get; }
    public ActionSpecId SpecId { get; }
    public WorldActionPriority Priority { get; }
    public ActionSourceContext Source { get; }
    public long EntityId { get; }
    public ActionTarget Target { get; }
    public ActionRuntimeParams RuntimeParams { get; }
    public long CreatedTick { get; }
    public long ReadyTick { get; }
    public long ClientTick { get; }
}

public readonly struct ActionClaim
{
    public ActionClaim(long actionId, long bodyId, long entityId, ActionClaimKind kind, GridCoord fromCoord, GridCoord toCoord, string conflictGroup, ActionClaimMode mode, WorldActionPriority priority)
    {
        ActionId = actionId;
        BodyId = bodyId;
        EntityId = entityId;
        Kind = kind;
        FromCoord = fromCoord;
        ToCoord = toCoord;
        ConflictGroup = conflictGroup ?? string.Empty;
        Mode = mode;
        Priority = priority;
    }

    public long ActionId { get; }
    public long BodyId { get; }
    public long EntityId { get; }
    public ActionClaimKind Kind { get; }
    public GridCoord FromCoord { get; }
    public GridCoord ToCoord { get; }
    public string ConflictGroup { get; }
    public ActionClaimMode Mode { get; }
    public WorldActionPriority Priority { get; }
}

public sealed class AcceptedAction
{
    public AcceptedAction(ActionRequest request, ActionSpec spec, BehaviorBody body, Direction direction, GridCoord? targetCoord, IReadOnlyList<ActionClaim> claims, long serverTick)
    {
        Request = request;
        Spec = spec;
        Body = body;
        Direction = direction;
        TargetCoord = targetCoord;
        Claims = claims;
        ServerTick = serverTick;
    }

    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public BehaviorBody Body { get; }
    public Direction Direction { get; }
    public GridCoord? TargetCoord { get; }
    public IReadOnlyList<ActionClaim> Claims { get; }
    public long ServerTick { get; }

}

public sealed class RejectedAction
{
    public RejectedAction(ActionRequest request, ActionSpec spec, MoveResult result, string reason)
    {
        Request = request;
        Spec = spec;
        Result = result;
        Reason = reason ?? string.Empty;
    }

    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public MoveResult Result { get; }
    public string Reason { get; }
    public ActionResultBranch Branch => ActionResultBranch.Failed;
}

public sealed class DerivedAction
{
    public DerivedAction(ActionRequest request, ActionSpec spec, string reason)
        : this(request, spec, ActionResultBranch.Handoff, reason)
    {
    }

    public DerivedAction(ActionRequest request, ActionSpec spec, ActionResultBranch branch, string reason)
    {
        Request = request;
        Spec = spec;
        Branch = branch;
        Reason = reason ?? string.Empty;
    }

    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public ActionResultBranch Branch { get; }
    public string Reason { get; }
}

public sealed class ActionArbitrationResult
{
    private readonly List<AcceptedAction> acceptedActions = new();
    private readonly List<RejectedAction> rejectedActions = new();
    private readonly List<DerivedAction> derivedActions = new();
    private readonly List<DeferredAction> deferredActions = new();
    private readonly List<CommitProposal> commitProposals = new();
    private readonly Dictionary<long, MoveResult> actionResults = new();
    private readonly List<string> reasons = new();

    public IReadOnlyList<AcceptedAction> AcceptedActions => acceptedActions;
    public IReadOnlyList<RejectedAction> RejectedActions => rejectedActions;
    public IReadOnlyList<DerivedAction> DerivedActions => derivedActions;
    public IReadOnlyList<DeferredAction> DeferredActions => deferredActions;
    public IReadOnlyList<CommitProposal> CommitProposals => commitProposals;
    public IReadOnlyDictionary<long, MoveResult> ActionResults => actionResults;
    public IReadOnlyList<string> Reasons => reasons;

    public void Accept(AcceptedAction action, MoveResult? result)
    {
        acceptedActions.Add(action);
        if (result.HasValue && action.Request.Source.SourceStateId == 0)
        {
            actionResults[action.Request.ActionId] = result.Value;
        }
    }

    public void Reject(RejectedAction action)
    {
        rejectedActions.Add(action);
        if (action.Request.Source.SourceStateId == 0)
        {
            actionResults[action.Request.ActionId] = action.Result;
        }

        AddReason(action.Reason);
    }

    public void Derive(DerivedAction action, MoveResult? result)
    {
        derivedActions.Add(action);
        if (result.HasValue && action.Request.Source.SourceStateId == 0)
        {
            actionResults[action.Request.ActionId] = result.Value;
        }

        AddReason(action.Reason);
    }

    public void AddProposal(CommitProposal proposal)
    {
        commitProposals.Add(proposal);
    }

    public void AddDeferred(DeferredAction action)
    {
        deferredActions.Add(action);
    }

    public void AddReason(string reason)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            reasons.Add(reason);
        }
    }
}

public sealed class ActionSpecRegistry
{
    private readonly Dictionary<ActionSpecId, ActionSpec> specs;

    public ActionSpecRegistry(IEnumerable<ActionSpec> specs)
    {
        this.specs = new Dictionary<ActionSpecId, ActionSpec>();
        foreach (ActionSpec spec in specs)
        {
            if (!spec.SpecId.IsValid)
            {
                throw new InvalidOperationException("Action spec id is empty.");
            }

            if (this.specs.ContainsKey(spec.SpecId))
            {
                throw new InvalidOperationException("Duplicate action spec id: " + spec.SpecId);
            }

            this.specs.Add(spec.SpecId, spec);
        }
    }

    public static ActionSpecRegistry Default { get; } = LubanActionSpecRegistry.FromDirectory(GameCoreConfigPath.FindGeneratedJsonDirectory());

    public IReadOnlyCollection<ActionSpec> Specs => specs.Values.ToArray();

    public bool TryGet(ActionSpecId id, out ActionSpec spec)
    {
        return specs.TryGetValue(id, out spec);
    }

    public ActionSpec Get(ActionSpecId id)
    {
        if (!TryGet(id, out ActionSpec spec))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown action spec");
        }

        return spec;
    }

}

public sealed class ActionRequestAdapter
{
    private readonly ActionSpecRegistry registry;

    public ActionRequestAdapter(ActionSpecRegistry registry)
    {
        this.registry = registry;
    }

    public ActionRequest FromWorldAction(WorldAction action)
    {
        ActionSpec spec = registry.Get(action.SpecId);
        var source = new ActionSourceContext(spec.DefaultSource, action.EntityId, 0, spec.SourceTag);
        var target = new ActionTarget(0, action.TargetCoord, action.Direction);
        return new ActionRequest(action.ActionId, spec.SpecId, spec.DefaultPriority, source, action.EntityId, target, ActionRuntimeParams.FromWorldAction(action), action.CreatedTick, action.ReadyTick, action.ClientTick);
    }

    public ActionRequest FromPendingActionState(PendingActionState state, long serverTick)
    {
        PendingActionUnit unit = state.ReadyUnit;
        return FromPendingActionUnit(state, unit, serverTick);
    }

    public ActionRequest FromPendingActionUnit(PendingActionState state, PendingActionUnit unit, long serverTick)
    {
        ActionSpec spec = registry.Get(unit.SpecId);
        ActionSourceKind sourceKind = unit.DerivedFromUnitId != 0 ? ActionSourceKind.Handoff : spec.DefaultSource;
        var source = new ActionSourceContext(sourceKind, state.RootEntityId, state.StateId, spec.SourceTag);
        var target = new ActionTarget(0, unit.TargetCoord, unit.Direction);
        return new ActionRequest(unit.ActionUnitId, spec.SpecId, unit.Priority, source, unit.EntityId, target, unit.RuntimeParams, unit.CreatedTick, unit.ReadyTick, unit.ClientTick, unit.OwnerActionId, unit.DerivedFromUnitId);
    }
}

public sealed class ActionArbiter
{
    private readonly BodyResolver bodyResolver = new();
    private readonly BodyCapabilityResolver bodyCapabilities = new();
    private readonly ActionSpecRegistry registry;

    public ActionArbiter() : this(ActionSpecRegistry.Default)
    {
    }

    public ActionArbiter(ActionSpecRegistry registry)
    {
        this.registry = registry;
    }

    public ActionArbitrationResult ArbitrateMoves(GameWorld world, IReadOnlyList<ActionRequest> requests, PendingRuleStateStore pendingStates, long serverTick)
    {
        var result = new ActionArbitrationResult();
        var candidates = new List<AcceptedAction>();
        for (int i = 0; i < requests.Count; i++)
        {
            ProcessMoveRequest(world, requests[i], pendingStates, result, candidates, serverTick);
        }

        ResolveBodyConflicts(world, candidates, result);
        return result;
    }

    public bool TryBuildMoveClaims(GameWorld world, ActionRequest request, Direction direction, GridCoord? targetCoord, out IReadOnlyList<ActionClaim> claims)
    {
        claims = Array.Empty<ActionClaim>();
        if (!world.TryGetEntity(request.EntityId, out GameEntity entity))
        {
            return false;
        }

        return TryBuildMoveClaims(world, request, new BehaviorBody(request.EntityId, BehaviorBodyKind.SingleEntity, new[] { entity }), direction, targetCoord, out claims);
    }

    private static bool TryBuildMoveClaims(GameWorld world, ActionRequest request, BehaviorBody body, Direction direction, GridCoord? targetCoord, out IReadOnlyList<ActionClaim> claims)
    {
        claims = Array.Empty<ActionClaim>();
        var result = new List<ActionClaim>();
        for (int i = 0; i < body.Entities.Count; i++)
        {
            GameEntity member = body.Entities[i];
            if (!world.TryGetComponent(member, out PositionComponent position))
            {
                return false;
            }

            GridCoord to = targetCoord.HasValue && member.EntityId == request.EntityId ? targetCoord.Value : position.Coord.Add(direction);
            result.Add(new ActionClaim(request.ActionId, body.BodyId, member.EntityId, ActionClaimKind.BodyMove, position.Coord, to, "movement", ActionClaimMode.Exclusive, request.Priority));
        }

        claims = result;
        return true;
    }

    private void ProcessMoveRequest(GameWorld world, ActionRequest request, PendingRuleStateStore pendingStates, ActionArbitrationResult result, List<AcceptedAction> candidates, long serverTick)
    {
        ActionSpec spec = registry.Get(request.SpecId);
        if (!world.TryGetEntity(request.EntityId, out GameEntity entity))
        {
            Reject(world, request, spec, default, Direction.None, MoveErrorCode.UnknownEntity, UnknownEntityReason(spec), false, default, result);
            return;
        }

        if (!world.TryGetComponent(entity, out PositionComponent position))
        {
            Reject(world, request, spec, default, Direction.None, MoveErrorCode.MissingPosition, MissingPositionReason(spec), false, default, result);
            return;
        }

        if (!TryResolveMoveTarget(world, request, spec, entity, position, out Direction direction, out GridCoord target, out MoveErrorCode errorCode, out string reason))
        {
            Reject(world, request, spec, position.Coord, Direction.None, errorCode, reason, false, default, result);
            return;
        }

        if (!TryResolveActionBody(world, entity, spec, out BehaviorBody body, out string bodyReason))
        {
            Reject(world, request, spec, position.Coord, direction, MoveErrorCode.UnknownEntity, bodyReason, false, default, result);
            return;
        }

        if (!bodyCapabilities.CanMove(world, body))
        {
            Reject(world, request, spec, position.Coord, direction, MoveErrorCode.Blocked, "blocked by movement permission", false, default, result);
            return;
        }

        WorldTag bodyTags = AggregateTags(world, body);
        if (spec.RequiredTags != WorldTag.None && (bodyTags & spec.RequiredTags) != spec.RequiredTags)
        {
            Reject(world, request, spec, position.Coord, direction, MoveErrorCode.Blocked, "missing required tag", false, default, result);
            return;
        }

        if (spec.BlockedTags != WorldTag.None && (bodyTags & spec.BlockedTags) != WorldTag.None)
        {
            Reject(world, request, spec, position.Coord, direction, MoveErrorCode.Blocked, "blocked by tag", false, default, result);
            return;
        }

        if ((spec.CommitRules & ActionCommitRule.SetAutoMoveTick) != 0)
        {
            result.AddProposal(CommitProposal.SetAutoMoveTick(request.Priority, request.ActionId, request.EntityId, serverTick));
        }

        if (!TryBuildMoveClaims(world, request, body, direction, request.Target.TargetCoord, out IReadOnlyList<ActionClaim> claims))
        {
            Reject(world, request, spec, position.Coord, direction, MoveErrorCode.MissingPosition, "missing position", false, default, result);
            return;
        }

        IReadOnlyList<ExternalPushContact> contacts = bodyCapabilities.FindExternalPushContacts(world, claims, body);
        if (contacts.Count != 0)
        {
            ResolveBlocked(world, request, spec, pendingStates, result, position.Coord, direction, contacts, serverTick);
            return;
        }

        candidates.Add(new AcceptedAction(request, spec, body, direction, request.Target.TargetCoord, claims, serverTick));
    }

    private void ResolveBodyConflicts(GameWorld world, IReadOnlyList<AcceptedAction> candidates, ActionArbitrationResult result)
    {
        var accepted = new List<AcceptedAction>();
        foreach (IGrouping<long, AcceptedAction> group in candidates.GroupBy(candidate => candidate.Body.BodyId))
        {
            IReadOnlyList<AcceptedAction> ordered = group
                .OrderBy(candidate => candidate.Request.Priority)
                .ThenBy(candidate => candidate.Request.ActionId)
                .ThenBy(candidate => candidate.Request.Source.SourceStateId)
                .ThenBy(candidate => candidate.Request.EntityId)
                .ToArray();
            WorldActionPriority priority = ordered[0].Request.Priority;
            IReadOnlyList<AcceptedAction> winners = ordered.Where(candidate => candidate.Request.Priority == priority).ToArray();
            IReadOnlyList<AcceptedAction> losers = ordered.Where(candidate => candidate.Request.Priority != priority).ToArray();

            if (winners.Select(BuildClaimKey).Distinct().Count() > 1)
            {
                for (int i = 0; i < winners.Count; i++)
                {
                    RejectCandidate(world, winners[i], "conflicting body intents", result);
                }
            }
            else
            {
                accepted.Add(winners[0]);
                for (int i = 1; i < winners.Count; i++)
                {
                    if (winners[i].Spec.MergePolicy == ActionMergePolicy.SameClaim)
                    {
                        RejectCandidate(world, winners[i], "merged body intent", result);
                    }
                    else
                    {
                        RejectCandidate(world, winners[i], "conflicting body intents", result);
                    }
                }
            }

            for (int i = 0; i < losers.Count; i++)
            {
                RejectCandidate(world, losers[i], "interrupted by higher priority intent", result);
            }
        }

        ResolveTargetClaims(world, accepted, result);
    }

    private void ResolveTargetClaims(GameWorld world, IReadOnlyList<AcceptedAction> candidates, ActionArbitrationResult result)
    {
        var accepted = new HashSet<AcceptedAction>();
        var rejected = new HashSet<AcceptedAction>();
        foreach (IGrouping<GridCoord, AcceptedAction> group in candidates.SelectMany(candidate => candidate.Claims.Select(claim => new { candidate, claim })).GroupBy(item => item.claim.ToCoord, item => item.candidate))
        {
            IReadOnlyList<AcceptedAction> ordered = group
                .Distinct()
                .OrderBy(candidate => candidate.Request.Priority)
                .ThenBy(candidate => candidate.Request.ActionId)
                .ThenBy(candidate => candidate.Request.Source.SourceStateId)
                .ThenBy(candidate => candidate.Request.EntityId)
                .ToArray();
            if (ordered.Count == 0)
            {
                continue;
            }

            accepted.Add(ordered[0]);
            for (int i = 1; i < ordered.Count; i++)
            {
                if (rejected.Add(ordered[i]))
                {
                    RejectCandidate(world, ordered[i], "target reserved", result);
                }
            }
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            AcceptedAction candidate = candidates[i];
            if (!accepted.Contains(candidate) || rejected.Contains(candidate))
            {
                continue;
            }

            result.Accept(candidate, BuildAcceptedResult(world, candidate));
        }
    }

    private void ResolveBlocked(GameWorld world, ActionRequest request, ActionSpec spec, PendingRuleStateStore pendingStates, ActionArbitrationResult result, GridCoord current, Direction direction, IReadOnlyList<ExternalPushContact> contacts, long serverTick)
    {
        GameEntity blocking = FirstBlocking(world, contacts);
        if (spec.BlockedPolicy == ActionBlockedPolicy.StartPushIfPushable)
        {
            ResolvePushableBlock(world, request, spec, pendingStates, result, current, direction, contacts, serverTick);
            return;
        }

        if (spec.BlockedPolicy == ActionBlockedPolicy.BounceIfBouncable)
        {
            ResolveBounceBlock(world, request, spec, result, current, direction, blocking, serverTick);
            return;
        }

        RejectBlocked(world, request, spec, result, current, direction, blocking);
    }

    private void ResolvePushableBlock(GameWorld world, ActionRequest request, ActionSpec spec, PendingRuleStateStore pendingStates, ActionArbitrationResult result, GridCoord current, Direction direction, IReadOnlyList<ExternalPushContact> contacts, long serverTick)
    {
        if (!TryResolvePushContacts(world, spec, contacts, out IReadOnlyList<DeferredAction> deferredActions, out GameEntity blocking, out bool playerControlled, request, direction, serverTick))
        {
            RejectPushBlocked(world, request, spec, result, current, direction, blocking, playerControlled);
            return;
        }

        if (direction == Direction.None || !spec.Handoff.IsEnabled)
        {
            RejectPushBlocked(world, request, spec, result, current, direction, blocking, playerControlled);
            return;
        }

        for (int i = 0; i < deferredActions.Count; i++)
        {
            result.AddDeferred(deferredActions[i]);
        }

        result.Derive(new DerivedAction(request, spec, ActionResultBranch.Noop, "bounded/deferred-output"), BuildDeferredOutputResult(current, direction, request, blocking));
    }

    private bool TryResolvePushContacts(GameWorld world, ActionSpec spec, IReadOnlyList<ExternalPushContact> contacts, out IReadOnlyList<DeferredAction> deferredActions, out GameEntity firstBlocking, out bool firstBlockingPlayerControlled, ActionRequest request, Direction direction, long serverTick)
    {
        var result = new List<DeferredAction>();
        var seenSubjects = new HashSet<string>();
        firstBlocking = null!;
        firstBlockingPlayerControlled = false;
        for (int i = 0; i < contacts.Count; i++)
        {
            if (!world.TryGetEntity(contacts[i].BlockerEntityId, out GameEntity blocking))
            {
                deferredActions = Array.Empty<DeferredAction>();
                return false;
            }

            if (firstBlocking == null)
            {
                firstBlocking = blocking;
                firstBlockingPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
            }

            if (!bodyCapabilities.CanPushEntry(world, blocking))
            {
                firstBlocking = blocking;
                firstBlockingPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
                deferredActions = Array.Empty<DeferredAction>();
                return false;
            }

            ResolveHandoffSubject(world, spec, blocking, out ActionSpecId handoffSpecId, out IReadOnlyList<long> subjectEntityIds);
            if (!seenSubjects.Add(BuildSubjectKey(blocking.EntityId, subjectEntityIds)))
            {
                continue;
            }

            result.Add(new DeferredAction(handoffSpecId, blocking.EntityId, subjectEntityIds, direction, serverTick, serverTick + spec.DefaultCostTicks, spec.DefaultCostTicks, request.OwnerActionId, BuildDeferredDedupeKey(request, blocking.EntityId, subjectEntityIds, direction, serverTick)));
        }

        deferredActions = result;
        return result.Count != 0;
    }

    private static string BuildDeferredDedupeKey(ActionRequest request, long targetEntityId, IReadOnlyList<long> subjectEntityIds, Direction direction, long serverTick)
    {
        return request.OwnerActionId + ":" + serverTick + ":" + direction + ":" + BuildSubjectKey(targetEntityId, subjectEntityIds);
    }

    private static string BuildSubjectKey(long targetEntityId, IReadOnlyList<long> subjectEntityIds)
    {
        IReadOnlyList<long> ids = subjectEntityIds == null || subjectEntityIds.Count == 0 ? new[] { targetEntityId } : subjectEntityIds;
        return string.Join("|", ids.OrderBy(id => id));
    }

    private void RejectPushBlocked(GameWorld world, ActionRequest request, ActionSpec spec, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking, bool playerControlled)
    {
        if (request.Source.SourceStateId != 0)
        {
            Reject(world, request, spec, current, direction, playerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked, playerControlled ? "push occupied by player" : "push blocked", false, new CollisionInfo(blocking.EntityId, true, playerControlled), result);
            return;
        }

        RejectBlocked(world, request, spec, result, current, direction, blocking);
    }

    private static GameEntity FirstBlocking(GameWorld world, IReadOnlyList<ExternalPushContact> contacts)
    {
        return contacts.Count != 0 && world.TryGetEntity(contacts[0].BlockerEntityId, out GameEntity blocking) ? blocking : null!;
    }

    private void ResolveBounceBlock(GameWorld world, ActionRequest request, ActionSpec spec, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking, long serverTick)
    {
        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        string reason = targetPlayerControlled ? "occupied by player" : "blocked cell";
        MoveErrorCode errorCode = targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked;
        Direction finalDirection = direction;
        bool bounced = false;
        if ((spec.CommitRules & ActionCommitRule.SetDirectionOnBounce) != 0 &&
            world.TryGetEntity(request.EntityId, out GameEntity entity) &&
            world.HasComponent<BouncableComponent>(entity))
        {
            finalDirection = direction.Opposite();
            bounced = true;
            result.AddProposal(CommitProposal.SetDirection(request.Priority, request.ActionId, 0, request.EntityId, finalDirection, serverTick));
        }

        Reject(world, request, spec, current, finalDirection, errorCode, reason, bounced, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), result);
    }

    private void RejectBlocked(GameWorld world, ActionRequest request, ActionSpec spec, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking)
    {
        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        Reject(world, request, spec, current, direction, targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked, targetPlayerControlled ? "occupied by player" : "blocked cell", false, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), result);
    }

    private void RejectCandidate(GameWorld world, AcceptedAction action, string reason, ActionArbitrationResult result)
    {
        GridCoord coord = CurrentCoord(world, action.Request.EntityId);
        Reject(world, action.Request, action.Spec, coord, action.Direction, MoveErrorCode.Blocked, reason, false, default, result);
    }

    private void Reject(GameWorld world, ActionRequest request, ActionSpec spec, GridCoord coord, Direction direction, MoveErrorCode errorCode, string reason, bool bounced, CollisionInfo collision, ActionArbitrationResult result)
    {
        var moveResult = new MoveResult(false, request.EntityId, coord, direction, errorCode, reason, bounced, collision, request.ClientTick);
        result.Reject(new RejectedAction(request, spec, moveResult, reason));
    }

    private static bool TryResolveMoveTarget(GameWorld world, ActionRequest request, ActionSpec spec, GameEntity entity, PositionComponent position, out Direction direction, out GridCoord target, out MoveErrorCode errorCode, out string reason)
    {
        direction = Direction.None;
        target = position.Coord;
        errorCode = MoveErrorCode.None;
        reason = string.Empty;

        if (spec.TargetRule == ActionTargetRule.TargetCoordOneStep || spec.TargetRule == ActionTargetRule.TargetCoordAny)
        {
            if (!request.Target.TargetCoord.HasValue)
            {
                errorCode = MoveErrorCode.InvalidDirection;
                reason = "missing target";
                return false;
            }

            target = request.Target.TargetCoord.Value;
            if (spec.TargetRule == ActionTargetRule.TargetCoordOneStep && position.Coord.ManhattanDistance(target) > 1)
            {
                errorCode = MoveErrorCode.TooFar;
                reason = "target too far";
                return false;
            }

            direction = DirectionFromDelta(position.Coord, target);
            if (spec.TargetRule == ActionTargetRule.TargetCoordOneStep && direction == Direction.None && position.Coord != target)
            {
                errorCode = MoveErrorCode.InvalidDirection;
                reason = "invalid direction";
                return false;
            }

            return true;
        }

        if (spec.TargetRule == ActionTargetRule.DirectionFromRequest)
        {
            direction = request.Target.Direction;
            if (direction == Direction.None)
            {
                errorCode = MoveErrorCode.InvalidDirection;
                reason = "invalid direction";
                return false;
            }

            target = position.Coord.Add(direction);
            return true;
        }

        if (spec.TargetRule == ActionTargetRule.DirectionFromComponent)
        {
            if (!world.TryGetComponent(entity, out DirectionComponent directionComponent) ||
                !world.TryGetComponent(entity, out AutoMoveComponent _))
            {
                errorCode = MoveErrorCode.MissingPosition;
                reason = "missing auto move state";
                return false;
            }

            direction = directionComponent.Direction;
            target = position.Coord.Add(direction);
            return true;
        }

        errorCode = MoveErrorCode.InvalidDirection;
        reason = "invalid direction";
        return false;
    }

    private static Direction DirectionFromDelta(GridCoord current, GridCoord target)
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

    private static WorldTag AggregateTags(GameWorld world, BehaviorBody body)
    {
        WorldTag tags = WorldTag.None;
        for (int i = 0; i < body.Entities.Count; i++)
        {
            if (world.TryGetComponent(body.Entities[i], out TagSetComponent component))
            {
                tags |= component.Tags;
            }
        }

        return tags;
    }

    private bool TryResolveActionBody(GameWorld world, GameEntity entity, ActionSpec spec, out BehaviorBody body, out string reason)
    {
        if (spec.AllowsConnectedBodySubject)
        {
            return bodyResolver.TryResolve(world, entity, out body, out reason);
        }

        body = new BehaviorBody(entity.EntityId, BehaviorBodyKind.SingleEntity, new[] { entity });
        reason = string.Empty;
        return true;
    }

    private void ResolveHandoffSubject(GameWorld world, ActionSpec spec, GameEntity blocking, out ActionSpecId specId, out IReadOnlyList<long> subjectEntityIds)
    {
        specId = spec.Handoff.SpecId;
        if (spec.Handoff.SubjectKind != ActionSubjectKind.ConnectedBodyIfAny ||
            !bodyResolver.TryResolve(world, blocking, out BehaviorBody body, out _) ||
            body.Kind != BehaviorBodyKind.PortConnected)
        {
            subjectEntityIds = new[] { blocking.EntityId };
            return;
        }

        subjectEntityIds = body.Entities.Select(entity => entity.EntityId).ToArray();
    }

    private static PendingActionState FindActionState(PendingRuleStateStore pendingStates, long stateId)
    {
        IReadOnlyList<PendingActionState> states = pendingStates.ActionStates;
        for (int i = 0; i < states.Count; i++)
        {
            PendingActionState state = states[i];
            if (state.StateId == stateId && state.Status == PendingRuleStatus.Active)
            {
                return state;
            }
        }

        return null!;
    }

    private static string BuildClaimKey(AcceptedAction action)
    {
        return string.Join("|", action.Claims.OrderBy(claim => claim.EntityId).Select(claim => $"{claim.EntityId}:{claim.FromCoord.X},{claim.FromCoord.Y}>{claim.ToCoord.X},{claim.ToCoord.Y}"));
    }

    private static MoveResult BuildAcceptedResult(GameWorld world, AcceptedAction action)
    {
        GridCoord finalCoord = CurrentCoord(world, action.Request.EntityId);
        for (int i = 0; i < action.Claims.Count; i++)
        {
            if (action.Claims[i].EntityId == action.Request.EntityId)
            {
                finalCoord = action.Claims[i].ToCoord;
                break;
            }
        }

        return new MoveResult(true, action.Request.EntityId, finalCoord, action.Direction, MoveErrorCode.None, string.Empty, false, default, action.Request.ClientTick);
    }

    private static MoveResult BuildDeferredOutputResult(GridCoord current, Direction direction, ActionRequest request, GameEntity blocking)
    {
        return new MoveResult(true, request.EntityId, current, direction, MoveErrorCode.None, "bounded/deferred-output", false, new CollisionInfo(blocking.EntityId, true, false), request.ClientTick);
    }

    private static GridCoord CurrentCoord(GameWorld world, long entityId)
    {
        if (world.TryGetEntity(entityId, out GameEntity entity) &&
            world.TryGetComponent(entity, out PositionComponent position))
        {
            return position.Coord;
        }

        return default;
    }

    private static string UnknownEntityReason(ActionSpec spec)
    {
        return spec.DefaultSource == ActionSourceKind.Debug ? "entity not found" : "unknown entity";
    }

    private static string MissingPositionReason(ActionSpec spec)
    {
        return spec.DefaultSource == ActionSourceKind.Auto ? "missing auto move state" : "missing position";
    }
}
}
