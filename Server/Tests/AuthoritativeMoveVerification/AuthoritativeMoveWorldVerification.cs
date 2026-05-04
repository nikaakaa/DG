using DG.GameCore;

namespace Fantasy;

public static class AuthoritativeMoveWorldVerification
{
    public static bool Run(out string reason)
    {
        if (!VerifyLubanConfigComposition(out reason))
        {
            return false;
        }

        if (!VerifyUnityStreamingConfigLayout(out reason))
        {
            return false;
        }

        if (!VerifyPushOnEnter(out reason))
        {
            return false;
        }

        if (!VerifyGameCoreMovement(out reason))
        {
            return false;
        }

        if (!VerifyWorldDeltaRemoval(out reason))
        {
            return false;
        }

        if (!VerifyDebugWorldEdit(out reason))
        {
            return false;
        }

        if (!VerifySpatialIndex(out reason))
        {
            return false;
        }

        if (!VerifyAutoMove(out reason))
        {
            return false;
        }

        if (!VerifyMultiplayer(out reason))
        {
            return false;
        }

        if (!VerifyObserverRegistry(out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyUnityStreamingConfigLayout(out string reason)
    {
        string dataDirectory = ServerGameConfigPath.FindGameCoreConfigDirectory();
        if (!System.IO.Directory.Exists(dataDirectory))
        {
            reason = "luban default data directory missing";
            return false;
        }

        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(dataDirectory, "..", "..", "..", ".."));
        string streamingDirectory = System.IO.Path.Combine(root, "Client", "DG_Client", "Assets", "StreamingAssets", "GameConfig");
        if (!System.IO.File.Exists(System.IO.Path.Combine(streamingDirectory, "gamecore_tbentityarchetype.json")) ||
            !System.IO.File.Exists(System.IO.Path.Combine(streamingDirectory, "gamecore_tbworldspawn.json")) ||
            !System.IO.File.Exists(System.IO.Path.Combine(streamingDirectory, "gamecore_tbplayerspawnrule.json")))
        {
            reason = "unity streaming luban config files missing";
            return false;
        }

        IGameConfigProvider provider = LubanGameConfigProvider.FromDirectory(streamingDirectory);
        if (!provider.TryGetArchetype(DefaultWorldConfig.PlayerConfigId, out _) ||
            provider.GetWorldSpawns(DefaultWorldConfig.DemoWorldId).Count != 5 ||
            !provider.TryGetPlayerSpawnRule(DefaultWorldConfig.DefaultPlayerSpawnRuleId, out _))
        {
            reason = "unity streaming luban config load failed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyLubanConfigComposition(out string reason)
    {
        IGameConfigProvider provider = CreateLubanProvider();
        if (!provider.TryGetArchetype(DefaultWorldConfig.PlayerConfigId, out EntityArchetype playerArchetype) ||
            playerArchetype.Components.Count != 4 ||
            !playerArchetype.Components.Contains(ComponentKind.Position) ||
            !playerArchetype.Components.Contains(ComponentKind.Collider) ||
            !playerArchetype.Components.Contains(ComponentKind.Blocking) ||
            !playerArchetype.Components.Contains(ComponentKind.PlayerControl))
        {
            reason = "luban player archetype components invalid";
            return false;
        }

        if (!provider.TryGetArchetype(DefaultWorldConfig.BallConfigId, out EntityArchetype ballArchetype) ||
            !ballArchetype.Components.Contains(ComponentKind.Position) ||
            !ballArchetype.Components.Contains(ComponentKind.Direction) ||
            !ballArchetype.Components.Contains(ComponentKind.Collider) ||
            !ballArchetype.Components.Contains(ComponentKind.Blocking) ||
            !ballArchetype.Components.Contains(ComponentKind.Bouncable) ||
            !ballArchetype.Components.Contains(ComponentKind.AutoMove))
        {
            reason = "luban ball archetype components invalid";
            return false;
        }

        if (!provider.TryGetArchetype(DefaultWorldConfig.BlockerConfigId, out EntityArchetype blockerArchetype) ||
            blockerArchetype.Components.Count != 3 ||
            !blockerArchetype.Components.Contains(ComponentKind.Position) ||
            !blockerArchetype.Components.Contains(ComponentKind.Collider) ||
            !blockerArchetype.Components.Contains(ComponentKind.Blocking))
        {
            reason = "luban blocker archetype components invalid";
            return false;
        }

        if (!provider.TryGetArchetype(DefaultWorldConfig.ConveyorConfigId, out EntityArchetype conveyorArchetype) ||
            conveyorArchetype.Components.Count != 4 ||
            !conveyorArchetype.Components.Contains(ComponentKind.Position) ||
            !conveyorArchetype.Components.Contains(ComponentKind.Direction) ||
            !conveyorArchetype.Components.Contains(ComponentKind.Collider) ||
            !conveyorArchetype.Components.Contains(ComponentKind.PushOnEnter) ||
            conveyorArchetype.Components.Contains(ComponentKind.Blocking) ||
            !conveyorArchetype.Tags.Contains("Tile.Conveyor"))
        {
            reason = "luban conveyor archetype components invalid";
            return false;
        }

        IReadOnlyList<EntitySpawnSpec> demoSpawns = provider.GetWorldSpawns(DefaultWorldConfig.DemoWorldId);
        if (demoSpawns.Count != 5)
        {
            reason = "luban demo world spawn count invalid";
            return false;
        }

        if (!provider.TryGetPlayerSpawnRule(DefaultWorldConfig.DefaultPlayerSpawnRuleId, out PlayerSpawnRule spawnRule) ||
            spawnRule.PlayerConfigId != DefaultWorldConfig.PlayerConfigId ||
            spawnRule.MaxAttempts != 1024)
        {
            reason = "luban player spawn rule invalid";
            return false;
        }

        var world = new GameWorld(provider);
        if (!world.AddEntity(new EntitySpawnSpec(100, DefaultWorldConfig.PlayerConfigId, new GridCoord(0, 0), Direction.None, 100, 1)) ||
            !world.TryGetEntity(100, out GameEntity player) ||
            !world.HasComponent<PositionComponent>(player) ||
            !world.HasComponent<ColliderComponent>(player) ||
            !world.HasComponent<BlockingComponent>(player) ||
            !world.HasComponent<PlayerControlComponent>(player))
        {
            reason = "luban player build failed";
            return false;
        }

        if (!world.AddEntity(new EntitySpawnSpec(101, DefaultWorldConfig.BallConfigId, new GridCoord(1, 0), Direction.Right, 0, 1)) ||
            !world.TryGetEntity(101, out GameEntity ball) ||
            !world.HasComponent<DirectionComponent>(ball) ||
            !world.HasComponent<BouncableComponent>(ball) ||
            !world.HasComponent<AutoMoveComponent>(ball))
        {
            reason = "luban ball build failed";
            return false;
        }

        if (world.GetEntitiesAt(new GridCoord(0, 0), DefaultWorldConfig.PlayerTarget).Count != 1 ||
            world.GetEntitiesAt(new GridCoord(1, 0), DefaultWorldConfig.BallTarget).Count != 1 ||
            world.FlushDelta().ChangedEntities.Count != 2)
        {
            reason = "luban built entities did not enter spatial index or dirty delta";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static IGameConfigProvider CreateLubanProvider()
    {
        return LubanGameConfigProvider.FromDirectory(ServerGameConfigPath.FindGameCoreConfigDirectory());
    }

    private static bool VerifyPushOnEnter(out string reason)
    {
        IGameConfigProvider provider = CreateLubanProvider();
        var world = new GameWorld(provider);
        if (!world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right)))
        {
            reason = "push on enter world setup failed";
            return false;
        }

        if (!world.TryGetEntity(100, out GameEntity conveyor) ||
            !world.HasComponent<PushOnEnterComponent>(conveyor) ||
            world.HasComponent<BlockingComponent>(conveyor) ||
            !world.CanEnterNewEntity(new GridCoord(0, 0)))
        {
            reason = "push on enter conveyor composition invalid";
            return false;
        }

        if (!world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0))))
        {
            reason = "push on enter player setup failed";
            return false;
        }

        world.FlushDelta();
        world.NextTick();
        var pushSystem = new PushOnEnterSystem(new MovementResolveSystem());
        IReadOnlyList<MoveResult> results = pushSystem.Tick(world);
        if (results.Count != 1 ||
            !results[0].Success ||
            !world.TryGetEntity(1, out GameEntity player) ||
            !world.TryGetComponent(player, out PositionComponent position) ||
            position.Coord != new GridCoord(1, 0) ||
            world.FlushDelta().ChangedEntities.Count != 1)
        {
            reason = "push on enter movement failed";
            return false;
        }

        var blockedWorld = new GameWorld(provider);
        blockedWorld.AddEntity(DefaultWorldConfig.ConveyorSpawn(200, new GridCoord(0, 0), Direction.Right));
        blockedWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(2, 2, new GridCoord(0, 0)));
        blockedWorld.AddEntity(DefaultWorldConfig.BlockerSpawn(3, new GridCoord(1, 0)));
        blockedWorld.NextTick();
        IReadOnlyList<MoveResult> blockedResults = pushSystem.Tick(blockedWorld);
        if (blockedResults.Count != 1 ||
            blockedResults[0].Success ||
            !blockedWorld.TryGetEntity(2, out GameEntity blockedPlayer) ||
            !blockedWorld.TryGetComponent(blockedPlayer, out PositionComponent blockedPosition) ||
            blockedPosition.Coord != new GridCoord(0, 0))
        {
            reason = "push on enter blocked movement failed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifySpatialIndex(out string reason)
    {
        GridCoord worldCoord = new GridCoord(-1, -33);
        GridCoord chunkCoord = DG.GameCore.MapCoordinate.ToChunkCoord(worldCoord);
        GridCoord localCoord = DG.GameCore.MapCoordinate.ToLocalCoord(worldCoord);
        if (chunkCoord != new GridCoord(-1, -2) || localCoord != new GridCoord(31, 31) || DG.GameCore.MapCoordinate.ToWorldCoord(chunkCoord, localCoord) != worldCoord)
        {
            reason = "spatial negative coordinate conversion failed";
            return false;
        }

        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.BlockerSpawn(2, new GridCoord(32, 1)));
        if (world.GetEntitiesAt(new GridCoord(0, 0), DefaultWorldConfig.PlayerTarget).Count != 1)
        {
            reason = "spatial player target query failed";
            return false;
        }

        if (!world.TryGetChunkEntities(new GridCoord(1, 0), DefaultWorldConfig.BlockerTarget, out IReadOnlyList<GameEntity> chunkEntities) || chunkEntities.Count != 1 || chunkEntities[0].EntityId != 2)
        {
            reason = "spatial chunk target query failed";
            return false;
        }

        if (!world.TryGetEntity(1, out GameEntity player))
        {
            reason = "spatial player missing";
            return false;
        }

        world.MoveEntity(player, new GridCoord(32, 1));
        if (world.GetEntitiesAt(new GridCoord(0, 0), DefaultWorldConfig.PlayerTarget).Count != 0 ||
            world.GetEntitiesAt(new GridCoord(32, 1), DefaultWorldConfig.PlayerTarget).Count != 1)
        {
            reason = "spatial move index update failed";
            return false;
        }

        world.RemoveEntity(1);
        if (world.GetEntitiesAt(new GridCoord(32, 1), DefaultWorldConfig.PlayerTarget).Count != 0)
        {
            reason = "spatial remove index cleanup failed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyWorldDeltaRemoval(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.BlockerSpawn(10, new GridCoord(0, 0)));
        world.FlushDelta();
        if (!world.RemoveEntity(10))
        {
            reason = "world delta remove setup failed";
            return false;
        }

        WorldDelta delta = world.FlushDelta();
        if (delta.ChangedEntities.Count != 0 || delta.RemovedEntityIds.Count != 1 || delta.RemovedEntityIds[0] != 10)
        {
            reason = "world delta removed entity id missing";
            return false;
        }

        world.RemoveEntity(404);
        WorldDelta emptyDelta = world.FlushDelta();
        if (emptyDelta.ChangedEntities.Count != 0 || emptyDelta.RemovedEntityIds.Count != 0)
        {
            reason = "removing missing entity created delta";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyDebugWorldEdit(out string reason)
    {
        var world = new GameWorld();
        var service = new DebugWorldEditService(world);
        service.Enabled = false;
        if (service.TrySpawn(100, DefaultWorldConfig.BlockerConfigId, new GridCoord(0, 0), Direction.None, 0, 1, out _, out _) ||
            world.EntityCount != 0)
        {
            reason = "disabled debug spawn modified world";
            return false;
        }

        if (service.TryMove(100, new GridCoord(1, 0), out _, out _) ||
            service.TryRemove(100, out _))
        {
            reason = "disabled debug move or remove succeeded";
            return false;
        }

        service.Enabled = true;
        if (!service.TrySpawn(100, DefaultWorldConfig.BlockerConfigId, new GridCoord(0, 0), Direction.None, 0, 1, out long spawnedEntityId, out string spawnReason) ||
            spawnedEntityId != 100 ||
            !string.IsNullOrEmpty(spawnReason))
        {
            reason = "debug spawn failed";
            return false;
        }

        WorldDelta spawnDelta = world.FlushDelta();
        if (spawnDelta.ChangedEntities.Count != 1 || spawnDelta.ChangedEntities[0].EntityId != 100)
        {
            reason = "debug spawn did not create changed delta";
            return false;
        }

        if (!service.TryMove(100, new GridCoord(2, 3), out GridCoord finalCoord, out string moveReason) ||
            finalCoord != new GridCoord(2, 3) ||
            !string.IsNullOrEmpty(moveReason))
        {
            reason = "debug move failed";
            return false;
        }

        WorldDelta moveDelta = world.FlushDelta();
        if (moveDelta.ChangedEntities.Count != 1 ||
            moveDelta.ChangedEntities[0].EntityId != 100 ||
            moveDelta.ChangedEntities[0].X != 2 ||
            moveDelta.ChangedEntities[0].Y != 3)
        {
            reason = "debug move did not create changed delta";
            return false;
        }

        if (!service.TryRemove(100, out string removeReason) || !string.IsNullOrEmpty(removeReason))
        {
            reason = "debug remove failed";
            return false;
        }

        WorldDelta removeDelta = world.FlushDelta();
        if (removeDelta.ChangedEntities.Count != 0 || removeDelta.RemovedEntityIds.Count != 1 || removeDelta.RemovedEntityIds[0] != 100)
        {
            reason = "debug remove did not create removed delta";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyGameCoreMovement(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
        world.FlushDelta();

        var resolver = new MovementResolveSystem();
        world.NextTick();
        DG.GameCore.MoveResult success = resolver.Resolve(world, MoveCommand.ToTarget(1, new GridCoord(1, 0), MoveCommandSource.PlayerInput, world.ServerTick, 10));
        if (!success.Success || success.FinalCoord.X != 1 || success.FinalCoord.Y != 0)
        {
            reason = "gamecore legal move failed";
            return false;
        }

        WorldDelta successDelta = world.FlushDelta();
        if (successDelta.ChangedEntities.Count != 1 || successDelta.ChangedEntities[0].EntityId != 1)
        {
            reason = "gamecore legal move did not create delta";
            return false;
        }

        world.AddEntity(DefaultWorldConfig.BlockerSpawn(100, new GridCoord(2, 0)));
        world.FlushDelta();
        world.NextTick();
        DG.GameCore.MoveResult blocked = resolver.Resolve(world, MoveCommand.ToTarget(1, new GridCoord(2, 0), MoveCommandSource.PlayerInput, world.ServerTick, 11));
        if (blocked.Success || blocked.FinalCoord.X != 1 || blocked.FinalCoord.Y != 0 || blocked.ErrorCode != DG.GameCore.MoveErrorCode.Blocked)
        {
            reason = "gamecore blocker move was not rejected";
            return false;
        }

        world.AddEntity(DefaultWorldConfig.PlayerSpawn(2, 2, new GridCoord(1, 1)));
        world.FlushDelta();
        world.NextTick();
        DG.GameCore.MoveResult occupied = resolver.Resolve(world, MoveCommand.ToTarget(1, new GridCoord(1, 1), MoveCommandSource.PlayerInput, world.ServerTick, 12));
        if (occupied.Success || occupied.ErrorCode != DG.GameCore.MoveErrorCode.Occupied)
        {
            reason = "gamecore player occupancy was not rejected";
            return false;
        }

        var bounceWorld = new GameWorld();
        bounceWorld.AddEntity(DefaultWorldConfig.BallSpawn(900, new GridCoord(0, 0), Direction.Right, 1));
        bounceWorld.AddEntity(DefaultWorldConfig.BlockerSpawn(901, new GridCoord(1, 0)));
        bounceWorld.FlushDelta();
        bounceWorld.NextTick();
        DG.GameCore.MoveResult bounceBlocker = resolver.Resolve(bounceWorld, MoveCommand.ToDirection(900, Direction.Right, MoveCommandSource.AutoTick, bounceWorld.ServerTick, 0));
        if (bounceBlocker.Success || !bounceBlocker.Bounced || bounceBlocker.FinalCoord.X != 0 || bounceBlocker.FinalDirection != Direction.Left)
        {
            reason = "gamecore ball did not bounce from blocker";
            return false;
        }

        WorldDelta bounceDelta = bounceWorld.FlushDelta();
        if (bounceDelta.ChangedEntities.Count != 1 || bounceDelta.ChangedEntities[0].Direction != Direction.Left)
        {
            reason = "gamecore bounce did not create direction delta";
            return false;
        }

        var playerBounceWorld = new GameWorld();
        playerBounceWorld.AddEntity(DefaultWorldConfig.BallSpawn(910, new GridCoord(0, 0), Direction.Right, 1));
        playerBounceWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(911, 911, new GridCoord(1, 0)));
        playerBounceWorld.FlushDelta();
        playerBounceWorld.NextTick();
        DG.GameCore.MoveResult bouncePlayer = resolver.Resolve(playerBounceWorld, MoveCommand.ToDirection(910, Direction.Right, MoveCommandSource.AutoTick, playerBounceWorld.ServerTick, 0));
        if (bouncePlayer.Success || !bouncePlayer.Bounced || bouncePlayer.ErrorCode != DG.GameCore.MoveErrorCode.Occupied || bouncePlayer.FinalDirection != Direction.Left)
        {
            reason = "gamecore ball did not bounce from player";
            return false;
        }

        var playerBlockedByBallWorld = new GameWorld();
        playerBlockedByBallWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(920, 920, new GridCoord(0, 0)));
        playerBlockedByBallWorld.AddEntity(DefaultWorldConfig.BallSpawn(921, new GridCoord(1, 0), Direction.Left, 1));
        playerBlockedByBallWorld.FlushDelta();
        playerBlockedByBallWorld.NextTick();
        DG.GameCore.MoveResult playerIntoBall = resolver.Resolve(playerBlockedByBallWorld, MoveCommand.ToTarget(920, new GridCoord(1, 0), MoveCommandSource.PlayerInput, playerBlockedByBallWorld.ServerTick, 0));
        if (playerIntoBall.Success || playerIntoBall.FinalCoord.X != 0 || playerIntoBall.FinalCoord.Y != 0 || playerIntoBall.ErrorCode != DG.GameCore.MoveErrorCode.Blocked)
        {
            reason = "player was not blocked by ball";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyAutoMove(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.BallSpawn(900, new GridCoord(0, 0), Direction.Right, 1));
        world.FlushDelta();

        var autoMove = new AutoMoveSystem(new MovementResolveSystem());
        IReadOnlyList<DG.GameCore.MoveResult> results = autoMove.Tick(world);
        if (results.Count != 1 || !results[0].Success)
        {
            reason = "automove did not execute movement command";
            return false;
        }

        if (!world.TryGetEntity(900, out GameEntity ball) ||
            !world.TryGetComponent(ball, out PositionComponent ballPosition) ||
            ballPosition.Coord.X != 1 ||
            ballPosition.Coord.Y != 0)
        {
            reason = "automove did not update ball position";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyMultiplayer(out string reason)
    {
        var world = new GameWorld();
        var manager = new MultiplayerEntityManager<object>(world);
        var sessionA = new object();
        var sessionB = new object();
        var sessionC = new object();

        manager.Join(sessionA, out PlayerEntitySnapshot playerA);
        manager.Join(sessionB, out PlayerEntitySnapshot playerB);
        manager.Join(sessionC, out PlayerEntitySnapshot playerC);

        if (playerA.EntityId == playerB.EntityId || playerA.EntityId == playerC.EntityId || playerB.EntityId == playerC.EntityId)
        {
            reason = "multiplayer manager assigned duplicate entity ids";
            return false;
        }

        if (playerA.Coord.Y == playerB.Coord.Y || playerA.Coord.Y == playerC.Coord.Y || playerB.Coord.Y == playerC.Coord.Y)
        {
            reason = "multiplayer manager assigned overlapping spawn rows";
            return false;
        }

        bool repeatedCreated = manager.Join(sessionA, out PlayerEntitySnapshot repeatedA);
        if (repeatedCreated || repeatedA.EntityId != playerA.EntityId || manager.OnlineCount != 3)
        {
            reason = "repeat join allocated a new entity";
            return false;
        }

        if (!manager.TryAuthorizeMove(sessionA, playerA.EntityId, out long boundA, out MoveErrorCode authCode, out string _) || boundA != playerA.EntityId || authCode != MoveErrorCode.None)
        {
            reason = "owner move authorization failed";
            return false;
        }

        if (manager.TryAuthorizeMove(sessionA, playerB.EntityId, out _, out authCode, out _) || authCode != MoveErrorCode.UnauthorizedEntity)
        {
            reason = "unauthorized entity move was not rejected";
            return false;
        }

        if (manager.TryAuthorizeMove(new object(), playerA.EntityId, out _, out authCode, out _) || authCode != MoveErrorCode.NotJoined)
        {
            reason = "not joined session move was not rejected";
            return false;
        }

        var resolver = new MovementResolveSystem();
        world.NextTick();
        DG.GameCore.MoveResult occupied = resolver.Resolve(world, MoveCommand.ToTarget(playerA.EntityId, new GridCoord(playerB.Coord.X, playerB.Coord.Y), MoveCommandSource.PlayerInput, world.ServerTick, 0));
        if (occupied.Success || occupied.ErrorCode != DG.GameCore.MoveErrorCode.Occupied)
        {
            reason = "multiplayer occupied move was not rejected";
            return false;
        }

        IReadOnlyList<PlayerEntitySnapshot> snapshots = manager.EnumerateSnapshots();
        if (snapshots.Count != 3)
        {
            reason = "player snapshots were not enumerated";
            return false;
        }

        IReadOnlyList<object> availablePlayers = manager.EnumerateAvailable(session => !ReferenceEquals(session, sessionB));
        if (availablePlayers.Count != 2 || manager.OnlineCount != 2 || manager.TryGetEntity(sessionB, out _) || world.TryGetEntity(playerB.EntityId, out _))
        {
            reason = "unavailable session was not removed";
            return false;
        }

        var fullSpawnWorld = new GameWorld();
        for (int y = 0; y < 1024; y++)
        {
            fullSpawnWorld.AddEntity(DefaultWorldConfig.BlockerSpawn(10000 + y, new GridCoord(0, y)));
        }

        var fullSpawnManager = new MultiplayerEntityManager<object>(fullSpawnWorld);
        if (fullSpawnManager.TryJoin(new object(), out _, out _, out string joinFailureReason) || joinFailureReason != "no spawn coord")
        {
            reason = "join failure did not return reason";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyObserverRegistry(out string reason)
    {
        var registry = new MoveObserverRegistry<object>();
        var observerA = new object();
        var observerB = new object();
        registry.RegisterObserver(observerA, 1);
        registry.RegisterObserver(observerB, 2);
        registry.RefreshOwner(1, observerA);
        IReadOnlyList<object> observers = registry.EnumerateAvailable(_ => true);
        if (observers.Count != 2)
        {
            reason = "registered observers were not enumerated";
            return false;
        }

        IReadOnlyList<long> observedEntityIds = registry.EnumerateObservedEntityIds();
        if (observedEntityIds.Count != 2 || !observedEntityIds.Contains(1) || !observedEntityIds.Contains(2))
        {
            reason = "observed entity ids were not enumerated";
            return false;
        }

        registry.RefreshOwner(1, observerB);
        observers = registry.EnumerateAvailable(_ => true);
        if (observers.Count != 2 || registry.OwnerCount != 1)
        {
            reason = "owner refresh changed observer set";
            return false;
        }

        observers = registry.EnumerateAvailable(observer => !ReferenceEquals(observer, observerA));
        if (observers.Count != 1 || !ReferenceEquals(observers[0], observerB) || registry.ObserverCount != 1)
        {
            reason = "unavailable observer was not removed";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
