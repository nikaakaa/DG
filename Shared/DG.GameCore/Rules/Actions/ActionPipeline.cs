using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum ActionUnitLifecycleState
{
    Queued = 1,
    Ready = 2,
    CandidateBuilt = 3,
    Accepted = 4,
    Rejected = 5,
    Interrupted = 6,
    Planned = 7,
    Committed = 8,
    DeferredOutputEmitted = 9,
    Completed = 10,
    Failed = 11
}

public readonly struct ActionUnitTransition
{
    public ActionUnitTransition(long actionId, ActionUnitLifecycleState from, ActionUnitLifecycleState to, string reason)
    {
        ActionId = actionId;
        From = from;
        To = to;
        Reason = reason ?? string.Empty;
    }

    public long ActionId { get; }
    public ActionUnitLifecycleState From { get; }
    public ActionUnitLifecycleState To { get; }
    public string Reason { get; }
}

public sealed class ActionUnitStateMachine
{
    private readonly Dictionary<long, ActionUnitLifecycleState> states = new();

    public ActionUnitLifecycleState Get(long actionId)
    {
        return states.TryGetValue(actionId, out ActionUnitLifecycleState state) ? state : ActionUnitLifecycleState.Queued;
    }

    public ActionUnitTransition Advance(long actionId, ActionUnitLifecycleState next, string reason = "")
    {
        ActionUnitLifecycleState current = Get(actionId);
        states[actionId] = next;
        return new ActionUnitTransition(actionId, current, next, reason);
    }
}

public readonly struct ActionGateResult
{
    public ActionGateResult(bool accepted, MoveErrorCode errorCode, string reason)
    {
        Accepted = accepted;
        ErrorCode = errorCode;
        Reason = reason ?? string.Empty;
    }

    public bool Accepted { get; }
    public MoveErrorCode ErrorCode { get; }
    public string Reason { get; }

    public static ActionGateResult Pass => new(true, MoveErrorCode.None, string.Empty);

    public static ActionGateResult Fail(MoveErrorCode errorCode, string reason)
    {
        return new ActionGateResult(false, errorCode, reason);
    }
}

public sealed class ActionTagGate
{
    public ActionGateResult Evaluate(GameWorld world, ActionSpec spec, BehaviorBody body)
    {
        WorldTag bodyTags = AggregateTags(world, body);
        if (spec.RequiredTags != WorldTag.None && (bodyTags & spec.RequiredTags) != spec.RequiredTags)
        {
            return ActionGateResult.Fail(MoveErrorCode.Blocked, "missing required tag");
        }

        if (spec.BlockedTags != WorldTag.None && (bodyTags & spec.BlockedTags) != WorldTag.None)
        {
            return ActionGateResult.Fail(MoveErrorCode.Blocked, "blocked by tag");
        }

        return ActionGateResult.Pass;
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
}

public sealed class ActionSubjectSelector
{
    private readonly BodyResolver bodyResolver = new();

    public bool TryResolve(GameWorld world, ActionRequest request, GameEntity entity, ActionSpec spec, out BehaviorBody body, out string reason)
    {
        if (spec.AllowsConnectedBodySubject)
        {
            return bodyResolver.TryResolve(world, entity, out body, out reason);
        }

        if (request.SubjectEntityIds.Count > 1)
        {
            var members = new List<GameEntity>();
            for (int i = 0; i < request.SubjectEntityIds.Count; i++)
            {
                if (!world.TryGetEntity(request.SubjectEntityIds[i], out GameEntity member))
                {
                    body = null!;
                    reason = "entity not found";
                    return false;
                }

                members.Add(member);
            }

            body = new BehaviorBody(BodyResolver.BuildBodyId(members), BehaviorBodyKind.PortConnected, members);
            reason = string.Empty;
            return true;
        }

        body = new BehaviorBody(entity.EntityId, BehaviorBodyKind.SingleEntity, new[] { entity });
        reason = string.Empty;
        return true;
    }
}

public readonly struct TargetingResult
{
    public TargetingResult(IReadOnlyList<ActionTargetData> targetData, Direction direction, GridCoord primaryTargetCoord)
    {
        TargetData = targetData == null ? Array.Empty<ActionTargetData>() : targetData.ToArray();
        Direction = direction;
        PrimaryTargetCoord = primaryTargetCoord;
    }

    public IReadOnlyList<ActionTargetData> TargetData { get; }
    public Direction Direction { get; }
    public GridCoord PrimaryTargetCoord { get; }
}


public interface ITargetSelector
{
    TargetSelectorId SelectorId { get; }
    bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason);
}


