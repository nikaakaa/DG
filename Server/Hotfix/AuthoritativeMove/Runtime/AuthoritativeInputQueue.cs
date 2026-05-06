using Fantasy.Async;
using DG.GameCore;

namespace Fantasy;

public sealed class AuthoritativeMoveInput
{
    private readonly FTask<MoveResult> completion;

    public AuthoritativeMoveInput(long entityId, GridCoord targetCoord, long clientTick)
    {
        EntityId = entityId;
        TargetCoord = targetCoord;
        ClientTick = clientTick;
        completion = FTask<MoveResult>.Create(false);
    }

    public long EntityId { get; }
    public GridCoord TargetCoord { get; }
    public long ClientTick { get; }
    public bool IsCompleted => completion.IsCompleted;

    public FTask<MoveResult> WaitAsync()
    {
        return completion;
    }

    public void Complete(MoveResult result)
    {
        if (!completion.IsCompleted)
        {
            completion.SetResult(result);
        }
    }
}

public sealed class AuthoritativeDebugActionInput
{
    private readonly FTask<MoveResult> completion;

    public AuthoritativeDebugActionInput(WorldAction action)
    {
        Action = action;
        completion = FTask<MoveResult>.Create(false);
    }

    public WorldAction Action { get; }
    public bool IsCompleted => completion.IsCompleted;

    public FTask<MoveResult> WaitAsync()
    {
        return completion;
    }

    public void Complete(MoveResult result)
    {
        if (!completion.IsCompleted)
        {
            completion.SetResult(result);
        }
    }
}

public sealed class AuthoritativeInputQueue
{
    private readonly Queue<AuthoritativeMoveInput> moveInputs = new();
    private readonly Dictionary<long, AuthoritativeDebugActionInput> debugInputs = new();

    public int PendingMoveCount => moveInputs.Count;
    public WorldActionQueue ActionQueue { get; } = new();

    public AuthoritativeMoveInput EnqueueMove(long entityId, GridCoord targetCoord, long clientTick)
    {
        var input = new AuthoritativeMoveInput(entityId, targetCoord, clientTick);
        moveInputs.Enqueue(input);
        return input;
    }

    public IReadOnlyList<AuthoritativeMoveInput> DrainMoves()
    {
        if (moveInputs.Count == 0)
        {
            return Array.Empty<AuthoritativeMoveInput>();
        }

        var inputs = new List<AuthoritativeMoveInput>(moveInputs.Count);
        while (moveInputs.Count > 0)
        {
            inputs.Add(moveInputs.Dequeue());
        }

        return inputs;
    }

    public void FailPending(string reason)
    {
        while (moveInputs.Count > 0)
        {
            AuthoritativeMoveInput input = moveInputs.Dequeue();
            input.Complete(new MoveResult(false, input.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, reason, false, default, input.ClientTick));
        }

        foreach (AuthoritativeDebugActionInput input in debugInputs.Values)
        {
            input.Complete(new MoveResult(false, input.Action.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, reason, false, default, input.Action.ClientTick));
        }

        debugInputs.Clear();
    }

    public AuthoritativeDebugActionInput EnqueueDebugSpawn(long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks)
    {
        WorldAction action = ActionQueue.EnqueueDebugSpawn(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks);
        var input = new AuthoritativeDebugActionInput(action);
        debugInputs[action.ActionId] = input;
        return input;
    }

    public AuthoritativeDebugActionInput EnqueueDebugMove(long entityId, GridCoord targetCoord)
    {
        WorldAction action = ActionQueue.EnqueueDebugMove(entityId, targetCoord);
        var input = new AuthoritativeDebugActionInput(action);
        debugInputs[action.ActionId] = input;
        return input;
    }

    public AuthoritativeDebugActionInput EnqueueDebugRemove(long entityId)
    {
        WorldAction action = ActionQueue.EnqueueDebugRemove(entityId);
        var input = new AuthoritativeDebugActionInput(action);
        debugInputs[action.ActionId] = input;
        return input;
    }

    public IReadOnlyList<AuthoritativeDebugActionInput> DrainDebugInputs(IReadOnlyCollection<long> actionIds)
    {
        if (debugInputs.Count == 0 || actionIds.Count == 0)
        {
            return Array.Empty<AuthoritativeDebugActionInput>();
        }

        var result = new List<AuthoritativeDebugActionInput>();
        foreach (long actionId in actionIds)
        {
            if (debugInputs.TryGetValue(actionId, out AuthoritativeDebugActionInput? input) && input != null)
            {
                debugInputs.Remove(actionId);
                result.Add(input);
            }
        }

        return result;
    }
}
