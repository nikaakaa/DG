using System;
using System.Collections.Generic;
using System.Linq;
using Fantasy.Async;
using DG.GameCore;

namespace Fantasy;

public enum AuthoritativePlayerInputStatus
{
    Buffered = 1,
    Replaced = 2,
    Consumed = 3,
    Expired = 4,
    Rejected = 5,
    Resolved = 6
}

public sealed class AuthoritativeMoveInput
{
    private readonly FTask<MoveResult> completion;

    public AuthoritativeMoveInput(long entityId, long beatTick, Direction direction, long clientInputId, long clientTick, long submitSequence)
        : this(InputIntent.PlayerMove(entityId, direction, beatTick, clientInputId, clientTick, 0), beatTick, submitSequence)
    {
    }

    public AuthoritativeMoveInput(InputIntent intent, long consumeTick, long submitSequence)
    {
        Intent = intent;
        EntityId = intent.ActorEntityId;
        BeatTick = intent.BeatTick;
        ConsumeTick = consumeTick;
        Direction = intent.Direction;
        ClientInputId = intent.ClientInputId;
        ClientTick = intent.ClientTick;
        SubmitSequence = submitSequence;
        Status = AuthoritativePlayerInputStatus.Buffered;
        completion = FTask<MoveResult>.Create(false);
    }

    public InputIntent Intent { get; }
    public long EntityId { get; }
    public long BeatTick { get; }
    public long ConsumeTick { get; }
    public Direction Direction { get; }
    public long ClientInputId { get; }
    public long ClientTick { get; }
    public long SubmitSequence { get; }
    public AuthoritativePlayerInputStatus Status { get; private set; }
    public bool IsCompleted => completion.IsCompleted;

    public FTask<MoveResult> WaitAsync()
    {
        return completion;
    }

    public void Complete(AuthoritativePlayerInputStatus status, MoveResult result)
    {
        if (!completion.IsCompleted)
        {
            Status = status;
            completion.SetResult(result);
        }
    }

    public void MarkConsumed()
    {
        if (!completion.IsCompleted)
        {
            Status = AuthoritativePlayerInputStatus.Consumed;
        }
    }
}

public sealed class AuthoritativeDebugActionInput
{
    private readonly FTask<MoveResult> completion;
    private MoveResult result;

    public AuthoritativeDebugActionInput(WorldAction action)
    {
        Action = action;
        completion = FTask<MoveResult>.Create(false);
    }

    public WorldAction Action { get; }
    public bool IsCompleted => completion.IsCompleted;
    public MoveResult Result => result;

    public FTask<MoveResult> WaitAsync()
    {
        return completion;
    }

    public void Complete(MoveResult result)
    {
        if (!completion.IsCompleted)
        {
            this.result = result;
            completion.SetResult(result);
        }
    }
}

public sealed class AuthoritativeInputQueue
{
    private readonly Dictionary<PlayerInputKey, AuthoritativeMoveInput> moveInputs = new();
    private readonly Dictionary<long, AuthoritativeDebugActionInput> debugInputs = new();
    private readonly HashSet<long> consumedClientInputIds = new();
    private long nextSubmitSequence = 1;
    private long activeMoveConsumeTick;

    public AuthoritativeInputQueue() : this(ActionSpecRegistry.Default)
    {
    }

    public AuthoritativeInputQueue(ActionSpecRegistry actionSpecs)
    {
        ActionQueue = new WorldActionQueue(actionSpecs);
    }

    public int PendingMoveCount => moveInputs.Count;
    public WorldActionQueue ActionQueue { get; }

    public AuthoritativeMoveInput EnqueueMove(long entityId, GridCoord targetCoord, long clientTick)
    {
        return EnqueueMove(entityId, long.MaxValue, DirectionFromTarget(default, targetCoord), 0, clientTick, 0);
    }

    public AuthoritativeMoveInput EnqueueMove(long entityId, long beatTick, Direction direction, long clientInputId, long clientTick, long currentTick)
        => EnqueueIntent(InputIntent.PlayerMove(entityId, direction, beatTick, clientInputId, clientTick, currentTick), currentTick);

    public AuthoritativeMoveInput EnqueueIntent(InputIntent intent, long currentTick)
    {
        long consumeTick = ResolveMoveConsumeTick(currentTick);
        var input = new AuthoritativeMoveInput(intent, consumeTick, nextSubmitSequence++);

        if (intent.ClientInputId != 0 && consumedClientInputIds.Contains(intent.ClientInputId))
        {
            input.Complete(AuthoritativePlayerInputStatus.Rejected, new MoveResult(false, intent.ActorEntityId, default, intent.Direction, MoveErrorCode.UnknownEntity, "duplicate input", false, default, intent.ClientTick));
            return input;
        }

        var key = new PlayerInputKey(consumeTick, intent.ActorEntityId, intent.Channel, intent.InputKind);
        if (moveInputs.TryGetValue(key, out AuthoritativeMoveInput? previous))
        {
            previous.Complete(AuthoritativePlayerInputStatus.Replaced, new MoveResult(false, previous.EntityId, default, previous.Direction, MoveErrorCode.UnknownEntity, "input replaced", false, default, previous.ClientTick));
        }

        moveInputs[key] = input;
        return input;
    }

