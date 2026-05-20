using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum BehaviorBodyKind
{
    SingleEntity = 1,
    PortConnected = 2
}

public readonly struct BodyMember
{
    public BodyMember(long entityId, GridCoord from, GridCoord to, Direction direction = Direction.None)
    {
        EntityId = entityId;
        From = from;
        To = to;
        Direction = direction;
    }

    public long EntityId { get; }
    public GridCoord From { get; }
    public GridCoord To { get; }
    public Direction Direction { get; }
}

public sealed class BehaviorBody
{
    public BehaviorBody(long bodyId, BehaviorBodyKind kind, IReadOnlyList<GameEntity> entities)
    {
        BodyId = bodyId;
        Kind = kind;
        Entities = entities;
    }

    public long BodyId { get; }
    public BehaviorBodyKind Kind { get; }
    public IReadOnlyList<GameEntity> Entities { get; }
    public long RootEntityId => Entities.Count == 0 ? 0 : Entities[0].EntityId;
}

public enum BehaviorPlanKind
{
    Move = 1,
    StateChange = 2,
    Spawn = 3,
    Remove = 4,
    Rotate = 5,
    Teleport = 6
}

public sealed class MovePlan
{
    public MovePlan(WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, long serverTick, long bodyId, BehaviorBodyKind bodyKind, Direction direction, IReadOnlyList<BodyMember> members)
        : this(priority, sourceActionId, sourceStateId, entityId, serverTick, bodyId, bodyKind, direction, members, System.Array.Empty<ResourceKey>(), PresentationFactType.Unknown)
    {
    }

    public MovePlan(WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, long serverTick, long bodyId, BehaviorBodyKind bodyKind, Direction direction, IReadOnlyList<BodyMember> members, IReadOnlyList<ResourceKey> resources)
        : this(priority, sourceActionId, sourceStateId, entityId, serverTick, bodyId, bodyKind, direction, members, resources, PresentationFactType.Unknown)
    {
    }

    public MovePlan(WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, long serverTick, long bodyId, BehaviorBodyKind bodyKind, Direction direction, IReadOnlyList<BodyMember> members, IReadOnlyList<ResourceKey> resources, PresentationFactType presentationHint)
    {
        Priority = priority;
        SourceActionId = sourceActionId;
        SourceStateId = sourceStateId;
        EntityId = entityId;
        ServerTick = serverTick;
        BodyId = bodyId;
        BodyKind = bodyKind;
        Direction = direction;
        Members = members;
        Resources = resources ?? System.Array.Empty<ResourceKey>();
        PresentationHint = presentationHint;
    }

    public WorldActionPriority Priority { get; }
    public long SourceActionId { get; }
    public long SourceStateId { get; }
    public long EntityId { get; }
    public long ServerTick { get; }
    public long BodyId { get; }
    public BehaviorBodyKind BodyKind { get; }
    public Direction Direction { get; }
    public IReadOnlyList<BodyMember> Members { get; }
    public IReadOnlyList<ResourceKey> Resources { get; }
    public PresentationFactType PresentationHint { get; }
    public BehaviorPlanKind Kind => BehaviorPlanKind.Move;
}

public enum PlanFailureReason
{
    None = 0,
    UnknownEntity = 1,
    MissingPosition = 2,
    InvalidDirection = 3,
    SourceChanged = 4,
    EntityAlreadyMoved = 5,
    TargetReserved = 6,
    BlockedCell = 7,
    OccupiedByPlayer = 8,
    ConflictingBodyIntents = 9
}

public readonly struct PlanResult
{
    public PlanResult(bool accepted, PlanFailureReason reason, string message)
    {
        Accepted = accepted;
        Reason = reason;
        Message = message;
    }

    public bool Accepted { get; }
    public PlanFailureReason Reason { get; }
    public string Message { get; }

    public static PlanResult AcceptedResult => new(true, PlanFailureReason.None, string.Empty);

    public static PlanResult Failed(PlanFailureReason reason, string message)
    {
        return new PlanResult(false, reason, message);
    }
}

public sealed class BodyResolver
{
    public bool TryResolve(GameWorld world, GameEntity root, out BehaviorBody body, out string reason)
    {
        IReadOnlyList<GameEntity> group = PortConnectionSystem.CollectConnectedGroup(world, root);
        BehaviorBodyKind kind = group.Count > 1 ? BehaviorBodyKind.PortConnected : BehaviorBodyKind.SingleEntity;
        long bodyId = BuildBodyId(group);
        body = new BehaviorBody(bodyId, kind, group);
        reason = string.Empty;
        return true;
    }

