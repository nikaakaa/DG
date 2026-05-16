using System.Collections.Generic;

namespace DG.GameCore
{
internal static class CommitHandlerOperations
{
    public static CommitProposalResult ApplySetDirection(GameWorld world, CommitProposal proposal)
    {
        if (!world.TryGetEntity(proposal.EntityId, out GameEntity entity))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        world.SetDirection(entity, proposal.Direction);
        return new CommitProposalResult(proposal, true, string.Empty);
    }

    public static CommitProposalResult ApplyCreateEntity(GameWorld world, CommitProposal proposal)
    {
        if (proposal.ConfigId <= 0)
        {
            return new CommitProposalResult(proposal, false, "invalid config id");
        }

        if (world.TryGetEntity(proposal.EntityId, out _))
        {
            return new CommitProposalResult(proposal, false, "entity already exists");
        }

        var spawn = new EntitySpawnSpec(proposal.EntityId, proposal.ConfigId, proposal.To, proposal.Direction, proposal.PlayerId, proposal.AutoMoveIntervalTicks <= 0 ? 1 : proposal.AutoMoveIntervalTicks, proposal.RotatePivot);
        return world.AddEntity(spawn)
            ? new CommitProposalResult(proposal, true, string.Empty)
            : new CommitProposalResult(proposal, false, "spawn failed");
    }

    public static CommitProposalResult ApplyDeleteEntity(GameWorld world, CommitProposal proposal)
    {
        return world.RemoveEntity(proposal.EntityId)
            ? new CommitProposalResult(proposal, true, string.Empty)
            : new CommitProposalResult(proposal, false, "entity not found");
    }

    public static CommitProposalResult ApplySetAutoMoveTick(GameWorld world, CommitProposal proposal)
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

    public static CommitProposalResult ApplyAddRuntimeEffect(GameWorld world, CommitProposal proposal)
    {
        if (!world.TryGetEntity(proposal.EntityId, out _))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        RuntimeEffectInstance instance = world.AddRuntimeEffectSource(proposal.EffectApplication.ToRuntimeSpec());
        world.ResolveComponentResults();
        return new CommitProposalResult(proposal, true, "effect added:" + instance.Id.Value);
    }

    public static CommitProposalResult ApplyRemoveRuntimeEffect(GameWorld world, CommitProposal proposal)
    {
        if (!proposal.RuntimeEffectId.IsValid)
        {
            return new CommitProposalResult(proposal, false, "runtime effect id required");
        }

        bool removed = world.RemoveRuntimeEffectSource(proposal.RuntimeEffectId);
        if (removed)
        {
            world.ResolveComponentResults();
        }

        return removed
            ? new CommitProposalResult(proposal, true, "effect removed:" + proposal.RuntimeEffectId.Value)
            : new CommitProposalResult(proposal, false, "runtime effect not found");
    }

    public static CommitProposalResult ApplySetComponentResult(GameWorld world, CommitProposal proposal)
    {
        if (!world.TryGetEntity(proposal.EntityId, out _))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        world.AddRuntimeComponentSource(proposal.ComponentContribution);
        world.ResolveComponentResults();
        return new CommitProposalResult(proposal, true, string.Empty);
    }

    public static CommitProposalResult ApplyAddTag(GameWorld world, CommitProposal proposal)
    {
        if (!world.TryGetEntity(proposal.EntityId, out _))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        world.AddRuntimeTagSource(proposal.EntityId, ComponentSourceKey.Commit(proposal.SourceActionId, proposal.EntityId), proposal.Tag);
        world.ResolveComponentResults();
        return new CommitProposalResult(proposal, true, string.Empty);
    }

    public static CommitProposalResult ApplyRemoveTag(GameWorld world, CommitProposal proposal)
    {
        if (!world.TryGetEntity(proposal.EntityId, out _))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        bool removed = world.RemoveRuntimeTagSource(proposal.EntityId, ComponentSourceKey.Commit(proposal.SourceActionId, proposal.EntityId), proposal.Tag);
        world.ResolveComponentResults();
        return new CommitProposalResult(proposal, removed, removed ? string.Empty : "tag not found");
    }

    public static CommitProposalResult ApplyClearRuntimeSources(GameWorld world, CommitProposal proposal)
    {
        if (!world.TryGetEntity(proposal.EntityId, out _))
        {
            return new CommitProposalResult(proposal, false, "entity not found");
        }

        bool removed = world.RemoveEntityRuntimeSources(proposal.EntityId);
        return new CommitProposalResult(proposal, removed, removed ? string.Empty : "runtime sources not found");
    }

    public static CommitProposalResult ApplyMove(GameWorld world, CommitProposal proposal, CommitResolveContext context)
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

        if (context.MovedEntities.Contains(proposal.EntityId))
        {
            return new CommitProposalResult(proposal, false, "entity already moved");
        }

        if (context.OccupiedTargets.Contains(proposal.To))
        {
            return new CommitProposalResult(proposal, false, "target reserved");
        }

        var excluded = new HashSet<long> { proposal.EntityId };
        if (world.TryGetFirstBlockingAt(proposal.To, excluded, out BlockingSpatialQueryResult target))
        {
            if (proposal.SourceStateId != 0 &&
                context.MoveGroups.TryGetValue(proposal.SourceStateId, out HashSet<long> groupIds) &&
                groupIds.Contains(target.EntityId))
            {
                return new CommitProposalResult(proposal, true, string.Empty);
            }

            if (!world.TryGetEntity(target.EntityId, out GameEntity targetEntity))
            {
                return new CommitProposalResult(proposal, true, string.Empty);
            }

            return new CommitProposalResult(proposal, false, world.HasComponent<PlayerControlComponent>(targetEntity) ? "occupied by player" : "blocked cell");
        }

        context.MovedEntities.Add(proposal.EntityId);
        if (world.HasComponent<BlockingComponent>(entity))
        {
            context.OccupiedTargets.Add(proposal.To);
        }

        world.MoveEntity(entity, proposal.To);
        return new CommitProposalResult(proposal, true, string.Empty);
    }
}
}