public sealed class TargetSelectorRegistry
{
    private readonly Dictionary<TargetSelectorId, ITargetSelector> selectors = new();

    public TargetSelectorRegistry()
    {
    }

    public TargetSelectorRegistry(IEnumerable<ITargetSelector> selectors)
    {
        if (selectors == null)
        {
            return;
        }

        foreach (ITargetSelector selector in selectors)
        {
            Register(selector);
        }
    }

    public static TargetSelectorRegistry CreateDefault()
    {
        var registry = new TargetSelectorRegistry();
        registry.Register(new NoneTargetSelector());
        registry.Register(new SelfTargetSelector());
        registry.Register(new DirectionCellTargetSelector());
        registry.Register(new TargetCoordSelector());
        registry.Register(new FrontEntitiesTargetSelector());
        return registry;
    }

    public void Register(ITargetSelector selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        if (!selector.SelectorId.IsValid)
        {
            throw new InvalidOperationException("Target selector id is empty.");
        }

        if (selectors.ContainsKey(selector.SelectorId))
        {
            throw new InvalidOperationException("Duplicate target selector id: " + selector.SelectorId);
        }

        selectors.Add(selector.SelectorId, selector);
    }

    public ITargetSelector Get(TargetSelectorId selectorId)
    {
        if (!selectors.TryGetValue(selectorId, out ITargetSelector selector))
        {
            throw new ArgumentOutOfRangeException(nameof(selectorId), selectorId, "Unknown target selector");
        }

        return selector;
    }
}


public class TargetingSystem
{
    private readonly TargetSelectorRegistry selectors;
    private readonly Dictionary<TargetFilterSpecId, TargetFilterSpec> filters;

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
                TargetFilterConditionKind.HasComponent => HasComponent(world, target, condition.ComponentKind),
                TargetFilterConditionKind.MissingComponent => !HasComponent(world, target, condition.ComponentKind),
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

    private static bool HasComponent(GameWorld world, GameEntity target, ComponentKind kind)
    {
        return kind switch
        {
            ComponentKind.Position => world.HasComponent<PositionComponent>(target),
            ComponentKind.Direction => world.HasComponent<DirectionComponent>(target),
            ComponentKind.Collider => world.HasComponent<ColliderComponent>(target),
            ComponentKind.Blocking => world.HasComponent<BlockingComponent>(target),
            ComponentKind.Bouncable => world.HasComponent<BouncableComponent>(target),
            ComponentKind.AutoMove => world.HasComponent<AutoMoveComponent>(target),
            ComponentKind.PlayerControl => world.HasComponent<PlayerControlComponent>(target),
            ComponentKind.PushOnEnter => world.HasComponent<PushOnEnterComponent>(target),
            ComponentKind.Pushable => world.HasComponent<PushableComponent>(target),
            ComponentKind.PortConnector => world.HasComponent<PortConnectorComponent>(target),
            _ => false
        };
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


public sealed class NoneTargetSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("none");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = new TargetingResult(Array.Empty<ActionTargetData>(), Direction.None, position.Coord);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }
}


public sealed class SelfTargetSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("self");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = new TargetingResult(new[] { ActionTargetData.Self(entity.EntityId, position.Coord, context.TargetHint.Direction) }, context.TargetHint.Direction, position.Coord);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }
}


public sealed class DirectionCellTargetSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("direction_cell");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = default;
        bool found = true;
        Direction direction = targeting.DirectionSource == TargetDirectionSource.Component ? DirectionFromComponent(world, entity, out found) : context.TargetHint.Direction;
        if (targeting.DirectionSource == TargetDirectionSource.Component && !found)
        {
            errorCode = MoveErrorCode.MissingPosition;
            reason = "missing auto move state";
            return false;
        }

        if (direction == Direction.None)
        {
            errorCode = MoveErrorCode.InvalidDirection;
            reason = "invalid direction";
            return false;
        }

        GridCoord target = position.Coord.Add(direction);
        result = new TargetingResult(new[] { ActionTargetData.Cell(targeting.DirectionSource == TargetDirectionSource.Component ? "direction_component" : "direction", target, direction, 0) }, direction, target);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }

    private static Direction DirectionFromComponent(GameWorld world, GameEntity entity, out bool found)
    {
        found = world.TryGetComponent(entity, out DirectionComponent directionComponent) &&
            world.TryGetComponent(entity, out AutoMoveComponent _);
        return found ? directionComponent.Direction : Direction.None;
    }
}


