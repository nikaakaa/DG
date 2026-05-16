using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.GameCore;
using DG.Map;
using NUnit.Framework;
using UnityEngine;
using RuntimeComponentKind = DG.GameCore.ComponentKind;

namespace DG.EditorTests
{
    public sealed class ComponentSystemWorkflowTests
    {
        [Test]
        public void FormalLubanSchema_UsesComponentIdsInsteadOfComponentKindEnum()
        {
            string root = RepositoryRoot();
            string schema = File.ReadAllText(Path.Combine(root, "Config", "Luban", "Defines", "gamecore.xml"));
            string generated = File.ReadAllText(Path.Combine(root, "Config", "Luban", "Generated", "json", "gamecore_tbentityarchetype.json"));
            string streaming = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "GameConfig", "gamecore_tbentityarchetype.json"));

            Assert.IsFalse(schema.Contains("enum name=\"ComponentKind\""));
            Assert.IsFalse(schema.Contains("type=\"(list#sep=,),ComponentKind\""));
            Assert.IsTrue(schema.Contains("component_ids"));
            Assert.IsFalse(generated.Contains("\"components\""));
            Assert.IsFalse(streaming.Contains("\"components\""));
            Assert.IsTrue(generated.Contains("\"component_ids\""));
            Assert.IsTrue(streaming.Contains("\"component_ids\""));
        }

        [Test]
        public void DefaultComponentApplicationRegistry_CoversEveryRuntimeComponentKind()
        {
            RuntimeComponentKind[] runtimeKinds = Enum.GetValues(typeof(RuntimeComponentKind)).Cast<RuntimeComponentKind>().ToArray();
            RuntimeComponentKind[] registeredKinds = ComponentApplicationRegistry.Default.RegisteredKinds.ToArray();

            CollectionAssert.AreEquivalent(runtimeKinds, registeredKinds);
        }

        [Test]
        public void DefaultComponentApplicationRegistry_AppliesAllCurrentComponentKinds()
        {
            var world = new GameWorld();
            var archetype = new EntityArchetype(
                990001,
                990001,
                DefaultWorldConfig.PlayerTarget,
                Enum.GetValues(typeof(RuntimeComponentKind)).Cast<RuntimeComponentKind>().ToArray(),
                Array.Empty<string>(),
                3);
            var spawn = new EntitySpawnSpec(990001, 990001, new GridCoord(4, 5), Direction.Up, 77, 2);

            Assert.IsTrue(EntityBuilder.AddEntity(world, new SingleArchetypeProvider(archetype), spawn));
            Assert.IsTrue(world.TryGetEntity(990001, out GameEntity entity));
            Assert.IsTrue(world.HasComponent<PositionComponent>(entity));
            Assert.IsTrue(world.HasComponent<DirectionComponent>(entity));
            Assert.IsTrue(world.HasComponent<ColliderComponent>(entity));
            Assert.IsTrue(world.HasComponent<BlockingComponent>(entity));
            Assert.IsTrue(world.HasComponent<BouncableComponent>(entity));
            Assert.IsTrue(world.HasComponent<AutoMoveComponent>(entity));
            Assert.IsTrue(world.HasComponent<PlayerControlComponent>(entity));
            Assert.IsTrue(world.HasComponent<PushOnEnterComponent>(entity));
            Assert.IsTrue(world.HasComponent<PushableComponent>(entity));
            Assert.IsTrue(world.HasComponent<PortConnectorComponent>(entity));
            Assert.IsTrue(world.TryGetComponent(entity, out PortConnectorComponent portConnector));
            Assert.AreEqual(DirectionMask.All, portConnector.LocalPorts);
        }

        [Test]
        public void DefaultComponentApplicationRegistry_FailsUnknownComponentKindClearly()
        {
            var world = new GameWorld();
            var entity = new GameEntity(1, 1, 1, DefaultWorldConfig.PlayerTarget);
            var archetype = new EntityArchetype(1, 1, DefaultWorldConfig.PlayerTarget, Array.Empty<RuntimeComponentKind>(), Array.Empty<string>(), 1);
            var spawn = DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0));
            world.AddEntity(entity);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                ComponentApplicationRegistry.Default.Apply(world, new SingleArchetypeProvider(archetype), entity, archetype, spawn, (RuntimeComponentKind)9999));
            StringAssert.Contains("Unsupported component kind", exception.Message);
        }

        [Test]
        public void ComponentApplicationRegistry_AddsComponentIdWithoutEntityBuilderBranch()
        {
            var registry = new ComponentApplicationRegistry(new Dictionary<ComponentId, ComponentApplication>
            {
                ["test_component"] = (world, _, entity, _, _) => world.SetComponent(entity, new BouncableComponent())
            });
            var world = new GameWorld();
            var entity = new GameEntity(42, 42, 42, DefaultWorldConfig.PlayerTarget);
            var archetype = new EntityArchetype(42, 42, DefaultWorldConfig.PlayerTarget, new[] { new ComponentId("test_component") }, Array.Empty<string>(), 1);
            var spawn = new EntitySpawnSpec(42, 42, new GridCoord(0, 0), Direction.None, 0, 1);
            Assert.IsTrue(world.AddEntity(entity));

            registry.Apply(world, FallbackGameConfigProvider.Instance, entity, archetype, spawn, "test_component");

            Assert.IsTrue(world.HasComponent<BouncableComponent>(entity));
        }

        [Test]
        public void ComponentFactQueryRegistry_UnknownComponentIdFailsClearly()
        {
            var registry = new ComponentFactQueryRegistry();
            var world = new GameWorld();
            var entity = new GameEntity(43, 43, 43, DefaultWorldConfig.PlayerTarget);
            Assert.IsTrue(world.AddEntity(entity));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => registry.Has(world, entity, "missing_component"));

            StringAssert.Contains("No component query registered for id", exception.Message);
        }

        [Test]
        public void ClientMapWorld_UsesSharedGameWorldForConfiguredComponentComposition()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());

            Assert.IsTrue(world.ApplySpawn(DefaultWorldConfig.PushableBlockerSpawn(990002, new GridCoord(6, 7))));
            Assert.IsTrue(world.TryGetCoreEntity(990002, out GameEntity entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<PositionComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<ColliderComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<BlockingComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<PushableComponent>(entity));
            Assert.IsTrue(world.TryGetPosition(990002, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(6, 7), coord);
        }

        [Test]
        public void ClientMapWorld_UsesSharedGameWorldForPortConnectorComposition()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());

            Assert.IsTrue(world.ApplySpawn(DefaultWorldConfig.PortConnectorBlockerSpawn(990003, new GridCoord(7, 8), Direction.Down)));
            Assert.IsTrue(world.TryGetCoreEntity(990003, out GameEntity entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<PortConnectorComponent>(entity));
            Assert.IsTrue(world.CoreWorld.TryGetComponent(entity, out PortConnectorComponent portConnector));
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right, portConnector.LocalPorts);
            Assert.AreEqual(DirectionMask.Up | DirectionMask.Down, PortConnectionSystem.GetWorldPorts(world.CoreWorld, entity));
        }

        [Test]
        public void LubanGeneratedJson_MatchesUnityStreamingAssets()
        {
            string generatedDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Config", "Luban", "Generated", "json"));
            string streamingDirectory = Path.Combine(Application.streamingAssetsPath, "GameConfig");
            string[] generatedFiles = Directory.GetFiles(generatedDirectory, "*.json").Select(Path.GetFileName).OrderBy(name => name).ToArray();
            string[] streamingFiles = Directory.GetFiles(streamingDirectory, "*.json").Select(Path.GetFileName).OrderBy(name => name).ToArray();

            CollectionAssert.AreEqual(generatedFiles, streamingFiles);
            foreach (string fileName in generatedFiles)
            {
                string generatedJson = File.ReadAllText(Path.Combine(generatedDirectory, fileName));
                string streamingJson = File.ReadAllText(Path.Combine(streamingDirectory, fileName));
                Assert.AreEqual(generatedJson, streamingJson, fileName);
            }
        }

        [Test]
        public void LubanDemoConfig_ContainsCurrentFormalConfig()
        {
            IGameConfigProvider luban = ClientGameConfigProviderFactory.Create();

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

            foreach (int configId in requiredConfigs)
            {
                Assert.IsTrue(luban.TryGetArchetype(configId, out _), configId.ToString());
            }

            Assert.IsTrue(NormalizeSpawns(luban.GetWorldSpawns(DefaultWorldConfig.DemoWorldId)).Length > 0);

            Assert.IsTrue(luban.TryGetPlayerSpawnRule(DefaultWorldConfig.DefaultPlayerSpawnRuleId, out PlayerSpawnRule lubanRule));
            Assert.AreEqual(DefaultWorldConfig.PlayerConfigId, lubanRule.PlayerConfigId);
            Assert.AreEqual(new GridCoord(0, 0), lubanRule.StartCoord);
            Assert.AreEqual(new GridCoord(0, 1), lubanRule.StepCoord);
            Assert.Greater(lubanRule.MaxAttempts, 0);

            Assert.IsTrue(luban.TryGetPortConnector(DefaultWorldConfig.PortConnectorBlockerConfigId, out PortConnectorConfig lubanPort));
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right, lubanPort.LocalPorts);
            Assert.IsTrue(luban.TryGetPushOnEnter(DefaultWorldConfig.ConveyorConfigId, out PushOnEnterConfig conveyorOutput));
            Assert.AreEqual(new ActionSpecId("mechanism_push"), conveyorOutput.OutputSpecId);
            Assert.AreEqual(3, conveyorOutput.OutputCostTicks);
            Assert.IsTrue(luban.TryGetPushOnEnter(DefaultWorldConfig.WindFieldConfigId, out PushOnEnterConfig windOutput));
            Assert.AreEqual(new ActionSpecId("configured_wind_push"), windOutput.OutputSpecId);
            Assert.AreEqual(2, windOutput.OutputCostTicks);
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }

        private static string[] NormalizeSpawns(IReadOnlyList<EntitySpawnSpec> spawns)
        {
            return spawns
                .OrderBy(spawn => spawn.EntityId)
                .Select(spawn => $"{spawn.EntityId}:{spawn.ConfigId}:{spawn.Position.X}:{spawn.Position.Y}:{spawn.Direction}:{spawn.PlayerId}:{spawn.AutoMoveIntervalTicks}")
                .ToArray();
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
}
}
