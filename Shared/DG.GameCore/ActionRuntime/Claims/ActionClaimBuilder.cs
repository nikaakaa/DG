using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class ActionExecutionOutput
{
    public ActionExecutionOutput(ActionContext context, ActionExecutionSuccessPolicy successPolicy, IReadOnlyList<ActionTargetData> targetData, IReadOnlyList<ActionClaim> claims, IReadOnlyList<CommitProposal> commitProposals, IReadOnlyList<DeferredAction> deferredActions, MoveResult? blockedResult = null)
        : this(context, successPolicy, targetData, claims, commitProposals, deferredActions, Array.Empty<EffectApplication>(), blockedResult)
    {
    }

    public ActionExecutionOutput(ActionContext context, ActionExecutionSuccessPolicy successPolicy, IReadOnlyList<ActionTargetData> targetData, IReadOnlyList<ActionClaim> claims, IReadOnlyList<CommitProposal> commitProposals, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<EffectApplication> effectApplications, MoveResult? blockedResult = null)
    {
        Context = context;
        SuccessPolicy = successPolicy;
        TargetData = targetData == null ? Array.Empty<ActionTargetData>() : targetData.ToArray();
        Claims = claims == null ? Array.Empty<ActionClaim>() : claims.ToArray();
        CommitProposals = commitProposals == null ? Array.Empty<CommitProposal>() : commitProposals.ToArray();
        DeferredActions = deferredActions == null ? Array.Empty<DeferredAction>() : deferredActions.ToArray();
        EffectApplications = effectApplications == null ? Array.Empty<EffectApplication>() : effectApplications.ToArray();
        BlockedResult = blockedResult;
    }

    public ActionContext Context { get; }
    public ActionExecutionSuccessPolicy SuccessPolicy { get; }
    public IReadOnlyList<ActionTargetData> TargetData { get; }
    public IReadOnlyList<ActionClaim> Claims { get; }
    public IReadOnlyList<CommitProposal> CommitProposals { get; }
    public IReadOnlyList<DeferredAction> DeferredActions { get; }
    public IReadOnlyList<EffectApplication> EffectApplications { get; }
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

}
