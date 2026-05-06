using Fantasy.Async;
using Fantasy.Network;
using DG.GameCore;
using System.Linq;

namespace Fantasy;

public sealed class AuthoritativeWorldTickRunner
{
    private readonly GameWorld World;
    private readonly AuthoritativeInputQueue InputQueue;
    private readonly AuthoritativeWorldSyncSystem SyncSystem;
    private readonly WorldActionQueue ActionQueue;
    private readonly PendingRuleStateStore PendingStates = new();
    private readonly StateDrivenRuleExecutionSystem RuleExecutionSystem = new();
    private readonly int tickIntervalMs;
    private FCancellationToken? cancellationToken;
    private bool running;

    public AuthoritativeWorldTickRunner(GameWorld world, AuthoritativeInputQueue inputQueue, AuthoritativeWorldSyncSystem syncSystem, int tickIntervalMs)
    {
        World = world;
        InputQueue = inputQueue;
        SyncSystem = syncSystem;
        ActionQueue = inputQueue.ActionQueue;
        this.tickIntervalMs = tickIntervalMs;
    }

    public bool Start(Scene scene, Func<IReadOnlyList<Session>> observerProvider)
    {
        if (running)
        {
            return false;
        }

        running = true;
        cancellationToken = FCancellationToken.ToKen;
        Run(scene, observerProvider, cancellationToken).Coroutine();
        return true;
    }

    public void Stop()
    {
        cancellationToken?.Cancel();
        cancellationToken?.Dispose();
        cancellationToken = null;
        running = false;
        InputQueue.FailPending("tick runner stopped");
    }

    public int ActivePendingStateCount => PendingStates.ActiveCount;

    public WorldDelta Tick(IReadOnlyList<Session> observers)
    {
        long serverTick = World.NextTick();
        IReadOnlyList<AuthoritativeMoveInput> inputs = InputQueue.DrainMoves();
        var inputActions = new Dictionary<long, AuthoritativeMoveInput>();
        for (int i = 0; i < inputs.Count; i++)
        {
            AuthoritativeMoveInput input = inputs[i];
            WorldAction action = ActionQueue.EnqueuePlayerMove(input.EntityId, input.TargetCoord, input.ClientTick);
            inputActions[action.ActionId] = input;
        }

        EnqueueAutoMoveActions(serverTick);
        EnqueueMechanismPushActions(serverTick);
        IReadOnlyList<WorldAction> actions = ActionQueue.DrainReady(serverTick);
        StateDrivenRuleExecutionResult ruleResult = RuleExecutionSystem.Tick(World, actions, PendingStates, serverTick);
        foreach (KeyValuePair<long, AuthoritativeMoveInput> pair in inputActions)
        {
            if (ruleResult.ActionResults.TryGetValue(pair.Key, out MoveResult result))
            {
                pair.Value.Complete(result);
            }
            else
            {
                pair.Value.Complete(new MoveResult(false, pair.Value.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "action not resolved", false, default, pair.Value.ClientTick));
            }
        }
        IReadOnlyList<AuthoritativeDebugActionInput> debugInputs = InputQueue.DrainDebugInputs(actions.Select(action => action.ActionId).ToArray());
        for (int i = 0; i < debugInputs.Count; i++)
        {
            AuthoritativeDebugActionInput input = debugInputs[i];
            if (ruleResult.ActionResults.TryGetValue(input.Action.ActionId, out MoveResult result))
            {
                input.Complete(result);
            }
            else
            {
                input.Complete(new MoveResult(false, input.Action.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "action not resolved", false, default, input.Action.ClientTick));
            }
        }
        return SyncSystem.BroadcastDelta(observers);
    }

    public WorldDelta Tick()
    {
        return Tick(Array.Empty<Session>());
    }

    private async FTask Run(Scene scene, Func<IReadOnlyList<Session>> observerProvider, FCancellationToken token)
    {
        while (!token.IsCancel && running)
        {
            bool completed = await FTask.Wait(scene, tickIntervalMs, token);
            if (!completed)
            {
                break;
            }

            Tick(observerProvider());
        }
    }

    private void EnqueueAutoMoveActions(long serverTick)
    {
        IReadOnlyList<GameEntity> entities = World.EnumerateEntities();
        for (int i = 0; i < entities.Count; i++)
        {
            GameEntity entity = entities[i];
            if (!World.TryGetComponent(entity, out PositionComponent _) ||
                !World.TryGetComponent(entity, out DirectionComponent _) ||
                !World.TryGetComponent(entity, out AutoMoveComponent autoMove))
            {
                continue;
            }

            if (serverTick - autoMove.LastMoveTick < autoMove.IntervalTicks)
            {
                continue;
            }

            ActionQueue.EnqueueAutoMove(entity.EntityId, serverTick - 1, 1);
        }
    }

    private void EnqueueMechanismPushActions(long serverTick)
    {
        var moved = new HashSet<long>();
        IReadOnlyList<GameEntity> triggers = World.EnumerateEntities();
        for (int i = 0; i < triggers.Count; i++)
        {
            GameEntity trigger = triggers[i];
            if (!World.TryGetComponent(trigger, out PositionComponent triggerPosition) ||
                !World.TryGetComponent(trigger, out DirectionComponent triggerDirection) ||
                !World.HasComponent<PushOnEnterComponent>(trigger))
            {
                continue;
            }

            IReadOnlyList<GameEntity> targets = World.GetEntitiesAt(triggerPosition.Coord);
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                GameEntity target = targets[targetIndex];
                if (target.EntityId == trigger.EntityId ||
                    moved.Contains(target.EntityId) ||
                    !World.TryGetComponent(target, out PositionComponent _))
                {
                    continue;
                }

                moved.Add(target.EntityId);
                ActionQueue.EnqueueMechanismPush(target.EntityId, triggerDirection.Direction, serverTick - 1, 1);
            }
        }
    }
}
