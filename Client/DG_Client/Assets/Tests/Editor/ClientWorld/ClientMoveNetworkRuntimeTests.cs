using System.Collections.Generic;
using DG.GameCore;
using DG.Map;
using Fantasy;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class ClientMoveNetworkRuntimeTests
    {
        [TearDown]
        public void TearDown()
        {
            ClientMoveNetworkRuntime.Clear();
            foreach (GameObject gameObject in Object.FindObjectsOfType<GameObject>())
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ApplyWorldDelta_RemovesMirrorEntity()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 10 }, DefaultWorldConfig.BlockerSpawn(10, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(1, new List<G2C_WorldEntityState>(), new List<long> { 10 });

            Assert.IsTrue(applied);
            Assert.IsFalse(runner.Context.ClientMapWorld.TryGetEntity(10, out _));
        }

        [Test]
        public void ApplyWorldDelta_RemovesBeforeChangedRebuild()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 10 }, DefaultWorldConfig.BlockerSpawn(10, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            var states = new List<G2C_WorldEntityState>
            {
                new G2C_WorldEntityState
                {
                    EntityId = 10,
                    ConfigId = DefaultWorldConfig.BallConfigId,
                    ArchetypeId = DefaultWorldConfig.BallArchetypeId,
                    EntityTarget = DefaultWorldConfig.BallTarget,
                    X = 2,
                    Y = 3,
                    Direction = (int)Direction.Right,
                    HasCollider = true,
                    Bouncable = true,
                    AutoMove = true,
                    AutoMoveIntervalTicks = 1
                }
            };

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(2, states, new List<long> { 10 });

            Assert.IsTrue(applied);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetSnapshot(10, out EntitySnapshot snapshot));
            Assert.AreEqual(DefaultWorldConfig.BallConfigId, snapshot.ConfigId);
            Assert.AreEqual(2, snapshot.X);
            Assert.AreEqual(3, snapshot.Y);
        }

        [Test]
        public void ApplyWorldDelta_UpdatesPushedEntityMirrorCoord()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 2 }, DefaultWorldConfig.PushableBlockerSpawn(2, new GridCoord(1, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            var states = new List<G2C_WorldEntityState>
            {
                new G2C_WorldEntityState
                {
                    EntityId = 1,
                    ConfigId = DefaultWorldConfig.PlayerConfigId,
                    ArchetypeId = DefaultWorldConfig.PlayerArchetypeId,
                    EntityTarget = DefaultWorldConfig.PlayerTarget,
                    X = 1,
                    Y = 0,
                    Direction = (int)Direction.None,
                    HasCollider = true,
                    Blocking = true,
                    PlayerControlled = true
                },
                new G2C_WorldEntityState
                {
                    EntityId = 2,
                    ConfigId = DefaultWorldConfig.PushableBlockerConfigId,
                    ArchetypeId = DefaultWorldConfig.PushableBlockerArchetypeId,
                    EntityTarget = DefaultWorldConfig.BlockerTarget,
                    X = 2,
                    Y = 0,
                    Direction = (int)Direction.None,
                    HasCollider = true,
                    Blocking = true,
                    Pushable = true
                }
            };

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(3, states, new List<long>());

            Assert.IsTrue(applied);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int playerCoord));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(2, out Vector2Int pushableCoord));
            Assert.AreEqual(new Vector2Int(1, 0), playerCoord);
            Assert.AreEqual(new Vector2Int(2, 0), pushableCoord);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetCoreEntity(2, out GameEntity pushableEntity));
            Assert.IsTrue(runner.Context.ClientMapWorld.CoreWorld.HasComponent<PushableComponent>(pushableEntity));
        }

        [Test]
        public void ApplyWorldDelta_DoesNotPredictPendingPushChain()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 2 }, DefaultWorldConfig.PushableBlockerSpawn(2, new GridCoord(1, 0)));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 3 }, DefaultWorldConfig.PushableBlockerSpawn(3, new GridCoord(2, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            var states = new List<G2C_WorldEntityState>
            {
                new G2C_WorldEntityState
                {
                    EntityId = 3,
                    ConfigId = DefaultWorldConfig.PushableBlockerConfigId,
                    ArchetypeId = DefaultWorldConfig.PushableBlockerArchetypeId,
                    EntityTarget = DefaultWorldConfig.BlockerTarget,
                    X = 3,
                    Y = 0,
                    Direction = (int)Direction.None,
                    HasCollider = true,
                    Blocking = true,
                    Pushable = true
                }
            };

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(4, states, new List<long>());

            Assert.IsTrue(applied);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int playerCoord));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(2, out Vector2Int firstBoxCoord));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(3, out Vector2Int secondBoxCoord));
            Assert.AreEqual(new Vector2Int(0, 0), playerCoord);
            Assert.AreEqual(new Vector2Int(1, 0), firstBoxCoord);
            Assert.AreEqual(new Vector2Int(3, 0), secondBoxCoord);
        }

        [Test]
        public void DebugSubmitter_NoSessionCannotSubmit()
        {
            GameObject gameObject = new GameObject("Submitter");
            var submitter = gameObject.AddComponent<ClientMoveNetworkSubmitter>();

            Assert.IsFalse(submitter.CanSubmitDebugRequest(out string reason));
            Assert.AreEqual("session unavailable", reason);
        }

        [Test]
        public void ClientWorldDebugEditor_SelectsPortAndRotates()
        {
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();

            Assert.AreEqual(DebugWorldEditorSlot.Blocker, editor.CurrentSlot);
            Assert.AreEqual(Direction.Right, editor.BuildDirection);

            editor.SelectSlot(DebugWorldEditorSlot.PortConnector);
            editor.RotateDirection();

            Assert.AreEqual(DebugWorldEditorSlot.PortConnector, editor.CurrentSlot);
            Assert.AreEqual(Direction.Down, editor.BuildDirection);
        }

        [Test]
        public void ClientWorldDebugEditor_SelectSlotKeepsSelectedEntity()
        {
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();

            editor.SelectEntity(42);
            editor.SelectSlot(DebugWorldEditorSlot.Select);

            Assert.AreEqual(DebugWorldEditorSlot.Select, editor.CurrentSlot);
            Assert.AreEqual(42, editor.SelectedEntityId);

            editor.SelectSlot(DebugWorldEditorSlot.Delete);

            Assert.AreEqual(0, editor.SelectedEntityId);
        }

        [Test]
        public void DGDebugPanelController_SelectToolKeepsSelectedEntity()
        {
            GameObject gameObject = new GameObject("DebugPanel");
            var panel = gameObject.AddComponent<DGDebugPanelController>();

            panel.SelectEntity(42);
            panel.SelectTool(DGDebugPanelTool.Select);

            Assert.AreEqual(DGDebugPanelTool.Select, panel.CurrentTool);
            Assert.AreEqual(42, panel.SelectedEntityId);

            panel.SelectTool(DGDebugPanelTool.Delete);

            Assert.AreEqual(DGDebugPanelTool.Delete, panel.CurrentTool);
            Assert.AreEqual(0, panel.SelectedEntityId);
        }

        [Test]
        public void ClientWorldDebugEditor_ConfiguresRuntimeEffectControls()
        {
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();

            editor.SelectEntity(42);
            editor.SetRuntimeAutoMoveIntervalTicks(0);
            editor.SetRuntimeExpireAfterTicks(-5);

            Assert.AreEqual(42, editor.SelectedEntityId);
            Assert.AreEqual(1, editor.RuntimeAutoMoveIntervalTicks);
            Assert.AreEqual(0, editor.RuntimeExpireAfterTicks);

            editor.SetRuntimeAutoMoveIntervalTicks(3);
            editor.SetRuntimeExpireAfterTicks(5);

            Assert.AreEqual(3, editor.RuntimeAutoMoveIntervalTicks);
            Assert.AreEqual(5, editor.RuntimeExpireAfterTicks);
        }

        private static ClientWorldRunner CreateRunner()
        {
            GameObject gameObject = new GameObject("Runner");
            var world = gameObject.AddComponent<WorldBootstrap>();
            ClientWorldRunner runner = gameObject.AddComponent<ClientWorldRunner>();
            runner.Initialize(world.EnsureWorld());
            return runner;
        }

    }
}
