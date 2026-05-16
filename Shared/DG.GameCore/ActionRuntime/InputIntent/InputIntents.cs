using System;

namespace DG.GameCore
{
public enum InputKind
{
    None = 0,
    Move = 1,
    Wait = 2,
    Attack = 3,
    Interact = 4,
    Skill = 5,
    Cancel = 6
}

public enum InputSourceKind
{
    None = 0,
    Player = 1,
    Debug = 2,
    AI = 3,
    Replay = 4,
    Script = 5
}

public enum RhythmJudge
{
    None = 0,
    Perfect = 1,
    Good = 2,
    Late = 3,
    Miss = 4
}

public enum TargetHintKind
{
    None = 0,
    Direction = 1,
    Grid = 2,
    Entity = 3
}

public readonly struct TargetHint
{
    private TargetHint(TargetHintKind kind, Direction direction, GridCoord? gridCoord, long entityId)
    {
        Kind = kind;
        Direction = direction;
        GridCoord = gridCoord;
        EntityId = entityId;
    }

    public TargetHintKind Kind { get; }
    public Direction Direction { get; }
    public GridCoord? GridCoord { get; }
    public long EntityId { get; }
    public bool IsEmpty => Kind == TargetHintKind.None;

    public static TargetHint None => default;
    public static TargetHint FromDirection(Direction direction) => new TargetHint(TargetHintKind.Direction, direction, null, 0);
    public static TargetHint FromGrid(GridCoord coord) => new TargetHint(TargetHintKind.Grid, Direction.None, coord, 0);
    public static TargetHint FromEntity(long entityId) => new TargetHint(TargetHintKind.Entity, Direction.None, null, entityId);
}

public readonly struct InputIntent
{
    public InputIntent(
        long intentId,
        InputSourceKind sourceKind,
        long actorEntityId,
        InputKind inputKind,
        Direction direction,
        TargetHint targetHint,
        long beatTick,
        RhythmJudge rhythmJudge,
        long clientInputId,
        long clientTick,
        long sampleTimeMs,
        long createdServerTick,
        string channel)
    {
        IntentId = intentId;
        SourceKind = sourceKind;
        ActorEntityId = actorEntityId;
        InputKind = inputKind;
        Direction = direction;
        TargetHint = targetHint;
        BeatTick = beatTick;
        RhythmJudge = rhythmJudge;
        ClientInputId = clientInputId;
        ClientTick = clientTick;
        SampleTimeMs = sampleTimeMs;
        CreatedServerTick = createdServerTick;
        Channel = string.IsNullOrEmpty(channel) ? "default" : channel;
    }

    public long IntentId { get; }
    public InputSourceKind SourceKind { get; }
    public long ActorEntityId { get; }
    public InputKind InputKind { get; }
    public Direction Direction { get; }
    public TargetHint TargetHint { get; }
    public long BeatTick { get; }
    public RhythmJudge RhythmJudge { get; }
    public long ClientInputId { get; }
    public long ClientTick { get; }
    public long SampleTimeMs { get; }
    public long CreatedServerTick { get; }
    public string Channel { get; }

    public bool IsMove => InputKind == InputKind.Move;

    public static InputIntent PlayerMove(long actorEntityId, Direction direction, long beatTick, long clientInputId, long clientTick, long createdServerTick)
        => new InputIntent(clientInputId, InputSourceKind.Player, actorEntityId, InputKind.Move, direction, TargetHint.FromDirection(direction), beatTick, RhythmJudge.None, clientInputId, clientTick, 0, createdServerTick, "player");
}

public enum InputIntentAuthorizationStatus
{
    Accepted = 1,
    Rejected = 2,
    Expired = 3,
    Duplicate = 4,
    RateLimited = 5
}

public readonly struct InputIntentAuthorizationResult
{
    public InputIntentAuthorizationResult(bool accepted, InputIntentAuthorizationStatus status, MoveErrorCode errorCode, string reason)
    {
        Accepted = accepted;
        Status = status;
        ErrorCode = errorCode;
        Reason = reason ?? string.Empty;
    }

    public bool Accepted { get; }
    public InputIntentAuthorizationStatus Status { get; }
    public MoveErrorCode ErrorCode { get; }
    public string Reason { get; }

    public static InputIntentAuthorizationResult Accept()
        => new InputIntentAuthorizationResult(true, InputIntentAuthorizationStatus.Accepted, MoveErrorCode.None, string.Empty);

    public static InputIntentAuthorizationResult Reject(MoveErrorCode errorCode, string reason)
        => new InputIntentAuthorizationResult(false, InputIntentAuthorizationStatus.Rejected, errorCode, reason);
}

public sealed class MoveIntentAdapter
{
    public bool TryResolveMoveTarget(GameWorld world, InputIntent intent, out GridCoord targetCoord, out MoveResult rejectResult)
    {
        if (intent.InputKind != InputKind.Move)
        {
            targetCoord = default;
            rejectResult = new MoveResult(false, intent.ActorEntityId, default, Direction.None, MoveErrorCode.InvalidDirection, "unsupported input intent", false, default, intent.ClientTick);
            return false;
        }

        if (intent.Direction == Direction.None)
        {
            targetCoord = default;
            rejectResult = new MoveResult(false, intent.ActorEntityId, default, Direction.None, MoveErrorCode.InvalidDirection, "invalid direction", false, default, intent.ClientTick);
            return false;
        }

        if (!world.TryGetEntity(intent.ActorEntityId, out GameEntity entity))
        {
            targetCoord = default;
            rejectResult = new MoveResult(false, intent.ActorEntityId, default, intent.Direction, MoveErrorCode.UnknownEntity, "entity not found", false, default, intent.ClientTick);
            return false;
        }

        if (!world.TryGetComponent(entity, out PositionComponent position))
        {
            targetCoord = default;
            rejectResult = new MoveResult(false, intent.ActorEntityId, default, intent.Direction, MoveErrorCode.MissingPosition, "missing position", false, default, intent.ClientTick);
            return false;
        }

        targetCoord = position.Coord.Add(intent.Direction);
        rejectResult = default;
        return true;
    }
}
}
