using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct ExternalPushContact
{
    public ExternalPushContact(long blockerEntityId, long sourceEntityId, GridCoord fromCoord, GridCoord toCoord, long claimActionId, Direction pushDirection = Direction.None)
        : this(blockerEntityId, sourceEntityId, fromCoord, toCoord, claimActionId, pushDirection, 1d, 0)
    {
    }

    public ExternalPushContact(long blockerEntityId, long sourceEntityId, GridCoord fromCoord, GridCoord toCoord, long claimActionId, Direction pushDirection, double progress, int sampleOrder)
    {
        BlockerEntityId = blockerEntityId;
        SourceEntityId = sourceEntityId;
        FromCoord = fromCoord;
        ToCoord = toCoord;
        ClaimActionId = claimActionId;
        PushDirection = pushDirection;
        Progress = progress;
        SampleOrder = sampleOrder;
    }

    public long BlockerEntityId { get; }
    public long SourceEntityId { get; }
    public GridCoord FromCoord { get; }
    public GridCoord ToCoord { get; }
    public long ClaimActionId { get; }
    public Direction PushDirection { get; }
    public double Progress { get; }
    public int SampleOrder { get; }
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
        for (int i = 0; i < claims.Count; i++)
        {
            if (claims[i].Kind == ActionClaimKind.BodyMove)
            {
                bodyIds.Add(claims[i].EntityId);
            }
        }

        var contacts = new List<ExternalPushContact>();
        for (int i = 0; i < claims.Count; i++)
        {
            ActionClaim claim = claims[i];
            if (claim.Kind != ActionClaimKind.BodyMove)
            {
                continue;
            }

            if (!world.TryGetFirstBlockingAt(claim.ToCoord, bodyIds, out BlockingSpatialQueryResult target))
            {
                continue;
            }

            contacts.Add(new ExternalPushContact(target.EntityId, claim.EntityId, claim.FromCoord, claim.ToCoord, claim.ActionId));
        }

        return contacts
            .OrderBy(contact => contact.ToCoord.X)
            .ThenBy(contact => contact.ToCoord.Y)
            .ThenBy(contact => contact.BlockerEntityId)
            .ToArray();
    }
}
}
