using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum EntityTarget
{
    All = 0,
    Player = 1,
    Monster = 2,
    Object = 3
}

public static class DefaultWorldConfig
{
    public const int PlayerConfigId = 1;
    public const int BallConfigId = 1001;
    public const int BlockerConfigId = 1002;
    public const int ConveyorConfigId = 2001;
    public const int PlayerArchetypeId = 1;
    public const int BallArchetypeId = 2;
    public const int BlockerArchetypeId = 3;
    public const int ConveyorArchetypeId = 4;
    public const int PlayerTarget = (int)EntityTarget.Player;
    public const int BallTarget = (int)EntityTarget.Monster;
    public const int BlockerTarget = (int)EntityTarget.Object;
    public const long BallEntityId = 900000001;
    public const long FirstBlockerEntityId = 900000100;
    public const long ConveyorEntityId = 900000200;
    public const long BlockedConveyorEntityId = 900000201;
    public const string DemoWorldId = "demo";
    public const string DefaultPlayerSpawnRuleId = "default";

    public static EntitySpawnSpec PlayerSpawn(long entityId, long playerId, GridCoord position)
    {
        return new EntitySpawnSpec(entityId, PlayerConfigId, position, Direction.None, playerId, 1);
    }

    public static EntitySpawnSpec BallSpawn(long entityId, GridCoord position, Direction direction, int intervalTicks)
    {
        return new EntitySpawnSpec(entityId, BallConfigId, position, direction, 0, intervalTicks);
    }

    public static EntitySpawnSpec BlockerSpawn(long entityId, GridCoord position)
    {
        return new EntitySpawnSpec(entityId, BlockerConfigId, position, Direction.None, 0, 1);
    }

    public static EntitySpawnSpec ConveyorSpawn(long entityId, GridCoord position, Direction direction)
    {
        return new EntitySpawnSpec(entityId, ConveyorConfigId, position, direction, 0, 1);
    }

    public static void AddFallbackDemoEntities(GameWorld world)
    {
        world.AddEntity(BallSpawn(BallEntityId, new GridCoord(-2, 0), Direction.Right, 1));
        world.AddEntity(BlockerSpawn(FirstBlockerEntityId, new GridCoord(4, 0)));
        world.AddEntity(BlockerSpawn(FirstBlockerEntityId + 1, new GridCoord(-4, 0)));
        world.AddEntity(ConveyorSpawn(ConveyorEntityId, new GridCoord(1, 0), Direction.Right));
        world.AddEntity(ConveyorSpawn(BlockedConveyorEntityId, new GridCoord(3, 0), Direction.Right));
    }
}
}
