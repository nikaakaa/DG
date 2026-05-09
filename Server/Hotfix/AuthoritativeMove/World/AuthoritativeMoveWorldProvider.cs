using Fantasy.Network;
using DG.GameCore;

namespace Fantasy;

public static class AuthoritativeMoveWorldProvider
{
    public const int TickIntervalMs = 200;

    public static ActionSpecRegistry ActionSpecs { get; } = CreateActionSpecs();
    public static IGameConfigProvider ConfigProvider { get; } = CreateConfigProvider();
    public static GameWorld World { get; } = CreateDefaultWorld(ConfigProvider);
    public static AuthoritativeInputQueue InputQueue { get; } = new(ActionSpecs);
    public static AuthoritativeWorldSyncSystem SyncSystem { get; } = new(World);
    public static AuthoritativeWorldTickRunner TickRunner { get; } = new(World, InputQueue, SyncSystem, TickIntervalMs, ActionSpecs);
    public static MoveObserverRegistry<Session> Observers { get; } = new();
    public static MultiplayerEntityManager<Session> Players { get; } = new(World, ConfigProvider);
    public static DebugWorldEditService DebugEdit { get; } = new(World);

    public static long NextServerTick()
    {
        return World.NextTick();
    }

    private static IGameConfigProvider CreateConfigProvider()
    {
        return LubanGameConfigProvider.FromDirectory(ServerGameConfigPath.FindGameCoreConfigDirectory());
    }

    private static ActionSpecRegistry CreateActionSpecs()
    {
        return LubanActionSpecRegistry.FromDirectory(ServerGameConfigPath.FindGameCoreConfigDirectory());
    }

    private static GameWorld CreateDefaultWorld(IGameConfigProvider configProvider)
    {
        var world = new GameWorld(configProvider);
        IReadOnlyList<EntitySpawnSpec> spawns = configProvider.GetWorldSpawns(DefaultWorldConfig.DemoWorldId);
        for (int i = 0; i < spawns.Count; i++)
        {
            world.AddEntity(spawns[i]);
        }

        world.FlushDelta();
        return world;
    }
}
