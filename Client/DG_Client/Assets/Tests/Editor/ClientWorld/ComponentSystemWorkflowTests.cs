using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DG.GameCore;
using DG.Map;
using NUnit.Framework;
using UnityEngine;
using RuntimeComponentKind = DG.GameCore.ComponentKind;
using LubanComponentKind = cfg.gamecore.ComponentKind;

namespace DG.EditorTests
{
    public sealed class ComponentSystemWorkflowTests
    {
        [Test]
        public void ComponentKindSchema_RuntimeAndGeneratedEnumsDoNotDrift()
        {
            IReadOnlyDictionary<string, int> schemaValues = ReadComponentKindSchema();
            IReadOnlyDictionary<string, int> runtimeValues = EnumValues<RuntimeComponentKind>();
            IReadOnlyDictionary<string, int> lubanValues = EnumValues<LubanComponentKind>();

            CollectionAssert.AreEquivalent(schemaValues, runtimeValues);
            CollectionAssert.AreEquivalent(schemaValues, lubanValues);
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
        public void FallbackDemoConfig_MatchesLubanDemoConfig()
        {
            IGameConfigProvider luban = ClientGameConfigProviderFactory.Create();
            IGameConfigProvider fallback = FallbackGameConfigProvider.Instance;

            foreach (EntityArchetype fallbackArchetype in fallback.GetEntityArchetypes())
            {
                Assert.IsTrue(luban.TryGetArchetype(fallbackArchetype.ConfigId, out EntityArchetype lubanArchetype), fallbackArchetype.ConfigId.ToString());
                Assert.AreEqual(lubanArchetype.ArchetypeId, fallbackArchetype.ArchetypeId);
                Assert.AreEqual(lubanArchetype.EntityTarget, fallbackArchetype.EntityTarget);
                CollectionAssert.AreEqual(lubanArchetype.Components.OrderBy(kind => (int)kind).ToArray(), fallbackArchetype.Components.OrderBy(kind => (int)kind).ToArray());
                CollectionAssert.AreEqual(lubanArchetype.Tags.OrderBy(tag => tag).ToArray(), fallbackArchetype.Tags.OrderBy(tag => tag).ToArray());
                Assert.AreEqual(lubanArchetype.DefaultAutoMoveIntervalTicks, fallbackArchetype.DefaultAutoMoveIntervalTicks);
            }

            CollectionAssert.AreEqual(
                NormalizeSpawns(luban.GetWorldSpawns(DefaultWorldConfig.DemoWorldId)),
                NormalizeSpawns(fallback.GetWorldSpawns(DefaultWorldConfig.DemoWorldId)));

            Assert.IsTrue(luban.TryGetPlayerSpawnRule(DefaultWorldConfig.DefaultPlayerSpawnRuleId, out PlayerSpawnRule lubanRule));
            Assert.IsTrue(fallback.TryGetPlayerSpawnRule(DefaultWorldConfig.DefaultPlayerSpawnRuleId, out PlayerSpawnRule fallbackRule));
            Assert.AreEqual(lubanRule.PlayerConfigId, fallbackRule.PlayerConfigId);
            Assert.AreEqual(lubanRule.StartCoord, fallbackRule.StartCoord);
            Assert.AreEqual(lubanRule.StepCoord, fallbackRule.StepCoord);
            Assert.AreEqual(lubanRule.MaxAttempts, fallbackRule.MaxAttempts);

            Assert.IsTrue(luban.TryGetPortConnector(DefaultWorldConfig.PortConnectorBlockerConfigId, out PortConnectorConfig lubanPort));
            Assert.IsTrue(fallback.TryGetPortConnector(DefaultWorldConfig.PortConnectorBlockerConfigId, out PortConnectorConfig fallbackPort));
            Assert.AreEqual(lubanPort.LocalPorts, fallbackPort.LocalPorts);
        }

        private static IReadOnlyDictionary<string, int> ReadComponentKindSchema()
        {
            string xmlPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Config", "Luban", "Defines", "gamecore.xml"));
            XDocument document = XDocument.Load(xmlPath);
            XElement enumElement = document.Root.Element("enum");
            return enumElement.Elements("var")
                .ToDictionary(
                    item => item.Attribute("name").Value,
                    item => int.Parse(item.Attribute("value").Value));
        }

        private static IReadOnlyDictionary<string, int> EnumValues<TEnum>() where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .ToDictionary(value => value.ToString(), value => Convert.ToInt32(value));
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
        }
    }
}