public sealed class TargetCoordSelector : ITargetSelector
{
    public TargetSelectorId SelectorId => new("target_coord");

    public bool TrySelect(GameWorld world, ActionContext context, ActionSpec spec, TargetingSpec targeting, GameEntity entity, PositionComponent position, TargetFilterSpec filter, out TargetingResult result, out MoveErrorCode errorCode, out string reason)
    {
        result = default;
        if (!context.TargetHint.TargetCoord.HasValue)
        {
            errorCode = MoveErrorCode.InvalidDirection;
            reason = "missing target";
            return false;
        }

        GridCoord target = context.TargetHint.TargetCoord.Value;
        if (targeting.Range > 0 && position.Coord.ManhattanDistance(target) > targeting.Range)
        {
            errorCode = MoveErrorCode.TooFar;
            reason = "target too far";
            return false;
        }

        Direction direction = TargetingSystem.DirectionFromDelta(position.Coord, target);
        if (targeting.Range == 1 && direction == Direction.None && position.Coord != target)
        {
            errorCode = MoveErrorCode.InvalidDirection;
            reason = "invalid direction";
            return false;
        }

        result = new TargetingResult(new[] { ActionTargetData.Cell("target", target, direction, 0) }, direction, target);
        errorCode = MoveErrorCode.None;
        reason = string.Empty;
        return true;
    }
}


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

public readonly struct ActionTargetData
{
    public ActionTargetData(ActionTargetDataKind kind, long targetEntityId, GridCoord targetCoord, long bodyId, GridCoord hitCell, int hitOrder, Direction direction, string queryId)
    {
        Kind = kind;
        TargetEntityId = targetEntityId;
        TargetCoord = targetCoord;
        BodyId = bodyId;
        HitCell = hitCell;
        HitOrder = hitOrder;
        Direction = direction;
        QueryId = queryId ?? string.Empty;
    }

    public ActionTargetDataKind Kind { get; }
    public long TargetEntityId { get; }
    public GridCoord TargetCoord { get; }
    public long BodyId { get; }
    public GridCoord HitCell { get; }
    public int HitOrder { get; }
    public Direction Direction { get; }
    public string QueryId { get; }

    public static ActionTargetData Self(long entityId, GridCoord coord, Direction direction)
    {
        return new ActionTargetData(ActionTargetDataKind.Self, entityId, coord, entityId, coord, 0, direction, "self");
    }

    public static ActionTargetData Cell(string queryId, GridCoord coord, Direction direction, int hitOrder)
    {
        return new ActionTargetData(ActionTargetDataKind.Cell, 0, coord, 0, coord, hitOrder, direction, queryId);
    }

    public static ActionTargetData Entity(string queryId, long entityId, GridCoord coord, Direction direction, int hitOrder)
    {
        return new ActionTargetData(ActionTargetDataKind.Entity, entityId, coord, entityId, coord, hitOrder, direction, queryId);
    }
}

public sealed class ActionExecutionOutput
{
    public ActionExecutionOutput(ActionContext context, ActionExecutionSuccessPolicy successPolicy, IReadOnlyList<ActionTargetData> targetData, IReadOnlyList<ActionClaim> claims, IReadOnlyList<CommitProposal> commitProposals, IReadOnlyList<DeferredAction> deferredActions, MoveResult? blockedResult = null)
    {
        Context = context;
        SuccessPolicy = successPolicy;
        TargetData = targetData == null ? Array.Empty<ActionTargetData>() : targetData.ToArray();
        Claims = claims == null ? Array.Empty<ActionClaim>() : claims.ToArray();
        CommitProposals = commitProposals == null ? Array.Empty<CommitProposal>() : commitProposals.ToArray();
        DeferredActions = deferredActions == null ? Array.Empty<DeferredAction>() : deferredActions.ToArray();
        BlockedResult = blockedResult;
    }

    public ActionContext Context { get; }
    public ActionExecutionSuccessPolicy SuccessPolicy { get; }
    public IReadOnlyList<ActionTargetData> TargetData { get; }
    public IReadOnlyList<ActionClaim> Claims { get; }
    public IReadOnlyList<CommitProposal> CommitProposals { get; }
    public IReadOnlyList<DeferredAction> DeferredActions { get; }
    public MoveResult? BlockedResult { get; }
    public long ResultOwnerEntityId => Context.SourceEntityId;

    public static ActionExecutionOutput Empty(ActionContext context, IReadOnlyList<ActionTargetData> targetData)
    {
        return new ActionExecutionOutput(context, ActionExecutionSuccessPolicy.AllOrNothing, targetData, Array.Empty<ActionClaim>(), Array.Empty<CommitProposal>(), Array.Empty<DeferredAction>());
    }
}