    public static long BuildBodyId(IReadOnlyList<GameEntity> entities)
    {
        long bodyId = 17;
        for (int i = 0; i < entities.Count; i++)
        {
            unchecked
            {
                bodyId = bodyId * 31 + entities[i].EntityId;
            }
        }

        return bodyId;
    }
}

public sealed class OccupancyResolver
{
    public bool TryCreateMovePlan(GameWorld world, ActionRequest request, Direction direction, GridCoord? targetCoord, BehaviorBody body, long serverTick, out MovePlan plan, out PlanResult result)
        => TryCreateMovePlan(world, request, direction, targetCoord, body, serverTick, PresentationFactType.Unknown, out plan, out result);

    public bool TryCreateMovePlan(GameWorld world, ActionRequest request, Direction direction, GridCoord? targetCoord, BehaviorBody body, long serverTick, PresentationFactType presentationHint, out MovePlan plan, out PlanResult result)
    {
        if (direction == Direction.None && !targetCoord.HasValue)
        {
            plan = null!;
            result = PlanResult.Failed(PlanFailureReason.InvalidDirection, "invalid direction");
            return false;
        }

        var members = new List<BodyMember>();
        var bodyIds = new HashSet<long>(body.Entities.Select(entity => entity.EntityId));
        GridCoord? rootFrom = null;
        GridCoord? rootTo = null;
        for (int i = 0; i < body.Entities.Count; i++)
        {
            GameEntity entity = body.Entities[i];
            if (!world.TryGetComponent(entity, out PositionComponent position))
            {
                plan = null!;
                result = PlanResult.Failed(PlanFailureReason.MissingPosition, "missing position");
                return false;
            }

            if (entity.EntityId == request.EntityId)
            {
                rootFrom = position.Coord;
                rootTo = targetCoord;
            }

            members.Add(new BodyMember(entity.EntityId, position.Coord, position.Coord));
        }

        int deltaX = 0;
        int deltaY = 0;
        if (targetCoord.HasValue)
        {
            if (!rootFrom.HasValue)
            {
                plan = null!;
                result = PlanResult.Failed(PlanFailureReason.UnknownEntity, "entity not found");
                return false;
            }

            GridCoord from = rootFrom.Value;
            GridCoord to = rootTo!.Value;
            deltaX = to.X - from.X;
            deltaY = to.Y - from.Y;
        }

        var resolvedMembers = new List<BodyMember>();
        for (int i = 0; i < members.Count; i++)
        {
            BodyMember member = members[i];
            GridCoord to = targetCoord.HasValue ? new GridCoord(member.From.X + deltaX, member.From.Y + deltaY) : member.From.Add(direction);
            resolvedMembers.Add(new BodyMember(member.EntityId, member.From, to));
        }

        for (int i = 0; i < resolvedMembers.Count; i++)
        {
            BodyMember member = resolvedMembers[i];
            GameEntity blocking = FindExternalBlocking(world, member.To, bodyIds);
            if (blocking == null)
            {
                continue;
            }

            bool player = world.HasComponent<PlayerControlComponent>(blocking);
            plan = null!;
            result = PlanResult.Failed(player ? PlanFailureReason.OccupiedByPlayer : PlanFailureReason.BlockedCell, player ? "occupied by player" : "blocked cell");
            return false;
        }

        plan = new MovePlan(request.Priority, request.ActionId, request.Source.SourceStateId, request.EntityId, serverTick, body.BodyId, body.Kind, direction, resolvedMembers, System.Array.Empty<ResourceKey>(), presentationHint);
        result = PlanResult.AcceptedResult;
        return true;
    }

    private static GameEntity FindExternalBlocking(GameWorld world, GridCoord coord, HashSet<long> bodyIds)
    {
        return world.TryGetFirstBlockingAt(coord, bodyIds, out BlockingSpatialQueryResult blocking) &&
            world.TryGetEntity(blocking.EntityId, out GameEntity target) ? target : null!;
    }
}

