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
        public void PushOnEnterSystem_MovesPlayerInTileDirection()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            world.NextTick();

            var system = new PushOnEnterSystem(new MovementResolveSystem());
            IReadOnlyList<MoveResult> results = system.Tick(world);

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].Success);
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity player));
            Assert.IsTrue(world.TryGetComponent(player, out PositionComponent position));
            Assert.AreEqual(new GridCoord(1, 0), position.Coord);
        }

        [Test]
        public void PushOnEnterSystem_KeepsPlayerWhenTargetBlocked()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            world.AddEntity(DefaultWorldConfig.BlockerSpawn(2, new GridCoord(1, 0)));
            world.NextTick();

            var system = new PushOnEnterSystem(new MovementResolveSystem());
            IReadOnlyList<MoveResult> results = system.Tick(world);

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].Success);
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity player));
            Assert.IsTrue(world.TryGetComponent(player, out PositionComponent position));
            Assert.AreEqual(new GridCoord(0, 0), position.Coord);
        }

        [Test]
        public void PushOnEnterSystem_PushesEntityOncePerTick()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(100, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.ConveyorSpawn(101, new GridCoord(1, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            world.NextTick();

            var system = new PushOnEnterSystem(new MovementResolveSystem());
            IReadOnlyList<MoveResult> results = system.Tick(world);

            Assert.AreEqual(1, results.Count(result => result.EntityId == 1));
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
