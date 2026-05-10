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
    {
        SubjectKey = subjectKey ?? string.Empty;
        Direction = direction;
        ReadyTick = readyTick;
        SpecId = specId;
        ActionId = actionId;
        EntityId = entityId;
        CausalitySamples = causalitySamples == null ? Array.Empty<long>() : causalitySamples.ToArray();
        ContributionCount = Math.Max(1, contributionCount);
    }

    public string SubjectKey { get; }
    public Direction Direction { get; }
    public long ReadyTick { get; }
    public ActionSpecId SpecId { get; }
    public long ActionId { get; }
    public long EntityId { get; }
    public IReadOnlyList<long> CausalitySamples { get; }
    public int ContributionCount { get; }
}

public sealed class PushVectorMetadata
{
    public PushVectorMetadata(string subjectKey, long readyTick, PushVector netVector, int totalContributionCount, IReadOnlyDictionary<Direction, int> perDirectionContributionCount, int energy, IReadOnlyList<long> causalitySamples, IReadOnlyList<Direction> path)
    {
        SubjectKey = subjectKey ?? string.Empty;
        ReadyTick = readyTick;
        NetVector = netVector;
        TotalContributionCount = Math.Max(0, totalContributionCount);
        PerDirectionContributionCount = perDirectionContributionCount;
        Energy = energy;
        CausalitySamples = causalitySamples;
        Path = path;
    }

    public string SubjectKey { get; }
    public long ReadyTick { get; }
    public PushVector NetVector { get; }
    public int TotalContributionCount { get; }
    public IReadOnlyDictionary<Direction, int> PerDirectionContributionCount { get; }
    public int Energy { get; }
    public IReadOnlyList<long> CausalitySamples { get; }
    public IReadOnlyList<Direction> Path { get; }
}

public sealed class PushVectorCompositionResult
{
    public PushVectorCompositionResult(IReadOnlyList<ActionRequest> requests, IReadOnlyList<ActionRequest> cancelledRequests, IReadOnlyList<PushVectorMetadata> metadata, IReadOnlyList<string> reasons)
    {
        Requests = requests;
        CancelledRequests = cancelledRequests;
        Metadata = metadata;
        Reasons = reasons;
    }

    public IReadOnlyList<ActionRequest> Requests { get; }
    public IReadOnlyList<ActionRequest> CancelledRequests { get; }
    public IReadOnlyList<PushVectorMetadata> Metadata { get; }
    public IReadOnlyList<string> Reasons { get; }
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

            ActionRequest representative = byActionId[group.OrderBy(item => item.ActionId).First().ActionId];
            IReadOnlyList<Direction> path = groupMetadata.Path;
            output.Add(CloneWithDirection(representative, path[0]));
            if (path.Count > 1)
            {
                reasons.Add("push-vector-path-pending");
            }
        }

        return new PushVectorCompositionResult(
            output.OrderBy(request => request.Priority).ThenBy(request => request.ReadyTick).ThenBy(request => request.ActionId).ThenBy(request => request.EntityId).ToArray(),
            cancelled.OrderBy(request => request.ActionId).ToArray(),
            metadata.ToArray(),
            reasons.ToArray());
    }

    private static bool IsPushContribution(ActionSpec spec, ActionRequest request)
    {
        return spec.Primitive == ActionPrimitive.Move &&
            spec.TargetRule == ActionTargetRule.DirectionFromRequest &&
            spec.BlockedPolicy == ActionBlockedPolicy.StartPushIfPushable &&
            spec.Handoff.IsEnabled &&
            request.Target.Direction != Direction.None;
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

        contribution = new PushVectorContribution(subjectKey, request.Target.Direction, request.ReadyTick, request.SpecId, request.ActionId, request.EntityId, request.DeferredCausalitySamples, request.DeferredContributionCount);
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
        }

        var vector = new PushVector(x, y);
        return new PushVectorMetadata(group[0].SubjectKey, group[0].ReadyTick, vector, total, perDirection, 0, causality, BuildPath(vector));
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
        return new ActionRequest(request.ActionId, request.SpecId, request.Priority, request.Source, request.EntityId, target, request.RuntimeParams, request.CreatedTick, request.ReadyTick, request.ClientTick, request.OwnerActionId, request.DerivedFromUnitId, request.DeferredContributionCount, request.DeferredCausalitySamples);
    }
}
}
