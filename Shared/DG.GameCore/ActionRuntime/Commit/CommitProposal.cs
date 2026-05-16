namespace DG.GameCore
{
public enum CommitProposalKind
{
    MoveEntity = 1,
    SetDirection = 2,
    CreateEntity = 3,
    DeleteEntity = 4,
    SetAutoMoveTick = 5,
    AddRuntimeEffect = 6,
    RemoveRuntimeEffect = 7,
    SetComponentResult = 8,
    AddTag = 9,
    RemoveTag = 10,
    ClearRuntimeSources = 11
}

public readonly struct CommitProposal
{
    public CommitProposal(CommitProposalKind kind, WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, GridCoord from, GridCoord to, Direction direction, long serverTick, int configId, long playerId, int autoMoveIntervalTicks)
        : this(kind, priority, sourceActionId, sourceStateId, entityId, from, to, direction, serverTick, configId, playerId, autoMoveIntervalTicks, default, default, WorldTag.None, default)
    {
    }

    public CommitProposal(CommitProposalKind kind, WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, GridCoord from, GridCoord to, Direction direction, long serverTick, int configId, long playerId, int autoMoveIntervalTicks, EffectApplication effectApplication, RuntimeEffectId runtimeEffectId, WorldTag tag, ComponentSourceContribution componentContribution)
    {
        Kind = kind;
        Priority = priority;
        SourceActionId = sourceActionId;
        SourceStateId = sourceStateId;
        EntityId = entityId;
        From = from;
        To = to;
        Direction = direction;
        ServerTick = serverTick;
        ConfigId = configId;
        PlayerId = playerId;
        AutoMoveIntervalTicks = autoMoveIntervalTicks;
        EffectApplication = effectApplication;
        RuntimeEffectId = runtimeEffectId;
        Tag = tag;
        ComponentContribution = componentContribution;
    }

    public CommitProposalKind Kind { get; }
    public WorldActionPriority Priority { get; }
    public long SourceActionId { get; }
    public long SourceStateId { get; }
    public long EntityId { get; }
    public GridCoord From { get; }
    public GridCoord To { get; }
    public Direction Direction { get; }
    public long ServerTick { get; }
    public int ConfigId { get; }
    public long PlayerId { get; }
    public int AutoMoveIntervalTicks { get; }
    public EffectApplication EffectApplication { get; }
    public RuntimeEffectId RuntimeEffectId { get; }
    public WorldTag Tag { get; }
    public ComponentSourceContribution ComponentContribution { get; }

    public static CommitProposal Move(WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, GridCoord from, GridCoord to, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.MoveEntity, priority, sourceActionId, sourceStateId, entityId, from, to, Direction.None, serverTick, 0, 0, 0);
    }

    public static CommitProposal SetDirection(WorldActionPriority priority, long sourceActionId, long sourceStateId, long entityId, Direction direction, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.SetDirection, priority, sourceActionId, sourceStateId, entityId, default, default, direction, serverTick, 0, 0, 0);
    }

    public static CommitProposal Create(WorldActionPriority priority, long sourceActionId, long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.CreateEntity, priority, sourceActionId, 0, entityId, default, coord, direction, serverTick, configId, playerId, autoMoveIntervalTicks);
    }

    public static CommitProposal Delete(WorldActionPriority priority, long sourceActionId, long entityId, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.DeleteEntity, priority, sourceActionId, 0, entityId, default, default, Direction.None, serverTick, 0, 0, 0);
    }

    public static CommitProposal SetAutoMoveTick(WorldActionPriority priority, long sourceActionId, long entityId, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.SetAutoMoveTick, priority, sourceActionId, 0, entityId, default, default, Direction.None, serverTick, 0, 0, 0);
    }

    public static CommitProposal AddRuntimeEffect(WorldActionPriority priority, long sourceActionId, EffectApplication application, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.AddRuntimeEffect, priority, sourceActionId, 0, application.TargetEntityId, default, application.TargetCell, Direction.None, serverTick, 0, 0, 0, application, default, WorldTag.None, default);
    }

    public static CommitProposal RemoveRuntimeEffect(WorldActionPriority priority, long sourceActionId, long entityId, RuntimeEffectId effectId, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.RemoveRuntimeEffect, priority, sourceActionId, 0, entityId, default, default, Direction.None, serverTick, 0, 0, 0, default, effectId, WorldTag.None, default);
    }

    public static CommitProposal SetComponentResult(WorldActionPriority priority, long sourceActionId, ComponentSourceContribution contribution, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.SetComponentResult, priority, sourceActionId, 0, contribution.EntityId, default, default, Direction.None, serverTick, 0, 0, 0, default, default, WorldTag.None, contribution);
    }

    public static CommitProposal AddTag(WorldActionPriority priority, long sourceActionId, long entityId, WorldTag tag, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.AddTag, priority, sourceActionId, 0, entityId, default, default, Direction.None, serverTick, 0, 0, 0, default, default, tag, default);
    }

    public static CommitProposal RemoveTag(WorldActionPriority priority, long sourceActionId, long entityId, WorldTag tag, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.RemoveTag, priority, sourceActionId, 0, entityId, default, default, Direction.None, serverTick, 0, 0, 0, default, default, tag, default);
    }

    public static CommitProposal ClearRuntimeSources(WorldActionPriority priority, long sourceActionId, long entityId, long serverTick)
    {
        return new CommitProposal(CommitProposalKind.ClearRuntimeSources, priority, sourceActionId, 0, entityId, default, default, Direction.None, serverTick, 0, 0, 0, default, default, WorldTag.None, default);
    }
}
}