public sealed class ActionClaimBuilder
{
    public bool TryBuildMoveExecutionOutput(GameWorld world, ActionContext context, BehaviorBody body, Direction direction, IReadOnlyList<ActionTargetData> targetData, out ActionExecutionOutput output)
    {
        if (!TryBuildMoveClaims(world, context, body, direction, targetData, out IReadOnlyList<ActionClaim> claims))
        {
            output = ActionExecutionOutput.Empty(context, targetData);
            return false;
        }

        output = new ActionExecutionOutput(context, ActionExecutionSuccessPolicy.AllOrNothing, targetData, claims, Array.Empty<CommitProposal>(), Array.Empty<DeferredAction>());
        return true;
    }

    public bool TryBuildMoveClaims(GameWorld world, ActionContext context, BehaviorBody body, Direction direction, IReadOnlyList<ActionTargetData> targetData, out IReadOnlyList<ActionClaim> claims)
    {
        claims = Array.Empty<ActionClaim>();
        var result = new List<ActionClaim>();
        if (targetData != null &&
            targetData.Any(item => item.Kind == ActionTargetDataKind.Entity) &&
            body.Kind == BehaviorBodyKind.SingleEntity &&
            body.Entities.Count == 1)
        {
            for (int i = 0; i < targetData.Count; i++)
            {
                ActionTargetData target = targetData[i];
                if (target.TargetEntityId == 0 ||
                    !world.TryGetEntity(target.TargetEntityId, out GameEntity targetEntity) ||
                    !world.TryGetComponent(targetEntity, out PositionComponent position))
                {
                    continue;
                }

                GridCoord to = position.Coord.Add(direction);
                result.Add(new ActionClaim(context.ActionId, target.TargetEntityId, target.TargetEntityId, ActionClaimKind.BodyMove, position.Coord, to, "movement", ActionClaimMode.Exclusive, context.Priority));
            }

            claims = result
                .OrderBy(claim => claim.FromCoord.X)
                .ThenBy(claim => claim.FromCoord.Y)
                .ThenBy(claim => claim.EntityId)
                .ToArray();
            return claims.Count != 0;
        }

        GridCoord? targetCoord = targetData == null || targetData.Count == 0 ? null : targetData[0].TargetCoord;
        return TryBuildMoveClaims(world, context.ActionId, context.Priority, context.SubjectEntryEntityId, body, direction, targetCoord, out claims);
    }

    public bool TryBuildMoveClaims(GameWorld world, ActionRequest request, BehaviorBody body, Direction direction, GridCoord? targetCoord, out IReadOnlyList<ActionClaim> claims)
    {
        return TryBuildMoveClaims(world, request.ActionId, request.Priority, request.EntityId, body, direction, targetCoord, out claims);
    }

    private bool TryBuildMoveClaims(GameWorld world, long actionId, WorldActionPriority priority, long subjectEntryEntityId, BehaviorBody body, Direction direction, GridCoord? targetCoord, out IReadOnlyList<ActionClaim> claims)
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

            GridCoord to = targetCoord.HasValue && member.EntityId == subjectEntryEntityId ? targetCoord.Value : position.Coord.Add(direction);
            result.Add(new ActionClaim(actionId, body.BodyId, member.EntityId, ActionClaimKind.BodyMove, position.Coord, to, "movement", ActionClaimMode.Exclusive, priority));
        }

        claims = result;
        return true;
    }
}

public readonly struct BlockedOutcomeContext
{
    public BlockedOutcomeContext(GameWorld world, ActionRequest request, ActionContext actionContext, ActionSpec spec, BehaviorBody body, GridCoord current, Direction direction, IReadOnlyList<ActionTargetData> targetData, IReadOnlyList<ExternalPushContact> contacts, long serverTick)
    {
        World = world;
        Request = request;
        ActionContext = actionContext;
        Spec = spec;
        Body = body;
        Current = current;
        Direction = direction;
        TargetData = targetData ?? Array.Empty<ActionTargetData>();
        Contacts = contacts ?? Array.Empty<ExternalPushContact>();
        ServerTick = serverTick;
    }

    public GameWorld World { get; }
    public ActionRequest Request { get; }
    public ActionContext ActionContext { get; }
    public ActionSpec Spec { get; }
    public BehaviorBody Body { get; }
    public GridCoord Current { get; }
    public Direction Direction { get; }
    public IReadOnlyList<ActionTargetData> TargetData { get; }
    public IReadOnlyList<ExternalPushContact> Contacts { get; }
    public long ServerTick { get; }
}

