using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class ConflictResolver
{
    public IReadOnlyList<CommitProposalResult> Resolve(GameWorld world, IReadOnlyList<MovePlan> plans)
    {
        var results = new List<CommitProposalResult>();
        var movedEntities = new HashSet<long>();
        var occupiedTargets = new HashSet<GridCoord>();
        var acceptedKeys = new HashSet<string>();
        IReadOnlyList<MovePlan> ordered = plans
            .OrderBy(plan => plan.Priority)
            .ThenBy(plan => plan.SourceActionId)
            .ThenBy(plan => plan.SourceStateId)
            .ThenBy(plan => plan.BodyId)
            .ToArray();

        for (int i = 0; i < ordered.Count; i++)
        {
            MovePlan plan = ordered[i];
            string key = BuildPlanKey(plan);
            if (acceptedKeys.Contains(key))
            {
                AddPlanResults(results, plan, true, string.Empty);
                continue;
            }

            PlanResult validation = ValidatePlan(world, plan, movedEntities, occupiedTargets);
            if (!validation.Accepted)
            {
                AddPlanResults(results, plan, false, validation.Message);
                continue;
            }

            for (int memberIndex = 0; memberIndex < plan.Members.Count; memberIndex++)
            {
                BodyMember member = plan.Members[memberIndex];
                if (!world.TryGetEntity(member.EntityId, out GameEntity entity))
                {
                    continue;
                }

                movedEntities.Add(member.EntityId);
                if (world.HasComponent<BlockingComponent>(entity))
                {
                    occupiedTargets.Add(member.To);
                }

                world.MoveEntity(entity, member.To);
            }

            acceptedKeys.Add(key);
            AddPlanResults(results, plan, true, string.Empty);
        }

        return results;
    }

    private static PlanResult ValidatePlan(GameWorld world, MovePlan plan, HashSet<long> movedEntities, HashSet<GridCoord> occupiedTargets)
    {
        var bodyIds = new HashSet<long>(plan.Members.Select(member => member.EntityId));
        for (int i = 0; i < plan.Members.Count; i++)
        {
            BodyMember member = plan.Members[i];
            if (!world.TryGetEntity(member.EntityId, out GameEntity entity))
            {
                return PlanResult.Failed(PlanFailureReason.UnknownEntity, "entity not found");
            }

            if (!world.TryGetComponent(entity, out PositionComponent position))
            {
                return PlanResult.Failed(PlanFailureReason.MissingPosition, "missing position");
            }

            if (position.Coord != member.From)
            {
                return PlanResult.Failed(PlanFailureReason.SourceChanged, "source changed");
            }

            if (movedEntities.Contains(member.EntityId))
            {
                return PlanResult.Failed(PlanFailureReason.EntityAlreadyMoved, "entity already moved");
            }

            if (occupiedTargets.Contains(member.To))
            {
                return PlanResult.Failed(PlanFailureReason.TargetReserved, "target reserved");
            }

            GameEntity blocking = FindExternalBlocking(world, member.To, bodyIds);
            if (blocking != null)
            {
                bool player = world.HasComponent<PlayerControlComponent>(blocking);
                return PlanResult.Failed(player ? PlanFailureReason.OccupiedByPlayer : PlanFailureReason.BlockedCell, player ? "occupied by player" : "blocked cell");
            }
        }

        return PlanResult.AcceptedResult;
    }

    private static void AddPlanResults(List<CommitProposalResult> results, MovePlan plan, bool accepted, string reason)
    {
        for (int i = 0; i < plan.Members.Count; i++)
        {
            BodyMember member = plan.Members[i];
            CommitProposal proposal = CommitProposal.Move(plan.Priority, plan.SourceActionId, plan.SourceStateId, member.EntityId, member.From, member.To, plan.ServerTick);
            results.Add(new CommitProposalResult(proposal, accepted, reason));
        }
    }

    private static string BuildPlanKey(MovePlan plan)
    {
        return string.Join("|", plan.Members.OrderBy(member => member.EntityId).Select(member => $"{member.EntityId}:{member.From.X},{member.From.Y}>{member.To.X},{member.To.Y}"));
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
}
