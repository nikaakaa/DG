using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum IntentArbitrationReason
{
    None = 0,
    UnknownEntity = 1,
    MissingRequiredTag = 2,
    BlockedByTag = 3,
    InterruptedByHigherPriorityIntent = 4,
    ConflictingBodyIntents = 5,
    MergedBodyIntent = 6
}

public readonly struct IntentArbitrationItem
{
    public IntentArbitrationItem(BehaviorIntent intent, long bodyId, bool accepted, IntentArbitrationReason reason, string message)
    {
        Intent = intent;
        BodyId = bodyId;
        Accepted = accepted;
        Reason = reason;
        Message = message;
    }

    public BehaviorIntent Intent { get; }
    public long BodyId { get; }
    public bool Accepted { get; }
    public IntentArbitrationReason Reason { get; }
    public string Message { get; }
}

public sealed class IntentArbitrationResult
{
    public IntentArbitrationResult(IReadOnlyList<IntentArbitrationItem> items)
    {
        Items = items;
    }

    public IReadOnlyList<IntentArbitrationItem> Items { get; }
    public IReadOnlyList<BehaviorIntent> AcceptedIntents => Items.Where(item => item.Accepted).Select(item => item.Intent).ToArray();
}

public sealed class IntentArbiter
{
    private readonly BodyResolver bodyResolver;

    public IntentArbiter() : this(new BodyResolver())
    {
    }

    public IntentArbiter(BodyResolver bodyResolver)
    {
        this.bodyResolver = bodyResolver;
    }

    public IntentArbitrationResult Arbitrate(GameWorld world, IReadOnlyList<BehaviorIntent> intents)
    {
        var resolved = new List<ResolvedIntent>();
        var items = new List<IntentArbitrationItem>();
        for (int i = 0; i < intents.Count; i++)
        {
            BehaviorIntent intent = intents[i];
            if (!world.TryGetEntity(intent.EntityId, out GameEntity entity) ||
                !bodyResolver.TryResolve(world, entity, out BehaviorBody body, out _))
            {
                items.Add(new IntentArbitrationItem(intent, 0, false, IntentArbitrationReason.UnknownEntity, "entity not found"));
                continue;
            }

            WorldTag bodyTags = AggregateTags(world, body);
            if (intent.RequiredTags != WorldTag.None && (bodyTags & intent.RequiredTags) != intent.RequiredTags)
            {
                items.Add(new IntentArbitrationItem(intent, body.BodyId, false, IntentArbitrationReason.MissingRequiredTag, "missing required tag"));
                continue;
            }

            if (intent.BlockedTags != WorldTag.None && (bodyTags & intent.BlockedTags) != WorldTag.None)
            {
                items.Add(new IntentArbitrationItem(intent, body.BodyId, false, IntentArbitrationReason.BlockedByTag, "blocked by tag"));
                continue;
            }

            resolved.Add(new ResolvedIntent(intent, body.BodyId));
        }

        foreach (IGrouping<long, ResolvedIntent> group in resolved.GroupBy(item => item.BodyId))
        {
            IReadOnlyList<ResolvedIntent> ordered = group
                .OrderBy(item => item.Intent.Priority)
                .ThenBy(item => item.Intent.SourceActionId)
                .ThenBy(item => item.Intent.SourceStateId)
                .ThenBy(item => item.Intent.EntityId)
                .ToArray();
            WorldActionPriority winningPriority = ordered[0].Intent.Priority;
            IReadOnlyList<ResolvedIntent> winners = ordered.Where(item => item.Intent.Priority == winningPriority).ToArray();
            IReadOnlyList<ResolvedIntent> losers = ordered.Where(item => item.Intent.Priority != winningPriority).ToArray();

            if (winners.Select(item => BuildMoveKey(item.Intent)).Distinct().Count() > 1)
            {
                for (int i = 0; i < winners.Count; i++)
                {
                    items.Add(new IntentArbitrationItem(winners[i].Intent, winners[i].BodyId, false, IntentArbitrationReason.ConflictingBodyIntents, "conflicting body intents"));
                }
            }
            else
            {
                items.Add(new IntentArbitrationItem(winners[0].Intent, winners[0].BodyId, true, IntentArbitrationReason.None, string.Empty));
                for (int i = 1; i < winners.Count; i++)
                {
                    items.Add(new IntentArbitrationItem(winners[i].Intent, winners[i].BodyId, false, IntentArbitrationReason.MergedBodyIntent, "merged body intent"));
                }
            }

            for (int i = 0; i < losers.Count; i++)
            {
                items.Add(new IntentArbitrationItem(losers[i].Intent, losers[i].BodyId, false, IntentArbitrationReason.InterruptedByHigherPriorityIntent, "interrupted by higher priority intent"));
            }
        }

        return new IntentArbitrationResult(items
            .OrderBy(item => item.Intent.Priority)
            .ThenBy(item => item.Intent.SourceActionId)
            .ThenBy(item => item.Intent.SourceStateId)
            .ThenBy(item => item.Intent.EntityId)
            .ToArray());
    }

    private static WorldTag AggregateTags(GameWorld world, BehaviorBody body)
    {
        WorldTag tags = WorldTag.None;
        for (int i = 0; i < body.Entities.Count; i++)
        {
            if (world.TryGetComponent(body.Entities[i], out TagSetComponent component))
            {
                tags |= component.Tags;
            }
        }

        return tags;
    }

    private static string BuildMoveKey(BehaviorIntent intent)
    {
        return intent.TargetCoord.HasValue
            ? "T:" + intent.TargetCoord.Value.X + "," + intent.TargetCoord.Value.Y
            : "D:" + intent.Direction;
    }

    private readonly struct ResolvedIntent
    {
        public ResolvedIntent(BehaviorIntent intent, long bodyId)
        {
            Intent = intent;
            BodyId = bodyId;
        }

        public BehaviorIntent Intent { get; }
        public long BodyId { get; }
    }
}
}
