using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum RotatePivotDirection
{
    None = 0,
    Clockwise = 1,
    CounterClockwise = 2
}

public sealed class RotatePivotResponseResult
{
    public RotatePivotResponseResult(IReadOnlyList<ActionRequest> remainingRequests, IReadOnlyList<MovePlan> movePlans, IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionBehaviorInstance> behaviorInstances, IReadOnlyList<string> reasons)
        : this(remainingRequests, movePlans, actionResults, deferredActions, Array.Empty<ActionFact>(), behaviorInstances, reasons)
    {
    }

    public RotatePivotResponseResult(IReadOnlyList<ActionRequest> remainingRequests, IReadOnlyList<MovePlan> movePlans, IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionFact> actionFacts, IReadOnlyList<ActionBehaviorInstance> behaviorInstances, IReadOnlyList<string> reasons)
    {
        RemainingRequests = remainingRequests;
        MovePlans = movePlans;
        ActionResults = actionResults;
        DeferredActions = deferredActions;
        ActionFacts = actionFacts;
        BehaviorInstances = behaviorInstances;
        Reasons = reasons;
    }

    public IReadOnlyList<ActionRequest> RemainingRequests { get; }
    public IReadOnlyList<MovePlan> MovePlans { get; }
    public IReadOnlyDictionary<long, MoveResult> ActionResults { get; }
    public IReadOnlyList<DeferredAction> DeferredActions { get; }
    public IReadOnlyList<ActionFact> ActionFacts { get; }
    public IReadOnlyList<ActionBehaviorInstance> BehaviorInstances { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class RotatePivotResponseProcessor
{
    private readonly ActionSpecRegistry registry;
    private readonly BodyResolver bodyResolver = new();
    private readonly BodyCapabilityResolver bodyCapabilities = new();
    private readonly RotateSweepPlanner sweepPlanner = new();

    public RotatePivotResponseProcessor(ActionSpecRegistry registry)
    {
        this.registry = registry;
    }

    public RotatePivotResponseResult Process(GameWorld world, IReadOnlyList<ActionRequest> requests, long serverTick)
    {
        var remaining = new List<ActionRequest>();
        var movePlans = new List<MovePlan>();
        var results = new Dictionary<long, MoveResult>();
        var deferred = new List<DeferredAction>();
        var behaviorInstances = new List<ActionBehaviorInstance>();
        var reasons = new List<string>();
        var groups = new Dictionary<string, List<ActionRequest>>();

        for (int i = 0; i < requests.Count; i++)
        {
            ActionRequest request = requests[i];
            ActionSpec spec = registry.Get(request.SpecId);
            if (!IsPushContribution(spec, request) ||
                !world.TryGetEntity(request.EntityId, out GameEntity entity) ||
                !bodyResolver.TryResolve(world, entity, out BehaviorBody body, out _) ||
                body.Entities.Count == 0)
            {
                continue;
            }

            string key = request.ReadyTick + "|" + BuildBodyKey(body);
            if (!groups.TryGetValue(key, out List<ActionRequest> group))
            {
                group = new List<ActionRequest>();
                groups.Add(key, group);
            }

            group.Add(request);
        }

        var consumed = new HashSet<long>();
        foreach (List<ActionRequest> group in groups.Values.OrderBy(item => item[0].ReadyTick).ThenBy(item => item.Min(request => request.ActionId)))
        {
            if (!TryResolveGroupBody(world, group, out BehaviorBody body))
            {
                continue;
            }

            int pivotCount = CountPivots(world, body, out GameEntity pivot, out GridCoord pivotCoord);
            if (pivotCount == 0)
            {
                continue;
            }

            if (pivotCount > 1)
            {
                Consume(group);
                AddInvalidResults(world, group, "rotate-pivot-invalid");
                reasons.Add("rotate-pivot-invalid");
                continue;
            }

            RotatePivotDirection direction = ResolveTorque(world, group, pivotCoord, out bool hasTorque);
            if (direction == RotatePivotDirection.None)
            {
                if (!hasTorque)
                {
                    continue;
                }

                Consume(group);
                AddInvalidResults(world, group, "rotate-pivot-zero-torque");
                reasons.Add("rotate-pivot-zero-torque");
                continue;
            }

            Consume(group);
            if (!TryBuildPlan(world, body, pivot, pivotCoord, direction, group[0], serverTick, out MovePlan plan, out IReadOnlyList<ExternalPushContact> contacts, out string reason))
            {
                AddInvalidResults(world, group, reason);
                reasons.Add(reason);
                continue;
            }

            if (contacts.Count == 0)
            {
                int baseCost = Math.Max(1, group[0].RuntimeParams.CostTicks);
                long endTick = serverTick + baseCost;
                IReadOnlyList<ActionFact> startActionFacts = BuildRotateSuccessFacts(world, plan, pivot.EntityId, pivotCoord, direction, baseCost, serverTick, endTick);
                behaviorInstances.Add(new ActionBehaviorInstance(
                    group[0].ActionId,
                    group[0].ActionId,
                    group[0].OwnerActionId,
                    group[0].SpecId,
                    "rotate-pivot",
                    "rotate_pivot_runner",
                    ActionPrimitiveNames.NameOf(registry.Get(group[0].SpecId).Primitive),
                    group[0].Source,
                    BodyIds(body),
                    BehaviorInstanceState.Running,
                    string.Empty,
                    ReservationFor(plan, BodyIds(body), BehaviorIncomingPolicy.RejectIncoming),
                    serverTick,
                    serverTick,
                    string.Empty,
                    "rotate-pivot",
                    serverTick,
                    endTick,
                    baseCost,
                    baseCost,
                    BehaviorCompletionMode.CompleteAtEndTick,
                    BehaviorIncomingPolicy.RejectIncoming,
                    startActionFacts,
                    Array.Empty<BehaviorScheduledOutput>(),
                    new BehaviorStepOutput(new[] { plan }, BuildSuccessResults(world, group, plan, direction), Array.Empty<DeferredAction>(), BuildInternalCompletionFacts(group, BodyIds(body)))));
                reasons.Add(direction == RotatePivotDirection.Clockwise ? "rotate-pivot-cw" : "rotate-pivot-ccw");
                continue;
            }

            int bounceBaseCost = Math.Max(1, group[0].RuntimeParams.CostTicks);
            RotateBounceTiming bounceTiming = CalculateBounceTiming(serverTick, bounceBaseCost, contacts[0].Progress);
            if (!TryBuildDeferred(world, group[0], registry.Get(group[0].SpecId), contacts, pivot.EntityId, direction, bounceTiming.ContactTick, out IReadOnlyList<DeferredAction> groupDeferred, out string deferredReason))
            {
                AddInvalidResults(world, group, deferredReason);
                reasons.Add(deferredReason);
                continue;
            }

            IReadOnlyList<ActionFact> bounceStartActionFacts = BuildRotateBounceFacts(world, plan, pivot.EntityId, pivotCoord, direction, contacts, false, serverTick, bounceTiming.ContactTick, bounceTiming.EndTick, bounceTiming.ContactProgress, bounceTiming.EffectiveCostTicks);
            IReadOnlyList<ActionFact> impactActionFacts = BuildRotateBounceFacts(world, plan, pivot.EntityId, pivotCoord, direction, contacts, true, serverTick, bounceTiming.ContactTick, bounceTiming.EndTick, bounceTiming.ContactProgress, bounceTiming.EffectiveCostTicks);
            behaviorInstances.Add(new ActionBehaviorInstance(
                group[0].ActionId,
                group[0].ActionId,
                group[0].OwnerActionId,
                group[0].SpecId,
                "rotate-pivot",
                "rotate_pivot_runner",
                ActionPrimitiveNames.NameOf(registry.Get(group[0].SpecId).Primitive),
                group[0].Source,
                BodyIds(body),
                BehaviorInstanceState.Running,
                string.Empty,
                ReservationFor(plan, BodyIds(body), BehaviorIncomingPolicy.RejectIncoming),
                serverTick,
                serverTick,
                string.Empty,
                "rotate-pivot",
                serverTick,
                bounceTiming.EndTick,
                bounceBaseCost,
                bounceTiming.EffectiveCostTicks,
                BehaviorCompletionMode.CompleteAtEndTick,
                BehaviorIncomingPolicy.RejectIncoming,
                bounceStartActionFacts,
                new[] { new BehaviorScheduledOutput(bounceTiming.ContactTick, new BehaviorStepOutput(Array.Empty<MovePlan>(), new Dictionary<long, MoveResult>(), groupDeferred, impactActionFacts)) },
                new BehaviorStepOutput(Array.Empty<MovePlan>(), BuildDeferredResults(world, group, direction, contacts[0].BlockerEntityId), Array.Empty<DeferredAction>(), BuildInternalCompletionFacts(group, BodyIds(body)))));
            reasons.Add("rotate-pivot-deferred-output");
        }

        for (int i = 0; i < requests.Count; i++)
        {
            if (!consumed.Contains(requests[i].ActionId))
            {
                remaining.Add(requests[i]);
            }
        }

        return new RotatePivotResponseResult(
            remaining.OrderBy(request => request.Priority).ThenBy(request => request.ReadyTick).ThenBy(request => request.ActionId).ThenBy(request => request.EntityId).ToArray(),
            movePlans.ToArray(),
            results,
            deferred.ToArray(),
            Array.Empty<ActionFact>(),
            behaviorInstances.ToArray(),
            reasons.ToArray());

        void AddInvalidResults(GameWorld targetWorld, IReadOnlyList<ActionRequest> targetGroup, string reason)
        {
            for (int i = 0; i < targetGroup.Count; i++)
            {
                ActionRequest request = targetGroup[i];
                results[request.ActionId] = new MoveResult(false, request.EntityId, CurrentCoord(targetWorld, request.EntityId), Direction.None, MoveErrorCode.Blocked, reason, false, default, request.ClientTick);
            }
        }

        void Consume(IReadOnlyList<ActionRequest> targetGroup)
        {
            for (int i = 0; i < targetGroup.Count; i++)
            {
                consumed.Add(targetGroup[i].ActionId);
            }
        }

        Dictionary<long, MoveResult> BuildSuccessResults(GameWorld targetWorld, IReadOnlyList<ActionRequest> targetGroup, MovePlan targetPlan, RotatePivotDirection rotateDirection)
        {
            var targetResults = new Dictionary<long, MoveResult>();
            Direction resultDirection = rotateDirection == RotatePivotDirection.Clockwise ? Direction.Right : Direction.Left;
            for (int i = 0; i < targetGroup.Count; i++)
            {
                ActionRequest request = targetGroup[i];
                GridCoord final = targetPlan.Members.FirstOrDefault(member => member.EntityId == request.EntityId).To;
                if (final == default)
                {
                    final = CurrentCoord(targetWorld, request.EntityId);
                }

                targetResults[request.ActionId] = new MoveResult(true, request.EntityId, final, resultDirection, MoveErrorCode.None, string.Empty, false, default, request.ClientTick);
            }

            return targetResults;
        }

        Dictionary<long, MoveResult> BuildDeferredResults(GameWorld targetWorld, IReadOnlyList<ActionRequest> targetGroup, RotatePivotDirection rotateDirection, long blockerEntityId)
        {
            var targetResults = new Dictionary<long, MoveResult>();
            Direction resultDirection = rotateDirection == RotatePivotDirection.Clockwise ? Direction.Right : Direction.Left;
            for (int i = 0; i < targetGroup.Count; i++)
            {
                ActionRequest request = targetGroup[i];
                targetResults[request.ActionId] = new MoveResult(true, request.EntityId, CurrentCoord(targetWorld, request.EntityId), resultDirection, MoveErrorCode.None, "rotate-pivot-deferred-output", false, new CollisionInfo(blockerEntityId, true, false), request.ClientTick);
            }

            return targetResults;
        }
    }

    private static IReadOnlyList<ActionFact> BuildRotateSuccessFacts(GameWorld world, MovePlan plan, long pivotEntityId, GridCoord pivotCoord, RotatePivotDirection direction, int rotateCostTicks, long startTick, long endTick)
    {
        return new[] { CreateRotateFact(ActionFactType.RotateStarted, PresentationFactType.RotatePivotGroup, world, plan, pivotEntityId, pivotCoord, direction, PresentationFactResultKind.Success, Array.Empty<PresentationFactImpact>(), startTick, 0, endTick, 1d, Math.Max(1, rotateCostTicks)) };
    }

    private static IReadOnlyList<ActionFact> BuildInternalCompletionFacts(IReadOnlyList<ActionRequest> group, IReadOnlyList<long> subjectIds)
    {
        var result = new ActionFact[group.Count];
        for (int i = 0; i < group.Count; i++)
        {
            result[i] = ActionFact.Internal(ActionFactType.RotateCompleted, group[i].ActionId, subjectIds);
        }

        return result;
    }

    private static BehaviorClaimSet ReservationFor(MovePlan plan, IReadOnlyList<long> subjectIds, BehaviorIncomingPolicy policy)
    {
        var cells = new List<GridCoord>();
        for (int i = 0; i < plan.Members.Count; i++)
        {
            BodyMember member = plan.Members[i];
            cells.Add(member.From);
            if (member.To != member.From)
            {
                cells.Add(member.To);
            }
        }

        return new BehaviorClaimSet(subjectIds, cells, Array.Empty<ResourceKey>(), policy);
    }

    private static IReadOnlyList<ActionFact> BuildRotateBounceFacts(GameWorld world, MovePlan plan, long pivotEntityId, GridCoord pivotCoord, RotatePivotDirection direction, IReadOnlyList<ExternalPushContact> contacts, bool impactOnly, long startTick, long contactTick, long endTick, double contactProgress, int effectiveCostTicks)
    {
        var facts = new List<ActionFact>();
        if (!impactOnly)
        {
            facts.Add(CreateRotateFact(ActionFactType.RotateStarted, PresentationFactType.RotatePivotGroup, world, plan, pivotEntityId, pivotCoord, direction, PresentationFactResultKind.Bounce, BuildImpacts(contacts), startTick, contactTick, endTick, contactProgress, effectiveCostTicks));
            return facts;
        }

        var blockerFeedback = new HashSet<long>();
        for (int i = 0; i < contacts.Count; i++)
        {
            ExternalPushContact contact = contacts[i];
            if (!blockerFeedback.Add(contact.BlockerEntityId))
            {
                continue;
            }

            Direction feedbackDirection = contact.PushDirection == Direction.None ? DirectionFromDelta(contact.FromCoord, contact.ToCoord) : contact.PushDirection;
            facts.Add(ActionFact.WithProjection(
                ActionFactType.RotateContacted,
                contactTick,
                PresentationFactType.RotatePivotImpact,
                PresentationFactResultKind.Impact,
                plan.SourceActionId,
                0,
                pivotEntityId,
                new[] { contact.BlockerEntityId },
                contact.FromCoord,
                contact.ToCoord,
                feedbackDirection,
                startTick,
                contactTick,
                endTick,
                contactProgress,
                effectiveCostTicks,
                pivotEntityId,
                pivotCoord,
                direction,
                Array.Empty<PresentationFactMember>(),
                new[] { new PresentationFactImpact(contact.BlockerEntityId, contact.SourceEntityId, contact.FromCoord, contact.ToCoord, feedbackDirection) }));
        }

        return facts;
    }

    private static ActionFact CreateRotateFact(ActionFactType factType, PresentationFactType projectionFactType, GameWorld world, MovePlan plan, long pivotEntityId, GridCoord pivotCoord, RotatePivotDirection direction, PresentationFactResultKind resultKind, IReadOnlyList<PresentationFactImpact> impacts, long startTick, long contactTick, long endTick, double contactProgress, int effectiveCostTicks)
    {
        var members = new PresentationFactMember[plan.Members.Count];
        for (int i = 0; i < plan.Members.Count; i++)
        {
            BodyMember member = plan.Members[i];
            members[i] = new PresentationFactMember(member.EntityId, member.From, member.To, FromDirection(world, member.EntityId), member.Direction, FromPortLocalPorts(world, member.EntityId), DirectionMask.None);
        }

        GridCoord from = plan.Members.Count == 0 ? default : plan.Members[0].From;
        GridCoord to = plan.Members.Count == 0 ? default : plan.Members[0].To;
        return ActionFact.WithProjection(factType, plan.ServerTick, projectionFactType, resultKind, plan.SourceActionId, 0, pivotEntityId, SubjectIds(plan), from, to, Direction.None, startTick, contactTick, endTick, contactProgress, effectiveCostTicks, pivotEntityId, pivotCoord, direction, members, impacts);
    }

    private static IReadOnlyList<PresentationFactImpact> BuildImpacts(IReadOnlyList<ExternalPushContact> contacts)
    {
        if (contacts.Count == 0)
        {
            return Array.Empty<PresentationFactImpact>();
        }

        var result = new PresentationFactImpact[contacts.Count];
        for (int i = 0; i < contacts.Count; i++)
        {
            ExternalPushContact contact = contacts[i];
            Direction pushDirection = contact.PushDirection == Direction.None ? DirectionFromDelta(contact.FromCoord, contact.ToCoord) : contact.PushDirection;
            result[i] = new PresentationFactImpact(contact.BlockerEntityId, contact.SourceEntityId, contact.FromCoord, contact.ToCoord, pushDirection);
        }

        return result;
    }

    private static IReadOnlyList<long> SubjectIds(MovePlan plan)
    {
        var subjectIds = new long[plan.Members.Count];
        for (int i = 0; i < plan.Members.Count; i++)
        {
            subjectIds[i] = plan.Members[i].EntityId;
        }

        return subjectIds;
    }

    private static IReadOnlyList<long> BodyIds(BehaviorBody body)
    {
        var ids = new long[body.Entities.Count];
        for (int i = 0; i < body.Entities.Count; i++)
        {
            ids[i] = body.Entities[i].EntityId;
        }

        return ids;
    }

    private static Direction FromDirection(GameWorld world, long entityId)
    {
        return world.TryGetEntity(entityId, out GameEntity entity) && world.TryGetComponent(entity, out DirectionComponent direction)
            ? direction.Direction
            : Direction.None;
    }

    private static DirectionMask FromPortLocalPorts(GameWorld world, long entityId)
    {
        return world.TryGetEntity(entityId, out GameEntity entity) && world.TryGetComponent(entity, out PortConnectorComponent connector)
            ? connector.LocalPorts
            : DirectionMask.None;
    }

    private bool IsPushContribution(ActionSpec spec, ActionRequest request)
    {
        BlockedResultPolicy policy = registry.GetBlockedResultPolicy(spec.BlockedResultPolicyId);
        return spec.Targeting.SelectorId.Equals(new TargetSelectorId("direction_cell")) &&
            spec.AllowsConnectedBodySubject &&
            request.Target.Direction != Direction.None &&
            HasDeriveActionBranch(policy);
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

    private bool TryResolveGroupBody(GameWorld world, IReadOnlyList<ActionRequest> group, out BehaviorBody body)
    {
        body = null!;
        if (group.Count == 0 ||
            !world.TryGetEntity(group[0].EntityId, out GameEntity entity))
        {
            return false;
        }

        return bodyResolver.TryResolve(world, entity, out body, out _);
    }

    private static int CountPivots(GameWorld world, BehaviorBody body, out GameEntity pivot, out GridCoord pivotCoord)
    {
        int count = 0;
        pivot = null!;
        pivotCoord = default;
        for (int i = 0; i < body.Entities.Count; i++)
        {
            GameEntity member = body.Entities[i];
            if (!world.HasComponent<RotatePivotComponent>(member))
            {
                continue;
            }

            count++;
            pivot = member;
            world.TryGetComponent(member, out PositionComponent position);
            pivotCoord = position.Coord;
        }

        return count;
    }

    private static RotatePivotDirection ResolveTorque(GameWorld world, IReadOnlyList<ActionRequest> group, GridCoord pivotCoord, out bool hasTorque)
    {
        int clockwise = 0;
        int counterClockwise = 0;
        hasTorque = false;
        for (int i = 0; i < group.Count; i++)
        {
            ActionRequest request = group[i];
            if (!world.TryGetEntity(request.EntityId, out GameEntity entity) ||
                !world.TryGetComponent(entity, out PositionComponent position))
            {
                continue;
            }

            int rx = position.Coord.X - pivotCoord.X;
            int ry = position.Coord.Y - pivotCoord.Y;
            DirectionVector(request.Target.Direction, out int dx, out int dy);
            int cross = rx * dy - ry * dx;
            if (cross < 0)
            {
                hasTorque = true;
                clockwise += request.DeferredContributionCount;
            }
            else if (cross > 0)
            {
                hasTorque = true;
                counterClockwise += request.DeferredContributionCount;
            }
        }

        if (clockwise == counterClockwise)
        {
            return RotatePivotDirection.None;
        }

        return clockwise > counterClockwise ? RotatePivotDirection.Clockwise : RotatePivotDirection.CounterClockwise;
    }

    private bool TryBuildPlan(GameWorld world, BehaviorBody body, GameEntity pivot, GridCoord pivotCoord, RotatePivotDirection direction, ActionRequest request, long serverTick, out MovePlan plan, out IReadOnlyList<ExternalPushContact> contacts, out string reason)
    {
        var bodyIds = new HashSet<long>(body.Entities.Select(entity => entity.EntityId));
        var foundContacts = new List<ExternalPushContact>();
        plan = null!;
        contacts = Array.Empty<ExternalPushContact>();
        reason = string.Empty;

        if (!sweepPlanner.TryPlan(world, body, pivot, pivotCoord, direction, out IReadOnlyList<BodyMember> members, out IReadOnlyList<RotateSweepCell> sweptCells, out reason))
        {
            return false;
        }

        for (int i = 0; i < sweptCells.Count; i++)
        {
            RotateSweepCell sweptCell = sweptCells[i];
            if (!world.TryGetFirstBlockingAt(sweptCell.Coord, bodyIds, out BlockingSpatialQueryResult blocker))
            {
                continue;
            }

            foundContacts.Add(new ExternalPushContact(blocker.EntityId, sweptCell.EntityId, sweptCell.From, sweptCell.Coord, request.ActionId, sweptCell.PushDirection, sweptCell.Progress, sweptCell.SampleOrder));
        }

        plan = new MovePlan(request.Priority, request.ActionId, request.Source.SourceStateId, request.EntityId, serverTick, body.BodyId, body.Kind, request.Target.Direction, members);
        contacts = SelectEarliestContacts(foundContacts);
        return true;
    }

    private static IReadOnlyList<ExternalPushContact> SelectEarliestContacts(IReadOnlyList<ExternalPushContact> contacts)
    {
        if (contacts.Count == 0)
        {
            return Array.Empty<ExternalPushContact>();
        }

        double earliest = contacts.Min(contact => contact.Progress);
        return contacts
            .Where(contact => Math.Abs(contact.Progress - earliest) < 0.000001d)
            .OrderBy(contact => contact.SampleOrder)
            .ThenBy(contact => contact.SourceEntityId)
            .ThenBy(contact => contact.BlockerEntityId)
            .ThenBy(contact => contact.ToCoord.X)
            .ThenBy(contact => contact.ToCoord.Y)
            .ToArray();
    }

    private static RotateBounceTiming CalculateBounceTiming(long startTick, int baseCostTicks, double contactProgress)
    {
        int outTicks = Math.Max(1, (int)Math.Ceiling(Math.Max(0.000001d, contactProgress) * Math.Max(1, baseCostTicks)));
        int effective = outTicks + outTicks;
        return new RotateBounceTiming(Math.Min(1d, Math.Max(0d, contactProgress)), startTick + outTicks, startTick + effective, outTicks, effective);
    }

    private bool TryBuildDeferred(GameWorld world, ActionRequest request, ActionSpec spec, IReadOnlyList<ExternalPushContact> contacts, long pivotEntityId, RotatePivotDirection rotateDirection, long releaseTick, out IReadOnlyList<DeferredAction> deferredActions, out string reason)
    {
        var result = new List<DeferredAction>();
        var seenSubjects = new HashSet<string>();
        deferredActions = Array.Empty<DeferredAction>();
        reason = string.Empty;
        if (!spec.Handoff.IsEnabled)
        {
            reason = "rotate-pivot-handoff-disabled";
            return false;
        }

        for (int i = 0; i < contacts.Count; i++)
        {
            if (!world.TryGetEntity(contacts[i].BlockerEntityId, out GameEntity blocker))
            {
                reason = "entity not found";
                return false;
            }

            if (!bodyCapabilities.CanPushEntry(world, blocker))
            {
                reason = "rotate-pivot-blocked";
                return false;
            }

            IReadOnlyList<long> subjectIds = ResolveSubjectIds(world, spec.Handoff.SubjectKind, blocker);
            if (!seenSubjects.Add(BlockedOutcomeUtility.BuildSubjectKey(blocker.EntityId, subjectIds)))
            {
                continue;
            }

            Direction outputDirection = contacts[i].PushDirection == Direction.None
                ? DirectionFromDelta(contacts[i].FromCoord, contacts[i].ToCoord)
                : contacts[i].PushDirection;
            var context = new PushOriginContext(
                PushOriginKind.RotatePivotImpact,
                contacts[i].SourceEntityId,
                contacts[i].FromCoord,
                contacts[i].ToCoord,
                contacts[i].BlockerEntityId,
                pivotEntityId,
                rotateDirection == RotatePivotDirection.Clockwise ? Direction.Right : Direction.Left,
                request.OwnerActionId);
            result.Add(new DeferredAction(spec.Handoff.SpecId, blocker.EntityId, subjectIds, outputDirection, releaseTick, releaseTick, spec.DefaultCostTicks, request.OwnerActionId, BlockedOutcomeUtility.BuildDeferredDedupeKey(request, blocker.EntityId, subjectIds, outputDirection, releaseTick), new[] { context }));
        }

        deferredActions = result;
        return result.Count != 0;
    }

    private IReadOnlyList<long> ResolveSubjectIds(GameWorld world, ActionSubjectKind subjectKind, GameEntity blocker)
    {
        if (subjectKind != ActionSubjectKind.ConnectedBodyIfAny ||
            !bodyResolver.TryResolve(world, blocker, out BehaviorBody body, out _) ||
            body.Kind != BehaviorBodyKind.PortConnected)
        {
            return new[] { blocker.EntityId };
        }

        return body.Entities.Select(entity => entity.EntityId).ToArray();
    }

    private static void DirectionVector(Direction direction, out int x, out int y)
    {
        x = direction == Direction.Left ? -1 : direction == Direction.Right ? 1 : 0;
        y = direction == Direction.Down ? -1 : direction == Direction.Up ? 1 : 0;
    }

    private static Direction DirectionFromDelta(GridCoord from, GridCoord to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx < 0 ? Direction.Left : dx > 0 ? Direction.Right : Direction.None;
        }

        return dy < 0 ? Direction.Down : Direction.Up;
    }

    private static string BuildBodyKey(BehaviorBody body)
    {
        return string.Join("|", body.Entities.Select(entity => entity.EntityId).OrderBy(id => id));
    }

    private static GridCoord CurrentCoord(GameWorld world, long entityId)
    {
        return world.TryGetEntity(entityId, out GameEntity entity) &&
            world.TryGetComponent(entity, out PositionComponent position)
            ? position.Coord
            : default;
    }
}

public readonly struct RotateBounceTiming
{
    public RotateBounceTiming(double contactProgress, long contactTick, long endTick, int outTicks, int effectiveCostTicks)
    {
        ContactProgress = contactProgress;
        ContactTick = contactTick;
        EndTick = endTick;
        OutTicks = outTicks;
        EffectiveCostTicks = effectiveCostTicks;
    }

    public double ContactProgress { get; }
    public long ContactTick { get; }
    public long EndTick { get; }
    public int OutTicks { get; }
    public int EffectiveCostTicks { get; }
}

public readonly struct RotateSweepCell
{
    public RotateSweepCell(long entityId, GridCoord from, GridCoord coord, Direction pushDirection, double progress, int sampleOrder)
    {
        EntityId = entityId;
        From = from;
        Coord = coord;
        PushDirection = pushDirection;
        Progress = progress;
        SampleOrder = sampleOrder;
    }

    public long EntityId { get; }
    public GridCoord From { get; }
    public GridCoord Coord { get; }
    public Direction PushDirection { get; }
    public double Progress { get; }
    public int SampleOrder { get; }
}

internal readonly struct RotateSweepStep
{
    public RotateSweepStep(GridCoord coord, Direction pushDirection, int order)
    {
        Coord = coord;
        PushDirection = pushDirection;
        Order = order;
    }

    public GridCoord Coord { get; }
    public Direction PushDirection { get; }
    public int Order { get; }
}

public sealed class RotateSweepPlanner
{
    private const int TrigScale = 4096;
    private static readonly int[] SinQuarter = CreateSinQuarter();

    public bool TryPlan(GameWorld world, BehaviorBody body, GameEntity pivot, GridCoord pivotCoord, RotatePivotDirection direction, out IReadOnlyList<BodyMember> members, out IReadOnlyList<RotateSweepCell> sweptCells, out string reason)
    {
        var plannedMembers = new List<BodyMember>(body.Entities.Count);
        var targetCells = new HashSet<GridCoord>();
        var bodyCells = new HashSet<GridCoord>();
        var swept = new List<RotateSweepCell>();
        members = Array.Empty<BodyMember>();
        sweptCells = Array.Empty<RotateSweepCell>();
        reason = string.Empty;

        for (int i = 0; i < body.Entities.Count; i++)
        {
            GameEntity member = body.Entities[i];
            if (!world.TryGetComponent(member, out PositionComponent position))
            {
                reason = "missing position";
                return false;
            }

            bodyCells.Add(position.Coord);
            GridCoord target = member.EntityId == pivot.EntityId ? position.Coord : Rotate(position.Coord, pivotCoord, direction);
            if (!targetCells.Add(target))
            {
                reason = "rotate-pivot-duplicate-target";
                return false;
            }

            plannedMembers.Add(new BodyMember(member.EntityId, position.Coord, target, RotateDirection(world, member, direction, member.EntityId == pivot.EntityId)));
        }

        for (int i = 0; i < plannedMembers.Count; i++)
        {
            BodyMember member = plannedMembers[i];
            if (member.From.Equals(member.To))
            {
                continue;
            }

            IReadOnlyList<RotateSweepStep> memberSwept = BuildSweptCells(pivotCoord, member.From, direction);
            for (int cellIndex = 0; cellIndex < memberSwept.Count; cellIndex++)
            {
                RotateSweepStep step = memberSwept[cellIndex];
                GridCoord coord = step.Coord;
                if (coord.Equals(member.From) ||
                    bodyCells.Contains(coord))
                {
                    continue;
                }

                swept.Add(new RotateSweepCell(member.EntityId, member.From, coord, step.PushDirection, step.Order / 90d, step.Order));
            }
        }

        members = plannedMembers;
        sweptCells = swept
            .OrderBy(cell => cell.SampleOrder)
            .ThenBy(cell => cell.EntityId)
            .ThenBy(cell => cell.Coord.X)
            .ThenBy(cell => cell.Coord.Y)
            .ThenBy(cell => cell.EntityId)
            .ToArray();
        return true;
    }

    internal static IReadOnlyList<RotateSweepStep> BuildSweptCells(GridCoord pivot, GridCoord from, RotatePivotDirection direction)
    {
        int rx = from.X - pivot.X;
        int ry = from.Y - pivot.Y;
        int radiusSquared = rx * rx + ry * ry;
        if (radiusSquared == 0)
        {
            return Array.Empty<RotateSweepStep>();
        }

        int steps = 90;
        var byCell = new Dictionary<GridCoord, RotateSweepStep>();
        GridCoord previous = from;
        for (int i = 1; i <= steps; i++)
        {
            GridCoord coord = SampleQuarterCircle(pivot, rx, ry, direction, i, steps);
            if (coord.Equals(previous))
            {
                continue;
            }

            Direction pushDirection = DirectionFromDelta(previous, coord);
            if (pushDirection == Direction.None)
            {
                pushDirection = TangentDirection(coord.X - pivot.X, coord.Y - pivot.Y, direction, Direction.None);
            }

            if (!byCell.ContainsKey(coord))
            {
                byCell.Add(coord, new RotateSweepStep(coord, pushDirection, i));
            }

            previous = coord;
        }

        AddInitialTangentCell(pivot, from, direction, byCell);

        GridCoord target = Rotate(from, pivot, direction);
        if (!byCell.ContainsKey(target))
        {
            Direction pushDirection = DirectionFromDelta(previous, target);
            if (pushDirection == Direction.None)
            {
                pushDirection = TangentDirection(target.X - pivot.X, target.Y - pivot.Y, direction, Direction.None);
            }

            byCell.Add(target, new RotateSweepStep(target, pushDirection, steps));
        }

        return byCell.Values
            .OrderBy(step => step.Order)
            .ThenBy(step => step.Coord.X)
            .ThenBy(step => step.Coord.Y)
            .ToArray();
    }

    private static GridCoord SampleQuarterCircle(GridCoord pivot, int rx, int ry, RotatePivotDirection direction, int step, int totalSteps)
    {
        int degree = DivideRounded(step * 90, totalSteps);
        int cos = SinQuarter[90 - degree];
        int sin = SinQuarter[degree];
        int x;
        int y;
        if (direction == RotatePivotDirection.Clockwise)
        {
            x = DivideRounded(rx * cos + ry * sin, TrigScale);
            y = DivideRounded(ry * cos - rx * sin, TrigScale);
        }
        else
        {
            x = DivideRounded(rx * cos - ry * sin, TrigScale);
            y = DivideRounded(ry * cos + rx * sin, TrigScale);
        }

        GridCoord target = Rotate(new GridCoord(pivot.X + rx, pivot.Y + ry), pivot, direction);
        if (step == totalSteps)
        {
            return target;
        }

        return new GridCoord(pivot.X + x, pivot.Y + y);
    }

    private static void AddInitialTangentCell(GridCoord pivot, GridCoord from, RotatePivotDirection direction, Dictionary<GridCoord, RotateSweepStep> byCell)
    {
        Direction pushDirection = TangentDirection(from.X - pivot.X, from.Y - pivot.Y, direction, Direction.None);
        if (!TryStep(from, pushDirection, out GridCoord tangentCell) ||
            byCell.ContainsKey(tangentCell))
        {
            return;
        }

        byCell.Add(tangentCell, new RotateSweepStep(tangentCell, pushDirection, 0));
    }

    private static bool TryStep(GridCoord from, Direction direction, out GridCoord to)
    {
        switch (direction)
        {
            case Direction.Up:
                to = new GridCoord(from.X, from.Y + 1);
                return true;
            case Direction.Down:
                to = new GridCoord(from.X, from.Y - 1);
                return true;
            case Direction.Left:
                to = new GridCoord(from.X - 1, from.Y);
                return true;
            case Direction.Right:
                to = new GridCoord(from.X + 1, from.Y);
                return true;
            default:
                to = from;
                return false;
        }
    }

    private static int DivideRounded(int value, int divisor)
    {
        return value >= 0
            ? (value + divisor / 2) / divisor
            : (value - divisor / 2) / divisor;
    }

    private static Direction TangentDirection(int rx, int ry, RotatePivotDirection direction, Direction fallback)
    {
        int tx;
        int ty;
        if (direction == RotatePivotDirection.Clockwise)
        {
            tx = ry;
            ty = -rx;
        }
        else
        {
            tx = -ry;
            ty = rx;
        }

        if (Math.Abs(tx) == Math.Abs(ty) && fallback != Direction.None)
        {
            return fallback;
        }

        if (Math.Abs(tx) > Math.Abs(ty))
        {
            return tx < 0 ? Direction.Left : Direction.Right;
        }

        if (Math.Abs(ty) > 0)
        {
            return ty < 0 ? Direction.Down : Direction.Up;
        }

        return fallback;
    }

    private static Direction DirectionFromDelta(GridCoord from, GridCoord to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            return dx < 0 ? Direction.Left : dx > 0 ? Direction.Right : Direction.None;
        }

        return dy < 0 ? Direction.Down : dy > 0 ? Direction.Up : Direction.None;
    }

    private static int[] CreateSinQuarter()
    {
        return new[]
        {
            0, 71, 143, 214, 286, 357, 428, 499, 570, 641, 711, 782, 852, 921, 991, 1060, 1129, 1198, 1266, 1334, 1401, 1468, 1534, 1600, 1666, 1731, 1796, 1860, 1923, 1986, 2048, 2110, 2171, 2231, 2290, 2349, 2408, 2465, 2522, 2578, 2633, 2687, 2741, 2793, 2845, 2896, 2946, 2996, 3044, 3091, 3138, 3183, 3228, 3271, 3314, 3355, 3396, 3435, 3474, 3511, 3547, 3582, 3617, 3650, 3681, 3712, 3742, 3770, 3798, 3824, 3849, 3873, 3896, 3917, 3937, 3956, 3974, 3991, 4006, 4021, 4034, 4046, 4056, 4065, 4074, 4080, 4086, 4090, 4094, 4095, 4096
        };
    }

    private static GridCoord Rotate(GridCoord coord, GridCoord pivot, RotatePivotDirection direction)
    {
        int x = coord.X - pivot.X;
        int y = coord.Y - pivot.Y;
        return direction == RotatePivotDirection.Clockwise
            ? new GridCoord(pivot.X + y, pivot.Y - x)
            : new GridCoord(pivot.X - y, pivot.Y + x);
    }

    private static Direction RotateDirection(GameWorld world, GameEntity entity, RotatePivotDirection direction, bool defaultRight)
    {
        Direction currentDirection = world.TryGetComponent(entity, out DirectionComponent current) ? current.Direction : Direction.None;
        if (currentDirection == Direction.None && defaultRight)
        {
            currentDirection = Direction.Right;
        }

        if (currentDirection == Direction.None)
        {
            return Direction.None;
        }

        return direction == RotatePivotDirection.Clockwise
            ? RotateClockwise(currentDirection)
            : RotateCounterClockwise(currentDirection);
    }

    private static Direction RotateClockwise(Direction direction)
    {
        return direction switch
        {
            Direction.Left => Direction.Up,
            Direction.Up => Direction.Right,
            Direction.Right => Direction.Down,
            Direction.Down => Direction.Left,
            _ => Direction.None
        };
    }

    private static Direction RotateCounterClockwise(Direction direction)
    {
        return direction switch
        {
            Direction.Left => Direction.Down,
            Direction.Down => Direction.Right,
            Direction.Right => Direction.Up,
            Direction.Up => Direction.Left,
            _ => Direction.None
        };
    }
}
}
