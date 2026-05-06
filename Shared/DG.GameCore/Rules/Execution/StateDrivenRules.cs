using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class StateDrivenRuleExecutionSystem
{
    private readonly CommitResolver commitResolver = new();
    private readonly IntentArbiter intentArbiter = new();
    private readonly RulePlanner rulePlanner = new();
    private readonly ConflictResolver conflictResolver = new();

    public StateDrivenRuleExecutionResult Tick(GameWorld world, IReadOnlyList<WorldAction> actions, PendingRuleStateStore pendingStates, long serverTick)
    {
        var proposals = new List<CommitProposal>();
        var intents = new List<BehaviorIntent>();
        var actionResults = new Dictionary<long, MoveResult>();
        var reasons = new List<string>();

        for (int i = 0; i < actions.Count; i++)
        {
            WorldAction action = actions[i];
            if (action.Kind == WorldActionKind.PlayerMove)
            {
                ProcessPlayerMove(world, action, pendingStates, intents, actionResults, reasons, serverTick);
                continue;
            }

            if (action.Kind == WorldActionKind.DebugMove)
            {
                ProcessDebugMove(world, action, intents, actionResults, reasons, serverTick);
                continue;
            }

            if (action.Kind == WorldActionKind.DebugRemove)
            {
                ProcessDebugRemove(world, action, pendingStates, proposals, actionResults, reasons, serverTick);
                continue;
            }

            if (action.Kind == WorldActionKind.DebugSpawn)
            {
                ProcessDebugSpawn(world, action, proposals, actionResults, reasons, serverTick);
                continue;
            }

            if (action.Kind == WorldActionKind.AutoMove)
            {
                ProcessAutoMove(world, action, proposals, intents, actionResults, reasons, serverTick);
                continue;
            }

            if (action.Kind == WorldActionKind.MechanismPush)
            {
                ProcessMechanismPush(world, action, pendingStates, intents, actionResults, reasons, serverTick);
            }
        }

        AdvancePushStates(world, pendingStates, intents, reasons, serverTick);
        IntentArbitrationResult arbitrationResult = intentArbiter.Arbitrate(world, intents);
        ApplyArbitrationResultsToActions(world, actionResults, arbitrationResult.Items, reasons);
        ApplyArbitrationResultsToPendingStates(pendingStates, arbitrationResult.Items, reasons, serverTick);
        IReadOnlyList<MovePlan> movePlans = CreateMovePlans(world, pendingStates, arbitrationResult.AcceptedIntents, actionResults, reasons, serverTick);
        var proposalResults = new List<CommitProposalResult>();
        proposalResults.AddRange(commitResolver.Resolve(world, proposals));
        proposalResults.AddRange(conflictResolver.Resolve(world, movePlans));
        ApplyProposalResultsToActions(actionResults, proposalResults, reasons);
        ApplyProposalResultsToPendingStates(pendingStates, proposalResults, reasons, serverTick);
        pendingStates.CleanupInactive();
        return new StateDrivenRuleExecutionResult(actionResults, proposalResults, pendingStates.ActiveCount, reasons);
    }

    private void ProcessPlayerMove(GameWorld world, WorldAction action, PendingRuleStateStore pendingStates, List<BehaviorIntent> intents, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (!world.TryGetEntity(action.EntityId, out GameEntity entity))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "unknown entity", false, default, action.ClientTick);
            reasons.Add("unknown entity");
            return;
        }

        if (!world.TryGetComponent(entity, out PositionComponent position))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.MissingPosition, "missing position", false, default, action.ClientTick);
            reasons.Add("missing position");
            return;
        }

        GridCoord current = position.Coord;
        if (!action.TargetCoord.HasValue)
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, current, Direction.None, MoveErrorCode.InvalidDirection, "missing target", false, default, action.ClientTick);
            reasons.Add("missing target");
            return;
        }

        GridCoord target = action.TargetCoord.Value;
        if (current.ManhattanDistance(target) > 1)
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, current, Direction.None, MoveErrorCode.TooFar, "target too far", false, default, action.ClientTick);
            reasons.Add("target too far");
            return;
        }

        Direction moveDirection = DirectionFromDelta(current, target);
        if (moveDirection == Direction.None && current != target)
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, current, Direction.None, MoveErrorCode.InvalidDirection, "invalid direction", false, default, action.ClientTick);
            reasons.Add("invalid direction");
            return;
        }

        GameEntity blockingEntity = FindBlocking(world, target, action.EntityId);
        if (blockingEntity == null)
        {
            AddIntent(world, new BehaviorIntent(BehaviorIntentKind.Move, action.Priority, action.ActionId, 0, entity.EntityId, moveDirection, serverTick), intents, actionResults, action.ClientTick);
            return;
        }

        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blockingEntity);
        if (!targetPlayerControlled && moveDirection != Direction.None && world.HasComponent<PushableComponent>(blockingEntity))
        {
            if (pendingStates.HasActivePushInvolving(blockingEntity.EntityId))
            {
                actionResults[action.ActionId] = new MoveResult(false, entity.EntityId, current, moveDirection, MoveErrorCode.Blocked, "push already pending", false, new CollisionInfo(blockingEntity.EntityId, true, false), action.ClientTick);
                reasons.Add("push already pending");
                return;
            }

            pendingStates.AddPush(action.ActionId, entity.EntityId, blockingEntity.EntityId, moveDirection, serverTick);
            actionResults[action.ActionId] = new MoveResult(true, entity.EntityId, current, moveDirection, MoveErrorCode.None, "push pending", false, new CollisionInfo(blockingEntity.EntityId, true, false), action.ClientTick);
            return;
        }

        MoveErrorCode errorCode = targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked;
        string reason = targetPlayerControlled ? "occupied by player" : "blocked cell";
        actionResults[action.ActionId] = new MoveResult(false, entity.EntityId, current, moveDirection, errorCode, reason, false, new CollisionInfo(blockingEntity.EntityId, true, targetPlayerControlled), action.ClientTick);
        reasons.Add(reason);
    }

    private void ProcessDebugMove(GameWorld world, WorldAction action, List<BehaviorIntent> intents, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (!world.TryGetEntity(action.EntityId, out GameEntity entity))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "entity not found", false, default, action.ClientTick);
            reasons.Add("entity not found");
            return;
        }

        if (!world.TryGetComponent(entity, out PositionComponent position))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.MissingPosition, "missing position", false, default, action.ClientTick);
            reasons.Add("missing position");
            return;
        }

        if (!action.TargetCoord.HasValue)
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, position.Coord, Direction.None, MoveErrorCode.InvalidDirection, "missing target", false, default, action.ClientTick);
            reasons.Add("missing target");
            return;
        }

        GridCoord target = action.TargetCoord.Value;
        Direction direction = DirectionFromDelta(position.Coord, target);
        AddIntent(world, new BehaviorIntent(BehaviorIntentKind.DebugMove, action.Priority, action.ActionId, 0, entity.EntityId, direction, serverTick, target), intents, actionResults, action.ClientTick);
    }

    private static void ProcessDebugSpawn(GameWorld world, WorldAction action, List<CommitProposal> proposals, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (!action.TargetCoord.HasValue)
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.InvalidDirection, "missing target", false, default, action.ClientTick);
            reasons.Add("missing target");
            return;
        }

        proposals.Add(CommitProposal.Create(action.Priority, action.ActionId, action.EntityId, action.ConfigId, action.TargetCoord.Value, action.Direction, action.PlayerId, action.AutoMoveIntervalTicks, serverTick));
        actionResults[action.ActionId] = new MoveResult(true, action.EntityId, action.TargetCoord.Value, action.Direction, MoveErrorCode.None, string.Empty, false, default, action.ClientTick);
    }

    private static void ProcessDebugRemove(GameWorld world, WorldAction action, PendingRuleStateStore pendingStates, List<CommitProposal> proposals, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (!world.TryGetEntity(action.EntityId, out GameEntity entity))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "entity not found", false, default, action.ClientTick);
            reasons.Add("entity not found");
            return;
        }

        CancelRelatedPushStates(pendingStates, action.EntityId, serverTick);
        GridCoord coord = world.TryGetComponent(entity, out PositionComponent position) ? position.Coord : default;
        proposals.Add(CommitProposal.Delete(action.Priority, action.ActionId, action.EntityId, serverTick));
        actionResults[action.ActionId] = new MoveResult(true, action.EntityId, coord, Direction.None, MoveErrorCode.None, string.Empty, false, default, action.ClientTick);
    }

    private void ProcessAutoMove(GameWorld world, WorldAction action, List<CommitProposal> proposals, List<BehaviorIntent> intents, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (!world.TryGetEntity(action.EntityId, out GameEntity entity))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "unknown entity", false, default, action.ClientTick);
            reasons.Add("unknown entity");
            return;
        }

        if (!world.TryGetComponent(entity, out PositionComponent position) ||
            !world.TryGetComponent(entity, out DirectionComponent direction) ||
            !world.TryGetComponent(entity, out AutoMoveComponent _))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.MissingPosition, "missing auto move state", false, default, action.ClientTick);
            reasons.Add("missing auto move state");
            return;
        }

        GridCoord target = position.Coord.Add(direction.Direction);
        GameEntity blocking = FindBlocking(world, target, action.EntityId);
        proposals.Add(CommitProposal.SetAutoMoveTick(action.Priority, action.ActionId, action.EntityId, serverTick));
        if (blocking == null)
        {
            AddIntent(world, new BehaviorIntent(BehaviorIntentKind.AutoMove, action.Priority, action.ActionId, 0, action.EntityId, direction.Direction, serverTick), intents, actionResults, action.ClientTick);
            return;
        }

        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        string reason = targetPlayerControlled ? "occupied by player" : "blocked cell";
        MoveErrorCode errorCode = targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked;
        Direction finalDirection = direction.Direction;
        bool bounced = false;
        if (world.HasComponent<BouncableComponent>(entity))
        {
            finalDirection = direction.Direction.Opposite();
            bounced = true;
            proposals.Add(CommitProposal.SetDirection(action.Priority, action.ActionId, 0, action.EntityId, finalDirection, serverTick));
        }

        actionResults[action.ActionId] = new MoveResult(false, action.EntityId, position.Coord, finalDirection, errorCode, reason, bounced, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), action.ClientTick);
        reasons.Add(reason);
    }

    private void ProcessMechanismPush(GameWorld world, WorldAction action, PendingRuleStateStore pendingStates, List<BehaviorIntent> intents, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        if (!world.TryGetEntity(action.EntityId, out GameEntity entity))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "unknown entity", false, default, action.ClientTick);
            reasons.Add("unknown entity");
            return;
        }

        if (!world.TryGetComponent(entity, out PositionComponent position))
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, default, Direction.None, MoveErrorCode.MissingPosition, "missing position", false, default, action.ClientTick);
            reasons.Add("missing position");
            return;
        }

        if (action.Direction == Direction.None)
        {
            actionResults[action.ActionId] = new MoveResult(false, action.EntityId, position.Coord, Direction.None, MoveErrorCode.InvalidDirection, "invalid direction", false, default, action.ClientTick);
            reasons.Add("invalid direction");
            return;
        }

        if (world.HasComponent<PortConnectorComponent>(entity) &&
            PortConnectionSystem.CollectConnectedGroup(world, entity).Count > 1)
        {
            AddIntent(world, new BehaviorIntent(BehaviorIntentKind.MechanismPush, action.Priority, action.ActionId, 0, action.EntityId, action.Direction, serverTick), intents, actionResults, action.ClientTick);
            return;
        }

        GridCoord target = position.Coord.Add(action.Direction);
        GameEntity blocking = FindBlocking(world, target, action.EntityId);
        if (blocking == null)
        {
            AddIntent(world, new BehaviorIntent(BehaviorIntentKind.MechanismPush, action.Priority, action.ActionId, 0, action.EntityId, action.Direction, serverTick), intents, actionResults, action.ClientTick);
            return;
        }

        bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(blocking);
        if (!targetPlayerControlled && world.HasComponent<PushableComponent>(blocking))
        {
            if (pendingStates.HasActivePushInvolving(blocking.EntityId))
            {
                actionResults[action.ActionId] = new MoveResult(false, action.EntityId, position.Coord, action.Direction, MoveErrorCode.Blocked, "push already pending", false, new CollisionInfo(blocking.EntityId, true, false), action.ClientTick);
                reasons.Add("push already pending");
                return;
            }

            pendingStates.AddPush(action.ActionId, action.EntityId, blocking.EntityId, action.Direction, serverTick);
            actionResults[action.ActionId] = new MoveResult(true, action.EntityId, position.Coord, action.Direction, MoveErrorCode.None, "push pending", false, new CollisionInfo(blocking.EntityId, true, false), action.ClientTick);
            return;
        }

        MoveErrorCode errorCode = targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked;
        string reason = targetPlayerControlled ? "occupied by player" : "blocked cell";
        actionResults[action.ActionId] = new MoveResult(false, action.EntityId, position.Coord, action.Direction, errorCode, reason, false, new CollisionInfo(blocking.EntityId, true, targetPlayerControlled), action.ClientTick);
        reasons.Add(reason);
    }

    private void AdvancePushStates(GameWorld world, PendingRuleStateStore pendingStates, List<BehaviorIntent> intents, List<string> reasons, long serverTick)
    {
        IReadOnlyList<PushPropagationState> states = pendingStates.PushStates;
        for (int i = 0; i < states.Count; i++)
        {
            PushPropagationState state = states[i];
            if (state.Status != PendingRuleStatus.Active)
            {
                continue;
            }

            if (state.NextStepTick > serverTick)
            {
                continue;
            }

            if (!world.TryGetEntity(state.CurrentFrontEntityId, out GameEntity front) ||
                !world.TryGetComponent(front, out PositionComponent frontPosition))
            {
                state.Fail("push entity missing", serverTick);
                reasons.Add("push entity missing");
                continue;
            }

            if (world.HasComponent<PortConnectorComponent>(front))
            {
                AddPushIntent(state, front, intents, serverTick);
                continue;
            }

            GridCoord target = frontPosition.Coord.Add(state.Direction);
            GameEntity blocking = FindBlocking(world, target, front.EntityId);
            if (blocking == null)
            {
                AddPushIntent(state, front, intents, serverTick);
                continue;
            }

            if (state.Chain.Contains(blocking.EntityId))
            {
                state.Fail("push loop", serverTick);
                reasons.Add("push loop");
                continue;
            }

            if (!world.HasComponent<PlayerControlComponent>(blocking) &&
                world.HasComponent<PushableComponent>(blocking))
            {
                state.AddFront(blocking.EntityId, serverTick);
                continue;
            }

            state.Fail(world.HasComponent<PlayerControlComponent>(blocking) ? "push occupied by player" : "push blocked", serverTick);
            reasons.Add(state.Reason);
        }
    }

    private static void AddPushIntent(PushPropagationState state, GameEntity front, List<BehaviorIntent> intents, long serverTick)
    {
        var intent = new BehaviorIntent(BehaviorIntentKind.Push, WorldActionPriority.Player, state.OwnerActionId, state.StateId, front.EntityId, state.Direction, serverTick);
        intents.Add(intent);
    }

    private static void ApplyProposalResultsToPendingStates(PendingRuleStateStore pendingStates, IReadOnlyList<CommitProposalResult> proposalResults, List<string> reasons, long serverTick)
    {
        IReadOnlyList<PushPropagationState> states = pendingStates.PushStates;
        for (int resultIndex = 0; resultIndex < proposalResults.Count; resultIndex++)
        {
            CommitProposalResult result = proposalResults[resultIndex];
            if (result.Proposal.SourceStateId == 0)
            {
                if (!result.Accepted && !string.IsNullOrEmpty(result.Reason))
                {
                    reasons.Add(result.Reason);
                }

                continue;
            }

            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                PushPropagationState state = states[stateIndex];
                if (state.StateId != result.Proposal.SourceStateId || state.Status != PendingRuleStatus.Active)
                {
                    continue;
                }

                if (result.Accepted)
                {
                    state.MarkMoveAccepted(serverTick);
                }
                else
                {
                    state.Fail(result.Reason, serverTick);
                    reasons.Add(result.Reason);
                }
            }
        }
    }

    private static void ApplyProposalResultsToActions(Dictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, List<string> reasons)
    {
        for (int i = 0; i < proposalResults.Count; i++)
        {
            CommitProposalResult result = proposalResults[i];
            if (result.Proposal.SourceStateId != 0 ||
                !actionResults.TryGetValue(result.Proposal.SourceActionId, out MoveResult actionResult))
            {
                continue;
            }

            if (result.Accepted)
            {
                continue;
            }

            actionResults[result.Proposal.SourceActionId] = new MoveResult(false, actionResult.EntityId, result.Proposal.From, actionResult.FinalDirection, MoveErrorCode.Blocked, result.Reason, false, default, actionResult.ClientTick);
            if (!string.IsNullOrEmpty(result.Reason))
            {
                reasons.Add(result.Reason);
            }
        }
    }

    private IReadOnlyList<MovePlan> CreateMovePlans(GameWorld world, PendingRuleStateStore pendingStates, IReadOnlyList<BehaviorIntent> intents, Dictionary<long, MoveResult> actionResults, List<string> reasons, long serverTick)
    {
        var movePlans = new List<MovePlan>();
        for (int i = 0; i < intents.Count; i++)
        {
            BehaviorIntent intent = intents[i];
            if (!rulePlanner.TryPlanMove(world, intent, out MovePlan plan, out PlanResult result))
            {
                if (intent.SourceStateId != 0)
                {
                    FailPendingState(pendingStates, intent.SourceStateId, result.Message, serverTick);
                    reasons.Add(result.Message);
                    continue;
                }

                if (actionResults.TryGetValue(intent.SourceActionId, out MoveResult actionResult))
                {
                    GridCoord coord = default;
                    if (world.TryGetEntity(intent.EntityId, out GameEntity entity) &&
                        world.TryGetComponent(entity, out PositionComponent position))
                    {
                        coord = position.Coord;
                    }

                    actionResults[intent.SourceActionId] = new MoveResult(false, intent.EntityId, coord, intent.Direction, ErrorCodeFromPlanReason(result.Reason), result.Message, false, default, actionResult.ClientTick);
                }

                if (!string.IsNullOrEmpty(result.Message))
                {
                    reasons.Add(result.Message);
                }

                continue;
            }

            movePlans.Add(plan);
        }

        return movePlans;
    }

    private static void FailPendingState(PendingRuleStateStore pendingStates, long stateId, string reason, long serverTick)
    {
        IReadOnlyList<PushPropagationState> states = pendingStates.PushStates;
        for (int i = 0; i < states.Count; i++)
        {
            PushPropagationState state = states[i];
            if (state.StateId == stateId && state.Status == PendingRuleStatus.Active)
            {
                state.Fail(reason, serverTick);
                return;
            }
        }
    }

    private static void AddIntent(GameWorld world, BehaviorIntent intent, List<BehaviorIntent> intents, Dictionary<long, MoveResult> actionResults, long clientTick)
    {
        intents.Add(intent);
        GridCoord finalCoord = default;
        if (world.TryGetEntity(intent.EntityId, out GameEntity entity) &&
            world.TryGetComponent(entity, out PositionComponent position))
        {
            finalCoord = intent.TargetCoord ?? position.Coord.Add(intent.Direction);
        }

        actionResults[intent.SourceActionId] = new MoveResult(true, intent.EntityId, finalCoord, intent.Direction, MoveErrorCode.None, string.Empty, false, default, clientTick);
    }

    private static void ApplyArbitrationResultsToActions(GameWorld world, Dictionary<long, MoveResult> actionResults, IReadOnlyList<IntentArbitrationItem> items, List<string> reasons)
    {
        for (int i = 0; i < items.Count; i++)
        {
            IntentArbitrationItem item = items[i];
            if (item.Accepted ||
                item.Intent.SourceStateId != 0 ||
                !actionResults.TryGetValue(item.Intent.SourceActionId, out MoveResult actionResult))
            {
                continue;
            }

            GridCoord coord = default;
            if (world.TryGetEntity(item.Intent.EntityId, out GameEntity entity) &&
                world.TryGetComponent(entity, out PositionComponent position))
            {
                coord = position.Coord;
            }

            actionResults[item.Intent.SourceActionId] = new MoveResult(false, item.Intent.EntityId, coord, item.Intent.Direction, MoveErrorCode.Blocked, item.Message, false, default, actionResult.ClientTick);
            if (!string.IsNullOrEmpty(item.Message))
            {
                reasons.Add(item.Message);
            }
        }
    }

    private static void ApplyArbitrationResultsToPendingStates(PendingRuleStateStore pendingStates, IReadOnlyList<IntentArbitrationItem> items, List<string> reasons, long serverTick)
    {
        IReadOnlyList<PushPropagationState> states = pendingStates.PushStates;
        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            IntentArbitrationItem item = items[itemIndex];
            if (item.Accepted || item.Intent.SourceStateId == 0)
            {
                continue;
            }

            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                PushPropagationState state = states[stateIndex];
                if (state.StateId != item.Intent.SourceStateId || state.Status != PendingRuleStatus.Active)
                {
                    continue;
                }

                state.Fail(item.Message, serverTick);
                if (!string.IsNullOrEmpty(item.Message))
                {
                    reasons.Add(item.Message);
                }
            }
        }
    }

    private static MoveErrorCode ErrorCodeFromPlanReason(PlanFailureReason reason)
    {
        return reason == PlanFailureReason.OccupiedByPlayer ? MoveErrorCode.Occupied :
            reason == PlanFailureReason.InvalidDirection ? MoveErrorCode.InvalidDirection :
            reason == PlanFailureReason.MissingPosition ? MoveErrorCode.MissingPosition :
            reason == PlanFailureReason.UnknownEntity ? MoveErrorCode.UnknownEntity :
            MoveErrorCode.Blocked;
    }

    private static void CancelRelatedPushStates(PendingRuleStateStore pendingStates, long entityId, long serverTick)
    {
        IReadOnlyList<PushPropagationState> states = pendingStates.PushStates;
        for (int i = 0; i < states.Count; i++)
        {
            PushPropagationState state = states[i];
            if (state.Status == PendingRuleStatus.Active && state.Chain.Contains(entityId))
            {
                state.Cancel("related entity removed", serverTick);
            }
        }
    }

    private static Direction DirectionFromDelta(GridCoord current, GridCoord target)
    {
        if (target.X == current.X - 1 && target.Y == current.Y)
        {
            return Direction.Left;
        }

        if (target.X == current.X + 1 && target.Y == current.Y)
        {
            return Direction.Right;
        }

        if (target.X == current.X && target.Y == current.Y + 1)
        {
            return Direction.Up;
        }

        if (target.X == current.X && target.Y == current.Y - 1)
        {
            return Direction.Down;
        }

        return Direction.None;
    }

    private static GameEntity FindBlocking(GameWorld world, GridCoord coord, long ignoredEntityId)
    {
        IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(coord);
        for (int i = 0; i < targets.Count; i++)
        {
            GameEntity target = targets[i];
            if (target.EntityId == ignoredEntityId || !world.HasComponent<BlockingComponent>(target))
            {
                continue;
            }

            return target;
        }

        return null!;
    }

    private static GameEntity FindExternalBlocking(GameWorld world, GridCoord coord, HashSet<long> groupIds)
    {
        IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(coord);
        for (int i = 0; i < targets.Count; i++)
        {
            GameEntity target = targets[i];
            if (groupIds.Contains(target.EntityId) || !world.HasComponent<BlockingComponent>(target))
            {
                continue;
            }

            return target;
        }

        return null!;
    }
}

}
