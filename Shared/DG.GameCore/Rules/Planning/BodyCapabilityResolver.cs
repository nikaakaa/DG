using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct ExternalPushContact
{
    public ExternalPushContact(long blockerEntityId, long sourceEntityId, GridCoord fromCoord, GridCoord toCoord, long claimActionId)
    {
        BlockerEntityId = blockerEntityId;
        SourceEntityId = sourceEntityId;
        FromCoord = fromCoord;
        ToCoord = toCoord;
        ClaimActionId = claimActionId;
    }

    public long BlockerEntityId { get; }
    public long SourceEntityId { get; }
    public GridCoord FromCoord { get; }
    public GridCoord ToCoord { get; }
    public long ClaimActionId { get; }
}

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
        IReadOnlyList<ExternalPushContact> contacts = FindExternalPushContacts(world, claims, body);
        return contacts.Count == 0 || !world.TryGetEntity(contacts[0].BlockerEntityId, out GameEntity blocker) ? null! : blocker;
    }

    public IReadOnlyList<ExternalPushContact> FindExternalPushContacts(GameWorld world, IReadOnlyList<ActionClaim> claims, BehaviorBody body)
    {
        var bodyIds = new HashSet<long>(body.Entities.Select(entity => entity.EntityId));
        var contacts = new List<ExternalPushContact>();
        for (int i = 0; i < claims.Count; i++)
        {
            ActionClaim claim = claims[i];
            if (claim.Kind != ActionClaimKind.BodyMove)
            {
                continue;
            }

            IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(claim.ToCoord);
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                GameEntity target = targets[targetIndex];
                if (bodyIds.Contains(target.EntityId) || !world.HasComponent<BlockingComponent>(target))
                {
                    continue;
                }

                contacts.Add(new ExternalPushContact(target.EntityId, claim.EntityId, claim.FromCoord, claim.ToCoord, claim.ActionId));
            }
        }

        return contacts
            .OrderBy(contact => contact.ToCoord.X)
            .ThenBy(contact => contact.ToCoord.Y)
            .ThenBy(contact => contact.BlockerEntityId)
            .ToArray();
    }
}
}
