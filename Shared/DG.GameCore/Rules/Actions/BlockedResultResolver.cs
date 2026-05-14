using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct BlockedResultContext
{
    public BlockedResultContext(GameWorld world, ActionRequest request, ActionSpec spec, BehaviorBody sourceBody, IReadOnlyList<ExternalPushContact> contacts, Direction direction)
    {
        World = world;
        Request = request;
        Spec = spec;
        SourceBody = sourceBody;
        Contacts = contacts ?? Array.Empty<ExternalPushContact>();
        Direction = direction;
    }

    public GameWorld World { get; }
    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public BehaviorBody SourceBody { get; }
    public IReadOnlyList<ExternalPushContact> Contacts { get; }
    public Direction Direction { get; }
}


public readonly struct BlockedResultDecision
{
    public BlockedResultDecision(BlockedResultBranch branch, IReadOnlyList<ExternalPushContact> matchedContacts)
    {
        Branch = branch;
        MatchedContacts = matchedContacts ?? Array.Empty<ExternalPushContact>();
    }

    public BlockedResultBranch Branch { get; }
    public IReadOnlyList<ExternalPushContact> MatchedContacts { get; }
}


public sealed class BlockedResultResolver
{
    private readonly BodyResolver bodyResolver = new();
    private readonly BodyCapabilityResolver bodyCapabilities = new();

    public bool TryResolve(BlockedResultPolicy policy, BlockedResultContext context, out BlockedResultDecision decision)
    {
        for (int i = 0; i < policy.Branches.Count; i++)
        {
            BlockedResultBranch branch = policy.Branches[i];
            IReadOnlyList<ExternalPushContact> contacts = MatchingContacts(branch, context);
            if (contacts.Count != 0)
            {
                decision = new BlockedResultDecision(branch, contacts);
                return true;
            }
        }

        decision = default;
        return false;
    }

    private IReadOnlyList<ExternalPushContact> MatchingContacts(BlockedResultBranch branch, BlockedResultContext context)
    {
        if (context.Contacts.Count == 0)
        {
            return Array.Empty<ExternalPushContact>();
        }

        if (branch.Conditions.Count == 0)
        {
            return context.Contacts;
        }

        var matches = new List<ExternalPushContact>();
        for (int contactIndex = 0; contactIndex < context.Contacts.Count; contactIndex++)
        {
            ExternalPushContact contact = context.Contacts[contactIndex];
            if (!context.World.TryGetEntity(contact.BlockerEntityId, out GameEntity blocking))
            {
                continue;
            }

            if (MatchesAll(branch, context, blocking))
            {
                matches.Add(contact);
            }
        }

        if (branch.ResultKind == BlockedResultKind.DeriveAction && matches.Count != context.Contacts.Count)
        {
            return Array.Empty<ExternalPushContact>();
        }

        return matches;
    }

    private bool MatchesAll(BlockedResultBranch branch, BlockedResultContext context, GameEntity blocking)
    {
        for (int i = 0; i < branch.Conditions.Count; i++)
        {
            if (!Matches(branch.Conditions[i], context, blocking))
            {
                return false;
            }
        }

        return true;
    }

    private bool Matches(ActionCondition condition, BlockedResultContext context, GameEntity blocking)
    {
        switch (condition.Kind)
        {
            case ActionConditionKind.Always:
                return true;
            case ActionConditionKind.HasTag:
                return HasTag(condition, context, blocking);
            case ActionConditionKind.MissingTag:
                return !HasTag(condition, context, blocking);
            case ActionConditionKind.HasComponent:
                return HasComponent(condition, context, blocking);
            case ActionConditionKind.MissingComponent:
                return !HasComponent(condition, context, blocking);
            case ActionConditionKind.CanMove:
                return CanMove(condition, context, blocking);
            case ActionConditionKind.CanBePushed:
                return CanBePushed(condition, context, blocking);
            case ActionConditionKind.BodyKindIs:
                return BodyKindIs(condition, context, blocking);
            default:
                return false;
        }
    }

    private static bool HasTag(ActionCondition condition, BlockedResultContext context, GameEntity blocking)
    {
        if (condition.Subject == ActionConditionSubject.SourceBody || condition.Subject == ActionConditionSubject.BlockingBody)
        {
            BehaviorBody body = condition.Subject == ActionConditionSubject.SourceBody ? context.SourceBody : ResolveBody(context.World, blocking);
            return BodyHasTag(context.World, body, condition.Tag);
        }

        GameEntity entity = condition.Subject == ActionConditionSubject.Source ? SourceEntity(context) : blocking;
        return entity != null && context.World.HasTag(entity, condition.Tag);
    }

