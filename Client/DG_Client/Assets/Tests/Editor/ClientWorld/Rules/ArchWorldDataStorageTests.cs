using System;
using System.Collections.Generic;
using System.Linq;
using DG.GameCore;
using NUnit.Framework;

namespace DG.EditorTests
{
    public sealed class ArchWorldDataStorageTests
    {
        [Test]
        public void ArchBackend_AddGetRemoveEntity_MatchesIndexedBackend()
        {
            GameWorld indexed = GameWorld.CreateIndexed(FallbackGameConfigProvider.Instance);
            GameWorld arch = GameWorld.CreateArchBacked(FallbackGameConfigProvider.Instance);

            AddBasicEntity(indexed, 3, new GridCoord(1, 2));
            AddBasicEntity(arch, 3, new GridCoord(1, 2));

            Assert.AreEqual(indexed.EntityCount, arch.EntityCount);
            Assert.IsTrue(indexed.TryGetEntity(3, out GameEntity indexedEntity));
            Assert.IsTrue(arch.TryGetEntity(3, out GameEntity archEntity));
            AssertEntityEquals(indexedEntity, archEntity);

            Assert.IsTrue(indexed.RemoveEntity(3));
            Assert.IsTrue(arch.RemoveEntity(3));
            Assert.AreEqual(indexed.EntityCount, arch.EntityCount);
            Assert.IsFalse(arch.TryGetEntity(3, out _));
        }

        [Test]
        public void ArchBackend_ComponentLifecycle_MatchesIndexedBackend()
        {
            GameWorld indexed = GameWorld.CreateIndexed(FallbackGameConfigProvider.Instance);
            GameWorld arch = GameWorld.CreateArchBacked(FallbackGameConfigProvider.Instance);
            GameEntity indexedEntity = AddBasicEntity(indexed, 7, new GridCoord(0, 0));
            GameEntity archEntity = AddBasicEntity(arch, 7, new GridCoord(0, 0));

            indexed.SetComponent(indexedEntity, new MovementPermissionComponent(true, false));
            arch.SetComponent(archEntity, new MovementPermissionComponent(true, false));

            Assert.AreEqual(indexed.HasComponent<MovementPermissionComponent>(indexedEntity), arch.HasComponent<MovementPermissionComponent>(archEntity));
            Assert.IsTrue(arch.TryGetComponent(archEntity, out MovementPermissionComponent permission));
            Assert.IsTrue(permission.CanMove);
            Assert.IsFalse(permission.CanBePushed);

            indexed.SetComponent(indexedEntity, new PositionComponent(new GridCoord(5, 6)));
            arch.SetComponent(archEntity, new PositionComponent(new GridCoord(5, 6)));
            Assert.AreEqual(indexed.GetComponent<PositionComponent>(indexedEntity).Coord, arch.GetComponent<PositionComponent>(archEntity).Coord);

            Assert.AreEqual(indexed.RemoveComponent<MovementPermissionComponent>(indexedEntity), arch.RemoveComponent<MovementPermissionComponent>(archEntity));
            Assert.IsFalse(arch.HasComponent<MovementPermissionComponent>(archEntity));
        }

        [Test]
        public void ArchBackend_RemovedEntityId_DoesNotReadRecycledArchEntity()
        {
            GameWorld arch = GameWorld.CreateArchBacked(FallbackGameConfigProvider.Instance);
            AddBasicEntity(arch, 10, new GridCoord(1, 1));

            Assert.IsTrue(arch.RemoveEntity(10));
            AddBasicEntity(arch, 11, new GridCoord(2, 2));

            Assert.IsFalse(arch.TryGetEntity(10, out _));
            Assert.AreEqual(1, arch.EntityCount);
        }

        [Test]
        public void ArchBackend_EntityIdIterationOrder_IsStable()
        {
            GameWorld arch = GameWorld.CreateArchBacked(FallbackGameConfigProvider.Instance);
            AddBasicEntity(arch, 30, new GridCoord(0, 0));
            AddBasicEntity(arch, 10, new GridCoord(1, 0));
            AddBasicEntity(arch, 20, new GridCoord(2, 0));

            CollectionAssert.AreEqual(new long[] { 10, 20, 30 }, arch.EnumerateEntities(EntityIterationOrder.EntityId).Select(entity => entity.EntityId).ToArray());
            CollectionAssert.AreEqual(new long[] { 10, 20, 30 }, arch.QueryEntities(ComponentQueryDescriptor.With<PositionComponent>(), EntityIterationOrder.EntityId).Select(entity => entity.EntityId).ToArray());
        }

