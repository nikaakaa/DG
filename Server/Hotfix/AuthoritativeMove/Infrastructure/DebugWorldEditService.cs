using DG.GameCore;

namespace Fantasy;

public sealed class DebugWorldEditService
{
    private readonly GameWorld World;
    private readonly long FirstDebugEntityId;
    private long nextDebugEntityId;

    public DebugWorldEditService(GameWorld world, long firstDebugEntityId = 800000000)
    {
        World = world;
        FirstDebugEntityId = firstDebugEntityId;
        nextDebugEntityId = firstDebugEntityId;
    }

    public bool Enabled { get; set; } = true;

    public bool TrySpawn(long requestedEntityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, out long entityId, out string reason)
    {
        entityId = requestedEntityId > 0 ? requestedEntityId : AllocateEntityId();
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (configId <= 0)
        {
            reason = "invalid config id";
            return false;
        }

        if (World.TryGetEntity(entityId, out _))
        {
            reason = "entity already exists";
            return false;
        }

        var spawn = new EntitySpawnSpec(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks <= 0 ? 1 : autoMoveIntervalTicks);
        if (!World.AddEntity(spawn))
        {
            reason = "spawn failed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryMove(long entityId, GridCoord coord, out GridCoord finalCoord, out string reason)
    {
        finalCoord = coord;
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (!World.TryGetEntity(entityId, out GameEntity entity))
        {
            reason = "entity not found";
            return false;
        }

        World.MoveEntity(entity, coord);
        reason = string.Empty;
        return true;
    }

    public bool TryRemove(long entityId, out string reason)
    {
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (!World.RemoveEntity(entityId))
        {
            reason = "entity not found";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private long AllocateEntityId()
    {
        while (World.TryGetEntity(nextDebugEntityId, out _))
        {
            nextDebugEntityId++;
        }

        if (nextDebugEntityId < FirstDebugEntityId)
        {
            nextDebugEntityId = FirstDebugEntityId;
        }

        return nextDebugEntityId++;
    }
}