    private static bool HasComponent(ActionCondition condition, BlockedResultContext context, GameEntity blocking)
    {
        if (condition.Subject == ActionConditionSubject.SourceBody || condition.Subject == ActionConditionSubject.BlockingBody)
        {
            BehaviorBody body = condition.Subject == ActionConditionSubject.SourceBody ? context.SourceBody : ResolveBody(context.World, blocking);
            return BodyHasComponent(context.World, body, condition.ComponentKind);
        }

        GameEntity entity = condition.Subject == ActionConditionSubject.Source ? SourceEntity(context) : blocking;
        return entity != null && HasComponentKind(context.World, entity, condition.ComponentKind);
    }

    private bool CanMove(ActionCondition condition, BlockedResultContext context, GameEntity blocking)
    {
        BehaviorBody body = condition.Subject == ActionConditionSubject.Blocking || condition.Subject == ActionConditionSubject.BlockingBody ? ResolveBody(context.World, blocking) : context.SourceBody;
        return body != null && bodyCapabilities.CanMove(context.World, body);
    }

    private bool CanBePushed(ActionCondition condition, BlockedResultContext context, GameEntity blocking)
    {
        if (condition.Subject == ActionConditionSubject.Blocking || condition.Subject == ActionConditionSubject.BlockingBody)
        {
            return bodyCapabilities.CanPushEntry(context.World, blocking);
        }

        GameEntity entity = SourceEntity(context);
        return entity != null && bodyCapabilities.CanPushEntry(context.World, entity);
    }

    private bool BodyKindIs(ActionCondition condition, BlockedResultContext context, GameEntity blocking)
    {
        BehaviorBody body = condition.Subject == ActionConditionSubject.Blocking || condition.Subject == ActionConditionSubject.BlockingBody ? ResolveBody(context.World, blocking) : context.SourceBody;
        return body != null && body.Kind == condition.BodyKind;
    }

    private static GameEntity SourceEntity(BlockedResultContext context)
    {
        return context.World.TryGetEntity(context.Request.EntityId, out GameEntity entity) ? entity : null!;
    }

    private static BehaviorBody ResolveBody(GameWorld world, GameEntity entity)
    {
        return new BodyResolver().TryResolve(world, entity, out BehaviorBody body, out _) ? body : new BehaviorBody(entity.EntityId, BehaviorBodyKind.SingleEntity, new[] { entity });
    }

    private static bool BodyHasTag(GameWorld world, BehaviorBody body, WorldTag tag)
    {
        if (body == null)
        {
            return false;
        }

        for (int i = 0; i < body.Entities.Count; i++)
        {
            if (world.HasTag(body.Entities[i], tag))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BodyHasComponent(GameWorld world, BehaviorBody body, ComponentKind componentKind)
    {
        if (body == null)
        {
            return false;
        }

        for (int i = 0; i < body.Entities.Count; i++)
        {
            if (HasComponentKind(world, body.Entities[i], componentKind))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasComponentKind(GameWorld world, GameEntity entity, ComponentKind componentKind)
    {
        switch (componentKind)
        {
            case ComponentKind.Position:
                return world.HasComponent<PositionComponent>(entity);
            case ComponentKind.Direction:
                return world.HasComponent<DirectionComponent>(entity);
            case ComponentKind.Collider:
                return world.HasComponent<ColliderComponent>(entity);
            case ComponentKind.Blocking:
                return world.HasComponent<BlockingComponent>(entity);
            case ComponentKind.Bouncable:
                return world.HasComponent<BouncableComponent>(entity);
            case ComponentKind.AutoMove:
                return world.HasComponent<AutoMoveComponent>(entity);
            case ComponentKind.PlayerControl:
                return world.HasComponent<PlayerControlComponent>(entity);
            case ComponentKind.PushOnEnter:
                return world.HasComponent<PushOnEnterComponent>(entity);
            case ComponentKind.Pushable:
                return world.HasComponent<PushableComponent>(entity);
            case ComponentKind.PortConnector:
                return world.HasComponent<PortConnectorComponent>(entity);
            default:
                return false;
        }
    }
}

}
