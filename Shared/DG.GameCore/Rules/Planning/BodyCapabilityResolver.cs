using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class BodyCapabilityResolver
{
    public bool CanPushEntry(GameWorld world, GameEntity entry)
    {
        if (!world.HasComponent<PushableComponent>(entry))
        {
            return false;
        }

        return !world.TryGetComponent(entry, out MovementPermissionComponent permission) || permission.CanBePushed;
    }

    public bool CanMove(GameWorld world, BehaviorBody body)
    {
        for (int i = 0; i < body.Entities.Count; i++)
        {
            if (world.TryGetComponent(body.Entities[i], out MovementPermissionComponent permission) &&
                !permission.CanMove)
            {
                return false;
            }
        }

        return true;
    }

    public bool Contains(BehaviorBody body, long entityId)
    {
        for (int i = 0; i < body.Entities.Count; i++)
        {
            if (body.Entities[i].EntityId == entityId)
            {
                return true;
            }
        }

        return false;
    }

    public GameEntity FindExternalBlocking(GameWorld world, IReadOnlyList<ActionClaim> claims, BehaviorBody body)
    {
        var bodyIds = new HashSet<long>(body.Entities.Select(entity => entity.EntityId));
        for (int i = 0; i < claims.Count; i++)
        {
            ActionClaim claim = claims[i];
            IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(claim.ToCoord);
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                GameEntity target = targets[targetIndex];
                if (bodyIds.Contains(target.EntityId) || !world.HasComponent<BlockingComponent>(target))
                {
                    continue;
                }

                return target;
            }
        }

        return null!;
    }
}
}
