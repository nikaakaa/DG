using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum CommitProposalKind
{
    MoveEntity = 1,
    SetDirection = 2,
    CreateEntity = 3,
    DeleteEntity = 4,
    SetAutoMoveTick = 5
}

public readonly struct CommitProposal
{
    public CommitProposal(CommitProposalKind kind, WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, GridCoord from, GridCoord to, Direction direction, long serverTick, int configId, long playerId, int autoMoveIntervalTicks)
    {
        Kind = kind;
        Priority = priority;
        SourceActionId = sourceActionId;
        SourceStateId = sourceStateId;
        EntityId = entityId;
        From = from;
        To = to;
        Direction = direction;
        ServerTick = serverTick;
        ConfigId = configId;
        PlayerId = playerId;
        AutoMoveIntervalTicks = autoMoveIntervalTicks;
    }

    public CommitProposalKind Kind { get; }
    public WorldActionPriority Priority { get; }
    public long SourceActionId { get; }
    public long SourceStateId { get; }
    public long EntityId { get; }
    public GridCoord From { get; }
    public GridCoord To { get; }
    public Direction Direction { get; }
    public long ServerTick { get; }
    public int ConfigId { get; }
    public long PlayerId { get; }
    public int AutoMoveIntervalTicks { get; }

    public static CommitProposal Move(WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, GridCoord from, GridCoord to, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.MoveEntity, priority, sourceActionId, sourceStateId, entityId, from, to, Direction.None, serverTick, 0, 0, 0);
    }

    public static CommitProposal SetDirection(WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, Direction direction, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.SetDirection, priority, sourceActionId, sourceStateId, entityId, default, default, direction, serverTick, 0, 0, 0);
    }

    public static CommitProposal Create(WorldActionPriority priority, long sourceActionId, long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.CreateEntity, priority, sourceActionId, 0, entityId, default, coord, direction, serverTick, configId, playerId, autoMoveIntervalTicks);
    }

    public static CommitProposal Delete(WorldActionPriority priority, long sourceActionId, long entityId, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.DeleteEntity, priority, sourceActionId, 0, entityId, default, default, Direction.None, serverTick, 0, 0, 0);
    }

    public static CommitProposal SetAutoMoveTick(WorldActionPriority priority, long sourceActionId, long entityId, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.SetAutoMoveTick, priority, sourceActionId, 0, entityId, default, default, Direction.None, serverTick, 0, 0, 0);
    }
}

public readonly struct CommitProposalResult
{
    public CommitProposalResult(CommitProposal proposal, bool accepted, string reason)
    {
        Proposal = proposal;
        Accepted = accepted;
        Reason = reason;
    }

    public CommitProposal Proposal { get; }
    public bool Accepted { get; }
    public string Reason { get; }
}

public sealed class CommitResolver
{
    public IReadOnlyList<CommitProposalResult> Resolve(GameWorld world, IReadOnlyList<CommitProposal> proposals)
    {
        var results = new List<CommitProposalResult>();
        var movedEntities = new HashSet<long>();
        var occupiedTargets = new HashSet<GridCoord>();
        IReadOnlyDictionary<long, HashSet<long>> moveGroups = BuildMoveGroups(proposals);
        IReadOnlyList<CommitProposal> ordered = proposals
            .OrderBy(proposal => proposal.Priority)
            .ThenBy(proposal => proposal.SourceActionId)
            .ThenBy(proposal => proposal.SourceStateId)
            .ThenBy(proposal => proposal.EntityId)
            .ToArray();

        for (int i = 0; i < ordered.Count; i++)
        {
            CommitProposal proposal = ordered[i];
            if (proposal.Kind == CommitProposalKind.SetDirection)
            {
                results.Add(ApplySetDirection(world, proposal));
                continue;
            }

            if (proposal.Kind == CommitProposalKind.CreateEntity)
            {
                results.Add(ApplyCreateEntity(world, proposal));
                continue;
            }

            if (proposal.Kind == CommitProposalKind.DeleteEntity)
            {
                results.Add(ApplyDeleteEntity(world, proposal));
                continue;
            }

            if (proposal.Kind == CommitProposalKind.SetAutoMoveTick)
            {
                results.Add(ApplySetAutoMoveTick(world, proposal));
                continue;
            }

            CommitProposalResult result = ValidateMove(world, proposal, movedEntities, occupiedTargets, moveGroups);
            results.Add(result);
            if (!result.Accepted)
            {
                continue;
            }

            movedEntities.Add(proposal.EntityId);
            if (world.TryGetEntity(proposal.EntityId, out GameEntity moved) &&
                world.HasComponent<BlockingComponent>(moved))
            {
                occupiedTargets.Add(proposal.To);
            }

            world.MoveEntity(moved, proposal.To);
        }

        return results;
    }