public sealed class ActionBlockedOutcomeExecutor
{
    private readonly BodyResolver bodyResolver = new();
    private readonly BodyCapabilityResolver bodyCapabilities = new();
    private readonly BlockedResultResolver blockedResultResolver = new();

    public void Resolve(BlockedResultPolicy policy, BlockedOutcomeContext context, ActionArbitrationResult result)
    {
        GameEntity blocking = FirstBlocking(context.World, context.Contacts);
        if (!blockedResultResolver.TryResolve(policy, new BlockedResultContext(context.World, context.Request, context.Spec, context.Body, context.Contacts, context.Direction), out BlockedResultDecision decision))
        {
            RejectBlocked(context.World, context.Request, context.Spec, result, context.Current, context.Direction, blocking);
            return;
        }

        BlockedResultBranch branch = decision.Branch;
        if (branch.ResultKind == BlockedResultKind.DeriveAction)
        {
            ResolveDerivedBlock(context.World, context.Request, context.Spec, branch, result, context.Current, context.Direction, decision.MatchedContacts, context.ServerTick);
            return;
        }

        if (branch.ResultKind == BlockedResultKind.Bounce)
        {
            ResolveBounceBlock(context.World, context.Request, context.Spec, branch, result, context.Current, context.Direction, blocking, context.ServerTick);
            return;
        }

        if (branch.ResultKind == BlockedResultKind.Noop)
        {
            ActionExecutionOutput output = BuildBlockedExecutionOutput(context, branch, BuildDeferredOutputResult(context.Current, context.Direction, context.Request, blocking), Array.Empty<DeferredAction>(), Array.Empty<CommitProposal>());
            ApplyExecutionOutput(output, result);
            result.Derive(new DerivedAction(context.Request, context.Spec, ActionResultBranch.Noop, branch.Reason), output.BlockedResult);
            return;
        }

        RejectBlocked(context.World, context.Request, context.Spec, branch, result, context.Current, context.Direction, blocking);
    }

    private void ResolveDerivedBlock(GameWorld world, ActionRequest request, ActionSpec spec, BlockedResultBranch branch, ActionArbitrationResult result, GridCoord current, Direction direction, IReadOnlyList<ExternalPushContact> contacts, long serverTick)
    {
        if (!TryResolvePushContacts(world, spec, branch, contacts, out IReadOnlyList<DeferredAction> deferredActions, out GameEntity blocking, out bool playerControlled, request, direction, serverTick))
        {
            RejectPushBlocked(world, request, spec, result, current, direction, blocking, playerControlled);
            return;
        }

        if (direction == Direction.None || !spec.Handoff.IsEnabled)
        {
            RejectPushBlocked(world, request, spec, result, current, direction, blocking, playerControlled);
            return;
        }

        ActionExecutionOutput output = BuildBlockedExecutionOutput(world, request, spec, branch, current, direction, blocking, serverTick, deferredActions, Array.Empty<CommitProposal>(), BuildDeferredOutputResult(current, direction, request, blocking));
        ApplyExecutionOutput(output, result);
        result.Derive(new DerivedAction(request, spec, ActionResultBranch.Noop, "bounded/deferred-output"), output.BlockedResult);
    }

    private bool TryResolvePushContacts(GameWorld world, ActionSpec spec, BlockedResultBranch branch, IReadOnlyList<ExternalPushContact> contacts, out IReadOnlyList<DeferredAction> deferredActions, out GameEntity firstBlocking, out bool firstBlockingPlayerControlled, ActionRequest request, Direction direction, long serverTick)
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

            ResolveHandoffSubject(world, branch, blocking, out ActionSpecId handoffSpecId, out IReadOnlyList<long> subjectEntityIds);
            if (!seenSubjects.Add(BuildSubjectKey(blocking.EntityId, subjectEntityIds)))
            {
                continue;
            }

