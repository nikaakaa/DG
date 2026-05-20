using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class ConflictResolver
{
    public IReadOnlyList<CommitProposalResult> Resolve(GameWorld world, IReadOnlyList<MovePlan> plans)
    {
        return Resolve(world, plans, null);
    }

    public IReadOnlyList<CommitProposalResult> Resolve(GameWorld world, IReadOnlyList<MovePlan> plans, BehaviorInstanceRunner? runningBehaviors)
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

            PlanResult validation = ValidatePlan(world, plan, movedEntities, occupiedTargets, runningBehaviors);
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

                bool moved = member.From != member.To;
                if (moved)
                {
                    movedEntities.Add(member.EntityId);
                }

                if (moved && world.HasComponent<BlockingComponent>(entity))
                {
                    occupiedTargets.Add(member.To);
                }

                if (moved)
                {
                    world.MoveEntity(entity, member.To);
                }

                if (member.Direction != Direction.None)
                {
                    world.SetDirection(entity, member.Direction);
                }
            }

            acceptedKeys.Add(key);
            AddPlanResults(results, plan, true, string.Empty);
        }

        return results;
    }

    private static PlanResult ValidatePlan(GameWorld world, MovePlan plan, HashSet<long> movedEntities, HashSet<GridCoord> occupiedTargets, BehaviorInstanceRunner? runningBehaviors)
    {
        var bodyIds = new HashSet<long>(plan.Members.Select(member => member.EntityId));
        var occupancyExcludedIds = new HashSet<long>(bodyIds);
        occupancyExcludedIds.UnionWith(movedEntities);
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

            if (TryFindExternalBlocking(world, member.To, occupancyExcludedIds, false, out _))
            {
                return PlanResult.Failed(PlanFailureReason.BlockedCell, "blocked cell");
            }
        }

        for (int i = 0; i < plan.Members.Count; i++)
        {
            BodyMember member = plan.Members[i];
            if (TryFindExternalBlocking(world, member.To, occupancyExcludedIds, true, out _))
            {
                return PlanResult.Failed(PlanFailureReason.OccupiedByPlayer, "occupied by player");
            }
        }

        for (int i = 0; i < plan.Members.Count; i++)
        {
            BodyMember member = plan.Members[i];
            if (movedEntities.Contains(member.EntityId))
            {
                return PlanResult.Failed(PlanFailureReason.EntityAlreadyMoved, "entity already moved");
            }

            if (occupiedTargets.Contains(member.To))
            {
                return PlanResult.Failed(PlanFailureReason.TargetReserved, "target reserved");
            }
        }

        if (runningBehaviors != null)
        {
            for (int i = 0; i < plan.Members.Count; i++)
            {
                BodyMember member = plan.Members[i];
                if (runningBehaviors.TryGetRunningSubject(member.EntityId, plan.ServerTick, plan.SourceActionId, out ActionBehaviorInstance subjectReserved))
                {
                    return PlanResult.Failed(PlanFailureReason.TargetReserved, subjectReserved.Reservation.IncomingPolicy == BehaviorIncomingPolicy.RejectIncoming ? "running subject in flight" : "entity reserved");
                }
            }

            for (int i = 0; i < plan.Members.Count; i++)
            {
                BodyMember member = plan.Members[i];
                if (runningBehaviors.TryGetReservedCell(member.To, plan.ServerTick, plan.SourceActionId, out ActionBehaviorInstance cellReserved))
                {
                    return PlanResult.Failed(PlanFailureReason.TargetReserved, cellReserved.Reservation.IncomingPolicy == BehaviorIncomingPolicy.RejectIncoming ? "running reservation in flight" : "target reserved");
                }
            }

            for (int i = 0; i < plan.Resources.Count; i++)
            {
                if (runningBehaviors.TryGetReservedResource(plan.Resources[i], BehaviorClaimChannel.Movement, plan.ServerTick, out ActionBehaviorInstance resourceReserved) &&
                    resourceReserved.SourceActionId != plan.SourceActionId)
                {
                    return PlanResult.Failed(PlanFailureReason.TargetReserved, resourceReserved.Reservation.IncomingPolicy == BehaviorIncomingPolicy.RejectIncoming ? "running resource in flight" : "resource reserved");
                }
            }
        }

        return PlanResult.AcceptedResult;
    }

    private static void AddPlanResults(List<CommitProposalResult> results, MovePlan plan, bool accepted, string reason)
    {
        for (int i = 0; i < plan.Members.Count; i++)
        {
            BodyMember member = plan.Members[i];
            CommitProposal proposal = CommitProposal.Move(plan.Priority, plan.SourceActionId, plan.SourceStateId, member.EntityId, member.From, member.To, plan.ServerTick, plan.PresentationHint);
            results.Add(new CommitProposalResult(proposal, accepted, reason));
        }
    }

    private static string BuildPlanKey(MovePlan plan)
    {
        return string.Join("|", plan.Members.OrderBy(member => member.EntityId).Select(member => $"{member.EntityId}:{member.From.X},{member.From.Y}>{member.To.X},{member.To.Y}"));
    }

    private static bool TryFindExternalBlocking(GameWorld world, GridCoord coord, HashSet<long> bodyIds, bool player, out GameEntity blocking)
    {
        IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(coord);
        for (int i = 0; i < targets.Count; i++)
        {
            GameEntity target = targets[i];
            if (bodyIds.Contains(target.EntityId) ||
                !world.HasComponent<BlockingComponent>(target) ||
                world.HasComponent<PlayerControlComponent>(target) != player)
            {
                continue;
            }

            blocking = target;
            return true;
        }

        blocking = null!;
        return false;
    }
}
}
