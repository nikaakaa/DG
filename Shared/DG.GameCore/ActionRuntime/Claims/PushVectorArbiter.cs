using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct PushVector
{
    public PushVector(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }
    public bool IsZero => X == 0 && Y == 0;
    public bool IsSingleAxis => X == 0 || Y == 0;
}

public readonly struct PushVectorContribution
{
    public PushVectorContribution(string subjectKey, Direction direction, long readyTick, ActionSpecId specId, long actionId, long entityId, IReadOnlyList<long> causalitySamples, int contributionCount)
        : this(subjectKey, direction, readyTick, specId, actionId, entityId, causalitySamples, contributionCount, Array.Empty<PushOriginContext>())
    {
    }

    public PushVectorContribution(string subjectKey, Direction direction, long readyTick, ActionSpecId specId, long actionId, long entityId, IReadOnlyList<long> causalitySamples, int contributionCount, IReadOnlyList<PushOriginContext> originContexts)
    {
        SubjectKey = subjectKey ?? string.Empty;
        Direction = direction;
        ReadyTick = readyTick;
        SpecId = specId;
        ActionId = actionId;
        EntityId = entityId;
        CausalitySamples = causalitySamples == null ? Array.Empty<long>() : causalitySamples.ToArray();
        ContributionCount = Math.Max(1, contributionCount);
        OriginContexts = originContexts == null ? Array.Empty<PushOriginContext>() : originContexts.ToArray();
    }

    public string SubjectKey { get; }
    public Direction Direction { get; }
    public long ReadyTick { get; }
    public ActionSpecId SpecId { get; }
    public long ActionId { get; }
    public long EntityId { get; }
    public IReadOnlyList<long> CausalitySamples { get; }
    public int ContributionCount { get; }
    public IReadOnlyList<PushOriginContext> OriginContexts { get; }
}

public sealed class PushVectorMetadata
{
    public PushVectorMetadata(string subjectKey, long readyTick, PushVector netVector, int totalContributionCount, IReadOnlyDictionary<Direction, int> perDirectionContributionCount, int energy, IReadOnlyList<long> causalitySamples, IReadOnlyList<Direction> path)
        : this(subjectKey, readyTick, netVector, totalContributionCount, perDirectionContributionCount, energy, causalitySamples, path, Array.Empty<PushOriginContext>())
    {
    }

    public PushVectorMetadata(string subjectKey, long readyTick, PushVector netVector, int totalContributionCount, IReadOnlyDictionary<Direction, int> perDirectionContributionCount, int energy, IReadOnlyList<long> causalitySamples, IReadOnlyList<Direction> path, IReadOnlyList<PushOriginContext> originContexts)
    {
        SubjectKey = subjectKey ?? string.Empty;
        ReadyTick = readyTick;
        NetVector = netVector;
        TotalContributionCount = Math.Max(0, totalContributionCount);
        PerDirectionContributionCount = perDirectionContributionCount;
        Energy = energy;
        CausalitySamples = causalitySamples;
        Path = path;
        OriginContexts = originContexts == null ? Array.Empty<PushOriginContext>() : originContexts.ToArray();
    }

    public string SubjectKey { get; }
    public long ReadyTick { get; }
    public PushVector NetVector { get; }
    public int TotalContributionCount { get; }
    public IReadOnlyDictionary<Direction, int> PerDirectionContributionCount { get; }
    public int Energy { get; }
    public IReadOnlyList<long> CausalitySamples { get; }
    public IReadOnlyList<Direction> Path { get; }
    public IReadOnlyList<PushOriginContext> OriginContexts { get; }
}

public sealed class PushVectorCompositionResult
{
    public PushVectorCompositionResult(IReadOnlyList<ActionRequest> requests, IReadOnlyList<ActionRequest> cancelledRequests, IReadOnlyList<PushVectorMetadata> metadata, IReadOnlyList<string> reasons)
        : this(requests, cancelledRequests, metadata, reasons, new Dictionary<long, IReadOnlyList<ActionRequest>>())
    {
    }

