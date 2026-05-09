using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DG.GameCore;
using DG.Map;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class RuntimeComponentResultTests
    {
        [Test]
        public void RuntimeRemove_KeepsStaticBlockingResult()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(1, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity entity));

            RuntimeEffectInstance effect = world.AddRuntimeEffect(RuntimeEffectSpec.Blocking(1));

            Assert.IsTrue(world.HasComponent<BlockingComponent>(entity));
            Assert.IsTrue(world.RemoveRuntimeEffect(effect.Id));
            Assert.IsTrue(world.HasComponent<BlockingComponent>(entity));
        }

        [Test]
        public void RuntimeMultiSourceRemove_KeepsOtherRuntimeThenRemovesLast()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.ConveyorSpawn(2, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(2, out GameEntity entity));

            RuntimeEffectInstance first = world.AddRuntimeEffect(RuntimeEffectSpec.AutoMove(2, 2));
            RuntimeEffectInstance second = world.AddRuntimeEffect(RuntimeEffectSpec.AutoMove(2, 3));

            Assert.IsTrue(world.HasComponent<AutoMoveComponent>(entity));
            Assert.IsTrue(world.RemoveRuntimeEffect(first.Id));
            Assert.IsTrue(world.HasComponent<AutoMoveComponent>(entity));
            Assert.IsTrue(world.RemoveRuntimeEffect(second.Id));
            Assert.IsFalse(world.HasComponent<AutoMoveComponent>(entity));
        }

        [Test]
        public void RuntimeEffectStore_DoesNotWriteGameWorldComponents()
        {
            var world = new GameWorld();
            var store = new RuntimeEffectStore();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.ConveyorSpawn(3, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(3, out GameEntity entity));

            store.Add(RuntimeEffectSpec.Blocking(3));

            Assert.AreEqual(1, store.Count);
            Assert.IsFalse(world.HasComponent<BlockingComponent>(entity));
        }

        [Test]
        public void EffectLifecycleSystem_ReportsActiveRemovedAndExpired()
        {
            var store = new RuntimeEffectStore();
            var lifecycle = new EffectLifecycleSystem();
            RuntimeEffectInstance expiring = store.Add(RuntimeEffectSpec.Blocking(4, 0, 2));
            RuntimeEffectInstance removable = store.Add(RuntimeEffectSpec.Pushable(4));

            RuntimeEffectLifecycleResult active = lifecycle.Active(store, 1);
            Assert.AreEqual(2, active.Active.Count);

            RuntimeEffectLifecycleResult expired = lifecycle.Expire(store, 2);
            Assert.AreEqual(expiring.Id, expired.Expired.Single().Id);
            Assert.AreEqual(removable.Id, expired.Active.Single().Id);

            RuntimeEffectLifecycleResult removed = lifecycle.Remove(store, removable.Id, 2);
            Assert.AreEqual(removable.Id, removed.Removed.Single().Id);
            Assert.AreEqual(0, removed.Active.Count);
        }

        [Test]
        public void RuntimePort_MergesWithStaticAndRemovalKeepsStaticPort()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(5, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(5, out GameEntity entity));
            Assert.IsTrue(world.TryGetComponent(entity, out PortConnectorComponent staticPort));
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right, staticPort.LocalPorts);

            RuntimeEffectInstance runtimePort = world.AddRuntimeEffect(RuntimeEffectSpec.Port(5, DirectionMask.Up));

            Assert.IsTrue(world.TryGetComponent(entity, out PortConnectorComponent mergedPort));
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right | DirectionMask.Up, mergedPort.LocalPorts);
            Assert.IsTrue(world.RemoveRuntimeEffect(runtimePort.Id));
            Assert.IsTrue(world.TryGetComponent(entity, out PortConnectorComponent finalPort));
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right, finalPort.LocalPorts);
        }

        [Test]
        public void RuntimeMovementPermission_RejectsMoveThroughRules()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(6, 6, new GridCoord(0, 0))));
            world.AddRuntimeEffect(RuntimeEffectSpec.Immobile(6));
            var action = new WorldAction(1, WorldActionPriority.Player, "player_move", 6, new GridCoord(1, 0), Direction.None, 0, 0, 0, 1);

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, new[] { action }, new PendingRuleStateStore(), 1);

            Assert.IsFalse(result.ActionResults[1].Success);
            Assert.AreEqual(MoveErrorCode.Blocked, result.ActionResults[1].ErrorCode);
            Assert.AreEqual("blocked by movement permission", result.ActionResults[1].Reason);
        }

        [Test]
        public void StaticMovementPermissionSource_AppliesThroughResolver()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(9, 9, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(9, out GameEntity entity));

            world.AddStaticComponentSource(ComponentSourceContribution.MovementPermission(9, ComponentSourceKey.Static(9), false, true));
            world.ResolveComponentResults();

            Assert.IsTrue(world.TryGetComponent(entity, out MovementPermissionComponent permission));
            Assert.IsFalse(permission.CanMove);
            Assert.IsTrue(permission.CanBePushed);
        }

        [Test]
        public void ClientMapWorld_AppliesServerFinalComponentResultsOnly()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());
            var snapshot = new EntitySnapshot(
                7,
                DefaultWorldConfig.ConveyorConfigId,
                DefaultWorldConfig.ConveyorArchetypeId,
                DefaultWorldConfig.BlockerTarget,
                0,
                0,
                Direction.Right,
                true,
                true,
                false,
                true,
                4,
                false,
                true,
                DirectionMask.Down,
                true,
                false,
                false,
                10);

            Assert.IsTrue(world.ApplySnapshot(snapshot));
            Assert.IsTrue(world.TryGetCoreEntity(7, out GameEntity entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<BlockingComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<PushableComponent>(entity));
            Assert.IsTrue(world.CoreWorld.TryGetComponent(entity, out PortConnectorComponent port));
            Assert.AreEqual(DirectionMask.Down, port.LocalPorts);
            Assert.IsTrue(world.CoreWorld.TryGetComponent(entity, out AutoMoveComponent autoMove));
            Assert.AreEqual(4, autoMove.IntervalTicks);
            Assert.IsTrue(world.CoreWorld.TryGetComponent(entity, out MovementPermissionComponent permission));
            Assert.IsFalse(permission.CanMove);
            Assert.IsFalse(permission.CanBePushed);
            Assert.AreEqual(0, world.CoreWorld.RuntimeEffects.Count);
        }

        [Test]
        public void ClientMapWorld_FinalSnapshotCanRemoveStaticFirstSliceResults()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());
            var snapshot = new EntitySnapshot(
                8,
                DefaultWorldConfig.PortConnectorBlockerConfigId,
                DefaultWorldConfig.PortConnectorBlockerArchetypeId,
                DefaultWorldConfig.BlockerTarget,
                0,
                0,
                Direction.Right,
                true,
                false,
                false,
                false,
                0,
                false,
                false,
                DirectionMask.None,
                false,
                true,
                true,
                11);

            Assert.IsTrue(world.ApplySnapshot(snapshot));
            Assert.IsTrue(world.TryGetCoreEntity(8, out GameEntity entity));
            Assert.IsFalse(world.CoreWorld.HasComponent<BlockingComponent>(entity));
            Assert.IsFalse(world.CoreWorld.HasComponent<PushableComponent>(entity));
            Assert.IsFalse(world.CoreWorld.HasComponent<PortConnectorComponent>(entity));
        }

        [Test]
        public void NoAbilityKindMappingExistsInFirstSlice()
        {
            string root = RepositoryRoot();
            string[] files = Directory.GetFiles(Path.Combine(root, "Shared", "DG.GameCore"), "*.cs", SearchOption.AllDirectories);
            string text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

            Assert.IsFalse(Regex.IsMatch(text, @"\bAbilityKind\b"));
            Assert.IsFalse(Regex.IsMatch(text, @"\bEffectKind\b"));
            Assert.IsFalse(Regex.IsMatch(text, @"\bAbilityKind\b[\s\S]*\bComponentKind\b|\bComponentKind\b[\s\S]*\bAbilityKind\b"));
        }

        [Test]
        public void RulesDoNotQueryRuntimeEffectOrAbilityKinds()
        {
            string rulesPath = Path.Combine(RepositoryRoot(), "Shared", "DG.GameCore", "Rules");
            string[] files = Directory.GetFiles(rulesPath, "*.cs", SearchOption.AllDirectories);
            string text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

            Assert.IsFalse(text.Contains("RuntimeEffectStore"));
            Assert.IsFalse(text.Contains("RuntimeEffectKind"));
            Assert.IsFalse(Regex.IsMatch(text, @"\bAbilityKind\b"));
            Assert.IsFalse(Regex.IsMatch(text, @"\bEffectKind\b"));
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }
    }
}