            result.Add(new DeferredAction(handoffSpecId, blocking.EntityId, subjectEntityIds, direction, serverTick, serverTick + spec.DefaultCostTicks, spec.DefaultCostTicks, request.OwnerActionId, BuildDeferredDedupeKey(request, blocking.EntityId, subjectEntityIds, direction, serverTick)));
        }

        deferredActions = result;
        return result.Count != 0;
    }

    private void ResolveBounceBlock(GameWorld world, ActionRequest request, ActionSpec spec, BlockedResultBranch branch, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking, long serverTick)
    {
        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        string reason = targetPlayerControlled ? "occupied by player" : BranchReason(branch, "blocked cell");
        MoveErrorCode errorCode = targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked;
        Direction finalDirection = direction;
        bool bounced = false;
        if ((branch.CommitRules & ActionCommitRule.SetDirectionOnBounce) != 0 &&
            world.TryGetEntity(request.EntityId, out GameEntity entity) &&
            world.HasComponent<BouncableComponent>(entity))
        {
            finalDirection = direction.Opposite();
            bounced = true;
        }

        var moveResult = new MoveResult(false, request.EntityId, current, finalDirection, errorCode, reason, bounced, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), request.ClientTick);
        IReadOnlyList<CommitProposal> proposals = bounced ? new[] { CommitProposal.SetDirection(request.Priority, request.ActionId, 0, request.EntityId, finalDirection, serverTick) } : Array.Empty<CommitProposal>();
        ActionExecutionOutput output = BuildBlockedExecutionOutput(world, request, spec, branch, current, direction, blocking, serverTick, Array.Empty<DeferredAction>(), proposals, moveResult);
        ApplyExecutionOutput(output, result);
        MoveResult blockedResult = output.BlockedResult ?? moveResult;
        result.Reject(new RejectedAction(request, spec, blockedResult, reason));
    }

    private void RejectBlocked(GameWorld world, ActionRequest request, ActionSpec spec, BlockedResultBranch branch, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking)
    {
        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        MoveErrorCode errorCode = targetPlayerControlled && branch.ErrorCode != MoveErrorCode.Immune ? MoveErrorCode.Occupied : branch.ErrorCode;
        if (errorCode == MoveErrorCode.None)
        {
            errorCode = MoveErrorCode.Blocked;
        }

        string reason = targetPlayerControlled && errorCode == MoveErrorCode.Occupied ? "occupied by player" : BranchReason(branch, "blocked cell");
        Reject(request, current, direction, errorCode, reason, false, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), result, spec);
    }

    private void RejectBlocked(GameWorld world, ActionRequest request, ActionSpec spec, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking)
    {
        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        Reject(request, current, direction, targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked, targetPlayerControlled ? "occupied by player" : "blocked cell", false, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), result, spec);
    }

    private void RejectPushBlocked(GameWorld world, ActionRequest request, ActionSpec spec, ActionArbitrationResult result, GridCoord current, Direction direction, GameEntity blocking, bool playerControlled)
    {
        if (request.Source.SourceStateId != 0)
        {
            Reject(request, current, direction, playerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked, playerControlled ? "push occupied by player" : "push blocked", false, new CollisionInfo(blocking.EntityId, true, playerControlled), result, spec);
            return;
        }

        RejectBlocked(world, request, spec, result, current, direction, blocking);
    }

    private void ResolveHandoffSubject(GameWorld world, BlockedResultBranch branch, GameEntity blocking, out ActionSpecId specId, out IReadOnlyList<long> subjectEntityIds)
    {
        specId = branch.ResultSpecId;
        if (branch.SubjectKind != ActionSubjectKind.ConnectedBodyIfAny ||
            !bodyResolver.TryResolve(world, blocking, out BehaviorBody body, out _) ||
            body.Kind != BehaviorBodyKind.PortConnected)
        {
            subjectEntityIds = new[] { blocking.EntityId };
            return;
        }

        subjectEntityIds = body.Entities.Select(entity => entity.EntityId).ToArray();
    }

    private static GameEntity FirstBlocking(GameWorld world, IReadOnlyList<ExternalPushContact> contacts)
    {
        return contacts.Count != 0 && world.TryGetEntity(contacts[0].BlockerEntityId, out GameEntity blocking) ? blocking : null!;
    }

    private static void Reject(ActionRequest request, GridCoord coord, Direction direction, MoveErrorCode errorCode, string reason, bool bounced, CollisionInfo collision, ActionArbitrationResult result, ActionSpec spec)
    {
        var moveResult = new MoveResult(false, request.EntityId, coord, direction, errorCode, reason, bounced, collision, request.ClientTick);
        result.Reject(new RejectedAction(request, spec, moveResult, reason));
    }

    private static ActionExecutionOutput BuildBlockedExecutionOutput(BlockedOutcomeContext context, BlockedResultBranch branch, MoveResult moveResult, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<CommitProposal> commitProposals)
    {
        return new ActionExecutionOutput(context.ActionContext, ActionExecutionSuccessPolicy.AllOrNothing, context.TargetData, Array.Empty<ActionClaim>(), commitProposals, deferredActions, moveResult);
    }

    private static ActionExecutionOutput BuildBlockedExecutionOutput(GameWorld world, ActionRequest request, ActionSpec spec, BlockedResultBranch branch, GridCoord current, Direction direction, GameEntity blocking, long serverTick, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<CommitProposal> commitProposals, MoveResult moveResult)
    {
        ActionContext.TryCreate(request, spec, out ActionContext context, out _);
        var targetData = new[] { ActionTargetData.Cell(BranchReason(branch, "blocked"), current.Add(direction), direction, 0) };
        return new ActionExecutionOutput(context, ActionExecutionSuccessPolicy.AllOrNothing, targetData, Array.Empty<ActionClaim>(), commitProposals, deferredActions, moveResult);
    }

    private static void ApplyExecutionOutput(ActionExecutionOutput output, ActionArbitrationResult result)
    {
        for (int i = 0; i < output.CommitProposals.Count; i++)
        {
            result.AddProposal(output.CommitProposals[i]);
        }

        for (int i = 0; i < output.DeferredActions.Count; i++)
        {
            result.AddDeferred(output.DeferredActions[i]);
        }
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

    private static string BranchReason(BlockedResultBranch branch, string fallback)
    {
        return string.IsNullOrEmpty(branch.Reason) ? fallback : branch.Reason;
    }

    private static MoveResult BuildDeferredOutputResult(GridCoord current, Direction direction, ActionRequest request, GameEntity blocking)
    {
        return new MoveResult(true, request.EntityId, current, direction, MoveErrorCode.None, "bounded/deferred-output", false, new CollisionInfo(blocking.EntityId, true, false), request.ClientTick);
    }
}

