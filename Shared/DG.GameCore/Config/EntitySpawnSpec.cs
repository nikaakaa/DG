using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct EntitySpawnSpec
{
    public EntitySpawnSpec(long entityId, int configId, GridCoord position, Direction direction, long playerId, int autoMoveIntervalTicks)
    {
        EntityId = entityId;
        ConfigId = configId;
        Position = position;
        Direction = direction;
        PlayerId = playerId;
        AutoMoveIntervalTicks = autoMoveIntervalTicks;
    }

    public long EntityId { get; }
    public int ConfigId { get; }
    public GridCoord Position { get; }
    public Direction Direction { get; }
    public long PlayerId { get; }
    public int AutoMoveIntervalTicks { get; }

    public static EntitySpawnSpec FromSnapshot(EntitySnapshot snapshot)
    {
        return new EntitySpawnSpec(
            snapshot.EntityId,
            snapshot.ConfigId,
            new GridCoord(snapshot.X, snapshot.Y),
            snapshot.Direction,
            snapshot.PlayerControlled ? snapshot.EntityId : 0,
            1);
    }
}
}
