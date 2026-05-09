using System.Collections.Generic;
using System.Linq;
using DG.GameCore;
using DG.Map;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class PushOnEnterTileTests
    {
        [Test]
        public void LubanConveyorArchetype_UsesPushOnEnterComposition()
        {
            IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();

            Assert.IsTrue(provider.TryGetArchetype(DefaultWorldConfig.ConveyorConfigId, out EntityArchetype archetype));
            Assert.AreEqual(DefaultWorldConfig.ConveyorArchetypeId, archetype.ArchetypeId);
            Assert.IsTrue(archetype.Components.Contains(ComponentKind.Position));
            Assert.IsTrue(archetype.Components.Contains(ComponentKind.Direction));
            Assert.IsTrue(archetype.Components.Contains(ComponentKind.Collider));
            Assert.IsTrue(archetype.Components.Contains(ComponentKind.PushOnEnter));
            Assert.IsFalse(archetype.Components.Contains(ComponentKind.Blocking));
            Assert.IsTrue(archetype.Tags.Contains("Tile.Conveyor"));
        }

        [Test]
        public void ConveyorTile_DoesNotBlockPlayerEntry()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right)));

            Assert.IsTrue(world.CanEnterNewEntity(new GridCoord(0, 0)));
        }

        [Test]
        public void StateDrivenMechanismPush_MovesPlayerInTileDirection()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            world.NextTick();

            var queue = new WorldActionQueue();
            queue.EnqueueConfiguredMove("mechanism_push", 1, Direction.Right, world.ServerTick - 1, 1);
            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), new PendingRuleStateStore(), world.ServerTick);

            Assert.AreEqual(1, result.ActionResults.Count);
            Assert.IsTrue(result.ActionResults.Values.Single().Success);
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity player));
            Assert.IsTrue(world.TryGetComponent(player, out PositionComponent position));
            Assert.AreEqual(new GridCoord(1, 0), position.Coord);
        }

        [Test]
        public void StateDrivenMechanismPush_KeepsPlayerWhenTargetBlocked()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            world.AddEntity(DefaultWorldConfig.BlockerSpawn(2, new GridCoord(1, 0)));
            world.NextTick();

            var queue = new WorldActionQueue();
            queue.EnqueueConfiguredMove("mechanism_push", 1, Direction.Right, world.ServerTick - 1, 1);
            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), new PendingRuleStateStore(), world.ServerTick);

            Assert.AreEqual(1, result.ActionResults.Count);
            Assert.IsFalse(result.ActionResults.Values.Single().Success);
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity player));
            Assert.IsTrue(world.TryGetComponent(player, out PositionComponent position));
            Assert.AreEqual(new GridCoord(0, 0), position.Coord);
        }

        [Test]
        public void StateDrivenMechanismPush_PushesEntityOncePerTick()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(101, new GridCoord(1, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            world.NextTick();

            var queue = new WorldActionQueue();
            queue.EnqueueConfiguredMove("mechanism_push", 1, Direction.Right, world.ServerTick - 1, 1);
            queue.EnqueueConfiguredMove("mechanism_push", 1, Direction.Right, world.ServerTick - 1, 1);
            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), new PendingRuleStateStore(), world.ServerTick);

            Assert.AreEqual(2, result.ActionResults.Count(item => item.Value.EntityId == 1));
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity player));
            Assert.IsTrue(world.TryGetComponent(player, out PositionComponent position));
            Assert.AreEqual(new GridCoord(1, 0), position.Coord);
        }

        [Test]
        public void PushOnEnterOutput_UsesComponentSpecAndReadyCost()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            Assert.IsTrue(world.TryGetEntity(100, out GameEntity conveyor));
            world.SetComponent(conveyor, new PushOnEnterComponent("configured_wind_push", 2));
            world.NextTick();
            var queue = new WorldActionQueue();

            int enqueued = ExplicitOutputPolicies.EnqueuePushOnEnterActions(world, queue, world.ServerTick);
            StateDrivenRuleExecutionResult beforeReady = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick + 1), new PendingRuleStateStore(), world.ServerTick + 1);
            StateDrivenRuleExecutionResult ready = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick + 2), new PendingRuleStateStore(), world.ServerTick + 2);

            Assert.AreEqual(1, enqueued);
            Assert.AreEqual(0, beforeReady.ActionResults.Count);
            Assert.AreEqual(1, ready.ActionResults.Count);
            Assert.IsTrue(ready.ActionResults.Values.Single().Success);
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity player));
            Assert.IsTrue(world.TryGetComponent(player, out PositionComponent position));
            Assert.AreEqual(new GridCoord(1, 0), position.Coord);
        }

        [Test]
        public void ApplySnapshot_PreservesConveyorMirrorEntity()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());
            var snapshot = new EntitySnapshot(
                100,
                DefaultWorldConfig.ConveyorConfigId,
                DefaultWorldConfig.ConveyorArchetypeId,
                DefaultWorldConfig.BlockerTarget,
                1,
                0,
                Direction.Right,
                true,
                false,
                false,
                false,
                false,
                1);

            Assert.IsTrue(world.ApplySnapshot(snapshot));
            Assert.IsTrue(world.TryGetCoreEntity(100, out GameEntity entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<PositionComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<DirectionComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<ColliderComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<PushOnEnterComponent>(entity));
            Assert.IsFalse(world.CoreWorld.HasComponent<BlockingComponent>(entity));
            Assert.IsTrue(world.TryGetPosition(100, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(1, 0), coord);
        }
    }
}