public readonly struct ActionStrategyContext
{
    public ActionStrategyContext(GameWorld world, ActionRequest request, ActionSpec spec, List<CommitProposal> proposals, List<ActionRequest> moveRequests, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        World = world;
        Request = request;
        Spec = spec;
        Proposals = proposals;
        MoveRequests = moveRequests;
        ActionResults = actionResults;
        Reasons = reasons;
        ServerTick = serverTick;
    }

    public GameWorld World { get; }
    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public List<CommitProposal> Proposals { get; }
    public List<ActionRequest> MoveRequests { get; }
    public Dictionary<long, MoveResult> ActionResults { get; }
    public List<string> Reasons { get; }
    public long ServerTick { get; }
}

public interface IActionStrategy
{
    ActionPrimitive Primitive { get; }
    void Process(ActionStrategyContext context);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ActionStrategyAttribute : Attribute
{
    public ActionStrategyAttribute(ActionPrimitive primitive)
    {
        Primitive = primitive;
        StrategyKey = primitive.ToString();
    }

    public ActionStrategyAttribute(string strategyKey, ActionPrimitive primitive)
    {
        StrategyKey = string.IsNullOrWhiteSpace(strategyKey) ? throw new ArgumentException("Strategy key is empty.", nameof(strategyKey)) : strategyKey;
        Primitive = primitive;
    }

    public string StrategyKey { get; }
    public ActionPrimitive Primitive { get; }
}

public readonly struct ActionStrategyRegistrationDescriptor
{
    public ActionStrategyRegistrationDescriptor(ActionPrimitive primitive, string strategyKey, string typeName)
    {
        Primitive = primitive;
        StrategyKey = string.IsNullOrWhiteSpace(strategyKey) ? primitive.ToString() : strategyKey;
        TypeName = string.IsNullOrWhiteSpace(typeName) ? throw new ArgumentException("Strategy type name is empty.", nameof(typeName)) : typeName.Replace('+', '.');
    }

    public ActionPrimitive Primitive { get; }
    public string StrategyKey { get; }
    public string TypeName { get; }
}

public static class ActionStrategyRegistrationGenerator
{
    public static string GenerateSource(string namespaceName, string className, IEnumerable<ActionStrategyRegistrationDescriptor> descriptors)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw new ArgumentException("Namespace is empty.", nameof(namespaceName));
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentException("Class name is empty.", nameof(className));
        }

        List<ActionStrategyRegistrationDescriptor> ordered = Validate(descriptors).OrderBy(item => item.StrategyKey, StringComparer.Ordinal).ToList();
        var lines = new List<string>
        {
            "namespace " + namespaceName,
            "{",
            "public static partial class " + className,
            "{",
            "    public static ActionStrategyRegistry CreateDefault()",
            "    {",
            "        var registry = new ActionStrategyRegistry();"
        };

