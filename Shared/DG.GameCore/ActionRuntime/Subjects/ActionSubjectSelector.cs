using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class ActionSubjectSelector
{
    private readonly BodyResolver bodyResolver = new();

    public bool TryResolve(GameWorld world, ActionRequest request, GameEntity entity, ActionSpec spec, out BehaviorBody body, out string reason)
    {
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

        if (spec.AllowsConnectedBodySubject)
        {
            return bodyResolver.TryResolve(world, entity, out body, out reason);
        }

        body = new BehaviorBody(entity.EntityId, BehaviorBodyKind.SingleEntity, new[] { entity });
        reason = string.Empty;
        return true;
    }
}

}
