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

        if (!VerifyComponentSystemWorkflow(out reason))
        {
            return false;
        }

        if (!VerifyGameCoreStorageAndRuntimeIds(out reason))
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

        if (!VerifyAuthoritativeTickQueue(out reason))
        {
            return false;
        }

        if (!VerifyInputIntentLayer(out reason))
        {
            return false;
        }

        if (!VerifyAuthoritativeTickSyncDiagnostics(out reason))
        {
            return false;
        }

        if (!VerifyParallelCandidateBoundary(out reason))
        {
            return false;
        }

        if (!VerifyStateDrivenPush(out reason))
        {
            return false;
        }

        if (!VerifyDeferredQueueDedupe(out reason))
        {
            return false;
        }

        if (!VerifyStateDrivenActionDeterminism(out reason))
        {
            return false;
        }

        if (!VerifyPortConnectedPush(out reason))
        {
            return false;
        }

        if (!VerifyRotatePivotPushResponse(out reason))
        {
            return false;
        }

        if (!VerifyBehaviorArbitration(out reason))
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

        if (!VerifyGeneratedRegisteredStrategyRuntimePath(out reason))
        {
            return false;
        }

        if (!VerifyEffectApplicationLayer(out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyGameCoreStorageAndRuntimeIds(out string reason)
    {
        var specId = new ActionSpecId(7101, "server_primary_push");
        var specAlias = new ActionSpecId(7101, "server_alias_push");
        var policyId = new BlockedResultPolicyId(7201, "server_reject_policy");
        var policyAlias = new BlockedResultPolicyId(7201, "server_reject_policy_alias");
        var registry = new ActionSpecRegistry(new[]
        {
            new ActionSpec(specId, ActionPrimitive.Move, ActionSourceKind.Mechanism, WorldActionPriority.Mechanism, WorldTag.SourceMechanism, WorldTag.AbilityMechanismPush, WorldTag.None, WorldTag.None, ActionTargetRule.DirectionFromRequest, policyId, ActionConflictPolicy.ExclusiveTargetCell, ActionInterruptPolicy.HigherPriorityInterruptsLower, ActionMergePolicy.SameClaim, ActionPlanRule.MoveBody, ActionCommitRule.None)
        }, new[]
        {
            BlockedResultPolicyFactory.RejectPolicy(policyId)
        }, new Dictionary<string, ActionSpecId>
        {
            ["server_alias_push"] = specAlias
        }, new Dictionary<string, BlockedResultPolicyId>
        {
            ["server_reject_policy_alias"] = policyAlias
        });

        if (!specId.Equals(specAlias) ||
            !policyId.Equals(policyAlias) ||
            !registry.Get("server_alias_push").SpecId.Equals(specId) ||
            !registry.GetBlockedResultPolicy("server_reject_policy_alias").PolicyId.Equals(policyId))
        {
            reason = "runtime id alias resolution failed";
            return false;
        }

        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(ServerGameConfigPath.FindGameCoreConfigDirectory(), "..", "..", "..", ".."));
        string storageSource = string.Concat(
            System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Shared", "DG.GameCore", "World", "Storage", "IWorldDataStorage.cs")),
            System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Shared", "DG.GameCore", "World", "Storage", "IndexedWorldDataStorage.cs")),
            System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Shared", "DG.GameCore", "World", "Storage", "ComponentPool.cs")),
            System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Shared", "DG.GameCore", "World", "Storage", "EntityRegistry.cs")),
            System.IO.File.ReadAllText(System.IO.Path.Combine(root, "Shared", "DG.GameCore", "World", "Storage", "QueryCache.cs")));
        string journalSource = System.IO.Path.Combine(root, "Shared", "DG.GameCore", "World", "Storage", "WorldStorageQuery.cs");
        string storageText = storageSource;
        string journalText = System.IO.File.Exists(journalSource) ? System.IO.File.ReadAllText(journalSource) : string.Empty;
        if (string.IsNullOrEmpty(storageText) ||
            storageText.Contains("Dictionary<") ||
            storageText.Contains("HashSet<") ||
            storageText.Contains("SortedDictionary<") ||
            !storageText.Contains("ComponentPool<TComponent>") ||
            !storageText.Contains("EntityRegistry") ||
            !storageText.Contains("QueryCache") ||
            string.IsNullOrEmpty(journalText) ||
            !journalText.Contains("DirtyWorldJournal"))
        {
            reason = "ecs storage source scan failed";
            return false;
        }

        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(991001, 991001, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.ConveyorSpawn(991002, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.BallSpawn(991003, new GridCoord(2, 0), Direction.Left, 2));
        world.ResetObservations();

        IReadOnlyList<GameEntity> pushOnEnter = world.QueryEntities(ComponentQueryDescriptor.With<PositionComponent, DirectionComponent, PushOnEnterComponent>(), EntityIterationOrder.EntityId);
        IReadOnlyList<GameEntity> autoMove = world.QueryEntities(ComponentQueryDescriptor.With<PositionComponent, DirectionComponent, AutoMoveComponent>(), EntityIterationOrder.EntityId);
        world.ResetObservations();
        IReadOnlyList<PushOnEnterQueryResult> narrowPushOnEnter = world.QueryPushOnEnter(EntityIterationOrder.EntityId);
        IReadOnlyList<AutoMoveQueryResult> narrowAutoMove = world.QueryAutoMove(EntityIterationOrder.EntityId);
        if (pushOnEnter.Count != 1 ||
            pushOnEnter[0].EntityId != 991002 ||
            autoMove.Count != 1 ||
            autoMove[0].EntityId != 991003 ||
            narrowPushOnEnter.Count != 1 ||
            narrowPushOnEnter[0].EntityId != 991002 ||
            narrowPushOnEnter[0].Position != new GridCoord(0, 0) ||
            narrowPushOnEnter[0].Direction != Direction.Right ||
            !narrowPushOnEnter[0].PushOnEnter.OutputSpecId.IsValid ||
            narrowAutoMove.Count != 1 ||
            narrowAutoMove[0].EntityId != 991003 ||
            narrowAutoMove[0].Position != new GridCoord(2, 0) ||
            narrowAutoMove[0].Direction != Direction.Left ||
            narrowAutoMove[0].AutoMove.IntervalTicks != 2 ||
            world.Observations.TryGetComponentCount != 0)
        {
            reason = "component query boundary returned unexpected entities or narrow query leaked component lookups";
            return false;
        }

        if (!world.TryGetEntityLocation(991002, out EntityLocation location) ||
            !location.HasSpatialLocation ||
            location.Coord != new GridCoord(0, 0))
        {
            reason = "entity location boundary failed";
            return false;
        }

        world.RemoveComponent<PushOnEnterComponent>(pushOnEnter[0]);
        if (world.QueryEntities(ComponentQueryDescriptor.With<PositionComponent, DirectionComponent, PushOnEnterComponent>(), EntityIterationOrder.EntityId).Count != 0)
        {
            reason = "ecs query cache kept removed push-on-enter component";
            return false;
        }

        world.SetComponent(pushOnEnter[0], new PushOnEnterComponent(new ActionSpecId("mechanism_push")));
        if (world.QueryEntities(ComponentQueryDescriptor.With<PositionComponent, DirectionComponent, PushOnEnterComponent>(), EntityIterationOrder.EntityId).Count != 1)
        {
            reason = "ecs query cache did not include restored push-on-enter component";
            return false;
        }

        if (!world.RemoveEntity(991001) ||
            world.TryGetEntity(991001, out _) ||
            world.QueryEntities(ComponentQueryDescriptor.With<PlayerControlComponent>(), EntityIterationOrder.EntityId).Count != 0)
        {
            reason = "ecs entity removal did not clear entity and component query state";
            return false;
        }

        if (!world.AddEntity(DefaultWorldConfig.PlayerSpawn(991004, 991004, new GridCoord(4, 0))) ||
            !world.TryGetEntity(991004, out GameEntity reusedPlayer) ||
            world.TryGetEntity(991001, out _) ||
            !world.HasComponent<PlayerControlComponent>(reusedPlayer) ||
            !world.TryGetComponent(reusedPlayer, out PositionComponent reusedPosition) ||
            reusedPosition.Coord != new GridCoord(4, 0))
        {
            reason = "ecs entity slot reuse or sparse component lookup failed";
            return false;
        }

        world.RemoveComponent<DirectionComponent>(autoMove[0]);
        if (world.QueryEntities(ComponentQueryDescriptor.With<PositionComponent, DirectionComponent, AutoMoveComponent>(), EntityIterationOrder.EntityId).Count != 0)
        {
            reason = "ecs component pool removal did not update auto move query";
            return false;
        }

        world.SetComponent(reusedPlayer, new PushableComponent());
        if (!world.TryGetFirstBlockingAt(new GridCoord(4, 0), null!, out BlockingSpatialQueryResult blocking) ||
            blocking.EntityId != 991004 ||
            world.GetColliderEntitiesAt(new GridCoord(4, 0), DefaultWorldConfig.PlayerTarget).Count != 1 ||
            world.GetPushableEntitiesAt(new GridCoord(4, 0), DefaultWorldConfig.PlayerTarget).Count != 1)
        {
            reason = "narrow spatial query boundary failed";
            return false;
        }

        world.FlushDelta();
        world.ResetObservations();
        world.SetComponent(reusedPlayer, new DirectionComponent(Direction.Up));
        world.AddAnimationMetadata(new WorldDeltaAnimationMetadata(reusedPlayer.EntityId, world.ServerTick, WorldDeltaMotionKind.PlayerMove, "player_move", Direction.Up));
        world.RecordTouchedDiagnostics(new[] { reusedPlayer.EntityId });
        WorldDelta dirtyDelta = world.FlushDelta();
        if (dirtyDelta.ChangedEntities.Count != 1 ||
            dirtyDelta.ChangedEntities[0].EntityId != reusedPlayer.EntityId ||
            dirtyDelta.AnimationMetadata.Count != 1 ||
            world.Observations.TouchedDiagnosticCount != 1)
        {
            reason = "dirty responsibility boundary failed";
            return false;
        }

        WorldDelta delta = world.FlushDelta();
        IReadOnlyList<EntitySnapshot> snapshot = world.CreateSnapshot();
        GameWorldObservation observation = world.Observations;
        if (delta.ChangedEntities.Count != 0 ||
            snapshot.Count != 3 ||
            observation.DeltaFlushCount < 2 ||
            observation.SnapshotBuildCount < 1)
        {
            reason = "storage observation or snapshot/delta semantics failed changed:" + delta.ChangedEntities.Count +
                " snapshot:" + snapshot.Count +
                " deltaFlush:" + observation.DeltaFlushCount +
                " snapshotBuild:" + observation.SnapshotBuildCount;
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
            provider.GetWorldSpawns(DefaultWorldConfig.DemoWorldId).Count != 8 ||
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

        if (!provider.TryGetArchetype(DefaultWorldConfig.PushableBlockerConfigId, out EntityArchetype pushableArchetype) ||
            pushableArchetype.Components.Count != 4 ||
            !pushableArchetype.Components.Contains(ComponentKind.Position) ||
            !pushableArchetype.Components.Contains(ComponentKind.Collider) ||
            !pushableArchetype.Components.Contains(ComponentKind.Blocking) ||
            !pushableArchetype.Components.Contains(ComponentKind.Pushable))
        {
            reason = "luban pushable archetype components invalid";
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

        if (!provider.TryGetArchetype(DefaultWorldConfig.PortConnectorBlockerConfigId, out EntityArchetype portConnectorArchetype) ||
            portConnectorArchetype.Components.Count != 6 ||
            !portConnectorArchetype.Components.Contains(ComponentKind.Position) ||
            !portConnectorArchetype.Components.Contains(ComponentKind.Direction) ||
            !portConnectorArchetype.Components.Contains(ComponentKind.Collider) ||
            !portConnectorArchetype.Components.Contains(ComponentKind.Blocking) ||
            !portConnectorArchetype.Components.Contains(ComponentKind.Pushable) ||
            !portConnectorArchetype.Components.Contains(ComponentKind.PortConnector) ||
            !portConnectorArchetype.Tags.Contains("Entity.PortConnectorBlocker") ||
            !provider.TryGetPortConnector(DefaultWorldConfig.PortConnectorBlockerConfigId, out PortConnectorConfig portConnectorConfig) ||
            portConnectorConfig.LocalPorts != (DirectionMask.Left | DirectionMask.Right))
        {
            reason = "luban port connector archetype invalid";
            return false;
        }

        IReadOnlyList<EntitySpawnSpec> demoSpawns = provider.GetWorldSpawns(DefaultWorldConfig.DemoWorldId);
        if (demoSpawns.Count != 8)
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

        if (!world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(102, new GridCoord(2, 0))) ||
            !world.TryGetEntity(102, out GameEntity pushable) ||
            !world.HasComponent<PositionComponent>(pushable) ||
            !world.HasComponent<ColliderComponent>(pushable) ||
            !world.HasComponent<BlockingComponent>(pushable) ||
            !world.HasComponent<PushableComponent>(pushable))
        {
            reason = "luban pushable build failed";
            return false;
        }

        if (!world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(103, new GridCoord(3, 0), Direction.Right)) ||
            !world.TryGetEntity(103, out GameEntity portConnector) ||
            !world.HasComponent<DirectionComponent>(portConnector) ||
            !world.HasComponent<PortConnectorComponent>(portConnector) ||
            !world.TryGetComponent(portConnector, out PortConnectorComponent portConnectorComponent) ||
            portConnectorComponent.LocalPorts != (DirectionMask.Left | DirectionMask.Right))
        {
            reason = "luban port connector build failed";
            return false;
        }

        if (world.GetEntitiesAt(new GridCoord(0, 0), DefaultWorldConfig.PlayerTarget).Count != 1 ||
            world.GetEntitiesAt(new GridCoord(1, 0), DefaultWorldConfig.BallTarget).Count != 1 ||
            world.GetEntitiesAt(new GridCoord(2, 0), DefaultWorldConfig.BlockerTarget).Count != 1 ||
            world.FlushDelta().ChangedEntities.Count != 4)
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

    private static bool VerifyComponentSystemWorkflow(out string reason)
    {
        IReadOnlyDictionary<string, int> runtimeValues = Enum.GetValues(typeof(ComponentKind))
            .Cast<ComponentKind>()
            .ToDictionary(kind => kind.ToString(), kind => (int)kind);
        IReadOnlyDictionary<string, int> lubanValues = Enum.GetValues(typeof(cfg.gamecore.ComponentKind))
            .Cast<cfg.gamecore.ComponentKind>()
            .ToDictionary(kind => kind.ToString(), kind => (int)kind);
        if (!runtimeValues.OrderBy(pair => pair.Key).SequenceEqual(lubanValues.OrderBy(pair => pair.Key)))
        {
            reason = "component kind runtime and luban generated enum drifted";
            return false;
        }

        ComponentKind[] runtimeKinds = Enum.GetValues(typeof(ComponentKind)).Cast<ComponentKind>().ToArray();
        ComponentKind[] registeredKinds = ComponentApplicationRegistry.Default.RegisteredKinds.ToArray();
        if (!runtimeKinds.OrderBy(kind => (int)kind).SequenceEqual(registeredKinds.OrderBy(kind => (int)kind)))
        {
            reason = "component application registry does not cover every component kind";
            return false;
        }

        var world = new GameWorld();
        var archetype = new EntityArchetype(990101, 990101, DefaultWorldConfig.PlayerTarget, runtimeKinds, Array.Empty<string>(), 4);
        var spawn = new EntitySpawnSpec(990101, 990101, new GridCoord(3, 4), Direction.Left, 990101, 2);
        if (!EntityBuilder.AddEntity(world, new SingleArchetypeProvider(archetype), spawn) ||
            !world.TryGetEntity(990101, out GameEntity entity) ||
            !world.HasComponent<PositionComponent>(entity) ||
            !world.HasComponent<DirectionComponent>(entity) ||
            !world.HasComponent<ColliderComponent>(entity) ||
            !world.HasComponent<BlockingComponent>(entity) ||
            !world.HasComponent<BouncableComponent>(entity) ||
            !world.HasComponent<AutoMoveComponent>(entity) ||
            !world.HasComponent<PlayerControlComponent>(entity) ||
            !world.HasComponent<PushOnEnterComponent>(entity) ||
            !world.HasComponent<PushableComponent>(entity) ||
            !world.HasComponent<PortConnectorComponent>(entity))
        {
            reason = "component application registry failed to apply all current components";
            return false;
        }

        try
        {
            ComponentApplicationRegistry.Default.Apply(world, new SingleArchetypeProvider(archetype), entity, archetype, spawn, (ComponentKind)9999);
            reason = "unknown component kind did not fail";
            return false;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Unsupported component kind"))
        {
        }

        if (!VerifyFormalConfigExists(out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyFormalConfigExists(out string reason)
    {
        IGameConfigProvider luban = CreateLubanProvider();
        int[] requiredConfigs =
        {
            DefaultWorldConfig.PlayerConfigId,
            DefaultWorldConfig.BallConfigId,
            DefaultWorldConfig.BlockerConfigId,
            DefaultWorldConfig.PushableBlockerConfigId,
            DefaultWorldConfig.PortConnectorBlockerConfigId,
            DefaultWorldConfig.ConveyorConfigId,
            DefaultWorldConfig.WindFieldConfigId
        };

        for (int i = 0; i < requiredConfigs.Length; i++)
        {
            if (!luban.TryGetArchetype(requiredConfigs[i], out _))
            {
                reason = "formal archetype missing: " + requiredConfigs[i];
                return false;
            }
        }

        if (!luban.TryGetPushOnEnter(DefaultWorldConfig.ConveyorConfigId, out PushOnEnterConfig conveyorOutput) ||
            !conveyorOutput.OutputSpecId.Equals(new ActionSpecId("mechanism_push")) ||
            conveyorOutput.OutputCostTicks != 3)
        {
            reason = "formal conveyor push on enter config invalid";
            return false;
        }

        if (!luban.TryGetPushOnEnter(DefaultWorldConfig.WindFieldConfigId, out PushOnEnterConfig windOutput) ||
            !windOutput.OutputSpecId.Equals(new ActionSpecId("configured_wind_push")) ||
            windOutput.OutputCostTicks != 2)
        {
            reason = "formal wind field push on enter config invalid";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private sealed class SingleArchetypeProvider : IGameConfigProvider
    {
        private readonly EntityArchetype archetype;

        public SingleArchetypeProvider(EntityArchetype archetype)
        {
            this.archetype = archetype;
        }

        public bool TryGetArchetype(int configId, out EntityArchetype found)
        {
            found = archetype;
            return configId == archetype.ConfigId;
        }

        public IReadOnlyList<EntityArchetype> GetEntityArchetypes()
        {
            return new[] { archetype };
        }

        public IReadOnlyList<EntitySpawnSpec> GetWorldSpawns(string worldId)
        {
            return Array.Empty<EntitySpawnSpec>();
        }

        public bool TryGetPlayerSpawnRule(string ruleId, out PlayerSpawnRule rule)
        {
            rule = default;
            return false;
        }

        public bool TryGetPortConnector(int configId, out PortConnectorConfig config)
        {
            config = new PortConnectorConfig(configId, DirectionMask.All);
            return true;
        }

        public bool TryGetPushOnEnter(int configId, out PushOnEnterConfig config)
        {
            config = new PushOnEnterConfig(configId, "mechanism_push", 1);
            return true;
        }

        public bool TryGetEffectSpec(EffectSpecId effectSpecId, out EffectSpec spec)
        {
            return FallbackGameConfigProvider.Instance.TryGetEffectSpec(effectSpecId, out spec);
        }
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
        var queue = new WorldActionQueue();
        queue.EnqueueConfiguredMove("mechanism_push", 1, Direction.Right, world.ServerTick - 1, 1);
        StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);
        if (result.ActionResults.Count != 1 ||
            !result.ActionResults.Values.First().Success ||
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
        var blockedQueue = new WorldActionQueue();
        blockedQueue.EnqueueConfiguredMove("mechanism_push", 2, Direction.Right, blockedWorld.ServerTick - 1, 1);
        StateDrivenRuleExecutionResult blockedResult = new StateDrivenRuleExecutionSystem().Tick(blockedWorld, blockedQueue.DrainReady(blockedWorld.ServerTick), blockedWorld.ServerTick);
        if (blockedResult.ActionResults.Count != 1 ||
            blockedResult.ActionResults.Values.First().Success ||
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

        if (!service.TrySetTag(100, WorldTag.ImmuneMechanismPush, true, out string tagReason) ||
            !string.IsNullOrEmpty(tagReason) ||
            !world.TryGetEntity(100, out GameEntity taggedEntity) ||
            !world.HasTag(taggedEntity, WorldTag.ImmuneMechanismPush))
        {
            reason = "debug set tag failed";
            return false;
        }

        WorldDelta tagDelta = world.FlushDelta();
        if (tagDelta.ChangedEntities.Count != 1 || tagDelta.ChangedEntities[0].EntityId != 100)
        {
            reason = "debug set tag did not create changed delta";
            return false;
        }

        if (service.TrySetTag(100, WorldTag.None, true, out _) ||
            service.TrySetTag(404, WorldTag.BlockPlayerMove, true, out _) ||
            world.FlushDelta().ChangedEntities.Count != 0)
        {
            reason = "failed debug set tag produced delta";
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

        world.NextTick();
        DG.GameCore.MoveResult success = ExecutePlayerMove(world, 1, new GridCoord(1, 0), 10);
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
        DG.GameCore.MoveResult blocked = ExecutePlayerMove(world, 1, new GridCoord(2, 0), 11);
        if (blocked.Success || blocked.FinalCoord.X != 1 || blocked.FinalCoord.Y != 0 || blocked.ErrorCode != DG.GameCore.MoveErrorCode.Blocked)
        {
            reason = "gamecore blocker move was not rejected";
            return false;
        }

        world.AddEntity(DefaultWorldConfig.PlayerSpawn(2, 2, new GridCoord(1, 1)));
        world.FlushDelta();
        world.NextTick();
        DG.GameCore.MoveResult occupied = ExecutePlayerMove(world, 1, new GridCoord(1, 1), 12);
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
        DG.GameCore.MoveResult bounceBlocker = ExecuteAutoMove(bounceWorld, 900, 0);
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
        DG.GameCore.MoveResult bouncePlayer = ExecuteAutoMove(playerBounceWorld, 910, 0);
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
        DG.GameCore.MoveResult playerIntoBall = ExecutePlayerMove(playerBlockedByBallWorld, 920, new GridCoord(1, 0), 0);
        if (playerIntoBall.Success || playerIntoBall.FinalCoord.X != 0 || playerIntoBall.FinalCoord.Y != 0 || playerIntoBall.ErrorCode != DG.GameCore.MoveErrorCode.Blocked)
        {
            reason = "player was not blocked by ball";
            return false;
        }

        var pushBlockedWorld = new GameWorld();
        pushBlockedWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(950, 950, new GridCoord(0, 0)));
        pushBlockedWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(951, new GridCoord(1, 0)));
        pushBlockedWorld.AddEntity(DefaultWorldConfig.BlockerSpawn(952, new GridCoord(2, 0)));
        pushBlockedWorld.FlushDelta();
        pushBlockedWorld.NextTick();
        var pushBlockedQueue = new WorldActionQueue();
        var pushBlockedSystem = new StateDrivenRuleExecutionSystem();
        WorldAction pushBlockedAction = pushBlockedQueue.EnqueuePlayerMove(950, new GridCoord(1, 0), 15);
        StateDrivenRuleExecutionResult pushBlockedFirst = pushBlockedSystem.Tick(pushBlockedWorld, pushBlockedQueue.DrainReady(pushBlockedWorld.ServerTick), pushBlockedWorld.ServerTick);
        for (int i = 0; i < pushBlockedFirst.DeferredActions.Count; i++)
        {
            pushBlockedQueue.EnqueueDeferred(pushBlockedFirst.DeferredActions[i]);
        }
        pushBlockedWorld.NextTick();
        StateDrivenRuleExecutionResult pushBlockedSecond = pushBlockedSystem.Tick(pushBlockedWorld, pushBlockedQueue.DrainReady(pushBlockedWorld.ServerTick), pushBlockedWorld.ServerTick);
        if (!pushBlockedFirst.ActionResults.TryGetValue(pushBlockedAction.ActionId, out MoveResult pushBlockedSource) ||
            !pushBlockedSource.Success ||
            !pushBlockedFirst.Reasons.Contains("bounded/deferred-output") ||
            !pushBlockedSecond.Reasons.Contains("blocked cell") ||
            pushBlockedWorld.FlushDelta().ChangedEntities.Count != 0 ||
            !pushBlockedWorld.TryGetEntity(950, out GameEntity pushBlockedPlayer) ||
            !pushBlockedWorld.TryGetEntity(951, out GameEntity pushBlockedBox) ||
            !pushBlockedWorld.TryGetComponent(pushBlockedPlayer, out PositionComponent pushBlockedPlayerPosition) ||
            !pushBlockedWorld.TryGetComponent(pushBlockedBox, out PositionComponent pushBlockedBoxPosition) ||
            pushBlockedPlayerPosition.Coord != new GridCoord(0, 0) ||
            pushBlockedBoxPosition.Coord != new GridCoord(1, 0))
        {
            reason = "gamecore blocked push was not rejected cleanly";
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

        world.NextTick();
        DG.GameCore.MoveResult result = ExecuteAutoMove(world, 900, 0);
        if (!result.Success)
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

    private static bool VerifyAuthoritativeTickQueue(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
        world.FlushDelta();

        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);

        AuthoritativeMoveInput legal = queue.EnqueueMove(1, new GridCoord(1, 0), 101);
        if (legal.IsCompleted ||
            !world.TryGetEntity(1, out GameEntity player) ||
            !world.TryGetComponent(player, out PositionComponent before) ||
            before.Coord != new GridCoord(0, 0))
        {
            reason = "queued move changed world before tick";
            return false;
        }

        WorldDelta legalDelta = runner.Tick();
        MoveResult legalResult = legal.WaitAsync().GetResult();
        if (!legalResult.Success ||
            legalResult.FinalCoord != new GridCoord(1, 0) ||
            legalResult.ClientTick != 101 ||
            legalDelta.ChangedEntities.Count != 1 ||
            legalDelta.ChangedEntities[0].EntityId != 1 ||
            !world.TryGetComponent(player, out PositionComponent after) ||
            after.Coord != new GridCoord(1, 0))
        {
            reason = "queued move was not resolved by tick";
            return false;
        }

        AuthoritativeMoveInput illegal = queue.EnqueueMove(1, 2, Direction.None, 9002, 102, world.ServerTick);
        WorldDelta illegalDelta = runner.Tick();
        MoveResult illegalResult = illegal.WaitAsync().GetResult();
        if (illegalResult.Success ||
            illegalResult.ErrorCode != MoveErrorCode.InvalidDirection ||
            illegalDelta.ChangedEntities.Count != 0 ||
            illegalDelta.RemovedEntityIds.Count != 0)
        {
            reason = "invalid direction queued move created dirty delta";
            return false;
        }

        if (!VerifyAuthoritativeBeatInputBuffer(out reason))
        {
            return false;
        }

        var autoWorld = new GameWorld();
        autoWorld.AddEntity(DefaultWorldConfig.BallSpawn(10, new GridCoord(0, 0), Direction.Right, 1));
        autoWorld.FlushDelta();
        var autoRunner = new AuthoritativeWorldTickRunner(
            autoWorld,
            new AuthoritativeInputQueue(),
            new AuthoritativeWorldSyncSystem(autoWorld),
            1);
        autoRunner.Tick();
        if (!autoWorld.TryGetEntity(10, out GameEntity ball) ||
            !autoWorld.TryGetComponent(ball, out PositionComponent ballPosition) ||
            ballPosition.Coord != new GridCoord(1, 0))
        {
            reason = "tick runner did not execute auto move";
            return false;
        }

        var pushWorld = new GameWorld();
        pushWorld.AddEntity(DefaultWorldConfig.ConveyorSpawn(20, new GridCoord(0, 0), Direction.Right));
        pushWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(21, 21, new GridCoord(0, 0)));
        pushWorld.FlushDelta();
        var pushRunner = new AuthoritativeWorldTickRunner(
            pushWorld,
            new AuthoritativeInputQueue(),
            new AuthoritativeWorldSyncSystem(pushWorld),
            1);
        pushRunner.Tick();
        pushRunner.Tick();
        if (pushWorld.TryGetEntity(21, out GameEntity tooEarlyPlayer) &&
            pushWorld.TryGetComponent(tooEarlyPlayer, out PositionComponent tooEarlyPosition) &&
            tooEarlyPosition.Coord != new GridCoord(0, 0))
        {
            reason = "push on enter ignored configured cost ticks";
            return false;
        }

        pushRunner.Tick();
        if (!pushWorld.TryGetEntity(21, out GameEntity pushedPlayer) ||
            !pushWorld.TryGetComponent(pushedPlayer, out PositionComponent pushedPosition) ||
            pushedPosition.Coord != new GridCoord(1, 0))
        {
            reason = "tick runner did not execute push on enter";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyAuthoritativeBeatInputBuffer(out string reason)
    {
        var queue = new AuthoritativeInputQueue();
        AuthoritativeMoveInput first = queue.EnqueueMove(10, 5, Direction.Left, 1, 100, 4);
        AuthoritativeMoveInput second = queue.EnqueueMove(10, 5, Direction.Right, 2, 101, 4);
        if (!first.IsCompleted ||
            first.Status != AuthoritativePlayerInputStatus.Replaced ||
            second.IsCompleted ||
            queue.PendingMoveCount != 1)
        {
            reason = "same beat input replacement failed";
            return false;
        }

        AuthoritativeMoveInput otherEntity = queue.EnqueueMove(11, 5, Direction.Up, 3, 102, 4);
        IReadOnlyList<AuthoritativeMoveInput> futureDrain = queue.DrainMoves(4);
        if (futureDrain.Count != 0 || queue.PendingMoveCount != 2)
        {
            reason = "future beat input drained early";
            return false;
        }

        IReadOnlyList<AuthoritativeMoveInput> drained = queue.DrainMoves(5);
        if (drained.Count != 2 ||
            drained[0].EntityId != 10 ||
            drained[0].Direction != Direction.Right ||
            drained[1].EntityId != 11 ||
            second.Status != AuthoritativePlayerInputStatus.Consumed ||
            otherEntity.Status != AuthoritativePlayerInputStatus.Consumed ||
            queue.DrainMoves(5).Count != 0)
        {
            reason = "beat drain did not return final inputs once";
            return false;
        }

        AuthoritativeMoveInput expired = queue.EnqueueMove(12, 4, Direction.Down, 4, 103, 5);
        if (!expired.IsCompleted ||
            expired.Status != AuthoritativePlayerInputStatus.Expired ||
            queue.PendingMoveCount != 0)
        {
            reason = "expired beat input entered queue";
            return false;
        }

        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(20, 20, new GridCoord(3, 3)));
        world.FlushDelta();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);
        AuthoritativeMoveInput right = queue.EnqueueMove(20, 1, Direction.Right, 5, 104, world.ServerTick);
        runner.Tick();
        MoveResult result = right.WaitAsync().GetResult();
        if (!result.Success ||
            result.FinalCoord != new GridCoord(4, 3) ||
            right.Status != AuthoritativePlayerInputStatus.Resolved)
        {
            reason = "direction input was not resolved from authoritative coord";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyInputIntentLayer(out string reason)
    {
        var queue = new AuthoritativeInputQueue();
        InputIntent firstIntent = InputIntent.PlayerMove(10, Direction.Left, 5, 7001, 100, 4);
        InputIntent secondIntent = InputIntent.PlayerMove(10, Direction.Right, 5, 7002, 101, 4);
        AuthoritativeMoveInput first = queue.EnqueueIntent(firstIntent, 4);
        AuthoritativeMoveInput second = queue.EnqueueIntent(secondIntent, 4);
        if (!first.IsCompleted ||
            first.Status != AuthoritativePlayerInputStatus.Replaced ||
            second.IsCompleted ||
            queue.PendingMoveCount != 1)
        {
            reason = "input intent replacement failed";
            return false;
        }

        AuthoritativeMoveInput otherActor = queue.EnqueueIntent(InputIntent.PlayerMove(11, Direction.Up, 5, 7003, 102, 4), 4);
        IReadOnlyList<AuthoritativeMoveInput> drained = queue.DrainMoves(5);
        if (drained.Count != 2 ||
            drained[0].Intent.InputKind != InputKind.Move ||
            drained[0].Direction != Direction.Right ||
            drained[1].EntityId != 11 ||
            otherActor.Status != AuthoritativePlayerInputStatus.Consumed)
        {
            reason = "input intent drain did not preserve actor boundaries";
            return false;
        }

        AuthoritativeMoveInput duplicate = queue.EnqueueIntent(InputIntent.PlayerMove(12, Direction.Down, 6, 7002, 103, 5), 5);
        if (!duplicate.IsCompleted || duplicate.Status != AuthoritativePlayerInputStatus.Rejected)
        {
            reason = "duplicate input intent was not rejected";
            return false;
        }

        var world = new GameWorld();
        var manager = new MultiplayerEntityManager<object>(world);
        var sessionA = new object();
        var sessionB = new object();
        manager.Join(sessionA, out PlayerEntitySnapshot playerA);
        manager.Join(sessionB, out PlayerEntitySnapshot playerB);
        var authorization = new PlayerIntentAuthorization<object>(manager);
        InputIntent allowed = InputIntent.PlayerMove(playerA.EntityId, Direction.Right, 1, 7101, 1, world.ServerTick);
        InputIntent denied = InputIntent.PlayerMove(playerB.EntityId, Direction.Right, 1, 7102, 1, world.ServerTick);
        if (!authorization.Authorize(sessionA, allowed, out long boundA).Accepted ||
            boundA != playerA.EntityId ||
            authorization.Authorize(sessionA, denied, out _).Accepted)
        {
            reason = "input intent authorization boundary failed";
            return false;
        }

        var blockedWorld = new GameWorld();
        blockedWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(20, 20, new GridCoord(0, 0)));
        blockedWorld.AddEntity(DefaultWorldConfig.BlockerSpawn(21, new GridCoord(1, 0)));
        blockedWorld.FlushDelta();
        blockedWorld.NextTick();
        var blockedQueue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            blockedWorld,
            blockedQueue,
            new AuthoritativeWorldSyncSystem(blockedWorld),
            1);
        AuthoritativeMoveInput blocked = blockedQueue.EnqueueIntent(InputIntent.PlayerMove(20, Direction.Right, blockedWorld.ServerTick + 1, 7103, 1, blockedWorld.ServerTick), blockedWorld.ServerTick);
        runner.Tick();
        MoveResult blockedResult = blocked.WaitAsync().GetResult();
        if (blockedResult.Success ||
            blockedResult.ErrorCode == MoveErrorCode.None ||
            blocked.Status != AuthoritativePlayerInputStatus.Resolved ||
            !HasPosition(blockedWorld, 20, new GridCoord(0, 0)))
        {
            reason = "authorized input intent did not leave rule failure to action pipeline";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyAuthoritativeTickSyncDiagnostics(out string reason)
    {
        var autoWorld = new GameWorld();
        autoWorld.AddEntity(DefaultWorldConfig.BallSpawn(5000, new GridCoord(0, 0), Direction.Right, 1));
        autoWorld.FlushDelta();
        var sync = new AuthoritativeWorldSyncSystem(autoWorld);
        var autoRunner = new AuthoritativeWorldTickRunner(
            autoWorld,
            new AuthoritativeInputQueue(),
            sync,
            1);

        WorldDelta first = autoRunner.Tick();
        WorldDelta second = autoRunner.Tick();
        GameWorldObservation autoObservation = autoWorld.Observations;
        if (first.ChangedEntities.Count != 1 ||
            second.ChangedEntities.Count != 1 ||
            first.ServerTick >= second.ServerTick ||
            !first.ChangedEntities.Any(entity => entity.EntityId == 5000 && entity.X == 1 && entity.Y == 0) ||
            !second.ChangedEntities.Any(entity => entity.EntityId == 5000 && entity.X == 2 && entity.Y == 0) ||
            sync.LastDelta.ServerTick != second.ServerTick ||
            !sync.LastDeltaSkippedNoObservers ||
            sync.LastDeltaBroadcasted ||
            autoObservation.FullSnapshotBuildCount != 0 ||
            autoObservation.TouchedSnapshotBuildCount == 0)
        {
            reason = "continuous tick diagnostics did not capture observerless auto move delta";
            return false;
        }

        var pushWorld = new GameWorld();
        pushWorld.AddEntity(DefaultWorldConfig.ConveyorSpawn(5010, new GridCoord(0, 0), Direction.Right));
        pushWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(5011, 5011, new GridCoord(0, 0)));
        pushWorld.FlushDelta();
        var pushSync = new AuthoritativeWorldSyncSystem(pushWorld);
        var pushRunner = new AuthoritativeWorldTickRunner(
            pushWorld,
            new AuthoritativeInputQueue(),
            pushSync,
            1);
        pushRunner.Tick(Array.Empty<Fantasy.Network.Session>());
        WorldDelta pushDelta = pushRunner.Tick(Array.Empty<Fantasy.Network.Session>());
        GameWorldObservation pushObservation = pushWorld.Observations;
        if (pushDelta.ChangedEntities.Count != 1 ||
            !pushDelta.ChangedEntities.Any(entity => entity.EntityId == 5011 && entity.X == 1 && entity.Y == 0) ||
            pushSync.LastDelta.ServerTick != pushDelta.ServerTick ||
            !pushSync.LastDeltaSkippedNoObservers ||
            pushObservation.FullSnapshotBuildCount != 0 ||
            pushObservation.TouchedSnapshotBuildCount == 0)
        {
            reason = "push-on-enter continuous tick diagnostics did not capture delta without player input";
            return false;
        }

        var metadataOnlyWorld = new GameWorld();
        metadataOnlyWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(5020, new GridCoord(0, 0), Direction.Right));
        metadataOnlyWorld.FlushDelta();
        metadataOnlyWorld.AddAnimationMetadata(new WorldDeltaAnimationMetadata(5020, metadataOnlyWorld.ServerTick, WorldDeltaMotionKind.MechanismPush, "mechanism_push", Direction.Up));
        var metadataOnlySync = new AuthoritativeWorldSyncSystem(metadataOnlyWorld);
        WorldDelta metadataOnlyDelta = metadataOnlySync.BroadcastDelta(Array.Empty<Fantasy.Network.Session>());
        if (metadataOnlyDelta.ChangedEntities.Count != 0 ||
            metadataOnlyDelta.AnimationMetadata.Count != 1 ||
            metadataOnlyDelta.AnimationMetadata[0].EntityId != 5020 ||
            metadataOnlyDelta.AnimationMetadata[0].Direction != Direction.Up ||
            !metadataOnlySync.LastDeltaSkippedNoObservers)
        {
            reason = "metadata-only delta was not retained for observer broadcast path";
            return false;
        }

        var fullDiagnosticWorld = new GameWorld();
        fullDiagnosticWorld.AddEntity(DefaultWorldConfig.BallSpawn(5030, new GridCoord(0, 0), Direction.Right, 1));
        fullDiagnosticWorld.FlushDelta();
        fullDiagnosticWorld.ResetObservations();
        var fullDiagnosticRunner = new AuthoritativeWorldTickRunner(
            fullDiagnosticWorld,
            new AuthoritativeInputQueue(),
            new AuthoritativeWorldSyncSystem(fullDiagnosticWorld),
            1)
        {
            FullWorldDiagnosticsEnabled = true
        };
        fullDiagnosticRunner.Tick();
        GameWorldObservation fullDiagnosticObservation = fullDiagnosticWorld.Observations;
        if (fullDiagnosticObservation.FullSnapshotBuildCount == 0)
        {
            reason = "full-world diagnostics mode did not record full snapshot construction";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyParallelCandidateBoundary(out string reason)
    {
        var serialWorld = CreateCandidateWorld();
        var parallelWorld = CreateCandidateWorld();
        serialWorld.NextTick();
        parallelWorld.NextTick();

        IReadOnlyList<AutoMoveActionCandidate> serialAuto = serialWorld.CollectAutoMoveCandidates(serialWorld.ServerTick, CandidateScanMode.Serial);
        IReadOnlyList<AutoMoveActionCandidate> parallelAuto = parallelWorld.CollectAutoMoveCandidates(parallelWorld.ServerTick, CandidateScanMode.Parallel);
        IReadOnlyList<PushOnEnterActionCandidate> serialPush = serialWorld.CollectPushOnEnterCandidates(serialWorld.ServerTick, CandidateScanMode.Serial);
        IReadOnlyList<PushOnEnterActionCandidate> parallelPush = parallelWorld.CollectPushOnEnterCandidates(parallelWorld.ServerTick, CandidateScanMode.Parallel);
        IReadOnlyList<RuntimeEffectId> serialExpired = serialWorld.CollectExpiredRuntimeEffectCandidates(serialWorld.ServerTick, CandidateScanMode.Serial);
        IReadOnlyList<RuntimeEffectId> parallelExpired = parallelWorld.CollectExpiredRuntimeEffectCandidates(parallelWorld.ServerTick, CandidateScanMode.Parallel);

        if (!CandidateAutoEqual(serialAuto, parallelAuto) ||
            !CandidatePushEqual(serialPush, parallelPush) ||
            !serialExpired.SequenceEqual(parallelExpired))
        {
            reason = "serial and parallel candidate scans diverged";
            return false;
        }

        if (!serialWorld.TryGetEntity(9002, out GameEntity serialPlayer) ||
            !parallelWorld.TryGetEntity(9002, out GameEntity parallelPlayer) ||
            !serialWorld.TryGetComponent(serialPlayer, out PositionComponent serialPosition) ||
            !parallelWorld.TryGetComponent(parallelPlayer, out PositionComponent parallelPosition) ||
            serialPosition.Coord != new GridCoord(0, 0) ||
            parallelPosition.Coord != new GridCoord(0, 0) ||
            serialWorld.RuntimeEffects.Count == 0 ||
            parallelWorld.RuntimeEffects.Count == 0)
        {
            reason = "candidate scan mutated GameWorld before commit";
            return false;
        }

        WorldDelta serialDelta = RunCandidateWorld(CandidateScanMode.Serial);
        WorldDelta parallelDelta = RunCandidateWorld(CandidateScanMode.Parallel);
        if (!DeltaEquivalent(serialDelta, parallelDelta))
        {
            reason = "serial and parallel candidate commits produced different deltas";
            return false;
        }

        GameWorld observationWorld = CreateCandidateWorld();
        var runner = new AuthoritativeWorldTickRunner(
            observationWorld,
            new AuthoritativeInputQueue(),
            new AuthoritativeWorldSyncSystem(observationWorld),
            1)
        {
            CandidateScanMode = CandidateScanMode.Parallel
        };
        runner.Tick();
        GameWorldObservation observation = observationWorld.Observations;
        if (observation.CandidateCount == 0 ||
            observation.CommitCount == 0 ||
            observation.QueryTimeTicks == 0 ||
            observation.DeltaMaterializationCount == 0)
        {
            reason = "candidate observation counters did not record query/candidate/commit/delta work";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyStateDrivenPush(out string reason)
    {
        var singleWorld = new GameWorld();
        singleWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(930, 930, new GridCoord(0, 0)));
        singleWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(931, new GridCoord(1, 0)));
        singleWorld.FlushDelta();
        var singleQueue = new AuthoritativeInputQueue();
        var singleRunner = new AuthoritativeWorldTickRunner(
            singleWorld,
            singleQueue,
            new AuthoritativeWorldSyncSystem(singleWorld),
            1);

        AuthoritativeMoveInput singleInput = singleQueue.EnqueueMove(930, new GridCoord(1, 0), 13);
        WorldDelta singleFirstDelta = singleRunner.Tick();
        MoveResult singleResult = singleInput.WaitAsync().GetResult();
        if (!singleResult.Success ||
            singleResult.FinalCoord != new GridCoord(0, 0) ||
            singleFirstDelta.ChangedEntities.Count != 0)
        {
            reason = "state push did not defer without immediate movement";
            return false;
        }

        WorldDelta singleSecondDelta = singleRunner.Tick();
        if (singleSecondDelta.ChangedEntities.Count != 1 ||
            !singleSecondDelta.ChangedEntities.Any(snapshot => snapshot.EntityId == 931 && snapshot.X == 2 && snapshot.Y == 0))
        {
            reason = "state deferred push did not move target entity on next tick";
            return false;
        }

        WorldDelta singleThirdDelta = singleRunner.Tick();
        if (!singleWorld.TryGetEntity(930, out GameEntity singlePlayer) ||
            !singleWorld.TryGetComponent(singlePlayer, out PositionComponent singlePlayerPosition) ||
            singlePlayerPosition.Coord != new GridCoord(0, 0) ||
            singleThirdDelta.ChangedEntities.Count != 0)
        {
            reason = "state handoff moved source after target moved";
            return false;
        }

        var bodyPushWorld = new GameWorld();
        bodyPushWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(933, 933, new GridCoord(0, 0)));
        bodyPushWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(934, new GridCoord(1, 0), Direction.Right));
        bodyPushWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(935, new GridCoord(2, 0), Direction.Right));
        bodyPushWorld.FlushDelta();
        var bodyPushQueue = new AuthoritativeInputQueue();
        var bodyPushRunner = new AuthoritativeWorldTickRunner(
            bodyPushWorld,
            bodyPushQueue,
            new AuthoritativeWorldSyncSystem(bodyPushWorld),
            1);

        AuthoritativeMoveInput bodyPushInput = bodyPushQueue.EnqueueMove(933, new GridCoord(1, 0), 17);
        WorldDelta bodyPushFirstDelta = bodyPushRunner.Tick();
        MoveResult bodyPushResult = bodyPushInput.WaitAsync().GetResult();
        WorldDelta bodyPushSecondDelta = bodyPushRunner.Tick();
        if (!bodyPushResult.Success ||
            bodyPushResult.FinalCoord != new GridCoord(0, 0) ||
            bodyPushFirstDelta.ChangedEntities.Count != 0 ||
            bodyPushSecondDelta.ChangedEntities.Count != 2 ||
            !bodyPushSecondDelta.ChangedEntities.Any(snapshot => snapshot.EntityId == 934 && snapshot.X == 2 && snapshot.Y == 0) ||
            !bodyPushSecondDelta.ChangedEntities.Any(snapshot => snapshot.EntityId == 935 && snapshot.X == 3 && snapshot.Y == 0) ||
            bodyPushSecondDelta.AnimationMetadata.Count != 2 ||
            !bodyPushSecondDelta.AnimationMetadata.Any(metadata => metadata.EntityId == 934 && metadata.MotionKind == WorldDeltaMotionKind.MechanismPush) ||
            !bodyPushSecondDelta.AnimationMetadata.Any(metadata => metadata.EntityId == 935 && metadata.MotionKind == WorldDeltaMotionKind.MechanismPush))
        {
            reason = "state deferred push did not move connected body members with metadata changed=" +
                string.Join(",", bodyPushSecondDelta.ChangedEntities.Select(snapshot => snapshot.EntityId + ":" + snapshot.X + "," + snapshot.Y)) +
                " metadata=" +
                string.Join(",", bodyPushSecondDelta.AnimationMetadata.Select(metadata => metadata.EntityId + ":" + metadata.MotionKind));
            return false;
        }

        var intermediateWorld = new GameWorld();
        intermediateWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(936, 936, new GridCoord(0, 0)));
        intermediateWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(937, new GridCoord(1, 0), Direction.Right));
        intermediateWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(938, new GridCoord(2, 0), Direction.Right));
        intermediateWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(939, new GridCoord(3, 0)));
        intermediateWorld.FlushDelta();
        var intermediateQueue = new AuthoritativeInputQueue();
        var intermediateRunner = new AuthoritativeWorldTickRunner(
            intermediateWorld,
            intermediateQueue,
            new AuthoritativeWorldSyncSystem(intermediateWorld),
            1);

        AuthoritativeMoveInput intermediateInput = intermediateQueue.EnqueueMove(936, new GridCoord(1, 0), 18);
        WorldDelta intermediateFirstDelta = intermediateRunner.Tick();
        MoveResult intermediateResult = intermediateInput.WaitAsync().GetResult();
        WorldDelta intermediateSecondDelta = intermediateRunner.Tick();
        WorldDelta intermediateThirdDelta = intermediateRunner.Tick();
        WorldDelta intermediateFourthDelta = intermediateRunner.Tick();
        if (!intermediateResult.Success ||
            intermediateResult.FinalCoord != new GridCoord(0, 0) ||
            intermediateFirstDelta.ChangedEntities.Count != 0 ||
            intermediateSecondDelta.ChangedEntities.Count != 0 ||
            intermediateSecondDelta.AnimationMetadata.Count != 2 ||
            !intermediateSecondDelta.AnimationMetadata.Any(metadata => metadata.EntityId == 937 && metadata.MotionKind == WorldDeltaMotionKind.MechanismPush && metadata.Direction == Direction.Right) ||
            !intermediateSecondDelta.AnimationMetadata.Any(metadata => metadata.EntityId == 938 && metadata.MotionKind == WorldDeltaMotionKind.MechanismPush && metadata.Direction == Direction.Right) ||
            intermediateThirdDelta.ChangedEntities.Count != 1 ||
            !intermediateThirdDelta.ChangedEntities.Any(snapshot => snapshot.EntityId == 939 && snapshot.X == 4 && snapshot.Y == 0) ||
            intermediateFourthDelta.ChangedEntities.Count != 0 ||
            intermediateFourthDelta.AnimationMetadata.Count != 0 ||
            !intermediateWorld.TryGetEntity(937, out GameEntity intermediateFirstMember) ||
            !intermediateWorld.TryGetEntity(938, out GameEntity intermediateSecondMember) ||
            !intermediateWorld.TryGetComponent(intermediateFirstMember, out PositionComponent intermediateFirstPosition) ||
            !intermediateWorld.TryGetComponent(intermediateSecondMember, out PositionComponent intermediateSecondPosition) ||
            intermediateFirstPosition.Coord != new GridCoord(1, 0) ||
            intermediateSecondPosition.Coord != new GridCoord(2, 0))
        {
            reason = "intermediate connected body push should feedback without moving middle body secondChanged=" +
                string.Join(",", intermediateSecondDelta.ChangedEntities.Select(snapshot => snapshot.EntityId + ":" + snapshot.X + "," + snapshot.Y)) +
                " secondMetadata=" +
                string.Join(",", intermediateSecondDelta.AnimationMetadata.Select(metadata => metadata.EntityId + ":" + metadata.MotionKind + ":" + metadata.Direction)) +
                " thirdChanged=" +
                string.Join(",", intermediateThirdDelta.ChangedEntities.Select(snapshot => snapshot.EntityId + ":" + snapshot.X + "," + snapshot.Y)) +
                " fourthChanged=" +
                string.Join(",", intermediateFourthDelta.ChangedEntities.Select(snapshot => snapshot.EntityId + ":" + snapshot.X + "," + snapshot.Y)) +
                " fourthMetadata=" +
                string.Join(",", intermediateFourthDelta.AnimationMetadata.Select(metadata => metadata.EntityId + ":" + metadata.MotionKind));
            return false;
        }

        var repeatedWorld = new GameWorld();
        repeatedWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(960, 960, new GridCoord(0, 0)));
        repeatedWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(961, new GridCoord(1, 0)));
        repeatedWorld.FlushDelta();
        var repeatedQueue = new AuthoritativeInputQueue();
        var repeatedRunner = new AuthoritativeWorldTickRunner(
            repeatedWorld,
            repeatedQueue,
            new AuthoritativeWorldSyncSystem(repeatedWorld),
            1);

        AuthoritativeMoveInput firstRepeatedInput = repeatedQueue.EnqueueMove(960, new GridCoord(1, 0), 21);
        repeatedRunner.Tick();
        AuthoritativeMoveInput secondRepeatedInput = repeatedQueue.EnqueueMove(960, new GridCoord(1, 0), 22);
        repeatedRunner.Tick();
        repeatedRunner.Tick();
        MoveResult firstRepeatedResult = firstRepeatedInput.WaitAsync().GetResult();
        MoveResult secondRepeatedResult = secondRepeatedInput.WaitAsync().GetResult();
        PositionComponent repeatedBoxPosition = default;
        bool hasRepeatedBox = repeatedWorld.TryGetEntity(961, out GameEntity repeatedBox);
        if (hasRepeatedBox)
        {
            hasRepeatedBox = repeatedWorld.TryGetComponent(repeatedBox, out repeatedBoxPosition);
        }
        GridCoord repeatedBoxCoord = hasRepeatedBox ? repeatedBoxPosition.Coord : default;
        if (!firstRepeatedResult.Success ||
            firstRepeatedResult.FinalCoord != new GridCoord(0, 0) ||
            !secondRepeatedResult.Success ||
            secondRepeatedResult.FinalCoord != new GridCoord(0, 0) ||
            !hasRepeatedBox ||
            repeatedBoxCoord != new GridCoord(3, 0))
        {
            reason = $"repeated push did not resolve as independent later action first={firstRepeatedResult.Success}:{firstRepeatedResult.FinalCoord.X},{firstRepeatedResult.FinalCoord.Y}:{firstRepeatedResult.Reason} second={secondRepeatedResult.Success}:{secondRepeatedResult.FinalCoord.X},{secondRepeatedResult.FinalCoord.Y}:{secondRepeatedResult.Reason} box=({repeatedBoxCoord.X},{repeatedBoxCoord.Y})";
            return false;
        }

        var chainWorld = new GameWorld();
        chainWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(940, 940, new GridCoord(0, 0)));
        chainWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(941, new GridCoord(1, 0)));
        chainWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(942, new GridCoord(2, 0)));
        chainWorld.FlushDelta();
        var chainQueue = new AuthoritativeInputQueue();
        var chainRunner = new AuthoritativeWorldTickRunner(
            chainWorld,
            chainQueue,
            new AuthoritativeWorldSyncSystem(chainWorld),
            1);

        AuthoritativeMoveInput chainInput = chainQueue.EnqueueMove(940, new GridCoord(1, 0), 14);
        chainRunner.Tick();
        chainRunner.Tick();
        chainRunner.Tick();
        chainRunner.Tick();
        chainRunner.Tick();
        MoveResult chainResult = chainInput.WaitAsync().GetResult();
        if (!chainWorld.TryGetEntity(941, out GameEntity chainBoxA) ||
            !chainWorld.TryGetEntity(942, out GameEntity chainBoxB) ||
            !chainWorld.TryGetEntity(940, out GameEntity chainPlayer) ||
            !chainWorld.TryGetComponent(chainBoxA, out PositionComponent chainBoxAPosition) ||
            !chainWorld.TryGetComponent(chainBoxB, out PositionComponent chainBoxBPosition) ||
            !chainWorld.TryGetComponent(chainPlayer, out PositionComponent chainPlayerPosition) ||
            !chainResult.Success ||
            chainResult.FinalCoord != new GridCoord(0, 0) ||
            chainPlayerPosition.Coord != new GridCoord(0, 0) ||
            chainBoxAPosition.Coord != new GridCoord(1, 0) ||
            chainBoxBPosition.Coord != new GridCoord(3, 0))
        {
            reason = "state chain push did not leave sources in place";
            return false;
        }

        var loopWorld = new GameWorld();
        loopWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(970, new GridCoord(0, 0)));
        loopWorld.FlushDelta();
        loopWorld.NextTick();
        var loopSystem = new StateDrivenRuleExecutionSystem();
        StateDrivenRuleExecutionResult loopResult = loopSystem.Tick(loopWorld, new[]
        {
            new WorldAction(1, WorldActionPriority.Mechanism, "mechanism_push", 970, null, Direction.Up, 0, loopWorld.ServerTick - 1, loopWorld.ServerTick, 1),
            new WorldAction(2, WorldActionPriority.Mechanism, "mechanism_push", 970, null, Direction.Down, 0, loopWorld.ServerTick - 1, loopWorld.ServerTick, 1)
        }, loopWorld.ServerTick);
        if (!loopWorld.TryGetEntity(970, out GameEntity loopBox) ||
            !loopWorld.TryGetComponent(loopBox, out PositionComponent loopBoxPosition) ||
            loopBoxPosition.Coord != new GridCoord(0, 0) ||
            loopResult.DeferredActions.Count != 0 ||
            loopResult.ProposalResults.Count != 0 ||
            loopResult.ActionResults.Count != 2 ||
            loopResult.ActionResults.Values.Any(result => result.Success || result.Reason != "push-vector-cancelled"))
        {
            reason = "same tick opposite push vector was not cancelled deterministically";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyStateDrivenActionDeterminism(out string reason)
    {
        if (!VerifyActionQueueOrdering(out reason))
        {
            return false;
        }

        if (!VerifyTickCostScheduling(out reason))
        {
            return false;
        }

        if (!VerifyProposalConflict(out reason))
        {
            return false;
        }

        if (!VerifyDebugActionsUseLogicTick(out reason))
        {
            return false;
        }

        string firstHash = RunDeterministicWorldHash();
        string secondHash = RunDeterministicWorldHash();
        if (firstHash != secondHash)
        {
            reason = "state driven execution was not deterministic";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static GameWorld CreateCandidateWorld()
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.BallSpawn(9001, new GridCoord(-1, 0), Direction.Right, 1));
        world.AddEntity(DefaultWorldConfig.ConveyorSpawn(9003, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(9002, 9002, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(9004, new GridCoord(1, 0)));
        world.NextTick();
        var effectSpec = new EffectSpec("candidate", EffectKind.Pushable, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOrExpire, 1, 1, DirectionMask.None, true, true, WorldTag.None);
        var context = new ActionContext(90000, 90000, "candidate", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, 9002, 0, WorldTag.SourceDebug), 9002, 9002, 9002, 9002, new ActionTarget(9002, null, Direction.None), Direction.None, world.ServerTick, world.ServerTick, 1, 0, 90000);
        new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 90000, new EffectApplication(context, effectSpec, ActionTargetData.Self(9002, default, Direction.None), world.ServerTick, "candidate"), world.ServerTick) });
        world.FlushDelta();
        return world;
    }

    private static WorldDelta RunCandidateWorld(CandidateScanMode mode)
    {
        GameWorld world = CreateCandidateWorld();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            new AuthoritativeInputQueue(),
            new AuthoritativeWorldSyncSystem(world),
            1)
        {
            CandidateScanMode = mode
        };
        return runner.Tick();
    }

    private static bool CandidateAutoEqual(IReadOnlyList<AutoMoveActionCandidate> left, IReadOnlyList<AutoMoveActionCandidate> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i].EntityId != right[i].EntityId ||
                left[i].CreatedTick != right[i].CreatedTick ||
                left[i].ReadyTick != right[i].ReadyTick ||
                left[i].CostTicks != right[i].CostTicks)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CandidatePushEqual(IReadOnlyList<PushOnEnterActionCandidate> left, IReadOnlyList<PushOnEnterActionCandidate> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i].SourceEntityId != right[i].SourceEntityId ||
                left[i].SubjectEntityId != right[i].SubjectEntityId ||
                !left[i].SpecId.Equals(right[i].SpecId) ||
                left[i].Direction != right[i].Direction ||
                left[i].CreatedTick != right[i].CreatedTick ||
                left[i].CostTicks != right[i].CostTicks)
            {
                return false;
            }
        }

        return true;
    }

    private static bool DeltaEquivalent(WorldDelta left, WorldDelta right)
    {
        return left.ServerTick == right.ServerTick &&
            left.RemovedEntityIds.SequenceEqual(right.RemovedEntityIds) &&
            left.ChangedEntities.Select(DeltaSnapshotKey).OrderBy(item => item).SequenceEqual(right.ChangedEntities.Select(DeltaSnapshotKey).OrderBy(item => item)) &&
            left.AnimationMetadata.Select(MetadataKey).OrderBy(item => item).SequenceEqual(right.AnimationMetadata.Select(MetadataKey).OrderBy(item => item));
    }

    private static string DeltaSnapshotKey(EntitySnapshot snapshot)
    {
        return snapshot.EntityId + ":" + snapshot.X + ":" + snapshot.Y + ":" + snapshot.Direction + ":" + snapshot.ServerTick;
    }

    private static string MetadataKey(WorldDeltaAnimationMetadata metadata)
    {
        return metadata.EntityId + ":" + metadata.ServerTick + ":" + metadata.MotionKind + ":" + metadata.Direction + ":" + metadata.StyleKey;
    }

    private static bool VerifyPortConnectedPush(out string reason)
    {
        if (!VerifyPortConnectorWorldPorts(out reason))
        {
            return false;
        }

        if (!VerifyPortMatchedPush(out reason))
        {
            return false;
        }

        if (!VerifyPortMismatchSingleSubjectPropagation(out reason))
        {
            return false;
        }

        if (!VerifyPortBlockedGroupFailsDeterministically(out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyBehaviorArbitration(out string reason)
    {
        if (!VerifyBehaviorBodyResolver(out reason))
        {
            return false;
        }

        if (!VerifyBehaviorGroupAtomicCommit(out reason))
        {
            return false;
        }

        if (!VerifyBehaviorGroupAtomicReject(out reason))
        {
            return false;
        }

        if (!VerifyConnectedBodyActionSubject(out reason))
        {
            return false;
        }

        if (!VerifyPlayerMoveConnectedBodyPolicy(out reason))
        {
            return false;
        }

        if (!VerifyBehaviorSameTickConflict(out reason))
        {
            return false;
        }

        if (!VerifyConflictingConveyorBodyIntentsStayStill(out reason))
        {
            return false;
        }

        if (!VerifyActionTagsBlockMoveRules(out reason))
        {
            return false;
        }

        if (!VerifyConveyorPushesPortGroupThroughPlanner(out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyRotatePivotPushResponse(out string reason)
    {
        var world = new GameWorld();
        AddPort(world, 80000100, new GridCoord(0, 0), DirectionMask.All);
        AddPort(world, 80000101, new GridCoord(1, 0), DirectionMask.All);
        if (world.TryGetEntity(80000101, out GameEntity memberDirectionEntity))
        {
            world.SetDirection(memberDirectionEntity, Direction.Right);
        }

        SetRotatePivot(world, 80000100);
        world.NextTick();
        StateDrivenRuleExecutionResult rotate = new StateDrivenRuleExecutionSystem().Tick(world, new[] { WorldMove(1, 80000101, Direction.Down) }, world.ServerTick);
        if (!rotate.ActionResults.TryGetValue(1, out MoveResult rotateResult) ||
            !rotateResult.Success ||
            !HasPosition(world, 80000100, new GridCoord(0, 0)) ||
            !HasPosition(world, 80000101, new GridCoord(0, -1)) ||
            !HasDirection(world, 80000101, Direction.Down))
        {
            reason = "rotate pivot body did not rotate from single push";
            return false;
        }

        if (rotate.AnimationMetadata.Count != 2 ||
            !rotate.AnimationMetadata.Any(metadata => metadata.EntityId == 80000100 && metadata.MotionKind == WorldDeltaMotionKind.RotatePivot && metadata.StyleKey == "rotate_pivot" && metadata.FromCoord.Equals(new GridCoord(0, 0)) && metadata.ToCoord.Equals(new GridCoord(0, 0)) && metadata.RotateDirection == RotatePivotDirection.Clockwise) ||
            !rotate.AnimationMetadata.Any(metadata => metadata.EntityId == 80000101 && metadata.MotionKind == WorldDeltaMotionKind.RotatePivot && metadata.StyleKey == "rotate_pivot" && metadata.PivotEntityId == 80000100 && metadata.PivotCoord.Equals(new GridCoord(0, 0)) && metadata.FromCoord.Equals(new GridCoord(1, 0)) && metadata.ToCoord.Equals(new GridCoord(0, -1)) && !metadata.Bounce))
        {
            reason = "rotate pivot success metadata incomplete:" + string.Join(",", rotate.AnimationMetadata.Select(MetadataKey));
            return false;
        }

        var cancelWorld = new GameWorld();
        AddPort(cancelWorld, 80000110, new GridCoord(0, 0), DirectionMask.All);
        AddPort(cancelWorld, 80000111, new GridCoord(1, 0), DirectionMask.All);
        AddPort(cancelWorld, 80000112, new GridCoord(-1, 0), DirectionMask.All);
        SetRotatePivot(cancelWorld, 80000110);
        cancelWorld.NextTick();
        StateDrivenRuleExecutionResult cancelled = new StateDrivenRuleExecutionSystem().Tick(cancelWorld, new[]
        {
            WorldMove(2, 80000111, Direction.Down),
            WorldMove(3, 80000112, Direction.Down)
        }, cancelWorld.ServerTick);
        if (!cancelled.ActionResults.TryGetValue(2, out MoveResult firstCancel) ||
            !cancelled.ActionResults.TryGetValue(3, out MoveResult secondCancel) ||
            firstCancel.Success ||
            secondCancel.Success ||
            cancelled.DeferredActions.Count != 0 ||
            !HasPosition(cancelWorld, 80000111, new GridCoord(1, 0)) ||
            !HasPosition(cancelWorld, 80000112, new GridCoord(-1, 0)))
        {
            reason = "opposite rotate pivot torque did not cancel";
            return false;
        }

        var blockedWorld = new GameWorld();
        AddPort(blockedWorld, 80000120, new GridCoord(0, 0), DirectionMask.All);
        AddPort(blockedWorld, 80000121, new GridCoord(1, 0), DirectionMask.All);
        AddPort(blockedWorld, 80000122, new GridCoord(-1, 0), DirectionMask.All);
        SetRotatePivot(blockedWorld, 80000120);
        blockedWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(80000123, new GridCoord(0, -1)));
        blockedWorld.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(80000124, new GridCoord(0, 1)));
        blockedWorld.NextTick();
        StateDrivenRuleExecutionResult blocked = new StateDrivenRuleExecutionSystem().Tick(blockedWorld, new[] { WorldMove(4, 80000121, Direction.Down) }, blockedWorld.ServerTick);
        if (!blocked.ActionResults.TryGetValue(4, out MoveResult blockedResult) ||
            !blockedResult.Success)
        {
            reason = "rotate pivot blocked result failed";
            return false;
        }

        if (blocked.DeferredActions.Count != 2 ||
            !blocked.DeferredActions.Any(action => action.EntityId == 80000123) ||
            !blocked.DeferredActions.Any(action => action.EntityId == 80000124))
        {
            reason = "rotate pivot deferred mismatch count:" + blocked.DeferredActions.Count + " ids:" + string.Join(",", blocked.DeferredActions.Select(action => action.EntityId));
            return false;
        }

        if (!HasPosition(blockedWorld, 80000121, new GridCoord(1, 0)) ||
            !HasPosition(blockedWorld, 80000122, new GridCoord(-1, 0)))
        {
            reason = "rotate pivot self moved while blocked";
            return false;
        }

        DeferredAction lowerImpact = blocked.DeferredActions.Single(action => action.EntityId == 80000123);
        DeferredAction upperImpact = blocked.DeferredActions.Single(action => action.EntityId == 80000124);
        if (lowerImpact.Direction != Direction.Left ||
            upperImpact.Direction != Direction.Right)
        {
            reason = "rotate pivot blocker directions were lower:" + lowerImpact.Direction + " upper:" + upperImpact.Direction;
            return false;
        }

        if (blocked.DeferredActions.Any(action => action.OriginContexts.Count != 1) ||
            blocked.DeferredActions.Any(action => action.OriginContexts[0].Kind != PushOriginKind.RotatePivotImpact))
        {
            reason = "rotate pivot impact context missing counts:" + string.Join(",", blocked.DeferredActions.Select(action => action.EntityId + ":" + action.OriginContexts.Count));
            return false;
        }

        if (blocked.AnimationMetadata.Count != 5 ||
            !blocked.AnimationMetadata.Any(metadata => metadata.EntityId == 80000120 && metadata.MotionKind == WorldDeltaMotionKind.RotatePivotBounce && metadata.Bounce && metadata.StyleKey == "rotate_pivot_bounce") ||
            !blocked.AnimationMetadata.Any(metadata => metadata.EntityId == 80000121 && metadata.MotionKind == WorldDeltaMotionKind.RotatePivotBounce && metadata.Bounce && metadata.ImpactCoord.Equals(new GridCoord(0, -1))) ||
            !blocked.AnimationMetadata.Any(metadata => metadata.EntityId == 80000122 && metadata.MotionKind == WorldDeltaMotionKind.RotatePivotBounce && metadata.Bounce && metadata.ImpactCoord.Equals(new GridCoord(0, 1))) ||
            !blocked.AnimationMetadata.Any(metadata => metadata.EntityId == 80000123 && metadata.MotionKind == WorldDeltaMotionKind.MechanismPush && metadata.StyleKey == "rotate_pivot_impact" && metadata.Direction == Direction.Left) ||
            !blocked.AnimationMetadata.Any(metadata => metadata.EntityId == 80000124 && metadata.MotionKind == WorldDeltaMotionKind.MechanismPush && metadata.StyleKey == "rotate_pivot_impact" && metadata.Direction == Direction.Right))
        {
            reason = "rotate pivot blocked metadata incomplete:" + string.Join(",", blocked.AnimationMetadata.Select(MetadataKey));
            return false;
        }

        var handoffWorld = new GameWorld();
        AddPort(handoffWorld, 80000130, new GridCoord(0, 0), DirectionMask.All);
        AddPort(handoffWorld, 80000131, new GridCoord(1, 0), DirectionMask.All);
        SetRotatePivot(handoffWorld, 80000130);
        AddPort(handoffWorld, 80000132, new GridCoord(0, -1), DirectionMask.Right);
        AddPort(handoffWorld, 80000133, new GridCoord(1, -1), DirectionMask.Left);
        handoffWorld.NextTick();
        StateDrivenRuleExecutionResult handoff = new StateDrivenRuleExecutionSystem().Tick(handoffWorld, new[] { WorldMove(5, 80000131, Direction.Down) }, handoffWorld.ServerTick);
        if (handoff.DeferredActions.Count != 1 ||
            !handoff.DeferredActions[0].SubjectEntityIds.Contains(80000132) ||
            !handoff.DeferredActions[0].SubjectEntityIds.Contains(80000133))
        {
            reason = "rotate pivot external connected body blocker did not hand off as whole subject";
            return false;
        }

        var runtimePivotWorld = new GameWorld();
        AddPort(runtimePivotWorld, 80000140, new GridCoord(0, 0), DirectionMask.All);
        AddPort(runtimePivotWorld, 80000141, new GridCoord(1, 0), DirectionMask.All);
        var runtimePivotSpec = new EffectSpec("runtime_pivot_verification", EffectKind.RotatePivot, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.None, true, true, WorldTag.None);
        AddVerificationEffect(runtimePivotWorld, runtimePivotSpec, 80000140, 0, "pivot");
        runtimePivotWorld.NextTick();
        StateDrivenRuleExecutionResult runtimePivot = new StateDrivenRuleExecutionSystem().Tick(runtimePivotWorld, new[] { WorldMove(6, 80000141, Direction.Down) }, runtimePivotWorld.ServerTick);
        if (!runtimePivot.ActionResults.TryGetValue(6, out MoveResult runtimePivotResult) ||
            !runtimePivotResult.Success ||
            !HasPosition(runtimePivotWorld, 80000141, new GridCoord(0, -1)))
        {
            reason = "runtime effect rotate pivot did not enter rotate response";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyDeferredQueueDedupe(out string reason)
    {
        var queue = new WorldActionQueue();
        DeferredEnqueueResult first = queue.EnqueueDeferred(new DeferredAction("player_push", 800000148, new[] { 800000148L }, Direction.Up, 10, 11, 1, 2001, "2001"));
        DeferredEnqueueResult second = queue.EnqueueDeferred(new DeferredAction("player_push", 800000148, new[] { 800000148L }, Direction.Up, 9, 11, 1, 2002, "2002"));
        DeferredEnqueueResult opposite = queue.EnqueueDeferred(new DeferredAction("player_push", 800000148, new[] { 800000148L }, Direction.Down, 10, 11, 1, 2003, "2003"));
        DeferredEnqueueResult otherSubject = queue.EnqueueDeferred(new DeferredAction("player_push", 800000149, new[] { 800000149L }, Direction.Up, 10, 11, 1, 2004, "2004"));
        IReadOnlyList<WorldAction> ready = queue.DrainReady(11);

        if (!first.Enqueued ||
            second.Enqueued ||
            !opposite.Enqueued ||
            !otherSubject.Enqueued ||
            second.ContributionCount != 2 ||
            ready.Count != 3 ||
            ready.Count(action => action.EntityId == 800000148 && action.Direction == Direction.Up) != 1 ||
            ready.Count(action => action.EntityId == 800000148 && action.Direction == Direction.Down) != 1 ||
            ready.Count(action => action.EntityId == 800000149 && action.Direction == Direction.Up) != 1 ||
            ready.Single(action => action.EntityId == 800000148 && action.Direction == Direction.Up).DeferredContributionCount != 2)
        {
            reason = "deferred queue dedupe did not preserve equivalent contribution boundary";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyConnectedBodyActionSubject(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1325, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1326, new GridCoord(1, 0), Direction.Right));
        world.FlushDelta();
        world.NextTick();
        var queue = new WorldActionQueue();
        WorldAction action = queue.EnqueueConfiguredMove("connected_body_move", 1325, Direction.Right, world.ServerTick - 1, 1);
        StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);
        WorldDelta delta = world.FlushDelta();
        if (!result.ActionResults.TryGetValue(action.ActionId, out MoveResult moveResult) ||
            !moveResult.Success ||
            delta.ChangedEntities.Count != 2 ||
            !delta.ChangedEntities.Any(snapshot => snapshot.EntityId == 1325 && snapshot.X == 1 && snapshot.Y == 0) ||
            !delta.ChangedEntities.Any(snapshot => snapshot.EntityId == 1326 && snapshot.X == 2 && snapshot.Y == 0))
        {
            reason = "connected body action subject did not move all members";
            return false;
        }

        var blockedWorld = new GameWorld();
        blockedWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1327, new GridCoord(0, 0), Direction.Right));
        blockedWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1328, new GridCoord(1, 0), Direction.Right));
        blockedWorld.AddEntity(DefaultWorldConfig.BlockerSpawn(1329, new GridCoord(2, 0)));
        blockedWorld.FlushDelta();
        blockedWorld.NextTick();
        var blockedQueue = new WorldActionQueue();
        WorldAction blockedAction = blockedQueue.EnqueueConfiguredMove("connected_body_move", 1327, Direction.Right, blockedWorld.ServerTick - 1, 1);
        StateDrivenRuleExecutionResult blockedResult = new StateDrivenRuleExecutionSystem().Tick(blockedWorld, blockedQueue.DrainReady(blockedWorld.ServerTick), blockedWorld.ServerTick);
        if (!blockedResult.ActionResults.TryGetValue(blockedAction.ActionId, out MoveResult blockedMoveResult) ||
            blockedMoveResult.Success ||
            blockedWorld.FlushDelta().ChangedEntities.Count != 0 ||
            !blockedWorld.TryGetEntity(1327, out GameEntity blockedFirst) ||
            !blockedWorld.TryGetEntity(1328, out GameEntity blockedSecond) ||
            !blockedWorld.TryGetComponent(blockedFirst, out PositionComponent blockedFirstPosition) ||
            !blockedWorld.TryGetComponent(blockedSecond, out PositionComponent blockedSecondPosition) ||
            blockedFirstPosition.Coord != new GridCoord(0, 0) ||
            blockedSecondPosition.Coord != new GridCoord(1, 0))
        {
            reason = "blocked connected body action subject did not reject all members";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyPlayerMoveConnectedBodyPolicy(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(1332, 1332, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1333, new GridCoord(1, 0), Direction.Right));
        if (world.TryGetEntity(1332, out GameEntity player))
        {
            world.SetComponent(player, new PortConnectorComponent(DirectionMask.Right));
        }
        world.FlushDelta();
        world.NextTick();
        var queue = new WorldActionQueue();
        WorldAction action = queue.EnqueuePlayerMove(1332, new GridCoord(1, 0), 1);
        StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);
        if (!result.ActionResults.TryGetValue(action.ActionId, out MoveResult moveResult) ||
            !moveResult.Success ||
            !world.TryGetEntity(1332, out GameEntity first) ||
            !world.TryGetEntity(1333, out GameEntity second) ||
            !world.TryGetComponent(first, out PositionComponent firstPosition) ||
            !world.TryGetComponent(second, out PositionComponent secondPosition) ||
            firstPosition.Coord != new GridCoord(1, 0) ||
            secondPosition.Coord != new GridCoord(2, 0))
        {
            reason = "player move connected body policy did not move linked body";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyBehaviorBodyResolver(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.BlockerSpawn(1301, new GridCoord(-1, 0)));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1302, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1303, new GridCoord(1, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1304, new GridCoord(2, 0), Direction.Down));
        if (!world.TryGetEntity(1301, out GameEntity single) ||
            !world.TryGetEntity(1302, out GameEntity first) ||
            !new BodyResolver().TryResolve(world, single, out BehaviorBody singleBody, out _) ||
            singleBody.Kind != BehaviorBodyKind.SingleEntity ||
            singleBody.Entities.Count != 1 ||
            !new BodyResolver().TryResolve(world, first, out BehaviorBody groupBody, out _) ||
            groupBody.Kind != BehaviorBodyKind.PortConnected ||
            groupBody.Entities.Count != 2 ||
            !groupBody.Entities.Any(entity => entity.EntityId == 1302) ||
            !groupBody.Entities.Any(entity => entity.EntityId == 1303))
        {
            reason = "behavior body resolver did not separate single, matched, and mismatched ports";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyBehaviorGroupAtomicCommit(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1310, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1311, new GridCoord(1, 0), Direction.Right));
        var planner = new RulePlanner();
        var resolver = new ConflictResolver();
        if (!planner.TryPlanMove(world, MoveRequest(10, "mechanism_push", WorldActionPriority.Mechanism, 1310, Direction.Right), Direction.Right, null, 1, out MovePlan plan, out PlanResult planResult) ||
            !planResult.Accepted ||
            plan.Members.Count != 2)
        {
            reason = "behavior planner did not create group move plan";
            return false;
        }

        IReadOnlyList<CommitProposalResult> results = resolver.Resolve(world, new[] { plan });
        WorldDelta resolverDelta = world.FlushDelta();
        if (results.Count != 2 ||
            results.Any(result => !result.Accepted) ||
            !results.Any(result => result.Proposal.EntityId == 1310 && result.Proposal.From == new GridCoord(0, 0) && result.Proposal.To == new GridCoord(1, 0)) ||
            !results.Any(result => result.Proposal.EntityId == 1311 && result.Proposal.From == new GridCoord(1, 0) && result.Proposal.To == new GridCoord(2, 0)) ||
            !world.TryGetEntity(1310, out GameEntity first) ||
            !world.TryGetEntity(1311, out GameEntity second) ||
            !world.TryGetComponent(first, out PositionComponent firstPosition) ||
            !world.TryGetComponent(second, out PositionComponent secondPosition) ||
            firstPosition.Coord != new GridCoord(1, 0) ||
            secondPosition.Coord != new GridCoord(2, 0) ||
            resolverDelta.ChangedEntities.Count != 2 ||
            !resolverDelta.ChangedEntities.Any(entity => entity.EntityId == 1310 && entity.X == 1 && entity.Y == 0) ||
            !resolverDelta.ChangedEntities.Any(entity => entity.EntityId == 1311 && entity.X == 2 && entity.Y == 0))
        {
            reason = "behavior conflict resolver did not atomically commit group move";
            return false;
        }

        var tickWorld = new GameWorld();
        tickWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1312, new GridCoord(0, 0), Direction.Right));
        tickWorld.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1313, new GridCoord(1, 0), Direction.Right));
        tickWorld.FlushDelta();
        var inputQueue = new AuthoritativeInputQueue();
        inputQueue.ActionQueue.EnqueueConfiguredMove("mechanism_push", 1312, Direction.Right, tickWorld.ServerTick, 1);
        var runner = new AuthoritativeWorldTickRunner(
            tickWorld,
            inputQueue,
            new AuthoritativeWorldSyncSystem(tickWorld),
            1);
        WorldDelta tickDelta = runner.Tick();
        if (tickDelta.ChangedEntities.Count != 2 ||
            !tickDelta.ChangedEntities.Any(entity => entity.EntityId == 1312 && entity.X == 1 && entity.Y == 0) ||
            !tickDelta.ChangedEntities.Any(entity => entity.EntityId == 1313 && entity.X == 2 && entity.Y == 0) ||
            tickDelta.AnimationMetadata.Count != 2 ||
            !tickDelta.AnimationMetadata.Any(metadata => metadata.EntityId == 1312 && metadata.MotionKind == WorldDeltaMotionKind.MechanismPush) ||
            !tickDelta.AnimationMetadata.Any(metadata => metadata.EntityId == 1313 && metadata.MotionKind == WorldDeltaMotionKind.MechanismPush))
        {
            reason = "authoritative tick did not sync group move delta and metadata for every member";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyBehaviorGroupAtomicReject(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1320, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1321, new GridCoord(1, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.BlockerSpawn(1322, new GridCoord(2, 0)));
        var planner = new RulePlanner();
        if (planner.TryPlanMove(world, MoveRequest(11, "mechanism_push", WorldActionPriority.Mechanism, 1320, Direction.Right), Direction.Right, null, 1, out _, out PlanResult result) ||
            result.Reason != PlanFailureReason.BlockedCell ||
            !world.TryGetEntity(1320, out GameEntity first) ||
            !world.TryGetEntity(1321, out GameEntity second) ||
            !world.TryGetComponent(first, out PositionComponent firstPosition) ||
            !world.TryGetComponent(second, out PositionComponent secondPosition) ||
            firstPosition.Coord != new GridCoord(0, 0) ||
            secondPosition.Coord != new GridCoord(1, 0))
        {
            reason = "behavior planner did not atomically reject blocked group";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyBehaviorSameTickConflict(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(1330, 1330, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(1331, 1331, new GridCoord(1, 1)));
        var planner = new RulePlanner();
        if (!planner.TryPlanMove(world, MoveRequest(21, "player_move", WorldActionPriority.Player, 1330, Direction.Right), Direction.Right, null, 1, out MovePlan firstPlan, out _) ||
            !planner.TryPlanMove(world, MoveRequest(22, "player_move", WorldActionPriority.Player, 1331, Direction.Down), Direction.Down, null, 1, out MovePlan secondPlan, out _))
        {
            reason = "behavior planner did not create competing plans";
            return false;
        }

        IReadOnlyList<CommitProposalResult> results = new ConflictResolver().Resolve(world, new[] { firstPlan, secondPlan });
        if (results.Count != 2 ||
            !results[0].Accepted ||
            results[1].Accepted ||
            results[1].Reason != "target reserved")
        {
            reason = "behavior conflict resolver did not deterministically reject same target";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyConflictingConveyorBodyIntentsStayStill(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.ConveyorSpawn(1350, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.ConveyorSpawn(1351, new GridCoord(1, 0), Direction.Left));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1352, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1353, new GridCoord(1, 0), Direction.Right));
        world.FlushDelta();
        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);

        WorldDelta delta = runner.Tick();
        if (delta.ChangedEntities.Count != 0 ||
            !world.TryGetEntity(1352, out GameEntity first) ||
            !world.TryGetEntity(1353, out GameEntity second) ||
            !world.TryGetComponent(first, out PositionComponent firstPosition) ||
            !world.TryGetComponent(second, out PositionComponent secondPosition) ||
            firstPosition.Coord != new GridCoord(0, 0) ||
            secondPosition.Coord != new GridCoord(1, 0))
        {
            reason = "conflicting conveyor body intents did not keep group still";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyActionTagsBlockMoveRules(out string reason)
    {
        var stunnedWorld = new GameWorld();
        stunnedWorld.AddEntity(DefaultWorldConfig.PlayerSpawn(1380, 1380, new GridCoord(0, 0)));
        if (!stunnedWorld.TryGetEntity(1380, out GameEntity stunnedPlayer))
        {
            reason = "stunned player setup failed";
            return false;
        }

        stunnedWorld.AddTag(stunnedPlayer, WorldTag.StateStunned);
        stunnedWorld.FlushDelta();
        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            stunnedWorld,
            queue,
            new AuthoritativeWorldSyncSystem(stunnedWorld),
            1);

        AuthoritativeMoveInput input = queue.EnqueueMove(1380, new GridCoord(1, 0), 88);
        WorldDelta delta = runner.Tick();
        MoveResult result = input.WaitAsync().GetResult();
        if (result.Success ||
            result.Reason != "blocked by tag" ||
            delta.ChangedEntities.Count != 0 ||
            !stunnedWorld.TryGetComponent(stunnedPlayer, out PositionComponent position) ||
            position.Coord != new GridCoord(0, 0))
        {
            reason = "stunned tag did not block authoritative player move";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyConveyorPushesPortGroupThroughPlanner(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.ConveyorSpawn(1340, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1341, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(1342, new GridCoord(1, 0), Direction.Right));
        world.FlushDelta();
        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);

        runner.Tick();
        WorldDelta delta = runner.Tick();
        if (!world.TryGetEntity(1341, out GameEntity first) ||
            !world.TryGetEntity(1342, out GameEntity second) ||
            !world.TryGetComponent(first, out PositionComponent firstPosition) ||
            !world.TryGetComponent(second, out PositionComponent secondPosition) ||
            firstPosition.Coord != new GridCoord(1, 0) ||
            secondPosition.Coord != new GridCoord(2, 0) ||
            delta.ChangedEntities.Count != 2 ||
            !delta.ChangedEntities.Any(snapshot => snapshot.EntityId == 1341 && snapshot.X == 1 && snapshot.Y == 0) ||
            !delta.ChangedEntities.Any(snapshot => snapshot.EntityId == 1342 && snapshot.X == 2 && snapshot.Y == 0))
        {
            reason = "conveyor port push did not move connected body through subject policy";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyPortConnectorWorldPorts(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(971, new GridCoord(0, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(972, new GridCoord(1, 0), Direction.Down));
        if (!world.TryGetEntity(971, out GameEntity rightFacing) ||
            !world.TryGetEntity(972, out GameEntity downFacing) ||
            PortConnectionSystem.GetWorldPorts(world, rightFacing) != (DirectionMask.Left | DirectionMask.Right) ||
            PortConnectionSystem.GetWorldPorts(world, downFacing) != (DirectionMask.Up | DirectionMask.Down))
        {
            reason = "port connector world port rotation failed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyPortMatchedPush(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(973, 973, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(974, new GridCoord(1, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(975, new GridCoord(2, 0), Direction.Right));
        world.FlushDelta();
        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);

        AuthoritativeMoveInput input = queue.EnqueueMove(973, new GridCoord(1, 0), 31);
        runner.Tick();
        MoveResult result = input.WaitAsync().GetResult();
        if (!result.Success ||
            result.FinalCoord != new GridCoord(0, 0) ||
            !world.TryGetEntity(973, out GameEntity player) ||
            !world.TryGetEntity(974, out GameEntity first) ||
            !world.TryGetEntity(975, out GameEntity second) ||
            !world.TryGetComponent(player, out PositionComponent playerPosition) ||
            !world.TryGetComponent(first, out PositionComponent firstPosition) ||
            !world.TryGetComponent(second, out PositionComponent secondPosition) ||
            playerPosition.Coord != new GridCoord(0, 0) ||
            firstPosition.Coord != new GridCoord(1, 0) ||
            secondPosition.Coord != new GridCoord(2, 0))
        {
            reason = "port matched push did not stay isolated as deferred output";
            return false;
        }

        runner.Tick();
        if (!world.TryGetComponent(player, out playerPosition) ||
            !world.TryGetComponent(first, out firstPosition) ||
            !world.TryGetComponent(second, out secondPosition) ||
            playerPosition.Coord != new GridCoord(0, 0) ||
            firstPosition.Coord != new GridCoord(2, 0) ||
            secondPosition.Coord != new GridCoord(3, 0))
        {
            reason = "port matched push did not complete owner at source position after body moved";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyPortMismatchSingleSubjectPropagation(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(976, 976, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(977, new GridCoord(1, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(978, new GridCoord(2, 0), Direction.Down));
        world.FlushDelta();
        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);

        AuthoritativeMoveInput input = queue.EnqueueMove(976, new GridCoord(1, 0), 32);
        runner.Tick();
        MoveResult result = input.WaitAsync().GetResult();
        if (!result.Success ||
            result.FinalCoord != new GridCoord(0, 0) ||
            !world.TryGetEntity(976, out GameEntity player) ||
            !world.TryGetEntity(977, out GameEntity first) ||
            !world.TryGetEntity(978, out GameEntity second) ||
            !world.TryGetComponent(player, out PositionComponent playerPosition) ||
            !world.TryGetComponent(first, out PositionComponent firstPosition) ||
            !world.TryGetComponent(second, out PositionComponent secondPosition) ||
            playerPosition.Coord != new GridCoord(0, 0) ||
            firstPosition.Coord != new GridCoord(1, 0) ||
            secondPosition.Coord != new GridCoord(2, 0))
        {
            reason = "port mismatch deferred output did not keep source and port members isolated";
            return false;
        }

        runner.Tick();
        if (!world.TryGetComponent(player, out playerPosition) ||
            !world.TryGetComponent(first, out firstPosition) ||
            !world.TryGetComponent(second, out secondPosition) ||
            playerPosition.Coord != new GridCoord(0, 0) ||
            firstPosition.Coord != new GridCoord(1, 0) ||
            secondPosition.Coord != new GridCoord(2, 0))
        {
            GridCoord playerCoord = world.TryGetComponent(player, out playerPosition) ? playerPosition.Coord : default;
            GridCoord firstCoord = world.TryGetComponent(first, out firstPosition) ? firstPosition.Coord : default;
            GridCoord secondCoord = world.TryGetComponent(second, out secondPosition) ? secondPosition.Coord : default;
            reason = $"port mismatch push unexpected result success={result.Success} final=({result.FinalCoord.X},{result.FinalCoord.Y}) player=({playerCoord.X},{playerCoord.Y}) first=({firstCoord.X},{firstCoord.Y}) second=({secondCoord.X},{secondCoord.Y})";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyPortBlockedGroupFailsDeterministically(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(979, 979, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(980, new GridCoord(1, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(981, new GridCoord(2, 0), Direction.Right));
        world.AddEntity(DefaultWorldConfig.BlockerSpawn(982, new GridCoord(3, 0)));
        world.FlushDelta();
        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);

        AuthoritativeMoveInput input = queue.EnqueueMove(979, new GridCoord(1, 0), 33);
        runner.Tick();
        MoveResult result = input.WaitAsync().GetResult();
        if (!result.Success ||
            result.FinalCoord != new GridCoord(0, 0))
        {
            reason = "port blocked group did not defer source action";
            return false;
        }

        if (!world.TryGetEntity(980, out GameEntity first) ||
            !world.TryGetEntity(981, out GameEntity second) ||
            !world.TryGetComponent(first, out PositionComponent firstPosition) ||
            !world.TryGetComponent(second, out PositionComponent secondPosition) ||
            firstPosition.Coord != new GridCoord(1, 0) ||
            secondPosition.Coord != new GridCoord(2, 0))
        {
            reason = "port blocked group moved despite external blocker";
            return false;
        }
        runner.Tick();

        reason = string.Empty;
        return true;
    }

    private static bool VerifyActionQueueOrdering(out string reason)
    {
        var queue = new WorldActionQueue();
        WorldAction player = queue.EnqueuePlayerMove(2, new GridCoord(1, 0), 10);
        WorldAction debug = queue.EnqueueDebugMove(1, new GridCoord(5, 0));
        IReadOnlyList<WorldAction> drained = queue.Drain();
        if (drained.Count != 2 ||
            drained[0].ActionId != debug.ActionId ||
            drained[1].ActionId != player.ActionId)
        {
            reason = "action queue did not sort by priority";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyTickCostScheduling(out string reason)
    {
        var queue = new WorldActionQueue();
        WorldAction auto = queue.EnqueueAutoMove(1, 1, 3);
        IReadOnlyList<WorldAction> early = queue.DrainReady(3);
        if (early.Count != 0)
        {
            reason = "action ran before ready tick";
            return false;
        }

        IReadOnlyList<WorldAction> ready = queue.DrainReady(4);
        if (ready.Count != 1 ||
            ready[0].ActionId != auto.ActionId ||
            ready[0].ReadyTick != 4 ||
            ready[0].CostTicks != 3)
        {
            reason = "action did not run on ready tick";
            return false;
        }

        var deferredQueue = new WorldActionQueue();
        deferredQueue.EnqueueDeferred(new DeferredAction("player_push", 2, new[] { 2L }, Direction.Right, 1, 3, 2, 10, "test"));
        IReadOnlyList<WorldAction> earlyDeferred = deferredQueue.DrainReady(2);
        if (earlyDeferred.Count != 0)
        {
            reason = "deferred output ran before ready tick";
            return false;
        }

        IReadOnlyList<WorldAction> readyDeferred = deferredQueue.DrainReady(3);
        if (readyDeferred.Count != 1 ||
            !readyDeferred[0].SpecId.Equals(new ActionSpecId("player_push")) ||
            readyDeferred[0].ReadyTick != 3 ||
            readyDeferred[0].CostTicks != 2)
        {
            reason = "deferred output did not run on ready tick";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyProposalConflict(out string reason)
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(2, 2, new GridCoord(0, 1)));
        world.FlushDelta();
        var resolver = new CommitResolver();
        IReadOnlyList<CommitProposalResult> results = resolver.Resolve(world, new[]
        {
            CommitProposal.Move(WorldActionPriority.Player, 2, 0, 2, new GridCoord(0, 1), new GridCoord(1, 0), 1),
            CommitProposal.Move(WorldActionPriority.Player, 1, 0, 1, new GridCoord(0, 0), new GridCoord(1, 0), 1)
        });

        if (results.Count != 2 ||
            !results[0].Accepted ||
            results[1].Accepted ||
            string.IsNullOrEmpty(results[1].Reason) ||
            !world.TryGetEntity(1, out GameEntity playerA) ||
            !world.TryGetEntity(2, out GameEntity playerB) ||
            !world.TryGetComponent(playerA, out PositionComponent playerAPosition) ||
            !world.TryGetComponent(playerB, out PositionComponent playerBPosition) ||
            playerAPosition.Coord != new GridCoord(1, 0) ||
            playerBPosition.Coord != new GridCoord(0, 1))
        {
            reason = "proposal conflict was not deterministic";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyDebugActionsUseLogicTick(out string reason)
    {
        var world = new GameWorld();
        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);

        AuthoritativeDebugActionInput spawn = queue.EnqueueDebugSpawn(100, DefaultWorldConfig.BlockerConfigId, new GridCoord(0, 0), Direction.None, 0, 1);
        if (spawn.IsCompleted || world.TryGetEntity(100, out _))
        {
            reason = "debug spawn changed world before logic tick";
            return false;
        }

        WorldDelta spawnDelta = runner.Tick();
        MoveResult spawnResult = spawn.WaitAsync().GetResult();
        if (!spawnResult.Success ||
            !world.TryGetEntity(100, out GameEntity spawned) ||
            spawnDelta.ChangedEntities.Count != 1 ||
            spawnDelta.ChangedEntities[0].EntityId != 100)
        {
            reason = "debug spawn action did not commit on logic tick";
            return false;
        }

        AuthoritativeDebugActionInput move = queue.EnqueueDebugMove(100, new GridCoord(2, 0));
        if (move.IsCompleted ||
            !world.TryGetComponent(spawned, out PositionComponent beforeMove) ||
            beforeMove.Coord != new GridCoord(0, 0))
        {
            reason = "debug move changed world before logic tick";
            return false;
        }

        WorldDelta moveDelta = runner.Tick();
        MoveResult moveResult = move.WaitAsync().GetResult();
        if (!moveResult.Success ||
            moveResult.FinalCoord != new GridCoord(2, 0) ||
            moveDelta.ChangedEntities.Count != 1 ||
            !world.TryGetComponent(spawned, out PositionComponent afterMove) ||
            afterMove.Coord != new GridCoord(2, 0))
        {
            reason = "debug move action did not commit on logic tick";
            return false;
        }

        AuthoritativeDebugActionInput remove = queue.EnqueueDebugRemove(100);
        if (remove.IsCompleted || !world.TryGetEntity(100, out _))
        {
            reason = "debug remove changed world before logic tick";
            return false;
        }

        WorldDelta removeDelta = runner.Tick();
        MoveResult removeResult = remove.WaitAsync().GetResult();
        if (!removeResult.Success ||
            world.TryGetEntity(100, out _) ||
            removeDelta.RemovedEntityIds.Count != 1 ||
            removeDelta.RemovedEntityIds[0] != 100)
        {
            reason = "debug remove action did not commit on logic tick";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static MoveResult ExecutePlayerMove(GameWorld world, long entityId, GridCoord target, long clientTick)
    {
        var queue = new WorldActionQueue();
        WorldAction action = queue.EnqueuePlayerMove(entityId, target, clientTick);
        StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);
        return result.ActionResults.TryGetValue(action.ActionId, out MoveResult moveResult)
            ? moveResult
            : new MoveResult(false, entityId, default, Direction.None, MoveErrorCode.UnknownEntity, "action not resolved", false, default, clientTick);
    }

    private static MoveResult ExecuteAutoMove(GameWorld world, long entityId, long clientTick)
    {
        var queue = new WorldActionQueue();
        WorldAction action = queue.EnqueueAutoMove(entityId, world.ServerTick - 1, 1);
        StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);
        return result.ActionResults.TryGetValue(action.ActionId, out MoveResult moveResult)
            ? moveResult
            : new MoveResult(false, entityId, default, Direction.None, MoveErrorCode.UnknownEntity, "action not resolved", false, default, clientTick);
    }

    private static bool VerifyGeneratedRegisteredStrategyRuntimePath(out string reason)
    {
        var actionSpecs = new ActionSpecRegistry(new[]
        {
            new ActionSpec("verification_runtime_effect", ActionPrimitive.ApplyRuntimeEffect, ActionSourceKind.Runtime, WorldActionPriority.Debug, WorldTag.None, WorldTag.None, WorldTag.None, WorldTag.None, ActionTargetRule.None, "reject", ActionConflictPolicy.None, ActionInterruptPolicy.None, ActionMergePolicy.None, ActionPlanRule.None, ActionCommitRule.None)
        }, new[] { BlockedResultPolicyFactory.RejectPolicy("reject") });
        ActionStrategyRegistry strategies = VerificationGeneratedActionStrategyRegistration.CreateDefault();
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(88, 88, new GridCoord(0, 0)));
        world.NextTick();
        var inputQueue = new AuthoritativeInputQueue(actionSpecs);
        inputQueue.ActionQueue.EnqueueConfiguredMove("verification_runtime_effect", 88, Direction.None, 0, 1);
        var runner = new AuthoritativeWorldTickRunner(
            world,
            inputQueue,
            new AuthoritativeWorldSyncSystem(world),
            1,
            actionSpecs,
            strategies);

        runner.Tick();
        if (!world.TryGetEntity(88, out GameEntity entity) ||
            !world.TryGetComponent(entity, out TagSetComponent tags) ||
            !tags.Has(WorldTag.StateSuperArmor))
        {
            reason = "generated registered strategy did not enter authoritative tick";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyEffectApplicationLayer(out string reason)
    {
        var provider = LubanGameConfigProvider.FromDirectory(ServerGameConfigPath.FindGameCoreConfigDirectory());
        if (!provider.TryGetEffectSpec("temporary_pushable", out EffectSpec pushable) ||
            pushable.Kind != EffectKind.Pushable)
        {
            reason = "effect spec registry failed";
            return false;
        }

        var actionSpec = new ActionSpec("verification_apply_effect", ActionPrimitive.ApplyRuntimeEffect, ActionSourceKind.Debug, WorldActionPriority.Debug, WorldTag.SourceDebug, WorldTag.None, WorldTag.None, WorldTag.None, ActionTargetRule.Self, "reject", ActionConflictPolicy.None, ActionInterruptPolicy.None, ActionMergePolicy.None, ActionPlanRule.None, ActionCommitRule.None, effectSpecId: "temporary_pushable");
        var actionSpecs = new ActionSpecRegistry(new[] { actionSpec }, new[] { BlockedResultPolicyFactory.RejectPolicy("reject") });
        var strategies = new ActionStrategyRegistry();
        strategies.Register(new ApplyRuntimeEffectActionStrategy());
        var world = new GameWorld(provider);
        world.AddEntity(DefaultWorldConfig.BlockerSpawn(12001, new GridCoord(0, 0)));
        world.NextTick();
        var queue = new WorldActionQueue(actionSpecs);
        WorldAction action = queue.EnqueueConfiguredMove("verification_apply_effect", 12001, Direction.None, 0, 1);
        StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem(actionSpecs, strategies, provider).Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);
        if (!result.ActionResults[action.ActionId].Success ||
            world.RuntimeEffects.Count != 1 ||
            !world.TryGetEntity(12001, out GameEntity entity) ||
            !world.HasComponent<PushableComponent>(entity))
        {
            reason = "effect application commit did not produce final component";
            return false;
        }

        RuntimeEffectId effectId = world.RuntimeEffects.ActiveAt(world.ServerTick).Single().Id;
        IReadOnlyList<CommitProposalResult> remove = new CommitResolver().Resolve(world, new[] { CommitProposal.RemoveRuntimeEffect(WorldActionPriority.Debug, 0, 12001, effectId, world.ServerTick) });
        if (remove.Count != 1 ||
            !remove[0].Accepted ||
            world.HasComponent<PushableComponent>(entity))
        {
            reason = "effect remove did not clear own runtime source";
            return false;
        }

        var refreshSpec = new EffectSpec("refresh_server", EffectKind.Blocking, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.RefreshDuration, EffectRemovePolicy.ExplicitOrExpire, 6, 1, DirectionMask.None, true, true, WorldTag.None);
        RuntimeEffectInstance first = AddVerificationEffect(world, refreshSpec, 12001, 1, "same");
        RuntimeEffectInstance second = AddVerificationEffect(world, refreshSpec, 12001, 3, "same");
        if (world.RuntimeEffects.ActiveAt(3).Count(effect => effect.Spec.EffectSpecId.Equals("refresh_server")) != 1 ||
            second.Id.Equals(first.Id) ||
            world.RuntimeEffects.ActiveAt(3).Single(effect => effect.Spec.EffectSpecId.Equals("refresh_server")).Spec.ExpireTick != 9)
        {
            reason = "effect stack refresh failed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static RuntimeEffectInstance AddVerificationEffect(GameWorld world, EffectSpec spec, long entityId, long startTick, string stackKey)
    {
        var context = new ActionContext(9000 + startTick, 9000 + startTick, "verification_effect", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, entityId, 0, WorldTag.SourceDebug), entityId, entityId, entityId, entityId, new ActionTarget(entityId, null, Direction.None), Direction.None, startTick, startTick, 1, 0, 9000 + startTick);
        var application = new EffectApplication(context, spec, ActionTargetData.Self(entityId, default, Direction.None), startTick, stackKey);
        new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, context.ActionId, application, startTick) });
        return world.RuntimeEffects.ActiveAt(startTick).OrderByDescending(effect => effect.Id.Value).First(effect => effect.TargetEntityId == entityId && effect.Spec.EffectSpecId.Equals(spec.SpecId));
    }

    private sealed class VerificationRuntimeEffectStrategy : IActionStrategy
    {
        public ActionStrategyId StrategyId => "runtime_effect";

        public void Process(ActionStrategyContext context)
        {
            if (!context.World.TryGetEntity(context.Request.EntityId, out GameEntity entity))
            {
                context.ActionResults[context.Request.ActionId] = new MoveResult(false, context.Request.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "entity not found", false, default, context.Request.ClientTick);
                return;
            }

            context.World.AddTag(entity, WorldTag.StateSuperArmor);
            GridCoord coord = context.World.TryGetComponent(entity, out PositionComponent position) ? position.Coord : default;
            context.ActionResults[context.Request.ActionId] = new MoveResult(true, context.Request.EntityId, coord, Direction.None, MoveErrorCode.None, "verification-runtime-effect", false, default, context.Request.ClientTick);
            context.Reasons.Add("verification-runtime-effect");
        }
    }

    private static class VerificationGeneratedActionStrategyRegistration
    {
        public static ActionStrategyRegistry CreateDefault()
        {
            var registry = new ActionStrategyRegistry();
            registry.Register(new MoveActionStrategy());
            registry.Register(new RemoveActionStrategy());
            registry.Register(new SpawnActionStrategy());
            registry.Register(new VerificationRuntimeEffectStrategy());
            return registry;
        }
    }

    private static string RunDeterministicWorldHash()
    {
        var world = new GameWorld();
        world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
        world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(2, new GridCoord(1, 0)));
        world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(3, new GridCoord(2, 0)));
        world.FlushDelta();
        var queue = new AuthoritativeInputQueue();
        var runner = new AuthoritativeWorldTickRunner(
            world,
            queue,
            new AuthoritativeWorldSyncSystem(world),
            1);
        queue.EnqueueMove(1, new GridCoord(1, 0), 1);
        for (int i = 0; i < 5; i++)
        {
            runner.Tick();
        }

        return string.Join("|", world.CreateSnapshot().Select(snapshot => $"{snapshot.EntityId}:{snapshot.X},{snapshot.Y}:{snapshot.Direction}"));
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

        world.NextTick();
        DG.GameCore.MoveResult occupied = ExecutePlayerMove(world, playerA.EntityId, new GridCoord(playerB.Coord.X, playerB.Coord.Y), 0);
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

    private static ActionRequest MoveRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, long entityId, Direction direction, long sourceStateId = 0)
    {
        ActionSpec spec = ActionSpecRegistry.Default.Get(specId);
        return new ActionRequest(
            actionId,
            specId,
            priority,
            new ActionSourceContext(spec.DefaultSource, entityId, sourceStateId, spec.SourceTag),
            entityId,
            new ActionTarget(0, null, direction),
            default,
            0,
            0,
            0);
    }

    private static WorldAction WorldMove(long actionId, long entityId, Direction direction)
    {
        return new WorldAction(actionId, WorldActionPriority.Mechanism, "mechanism_push", entityId, null, direction, 0, 0, 0, 1);
    }

    private static void AddPort(GameWorld world, long entityId, GridCoord coord, DirectionMask ports)
    {
        world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(entityId, coord, Direction.Right));
        if (world.TryGetEntity(entityId, out GameEntity entity))
        {
            world.SetComponent(entity, new PortConnectorComponent(ports));
        }
    }

    private static void SetRotatePivot(GameWorld world, long entityId)
    {
        if (world.TryGetEntity(entityId, out GameEntity entity))
        {
            world.SetComponent(entity, new RotatePivotComponent());
            world.CaptureStaticComponentSources(entity);
        }
    }

    private static bool HasPosition(GameWorld world, long entityId, GridCoord expected)
    {
        return world.TryGetEntity(entityId, out GameEntity entity) &&
            world.TryGetComponent(entity, out PositionComponent position) &&
            position.Coord == expected;
    }

    private static bool HasDirection(GameWorld world, long entityId, Direction expected)
    {
        return world.TryGetEntity(entityId, out GameEntity entity) &&
            world.TryGetComponent(entity, out DirectionComponent direction) &&
            direction.Direction == expected;
    }

}