    public IReadOnlyList<AuthoritativeMoveInput> DrainMoves(long consumeTick)
    {
        if (moveInputs.Count == 0)
        {
            if (activeMoveConsumeTick == consumeTick)
            {
                activeMoveConsumeTick = 0;
            }

            return Array.Empty<AuthoritativeMoveInput>();
        }

        var inputs = new List<AuthoritativeMoveInput>();
        foreach (KeyValuePair<PlayerInputKey, AuthoritativeMoveInput> pair in moveInputs.ToArray())
        {
            if (pair.Key.ConsumeTick != consumeTick && pair.Key.ConsumeTick != long.MaxValue)
            {
                continue;
            }

            moveInputs.Remove(pair.Key);
            pair.Value.MarkConsumed();
            if (pair.Value.ClientInputId != 0)
            {
                consumedClientInputIds.Add(pair.Value.ClientInputId);
            }

            inputs.Add(pair.Value);
        }

        if (activeMoveConsumeTick == consumeTick)
        {
            activeMoveConsumeTick = 0;
        }

        return inputs
            .OrderBy(input => input.ConsumeTick)
            .ThenBy(input => input.EntityId)
            .ThenBy(input => input.SubmitSequence)
            .ToArray();
    }

    public void FailPending(string reason)
    {
        foreach (AuthoritativeMoveInput input in moveInputs.Values)
        {
            input.Complete(AuthoritativePlayerInputStatus.Rejected, new MoveResult(false, input.EntityId, default, input.Direction, MoveErrorCode.UnknownEntity, reason, false, default, input.ClientTick));
        }

        moveInputs.Clear();
        activeMoveConsumeTick = 0;

        foreach (AuthoritativeDebugActionInput input in debugInputs.Values)
        {
            input.Complete(new MoveResult(false, input.Action.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, reason, false, default, input.Action.ClientTick));
        }

        debugInputs.Clear();
    }

    public AuthoritativeDebugActionInput EnqueueDebugSpawn(long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks)
        => EnqueueDebugSpawn(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks, false);

    public AuthoritativeDebugActionInput EnqueueDebugSpawn(long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, bool rotatePivot)
    {
        WorldAction action = ActionQueue.EnqueueDebugSpawn(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks, rotatePivot);
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

    public AuthoritativeDebugActionInput EnqueueDebugApplyEffect(long entityId, EffectSpecId effectSpecId, string stackKey, long expireTick)
    {
        WorldAction action = ActionQueue.EnqueueDebugApplyEffect(entityId, effectSpecId, stackKey, expireTick);
        var input = new AuthoritativeDebugActionInput(action);
        debugInputs[action.ActionId] = input;
        return input;
    }

    public AuthoritativeDebugActionInput EnqueueDebugRemoveEffect(long entityId, RuntimeEffectId runtimeEffectId)
    {
        WorldAction action = ActionQueue.EnqueueDebugRemoveEffect(entityId, runtimeEffectId);
        var input = new AuthoritativeDebugActionInput(action);
        debugInputs[action.ActionId] = input;
        return input;
    }

    public AuthoritativeDebugActionInput EnqueueDebugSetTag(long entityId, WorldTag tag, bool enabled)
    {
        WorldAction action = ActionQueue.EnqueueDebugSetTag(entityId, tag, enabled);
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

    private static Direction DirectionFromTarget(GridCoord current, GridCoord target)
    {
        int dx = target.X - current.X;
        int dy = target.Y - current.Y;
        if (dx == -1 && dy == 0)
        {
            return Direction.Left;
        }

        if (dx == 1 && dy == 0)
        {
            return Direction.Right;
        }

        if (dx == 0 && dy == 1)
        {
            return Direction.Up;
        }

        if (dx == 0 && dy == -1)
        {
            return Direction.Down;
        }

        return Direction.None;
    }

    private long ResolveMoveConsumeTick(long currentTick)
    {
        if (currentTick == 0)
        {
            return long.MaxValue;
        }

        if (activeMoveConsumeTick == 0)
        {
            activeMoveConsumeTick = currentTick + 1;
        }

        return activeMoveConsumeTick;
    }

    private readonly struct PlayerInputKey : IEquatable<PlayerInputKey>
    {
        public PlayerInputKey(long consumeTick, long entityId, string channel, InputKind inputKind)
        {
            ConsumeTick = consumeTick;
            EntityId = entityId;
            Channel = channel ?? string.Empty;
            InputKind = inputKind;
        }

        public long ConsumeTick { get; }
        public long EntityId { get; }
        public string Channel { get; }
        public InputKind InputKind { get; }

        public bool Equals(PlayerInputKey other)
        {
            return ConsumeTick == other.ConsumeTick &&
                EntityId == other.EntityId &&
                Channel == other.Channel &&
                InputKind == other.InputKind;
        }

        public override bool Equals(object? obj)
        {
            return obj is PlayerInputKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ConsumeTick, EntityId, Channel, InputKind);
        }
    }
}