        for (int i = 0; i < ordered.Count; i++)
        {
            lines.Add("        registry.Register(new " + ordered[i].TypeName + "());");
        }

        lines.Add("        return registry;");
        lines.Add("    }");
        lines.Add("}");
        lines.Add("}");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    public static IReadOnlyList<ActionStrategyRegistrationDescriptor> Validate(IEnumerable<ActionStrategyRegistrationDescriptor> descriptors)
    {
        if (descriptors == null)
        {
            throw new ArgumentNullException(nameof(descriptors));
        }

        var result = descriptors.ToList();
        if (result.Count == 0)
        {
            throw new InvalidOperationException("No action strategies found.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var primitives = new HashSet<ActionPrimitive>();
        for (int i = 0; i < result.Count; i++)
        {
            ActionStrategyRegistrationDescriptor descriptor = result[i];
            if (string.IsNullOrWhiteSpace(descriptor.TypeName))
            {
                throw new InvalidOperationException("Action strategy type is empty.");
            }

            if (!keys.Add(descriptor.StrategyKey))
            {
                throw new InvalidOperationException("Duplicate action strategy key: " + descriptor.StrategyKey);
            }

            if (!primitives.Add(descriptor.Primitive))
            {
                throw new InvalidOperationException("Duplicate action strategy primitive: " + descriptor.Primitive);
            }
        }

        return result;
    }
}

[ActionStrategy("move", ActionPrimitive.Move)]
public sealed class MoveActionStrategy : IActionStrategy
{
    public ActionPrimitive Primitive => ActionPrimitive.Move;

    public void Process(ActionStrategyContext context)
    {
        context.MoveRequests.Add(context.Request);
    }
}

[ActionStrategy("spawn", ActionPrimitive.Spawn)]
public sealed class SpawnActionStrategy : IActionStrategy
{
    public ActionPrimitive Primitive => ActionPrimitive.Spawn;

    public void Process(ActionStrategyContext context)
    {
        ActionRequest request = context.Request;
        if (!request.Target.TargetCoord.HasValue)
        {
            context.ActionResults[request.ActionId] = new MoveResult(false, request.EntityId, default, Direction.None, MoveErrorCode.InvalidDirection, "missing target", false, default, request.ClientTick);
            context.Reasons.Add("missing target");
            return;
        }

        context.Proposals.Add(CommitProposal.Create(request.Priority, request.ActionId, request.EntityId, request.RuntimeParams.ConfigId, request.Target.TargetCoord.Value, request.Target.Direction, request.RuntimeParams.PlayerId, request.RuntimeParams.AutoMoveIntervalTicks, context.ServerTick));
        context.ActionResults[request.ActionId] = new MoveResult(true, request.EntityId, request.Target.TargetCoord.Value, request.Target.Direction, MoveErrorCode.None, string.Empty, false, default, request.ClientTick);
    }
}

[ActionStrategy("remove", ActionPrimitive.Remove)]
public sealed class RemoveActionStrategy : IActionStrategy
{
    public ActionPrimitive Primitive => ActionPrimitive.Remove;

    public void Process(ActionStrategyContext context)
    {
        ActionRequest request = context.Request;
        if (!context.World.TryGetEntity(request.EntityId, out GameEntity entity))
        {
            context.ActionResults[request.ActionId] = new MoveResult(false, request.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "entity not found", false, default, request.ClientTick);
            context.Reasons.Add("entity not found");
            return;
        }

        GridCoord coord = context.World.TryGetComponent(entity, out PositionComponent position) ? position.Coord : default;
        context.Proposals.Add(CommitProposal.Delete(request.Priority, request.ActionId, request.EntityId, context.ServerTick));
        context.ActionResults[request.ActionId] = new MoveResult(true, request.EntityId, coord, Direction.None, MoveErrorCode.None, string.Empty, false, default, request.ClientTick);
    }
}

public sealed class ActionStrategyRegistry
{
    private readonly Dictionary<ActionPrimitive, IActionStrategy> strategies = new();

    public static ActionStrategyRegistry Default => GeneratedActionStrategyRegistration.CreateDefault();

    public void Register(IActionStrategy strategy)
    {
        if (strategy == null)
        {
            throw new ArgumentNullException(nameof(strategy));
        }

        strategies[strategy.Primitive] = strategy;
    }

    public IActionStrategy Get(ActionSpec spec)
    {
        if (!strategies.TryGetValue(spec.Primitive, out IActionStrategy strategy))
        {
            throw new InvalidOperationException("No action strategy registered for primitive: " + spec.Primitive);
        }

        return strategy;
    }
}
}
