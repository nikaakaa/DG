using DG.GameCore;
using DG.Map;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class ClientMapWorldCompositionTests
    {
        [Test]
        public void ApplySnapshot_UsesLubanArchetypeForMirrorEntity()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());
            var snapshot = new EntitySnapshot(
                900,
                DefaultWorldConfig.BallConfigId,
                DefaultWorldConfig.BallArchetypeId,
                DefaultWorldConfig.BallTarget,
                2,
                3,
                Direction.Right,
                true,
                true,
                true,
                true,
                false,
                1);

            Assert.IsTrue(world.ApplySnapshot(snapshot));
            Assert.IsTrue(world.TryGetCoreEntity(900, out GameEntity entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<PositionComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<DirectionComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<ColliderComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<BlockingComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<BouncableComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<AutoMoveComponent>(entity));
            Assert.IsTrue(world.TryGetPosition(900, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(2, 3), coord);
        }

        [Test]
        public void RemoveEntity_RemovesMirrorEntityAndViewHandle()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());
            Assert.IsTrue(world.ApplySnapshot(new EntitySnapshot(
                1,
                DefaultWorldConfig.PlayerConfigId,
                DefaultWorldConfig.PlayerArchetypeId,
                DefaultWorldConfig.PlayerTarget,
                0,
                0,
                Direction.None,
                true,
                true,
                false,
                false,
                true,
                1)));

            Assert.IsTrue(world.RemoveEntity(1));
            Assert.IsFalse(world.TryGetCoreEntity(1, out _));
            Assert.IsFalse(world.TryGetEntity(1, out _));
        }

        [Test]
        public void ClientRunner_AppliesAuthoritativeFinalMoveResult()
        {
            var gameObject = new GameObject("Runner");
            var bootstrap = gameObject.AddComponent<WorldBootstrap>();
            ClientWorldRunner runner = gameObject.AddComponent<ClientWorldRunner>();
            runner.Initialize(bootstrap.EnsureWorld());
            runner.Context.ClientMapWorld.AddEntity(
                new ClientMapEntity { EntityId = 1 },
                DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));

            Assert.IsTrue(runner.ApplyServerMovementOrCreate(1, new Vector2Int(1, 0), DefaultWorldConfig.PlayerConfigId, Direction.None, 1, 1));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(1, 0), coord);

            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ClientRunner_AdvanceDoesNotChangeAuthoritativeMirror()
        {
            var gameObject = new GameObject("Runner");
            var bootstrap = gameObject.AddComponent<WorldBootstrap>();
            ClientWorldRunner runner = gameObject.AddComponent<ClientWorldRunner>();
            runner.Initialize(bootstrap.EnsureWorld());
            runner.Context.ClientMapWorld.AddEntity(
                new ClientMapEntity { EntityId = 1 },
                DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));

            runner.Advance(runner.FixedTickInterval);

            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(0, 0), coord);

            Object.DestroyImmediate(gameObject);
        }
    }
}