    public PushVectorCompositionResult(IReadOnlyList<ActionRequest> requests, IReadOnlyList<ActionRequest> cancelledRequests, IReadOnlyList<PushVectorMetadata> metadata, IReadOnlyList<string> reasons, IReadOnlyDictionary<long, IReadOnlyList<ActionRequest>> mergedRequestsByRepresentative)
    {
        Requests = requests;
        CancelledRequests = cancelledRequests;
        Metadata = metadata;
        Reasons = reasons;
        MergedRequestsByRepresentative = mergedRequestsByRepresentative;
    }

    public IReadOnlyList<ActionRequest> Requests { get; }
    public IReadOnlyList<ActionRequest> CancelledRequests { get; }
    public IReadOnlyList<PushVectorMetadata> Metadata { get; }
    public IReadOnlyList<string> Reasons { get; }
    public IReadOnlyDictionary<long, IReadOnlyList<ActionRequest>> MergedRequestsByRepresentative { get; }
}

public sealed class PushVectorArbiter
{
    private readonly ActionSpecRegistry registry;
    private readonly BodyResolver bodyResolver = new();

    public PushVectorArbiter(ActionSpecRegistry registry)
    {
        this.registry = registry;
    }

    public PushVectorCompositionResult Compose(GameWorld world, IReadOnlyList<ActionRequest> requests)
    {
        var passthrough = new List<ActionRequest>();
        var groups = new Dictionary<string, List<PushVectorContribution>>();
        var byActionId = new Dictionary<long, ActionRequest>();

        for (int i = 0; i < requests.Count; i++)
        {
            ActionRequest request = requests[i];
            ActionSpec spec = registry.Get(request.SpecId);
            if (!IsPushContribution(spec, request) || !TryBuildContribution(world, request, spec, out PushVectorContribution contribution))
            {
                passthrough.Add(request);
                continue;
            }

            string key = contribution.ReadyTick + "|" + contribution.SubjectKey;
            if (!groups.TryGetValue(key, out List<PushVectorContribution> group))
            {
                group = new List<PushVectorContribution>();
                groups.Add(key, group);
            }

            group.Add(contribution);
            byActionId[request.ActionId] = request;
        }

        var output = new List<ActionRequest>(passthrough);
        var cancelled = new List<ActionRequest>();
        var metadata = new List<PushVectorMetadata>();
        var reasons = new List<string>();
        var merged = new Dictionary<long, IReadOnlyList<ActionRequest>>();

        foreach (List<PushVectorContribution> group in groups.Values.OrderBy(item => item[0].ReadyTick).ThenBy(item => item[0].SubjectKey))
        {
            PushVectorMetadata groupMetadata = BuildMetadata(group);
            metadata.Add(groupMetadata);
            if (groupMetadata.NetVector.IsZero)
            {
                for (int i = 0; i < group.Count; i++)
                {
                    cancelled.Add(byActionId[group[i].ActionId]);
                }

                reasons.Add("push-vector-cancelled");
                continue;
            }

            long representativeActionId = group.OrderBy(item => item.ActionId).First().ActionId;
            ActionRequest representative = byActionId[representativeActionId];
            ActionRequest[] mergedRequests = group
                .Where(item => item.ActionId != representativeActionId)
                .OrderBy(item => item.ActionId)
                .Select(item => byActionId[item.ActionId])
                .ToArray();
            if (mergedRequests.Length > 0)
            {
                merged[representativeActionId] = mergedRequests;
            }

            IReadOnlyList<Direction> path = groupMetadata.Path;
            output.Add(CloneWithDirection(representative, path[0]));
            if (path.Count > 1)
            {
                reasons.Add("push-vector-path-deferred");
            }
        }

        return new PushVectorCompositionResult(
            output.OrderBy(request => request.Priority).ThenBy(request => request.ReadyTick).ThenBy(request => request.ActionId).ThenBy(request => request.EntityId).ToArray(),
            cancelled.OrderBy(request => request.ActionId).ToArray(),
            metadata.ToArray(),
            reasons.ToArray(),
            merged);
    }

    private bool IsPushContribution(ActionSpec spec, ActionRequest request)
    {
        BlockedResultPolicy policy = registry.GetBlockedResultPolicy(spec.BlockedResultPolicyId);
        return spec.Targeting.SelectorId.Equals(new TargetSelectorId("direction_cell")) &&
            HasDeriveActionBranch(policy) &&
            request.Target.Direction != Direction.None;
    }