        [Test]
        public void ArchBackend_HotPathQueries_MatchIndexedBackend()
        {
            GameWorld indexed = GameWorld.CreateIndexed(FallbackGameConfigProvider.Instance);
            GameWorld arch = GameWorld.CreateArchBacked(FallbackGameConfigProvider.Instance);

            AddAutoMoveSource(indexed, 1, new GridCoord(1, 1), Direction.Right);
            AddAutoMoveSource(arch, 1, new GridCoord(1, 1), Direction.Right);
            AddPushOnEnterSource(indexed, 2, new GridCoord(2, 1), Direction.Left);
            AddPushOnEnterSource(arch, 2, new GridCoord(2, 1), Direction.Left);

            AssertAutoMoveEquals(indexed.QueryAutoMoveSources(), arch.QueryAutoMoveSources());
            AssertAutoMoveEquals(indexed.QueryAutoMove(), arch.QueryAutoMove());
            AssertPushOnEnterEquals(indexed.QueryPushOnEnter(), arch.QueryPushOnEnter());
        }

        [Test]
        public void ArchBackend_SpatialDirtyAndDelta_KeepDgIdentity()
        {
            GameWorld arch = GameWorld.CreateArchBacked(FallbackGameConfigProvider.Instance);
            GameEntity entity = AddBasicEntity(arch, 44, new GridCoord(3, 4));
            arch.FlushDelta();

            arch.SetComponent(entity, new PositionComponent(new GridCoord(4, 4)));
            Assert.IsTrue(arch.TryGetFirstBlockingAt(new GridCoord(4, 4), null, out BlockingSpatialQueryResult blocking));
            Assert.AreEqual(44, blocking.EntityId);

            WorldDelta delta = arch.FlushDelta();
            Assert.AreEqual(1, delta.ChangedEntities.Count);
            Assert.AreEqual(44, delta.ChangedEntities[0].EntityId);
            Assert.AreEqual(0, delta.RemovedEntityIds.Count);
        }

        private static GameEntity AddBasicEntity(GameWorld world, long entityId, GridCoord coord)
        {
            var entity = new GameEntity(entityId, 100, 100, DefaultWorldConfig.PlayerTarget);
            Assert.IsTrue(world.AddEntity(entity));
            world.SetComponent(entity, new PositionComponent(coord));
            world.SetComponent(entity, new ColliderComponent());
            world.SetComponent(entity, new BlockingComponent());
            return entity;
        }

        private static void AddAutoMoveSource(GameWorld world, long entityId, GridCoord coord, Direction direction)
        {
            GameEntity entity = AddBasicEntity(world, entityId, coord);
            world.SetComponent(entity, new DirectionComponent(direction));
            world.SetComponent(entity, new AutoMoveComponent(3));
        }

        private static void AddPushOnEnterSource(GameWorld world, long entityId, GridCoord coord, Direction direction)
        {
            GameEntity entity = AddBasicEntity(world, entityId, coord);
            world.SetComponent(entity, new DirectionComponent(direction));
            world.SetComponent(entity, new PushOnEnterComponent("mechanism_push", 2));
        }

        private static void AssertEntityEquals(GameEntity expected, GameEntity actual)
        {
            Assert.AreEqual(expected.EntityId, actual.EntityId);
            Assert.AreEqual(expected.ConfigId, actual.ConfigId);
            Assert.AreEqual(expected.ArchetypeId, actual.ArchetypeId);
            Assert.AreEqual(expected.EntityTarget, actual.EntityTarget);
        }

        private static void AssertAutoMoveEquals(IReadOnlyList<AutoMoveQueryResult> expected, IReadOnlyList<AutoMoveQueryResult> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].EntityId, actual[i].EntityId);
                Assert.AreEqual(expected[i].Position, actual[i].Position);
                Assert.AreEqual(expected[i].Direction, actual[i].Direction);
                Assert.AreEqual(expected[i].AutoMove.IntervalTicks, actual[i].AutoMove.IntervalTicks);
            }
        }

        private static void AssertPushOnEnterEquals(IReadOnlyList<PushOnEnterQueryResult> expected, IReadOnlyList<PushOnEnterQueryResult> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].EntityId, actual[i].EntityId);
                Assert.AreEqual(expected[i].Position, actual[i].Position);
                Assert.AreEqual(expected[i].Direction, actual[i].Direction);
                Assert.AreEqual(expected[i].PushOnEnter.OutputSpecId, actual[i].PushOnEnter.OutputSpecId);
                Assert.AreEqual(expected[i].PushOnEnter.OutputCostTicks, actual[i].PushOnEnter.OutputCostTicks);
            }
        }
    }
}
