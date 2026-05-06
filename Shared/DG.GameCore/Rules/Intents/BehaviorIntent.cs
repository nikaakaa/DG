using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public enum BehaviorIntentKind
{
    Move = 1,
    Push = 2,
    AutoMove = 3,
    MechanismPush = 4,
    DebugMove = 5
}

public enum IntentCancelPolicy
{
    None = 0,
    CancelLowerPriority = 1
}

public readonly struct BehaviorIntentDefinition
{
    public BehaviorIntentDefinition(BehaviorIntentKind kind, WorldTag sourceTag, WorldTag abilityTag, WorldTag requiredTags, WorldTag blockedTags, IntentCancelPolicy cancelPolicy)
    {
        Kind = kind;
        SourceTag = sourceTag;
        AbilityTag = abilityTag;
        RequiredTags = requiredTags;
        BlockedTags = blockedTags;
        CancelPolicy = cancelPolicy;
    }

    public BehaviorIntentKind Kind { get; }
    public WorldTag SourceTag { get; }
    public WorldTag AbilityTag { get; }
    public WorldTag RequiredTags { get; }
    public WorldTag BlockedTags { get; }
    public IntentCancelPolicy CancelPolicy { get; }
}

public static class BehaviorIntentDefinitions
{
    private static readonly IReadOnlyDictionary<BehaviorIntentKind, BehaviorIntentDefinition> Definitions = new Dictionary<BehaviorIntentKind, BehaviorIntentDefinition>
    {
        [BehaviorIntentKind.Move] = new(BehaviorIntentKind.Move, WorldTag.SourcePlayer, WorldTag.AbilityMove, WorldTag.None, WorldTag.BlockPlayerMove | WorldTag.StateStunned | WorldTag.StateRooted, IntentCancelPolicy.CancelLowerPriority),
        [BehaviorIntentKind.Push] = new(BehaviorIntentKind.Push, WorldTag.SourcePlayer, WorldTag.AbilityPlayerPush, WorldTag.None, WorldTag.StateStunned, IntentCancelPolicy.CancelLowerPriority),
        [BehaviorIntentKind.AutoMove] = new(BehaviorIntentKind.AutoMove, WorldTag.SourceAuto, WorldTag.AbilityAutoMove, WorldTag.None, WorldTag.None, IntentCancelPolicy.CancelLowerPriority),
        [BehaviorIntentKind.MechanismPush] = new(BehaviorIntentKind.MechanismPush, WorldTag.SourceMechanism, WorldTag.AbilityMechanismPush, WorldTag.None, WorldTag.ImmuneMechanismPush, IntentCancelPolicy.CancelLowerPriority),
        [BehaviorIntentKind.DebugMove] = new(BehaviorIntentKind.DebugMove, WorldTag.SourceDebug, WorldTag.AbilityMove, WorldTag.None, WorldTag.None, IntentCancelPolicy.CancelLowerPriority)
    };

    public static BehaviorIntentDefinition Get(BehaviorIntentKind kind)
    {
        if (!Definitions.TryGetValue(kind, out BehaviorIntentDefinition definition))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown behavior intent kind");
        }

        return definition;
    }
}

public readonly struct BehaviorIntent
{
    public BehaviorIntent(BehaviorIntentKind kind, WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, Direction direction, long serverTick, GridCoord? targetCoord = null, WorldTag sourceTag = WorldTag.None, WorldTag abilityTag = WorldTag.None, WorldTag requiredTags = WorldTag.None, WorldTag blockedTags = WorldTag.None, IntentCancelPolicy cancelPolicy = IntentCancelPolicy.CancelLowerPriority)
    {
        BehaviorIntentDefinition definition = BehaviorIntentDefinitions.Get(kind);
        Kind = kind;
        Priority = priority;
        SourceActionId = sourceActionId;
        SourceStateId = sourceStateId;
        EntityId = entityId;
        Direction = direction;
        ServerTick = serverTick;
        TargetCoord = targetCoord;
        SourceTag = sourceTag == WorldTag.None ? definition.SourceTag : sourceTag;
        AbilityTag = abilityTag == WorldTag.None ? definition.AbilityTag : abilityTag;
        RequiredTags = requiredTags == WorldTag.None ? definition.RequiredTags : requiredTags;
        BlockedTags = blockedTags == WorldTag.None ? definition.BlockedTags : blockedTags;
        CancelPolicy = cancelPolicy;
    }

    public BehaviorIntentKind Kind { get; }
    public WorldActionPriority Priority { get; }
    public long SourceActionId { get; }
    public long SourceStateId { get; }
    public long EntityId { get; }
    public Direction Direction { get; }
    public long ServerTick { get; }
    public GridCoord? TargetCoord { get; }
    public WorldTag SourceTag { get; }
    public WorldTag AbilityTag { get; }
    public WorldTag RequiredTags { get; }
    public WorldTag BlockedTags { get; }
    public IntentCancelPolicy CancelPolicy { get; }
}
}
