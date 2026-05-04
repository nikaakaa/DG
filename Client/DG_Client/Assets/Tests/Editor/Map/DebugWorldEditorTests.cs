using System.Collections.Generic;
using DG.GameCore;
using DG.Map;
using Fantasy;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class DebugWorldEditorTests
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
                    AutoMove = true
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
        public void DebugEditor_SelectSlotAndRotateDirection()
        {
            ClientWorldDebugEditor editor = CreateEditor(out _);

            editor.SelectSlot(DebugWorldEditorSlot.Conveyor);
            editor.RotateDirection();

            Assert.AreEqual(DebugWorldEditorSlot.Conveyor, editor.CurrentSlot);
            Assert.AreEqual(Direction.Down, editor.BuildDirection);
        }

        [Test]
        public void DebugEditor_PickCell_FromScreenPosition()
        {
            ClientWorldDebugEditor editor = CreateEditor(out Camera camera);
            Vector3 screenPosition = camera.WorldToScreenPoint(new Vector3(2.25f, -1.25f, 0f));

            Assert.IsTrue(editor.TryPickCell(screenPosition, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(2, -2), coord);
        }

        [Test]
        public void DebugSubmitter_NoSessionCannotSubmit()
        {
            GameObject gameObject = new GameObject("Submitter");
            var submitter = gameObject.AddComponent<ClientMoveNetworkSubmitter>();

            Assert.IsFalse(submitter.CanSubmitDebugRequest(out string reason));
            Assert.AreEqual("session unavailable", reason);
        }

        private static ClientWorldRunner CreateRunner()
        {
            GameObject gameObject = new GameObject("Runner");
            var world = gameObject.AddComponent<WorldBootstrap>();
            ClientWorldRunner runner = gameObject.AddComponent<ClientWorldRunner>();
            runner.Initialize(world.EnsureWorld());
            return runner;
        }

        private static ClientWorldDebugEditor CreateEditor(out Camera camera)
        {
            GameObject cameraObject = new GameObject("Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            GameObject editorObject = new GameObject("Editor");
            return editorObject.AddComponent<ClientWorldDebugEditor>();
        }
    }
}