    private static bool HasDeriveActionBranch(BlockedResultPolicy policy)
    {
        for (int i = 0; i < policy.Branches.Count; i++)
        {
            if (policy.Branches[i].ResultKind == BlockedResultKind.DeriveAction)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryBuildContribution(GameWorld world, ActionRequest request, ActionSpec spec, out PushVectorContribution contribution)
    {
        contribution = default;
        if (!world.TryGetEntity(request.EntityId, out GameEntity entity))
        {
            return false;
        }

        string subjectKey = request.EntityId.ToString();
        if (spec.AllowsConnectedBodySubject &&
            bodyResolver.TryResolve(world, entity, out BehaviorBody body, out _) &&
            body.Entities.Count != 0)
        {
            subjectKey = string.Join("|", body.Entities.Select(item => item.EntityId).OrderBy(id => id));
        }

        contribution = new PushVectorContribution(subjectKey, request.Target.Direction, request.ReadyTick, request.SpecId, request.ActionId, request.EntityId, request.DeferredCausalitySamples, request.DeferredContributionCount, request.PushOriginContexts);
        return true;
    }

    private static PushVectorMetadata BuildMetadata(IReadOnlyList<PushVectorContribution> group)
    {
        var perDirection = new Dictionary<Direction, int>
        {
            [Direction.Left] = 0,
            [Direction.Right] = 0,
            [Direction.Up] = 0,
            [Direction.Down] = 0
        };
        var causality = new List<long>();
        var originContexts = new List<PushOriginContext>();
        int x = 0;
        int y = 0;
        int total = 0;

        for (int i = 0; i < group.Count; i++)
        {
            PushVectorContribution contribution = group[i];
            int count = contribution.ContributionCount;
            perDirection[contribution.Direction] += count;
            total += count;
            for (int sampleIndex = 0; sampleIndex < contribution.CausalitySamples.Count; sampleIndex++)
            {
                long causalityId = contribution.CausalitySamples[sampleIndex];
                if (causality.Count < 8 && causalityId != 0 && !causality.Contains(causalityId))
                {
                    causality.Add(causalityId);
                }
            }

            if (contribution.Direction == Direction.Left)
            {
                x -= count;
            }
            else if (contribution.Direction == Direction.Right)
            {
                x += count;
            }
            else if (contribution.Direction == Direction.Up)
            {
                y += count;
            }
            else if (contribution.Direction == Direction.Down)
            {
                y -= count;
            }

            for (int contextIndex = 0; contextIndex < contribution.OriginContexts.Count && originContexts.Count < 8; contextIndex++)
            {
                PushOriginContext context = contribution.OriginContexts[contextIndex];
                if (!originContexts.Contains(context))
                {
                    originContexts.Add(context);
                }
            }
        }

        var vector = new PushVector(x, y);
        return new PushVectorMetadata(group[0].SubjectKey, group[0].ReadyTick, vector, total, perDirection, 0, causality, BuildPath(vector), originContexts);
    }

    private static IReadOnlyList<Direction> BuildPath(PushVector vector)
    {
        var path = new List<Direction>();
        if (vector.X < 0)
        {
            path.Add(Direction.Left);
        }
        else if (vector.X > 0)
        {
            path.Add(Direction.Right);
        }

        if (vector.Y < 0)
        {
            path.Add(Direction.Down);
        }
        else if (vector.Y > 0)
        {
            path.Add(Direction.Up);
        }

        return path;
    }

    private static ActionRequest CloneWithDirection(ActionRequest request, Direction direction)
    {
        var target = new ActionTarget(request.Target.TargetEntityId, null, direction);
        return new ActionRequest(request.ActionId, request.SpecId, request.Priority, request.Source, request.EntityId, target, request.RuntimeParams, request.CreatedTick, request.ReadyTick, request.ClientTick, request.OwnerActionId, request.DerivedFromUnitId, request.DeferredContributionCount, request.DeferredCausalitySamples, request.SubjectEntityIds, request.PushOriginContexts);
    }
}
}
