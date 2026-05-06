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
    public BodyMember(long entityId, GridCoord from, GridCoord to)
    {
        EntityId = entityId;
        From = from;
        To = to;
    }

    public long EntityId { get; }
    public GridCoord From { get; }
    public GridCoord To { get; }
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
    public MovePlan(BehaviorIntent intent, long bodyId, BehaviorBodyKind bodyKind, Direction direction, IReadOnlyList<BodyMember> members)
    {
        Intent = intent;
        BodyId = bodyId;
        BodyKind = bodyKind;
        Direction = direction;
        Members = members;
    }

    public BehaviorIntent Intent { get; }
    public long BodyId { get; }
    public BehaviorBodyKind BodyKind { get; }
    public Direction Direction { get; }
    public IReadOnlyList<BodyMember> Members { get; }
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

    private static long BuildBodyId(IReadOnlyList<GameEntity> entities)
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
    public bool TryCreateMovePlan(GameWorld world, BehaviorIntent intent, BehaviorBody body, out MovePlan plan, out PlanResult result)
    {
        if (intent.Direction == Direction.None && !intent.TargetCoord.HasValue)
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

            if (entity.EntityId == intent.EntityId)
            {
                rootFrom = position.Coord;
                rootTo = intent.TargetCoord;
            }

            members.Add(new BodyMember(entity.EntityId, position.Coord, position.Coord));
        }

        int deltaX = 0;
        int deltaY = 0;
        if (intent.TargetCoord.HasValue)
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
            GridCoord to = intent.TargetCoord.HasValue ? new GridCoord(member.From.X + deltaX, member.From.Y + deltaY) : member.From.Add(intent.Direction);
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

        plan = new MovePlan(intent, body.BodyId, body.Kind, intent.Direction, resolvedMembers);
        result = PlanResult.AcceptedResult;
        return true;
    }

    private static GameEntity FindExternalBlocking(GameWorld world, GridCoord coord, HashSet<long> bodyIds)
    {
        IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(coord);
        for (int i = 0; i < targets.Count; i++)
        {
            GameEntity target = targets[i];
            if (bodyIds.Contains(target.EntityId) || !world.HasComponent<BlockingComponent>(target))
            {
                continue;
            }

            return target;
        }

        return null!;
    }
}

public sealed class RulePlanner
{
    private readonly BodyResolver bodyResolver;
    private readonly OccupancyResolver occupancyResolver;

    public RulePlanner() : this(new BodyResolver(), new OccupancyResolver())
    {
    }

    public RulePlanner(BodyResolver bodyResolver, OccupancyResolver occupancyResolver)
    {
        this.bodyResolver = bodyResolver;
        this.occupancyResolver = occupancyResolver;
    }

    public bool TryPlanMove(GameWorld world, BehaviorIntent intent, out MovePlan plan, out PlanResult result)
    {
        if (!world.TryGetEntity(intent.EntityId, out GameEntity entity))
        {
            plan = null!;
            result = PlanResult.Failed(PlanFailureReason.UnknownEntity, "entity not found");
            return false;
        }

        if (!bodyResolver.TryResolve(world, entity, out BehaviorBody body, out string reason))
        {
            plan = null!;
            result = PlanResult.Failed(PlanFailureReason.UnknownEntity, reason);
            return false;
        }

        return occupancyResolver.TryCreateMovePlan(world, intent, body, out plan, out result);
    }
}
}