    private static IReadOnlyDictionary<long, HashSet<long>> BuildMoveGroups(IReadOnlyList<CommitProposal> proposals)
    {
        return proposals
            .Where(proposal => proposal.Kind == CommitProposalKind.MoveEntity && proposal.SourceStateId != 0)
            .GroupBy(proposal => proposal.SourceStateId)
            .ToDictionary(group => group.Key, group => new HashSet<long>(group.Select(proposal => proposal.EntityId)));
    }

    private static CommitProposalResult ApplySetDirection(GameWorld world, CommitProposal proposal)
    {
        if (!world.TryGetEntity(proposal.EntityId, out GameEntity entity))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        world.SetDirection(entity, proposal.Direction);
        return new CommitProposalResult(proposal, true, string.Empty);
    }

    private static CommitProposalResult ApplyCreateEntity(GameWorld world, CommitProposal proposal)
    {
        if (proposal.ConfigId <= 0)
        {
            return new CommitProposalResult(proposal, false, "invalid config id");
        }

        if (world.TryGetEntity(proposal.EntityId, out _))
        {
            return new CommitProposalResult(proposal, false, "entity already exists");
        }

        var spawn = new EntitySpawnSpec(proposal.EntityId, proposal.ConfigId, proposal.To, proposal.Direction, proposal.PlayerId, proposal.AutoMoveIntervalTicks <= 0 ? 1 : proposal.AutoMoveIntervalTicks);
        return world.AddEntity(spawn)
            ? new CommitProposalResult(proposal, true, string.Empty)
            : new CommitProposalResult(proposal, false, "spawn failed");
    }

    private static CommitProposalResult ApplyDeleteEntity(GameWorld world, CommitProposal proposal)
    {
        return world.RemoveEntity(proposal.EntityId)
            ? new CommitProposalResult(proposal, true, string.Empty)
            : new CommitProposalResult(proposal, false, "entity not found");
    }

    private static CommitProposalResult ApplySetAutoMoveTick(GameWorld world, CommitProposal proposal)
    {
        if (!world.TryGetEntity(proposal.EntityId, out GameEntity entity))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        if (!world.TryGetComponent(entity, out AutoMoveComponent autoMove))
        {
            return new CommitProposalResult(proposal, false, "missing auto move");
        }

        autoMove.LastMoveTick = proposal.ServerTick;
        world.SetAutoMove(entity, autoMove);
        return new CommitProposalResult(proposal, true, string.Empty);
    }

    private static CommitProposalResult ValidateMove(GameWorld world, CommitProposal proposal, HashSet<long> movedEntities, HashSet<GridCoord> occupiedTargets, IReadOnlyDictionary<long, HashSet<long>> moveGroups)
    {
        if (!world.TryGetEntity(proposal.EntityId, out GameEntity entity))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        if (!world.TryGetComponent(entity, out PositionComponent position))
        {
            return new CommitProposalResult(proposal, false, "missing position");
        }

        if (position.Coord != proposal.From)
        {
            return new CommitProposalResult(proposal, false, "source changed");
        }

        if (movedEntities.Contains(proposal.EntityId))
        {
            return new CommitProposalResult(proposal, false, "entity already moved");
        }

        if (occupiedTargets.Contains(proposal.To))
        {
            return new CommitProposalResult(proposal, false, "target reserved");
        }

        IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(proposal.To);
        for (int i = 0; i < targets.Count; i++)
        {
            GameEntity target = targets[i];
            if (target.EntityId == proposal.EntityId || !world.HasComponent<BlockingComponent>(target))
            {
                continue;
            }

            if (proposal.SourceStateId != 0 &&
                moveGroups.TryGetValue(proposal.SourceStateId, out HashSet<long> groupIds) &&
                groupIds.Contains(target.EntityId))
            {
                continue;
            }

            return new CommitProposalResult(proposal, false, world.HasComponent<PlayerControlComponent>(target) ? "occupied by player" : "blocked cell");
        }

        return new CommitProposalResult(proposal, true, string.Empty);
    }
}
}