public sealed class RulePlanner
{
    private readonly BodyResolver bodyResolver;
    private readonly OccupancyResolver occupancyResolver;
    private readonly ActionSpecRegistry actionSpecs;
    private readonly ActionPresentationRegistry presentationRegistry;

    public RulePlanner() : this(new BodyResolver(), new OccupancyResolver())
    {
    }

    public RulePlanner(ActionSpecRegistry actionSpecs) : this(new BodyResolver(), new OccupancyResolver(), actionSpecs)
    {
    }

    public RulePlanner(ActionSpecRegistry actionSpecs, ActionPresentationRegistry presentationRegistry) : this(new BodyResolver(), new OccupancyResolver(), actionSpecs, presentationRegistry)
    {
    }

    public RulePlanner(BodyResolver bodyResolver, OccupancyResolver occupancyResolver)
        : this(bodyResolver, occupancyResolver, ActionSpecRegistry.Default)
    {
    }

    public RulePlanner(BodyResolver bodyResolver, OccupancyResolver occupancyResolver, ActionSpecRegistry actionSpecs)
        : this(bodyResolver, occupancyResolver, actionSpecs, ActionPresentationRegistry.Default)
    {
    }

    public RulePlanner(BodyResolver bodyResolver, OccupancyResolver occupancyResolver, ActionSpecRegistry actionSpecs, ActionPresentationRegistry presentationRegistry)
    {
        this.bodyResolver = bodyResolver;
        this.occupancyResolver = occupancyResolver;
        this.actionSpecs = actionSpecs;
        this.presentationRegistry = presentationRegistry ?? new ActionPresentationRegistry(System.Array.Empty<ActionPresentationConfig>());
    }

    public bool TryPlanMove(GameWorld world, ActionRequest request, Direction direction, GridCoord? targetCoord, long serverTick, out MovePlan plan, out PlanResult result)
    {
        ActionSpec spec;
        try
        {
            spec = actionSpecs.Get(request.SpecId);
        }
        catch (System.ArgumentOutOfRangeException)
        {
            plan = null!;
            result = PlanResult.Failed(PlanFailureReason.UnknownEntity, "unknown action spec");
            return false;
        }

        if (!world.TryGetEntity(request.EntityId, out GameEntity entity))
        {
            plan = null!;
            result = PlanResult.Failed(PlanFailureReason.UnknownEntity, "entity not found");
            return false;
        }

        BehaviorBody body;
        if (spec.AllowsConnectedBodySubject)
        {
            if (!bodyResolver.TryResolve(world, entity, out body, out string reason))
            {
                plan = null!;
                result = PlanResult.Failed(PlanFailureReason.UnknownEntity, reason);
                return false;
            }
        }
        else
        {
            body = new BehaviorBody(entity.EntityId, BehaviorBodyKind.SingleEntity, new[] { entity });
        }

        PresentationFactType hint = MovePresentationResolver.ResolveForMove(request, spec, presentationRegistry);
        return occupancyResolver.TryCreateMovePlan(world, request, direction, targetCoord, body, serverTick, hint, out plan, out result);
    }

    public bool TryPlanMove(GameWorld world, AcceptedAction action, out MovePlan plan, out PlanResult result)
    {
        if (action.Direction == Direction.None && !action.TargetCoord.HasValue)
        {
            plan = null!;
            result = PlanResult.Failed(PlanFailureReason.InvalidDirection, "invalid direction");
            return false;
        }

        var members = new List<BodyMember>();
        for (int i = 0; i < action.Claims.Count; i++)
        {
            ActionClaim claim = action.Claims[i];
            if (claim.Kind != ActionClaimKind.BodyMove)
            {
                continue;
            }

            members.Add(new BodyMember(claim.EntityId, claim.FromCoord, claim.ToCoord));
        }

        if (members.Count == 0)
        {
            plan = null!;
            result = PlanResult.Failed(PlanFailureReason.InvalidDirection, "missing move claims");
            return false;
        }

        PresentationFactType hint = MovePresentationResolver.ResolveForMove(action.Request, action.Spec, presentationRegistry);
        plan = new MovePlan(action.Request.Priority, action.Request.ActionId, action.Request.Source.SourceStateId, action.Request.EntityId, action.ServerTick, action.Body.BodyId, action.Body.Kind, action.Direction, members, System.Array.Empty<ResourceKey>(), hint);
        result = PlanResult.AcceptedResult;
        return true;
    }
}
}
