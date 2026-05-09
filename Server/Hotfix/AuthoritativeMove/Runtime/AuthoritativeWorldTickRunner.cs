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
    private readonly StateDrivenRuleExecutionSystem RuleExecutionSystem;
    private readonly Dictionary<long, AuthoritativeMoveInput> pendingMoveInputs = new();
    private readonly int tickIntervalMs;
    private FCancellationToken? cancellationToken;
    private bool running;

    public AuthoritativeWorldTickRunner(GameWorld world, AuthoritativeInputQueue inputQueue, AuthoritativeWorldSyncSystem syncSystem, int tickIntervalMs)
        : this(world, inputQueue, syncSystem, tickIntervalMs, ActionSpecRegistry.Default)
    {
    }

    public AuthoritativeWorldTickRunner(GameWorld world, AuthoritativeInputQueue inputQueue, AuthoritativeWorldSyncSystem syncSystem, int tickIntervalMs, ActionSpecRegistry actionSpecs)
    {
        World = world;
        InputQueue = inputQueue;
        SyncSystem = syncSystem;
        ActionQueue = inputQueue.ActionQueue;
        RuleExecutionSystem = new StateDrivenRuleExecutionSystem(actionSpecs);
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
        foreach (AuthoritativeMoveInput input in pendingMoveInputs.Values)
        {
            input.Complete(new MoveResult(false, input.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "tick runner stopped", false, default, input.ClientTick));
        }

        pendingMoveInputs.Clear();
    }

    public int ActivePendingStateCount => PendingStates.ActiveCount;

    public WorldDelta Tick(IReadOnlyList<Session> observers)
    {
        long serverTick = World.NextTick();
        World.ExpireRuntimeEffects(serverTick);
        IReadOnlyList<AuthoritativeMoveInput> inputs = InputQueue.DrainMoves();
        for (int i = 0; i < inputs.Count; i++)
        {
            AuthoritativeMoveInput input = inputs[i];
            WorldAction action = ActionQueue.EnqueuePlayerMove(input.EntityId, input.TargetCoord, input.ClientTick);
            pendingMoveInputs[action.ActionId] = input;
        }

        EnqueueAutoMoveActions(serverTick);
        ExplicitOutputPolicies.EnqueuePushOnEnterActions(World, ActionQueue, serverTick);
        IReadOnlyList<WorldAction> actions = ActionQueue.DrainReady(serverTick);
        StateDrivenRuleExecutionResult ruleResult = RuleExecutionSystem.Tick(World, actions, PendingStates, serverTick);
        EnqueueDeferredActions(ruleResult.DeferredActions);
        LogPushCycleDiagnostics(ruleResult, serverTick);
        foreach (KeyValuePair<long, AuthoritativeMoveInput> pair in pendingMoveInputs.ToArray())
        {
            if (ruleResult.ActionResults.TryGetValue(pair.Key, out MoveResult result))
            {
                pair.Value.Complete(result);
                pendingMoveInputs.Remove(pair.Key);
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

    private void LogPushCycleDiagnostics(StateDrivenRuleExecutionResult ruleResult, long serverTick)
    {
        if (!ruleResult.Reasons.Contains("push chain cycle"))
        {
            return;
        }

        PendingCycleDiagnostic diagnostic = PendingStates.LastCycleDiagnostic;
        string cycle = diagnostic.ConflictEntityId == 0
            ? "none"
            : "state:" + diagnostic.StateId +
              " conflict:" + diagnostic.ConflictEntityId +
              " inChain:" + diagnostic.ConflictInChain +
              " inAdding:" + diagnostic.ConflictInAdding +
              " candidate:" + string.Join(",", diagnostic.CandidateSubjectEntityIds.OrderBy(entityId => entityId)) +
              " chain:" + string.Join(",", diagnostic.ChainEntityIds.OrderBy(entityId => entityId));
        string states = string.Join(" || ", PendingStates.ActionStates.Select(state =>
            "state:" + state.StateId +
            " status:" + state.Status +
            " chain:" + string.Join(",", state.Chain.OrderBy(entityId => entityId)) +
            " units:" + string.Join(";", state.Units.Select(unit =>
                unit.ActionUnitId + ":" + unit.Status + ":" + string.Join(".", unit.SubjectEntityIds.OrderBy(entityId => entityId))))));
        string snapshots = string.Join(" || ", World.CreateSnapshot()
            .OrderBy(snapshot => snapshot.Y)
            .ThenBy(snapshot => snapshot.X)
            .ThenBy(snapshot => snapshot.EntityId)
            .Select(snapshot =>
                snapshot.EntityId +
                " cfg:" + snapshot.ConfigId +
                " pos:(" + snapshot.X + "," + snapshot.Y + ")" +
                " dir:" + snapshot.Direction +
                " ports:" + snapshot.PortLocalPorts +
                " push:" + snapshot.Pushable +
                " move:" + snapshot.CanMove +
                " bePushed:" + snapshot.CanBePushed));
        Log.Info("[AuthoritativeWorldTickRunner] push cycle diagnostics tick:{0} cycle:{1} states:{2} snapshots:{3}", serverTick, cycle, states, snapshots);
    }

    private void EnqueueDeferredActions(IReadOnlyList<DeferredAction> deferredActions)
    {
        for (int i = 0; i < deferredActions.Count; i++)
        {
            ActionQueue.EnqueueDeferred(deferredActions[i]);
        }
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

}
