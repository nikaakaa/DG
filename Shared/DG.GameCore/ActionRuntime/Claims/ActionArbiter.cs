using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class ActionArbiter
{
    private readonly BodyCapabilityResolver bodyCapabilities = new();
    private readonly ActionBlockedOutcomeExecutor blockedOutcomeExecutor = new();
    private readonly ActionTagGate tagGate = new();
    private readonly ActionSubjectSelector subjectSelector = new();
    private readonly TargetingSystem targetingSystem;
    private readonly ActionClaimBuilder claimBuilder = new();
    private readonly ActionSpecRegistry registry;

    public ActionArbiter() : this(ActionSpecRegistry.Default)
    {
    }

    public ActionArbiter(ActionSpecRegistry registry)
    {
        this.registry = registry;
        targetingSystem = new TargetingSystem(TargetSelectorRegistry.CreateDefault(), registry.TargetFilters);
    }

    public ActionArbitrationResult ArbitrateMoves(GameWorld world, IReadOnlyList<ActionRequest> requests, long serverTick)
    {
        var result = new ActionArbitrationResult();
        var candidates = new List<AcceptedAction>();
        for (int i = 0; i < requests.Count; i++)
        {
            ProcessMoveRequest(world, requests[i], result, candidates, serverTick);
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

        return claimBuilder.TryBuildMoveClaims(world, request, new BehaviorBody(request.EntityId, BehaviorBodyKind.SingleEntity, new[] { entity }), direction, targetCoord, out claims);
    }

    private void ProcessMoveRequest(GameWorld world, ActionRequest request, ActionArbitrationResult result, List<AcceptedAction> candidates, long serverTick)
    {
        ActionSpec spec = registry.Get(request.SpecId);
        if (!world.TryGetEntity(request.EntityId, out GameEntity entity))
        {
            Reject(world, request, spec, default, Direction.None, MoveErrorCode.UnknownEntity, UnknownEntityReason(spec), false, default, result);
            return;
        }

        if (!request.TryCreateContext(spec, out ActionContext context, out string contextReason))
        {
            Reject(world, request, spec, default, Direction.None, MoveErrorCode.InvalidDirection, contextReason, false, default, result);
            return;
        }

        if (!world.TryGetComponent(entity, out PositionComponent position))
        {
            Reject(world, request, spec, default, Direction.None, MoveErrorCode.MissingPosition, MissingPositionReason(spec), false, default, result);
            return;
        }

        if (!targetingSystem.TryResolveTargetData(world, context, spec, entity, position, out IReadOnlyList<ActionTargetData> targetData, out Direction direction, out GridCoord target, out MoveErrorCode errorCode, out string reason))
        {
            Reject(world, request, spec, position.Coord, Direction.None, errorCode, reason, false, default, result);
            return;
        }

        if (!subjectSelector.TryResolve(world, request, entity, spec, out BehaviorBody body, out string bodyReason))
        {
            Reject(world, request, spec, position.Coord, direction, MoveErrorCode.UnknownEntity, bodyReason, false, default, result);
            return;
        }

        if (!bodyCapabilities.CanMove(world, body))
        {
            Reject(world, request, spec, position.Coord, direction, MoveErrorCode.Blocked, "blocked by movement permission", false, default, result);
            return;
        }

        ActionGateResult gateResult = tagGate.Evaluate(world, spec, body);
        if (!gateResult.Accepted)
        {
            Reject(world, request, spec, position.Coord, direction, gateResult.ErrorCode, gateResult.Reason, false, default, result);
            return;
        }

        if ((spec.CommitRules & ActionCommitRule.SetAutoMoveTick) != 0)
        {
            result.AddProposal(CommitProposal.SetAutoMoveTick(request.Priority, request.ActionId, request.EntityId, serverTick));
        }

        if (!claimBuilder.TryBuildMoveExecutionOutput(world, context, body, direction, targetData, out ActionExecutionOutput output))
        {
            Reject(world, request, spec, position.Coord, direction, MoveErrorCode.MissingPosition, "missing position", false, default, result);
            return;
        }

        IReadOnlyList<ActionClaim> claims = output.Claims;
        IReadOnlyList<ExternalPushContact> contacts = bodyCapabilities.FindExternalPushContacts(world, claims, body);
        if (contacts.Count != 0)
        {
            ResolveBlocked(world, request, context, spec, body, result, position.Coord, direction, targetData, contacts, serverTick);
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

    private void ResolveBlocked(GameWorld world, ActionRequest request, ActionContext context, ActionSpec spec, BehaviorBody body, ActionArbitrationResult result, GridCoord current, Direction direction, IReadOnlyList<ActionTargetData> targetData, IReadOnlyList<ExternalPushContact> contacts, long serverTick)
    {
        BlockedResultPolicy policy = registry.GetBlockedResultPolicy(spec.BlockedResultPolicyId);
        blockedOutcomeExecutor.Resolve(policy, new BlockedOutcomeContext(world, request, context, spec, body, current, direction, targetData, contacts, serverTick), result);
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
