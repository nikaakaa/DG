using System.Collections.Generic;
using System.IO;
using DG.GameCore;
using DG.Map;
using Fantasy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DG.EditorTests
{
    public sealed class ClientMoveNetworkRuntimeTests
    {
        [TearDown]
        public void TearDown()
        {
            ClientMoveNetworkRuntime.Clear();
            DeleteLayoutIfExists("palette_a");
            DeleteLayoutIfExists("palette_b");
            DeleteLayoutIfExists("palette_load");
            DeleteLayoutIfExists("palette_runtime_effect");
            DeleteLayoutIfExists("palette_runtime_cache");
            DeleteLayoutIfExists("palette_runtime_multi_port");
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
        public void ApplyWorldDelta_CreatesAnimationForEveryServerMovedConnectedBodyMember()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 20 }, DefaultWorldConfig.PortConnectorBlockerSpawn(20, new GridCoord(0, 0), Direction.Right));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 21 }, DefaultWorldConfig.PortConnectorBlockerSpawn(21, new GridCoord(1, 0), Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);

            var states = new List<G2C_WorldEntityState>
            {
                new G2C_WorldEntityState
                {
                    EntityId = 20,
                    ConfigId = DefaultWorldConfig.PortConnectorBlockerConfigId,
                    ArchetypeId = DefaultWorldConfig.PortConnectorBlockerArchetypeId,
                    EntityTarget = DefaultWorldConfig.BlockerTarget,
                    X = 1,
                    Y = 0,
                    Direction = (int)Direction.Right,
                    HasCollider = true,
                    Blocking = true,
                    Pushable = true,
                    PortLocalPorts = (int)DirectionMask.Right,
                    CanMove = true,
                    CanBePushed = true
                },
                new G2C_WorldEntityState
                {
                    EntityId = 21,
                    ConfigId = DefaultWorldConfig.PortConnectorBlockerConfigId,
                    ArchetypeId = DefaultWorldConfig.PortConnectorBlockerArchetypeId,
                    EntityTarget = DefaultWorldConfig.BlockerTarget,
                    X = 2,
                    Y = 0,
                    Direction = (int)Direction.Right,
                    HasCollider = true,
                    Blocking = true,
                    Pushable = true,
                    PortLocalPorts = (int)DirectionMask.Right,
                    CanMove = true,
                    CanBePushed = true
                }
            };

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(5, states, new List<long>(), new[]
            {
                new G2C_WorldDeltaAnimationMetadata
                {
                    EntityId = 20,
                    ServerTick = 5,
                    MotionKind = (int)WorldDeltaMotionKind.MechanismPush,
                    StyleKey = "mechanism_push",
                    Direction = (int)Direction.Right
                },
                new G2C_WorldDeltaAnimationMetadata
                {
                    EntityId = 21,
                    ServerTick = 5,
                    MotionKind = (int)WorldDeltaMotionKind.MechanismPush,
                    StyleKey = "mechanism_push",
                    Direction = (int)Direction.Right
                }
            });

            Assert.IsTrue(applied);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(20, out Vector2Int firstCoord));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(21, out Vector2Int secondCoord));
            Assert.AreEqual(new Vector2Int(1, 0), firstCoord);
            Assert.AreEqual(new Vector2Int(2, 0), secondCoord);
            var movedIds = new List<long>();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, animationEvent.MotionKind);
                movedIds.Add(animationEvent.EntityId);
            }

            CollectionAssert.AreEquivalent(new[] { 20L, 21L }, movedIds);
        }

        [Test]
        public void ApplyWorldDelta_DoesNotInferMissingConnectedBodyMembers()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 22 }, DefaultWorldConfig.PortConnectorBlockerSpawn(22, new GridCoord(0, 0), Direction.Right));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 23 }, DefaultWorldConfig.PortConnectorBlockerSpawn(23, new GridCoord(1, 0), Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(6, new[]
            {
                new G2C_WorldEntityState
                {
                    EntityId = 22,
                    ConfigId = DefaultWorldConfig.PortConnectorBlockerConfigId,
                    ArchetypeId = DefaultWorldConfig.PortConnectorBlockerArchetypeId,
                    EntityTarget = DefaultWorldConfig.BlockerTarget,
                    X = 1,
                    Y = 0,
                    Direction = (int)Direction.Right,
                    HasCollider = true,
                    Blocking = true,
                    Pushable = true,
                    PortLocalPorts = (int)DirectionMask.Right,
                    CanMove = true,
                    CanBePushed = true
                }
            }, new List<long>());

            Assert.IsTrue(applied);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(22, out Vector2Int firstCoord));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(23, out Vector2Int secondCoord));
            Assert.AreEqual(new Vector2Int(1, 0), firstCoord);
            Assert.AreEqual(new Vector2Int(1, 0), secondCoord);
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(22, animationEvent.EntityId);
            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
        }

        [Test]
        public void PendingInput_RecordDoesNotModifyMirrorCoord()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.RecordPendingInput(100, 1, 7, Direction.Right, 22);

            Assert.AreEqual(1, ClientMoveNetworkRuntime.PendingInputCount);
            Assert.IsTrue(ClientMoveNetworkRuntime.TryGetPendingInput(100, out PendingPlayerInput pending));
            Assert.AreEqual(Direction.Right, pending.Direction);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(0, 0), coord);
        }

        [Test]
        public void InputIntentSource_CreatesMoveIntentFromInputSystemVector()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            ClientMoveNetworkRuntime.ApplyJoinedPlayer(1, 0, 0);
            GameObject gameObject = new GameObject("InputIntentSource");
            var source = gameObject.AddComponent<ClientInputIntentSource>();

            ClientDeclaredInputIntent intent = source.CreateMoveIntent(Vector2.right, 100, 7, 22);

            Assert.AreEqual(ClientInputKind.Move, intent.InputKind);
            Assert.AreEqual(ClientInputSourceKind.Player, intent.SourceKind);
            Assert.AreEqual(Direction.Right, intent.Direction);
            Assert.AreEqual(ClientRhythmJudge.None, intent.RhythmJudge);
            Assert.AreEqual(1, intent.ActorEntityId);
        }

        [Test]
        public void PendingIntent_RecordDoesNotModifyMirrorCoord()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.RecordPendingIntent(103, 1, 7, Direction.Right, 22);

            Assert.AreEqual(1, ClientMoveNetworkRuntime.PendingInputCount);
            Assert.IsTrue(ClientMoveNetworkRuntime.TryGetPendingInput(103, out PendingPlayerInput pending));
            Assert.AreEqual(ClientInputKind.Move, pending.InputKind);
            Assert.AreEqual(ClientInputSourceKind.Player, pending.SourceKind);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(0, 0), coord);
        }

        [Test]
        public void PendingInput_RejectedStatusClearsWithoutMirrorChange()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            ClientMoveNetworkRuntime.RecordPendingInput(101, 1, 7, Direction.Right, 22);

            ClientMoveNetworkRuntime.ResolvePendingInput(101, (int)ClientPlayerInputStatus.Rejected);

            Assert.AreEqual(0, ClientMoveNetworkRuntime.PendingInputCount);
            Assert.IsFalse(ClientMoveNetworkRuntime.TryGetPendingInput(101, out _));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(0, 0), coord);
        }

        [Test]
        public void PendingInput_ReplacedAndExpiredStatusClearsWithoutMirrorChange()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            ClientMoveNetworkRuntime.RecordPendingInput(104, 1, 7, Direction.Right, 22);
            ClientMoveNetworkRuntime.RecordPendingInput(105, 1, 8, Direction.Left, 23);

            ClientMoveNetworkRuntime.ResolvePendingInput(104, (int)ClientPlayerInputStatus.Replaced);
            ClientMoveNetworkRuntime.ResolvePendingInput(105, (int)ClientPlayerInputStatus.Expired);

            Assert.AreEqual(0, ClientMoveNetworkRuntime.PendingInputCount);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(0, 0), coord);
        }

        [Test]
        public void ApplyWorldDelta_ClearsResolvedPendingInputByServerTick()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            ClientMoveNetworkRuntime.RecordPendingInput(102, 1, 4, Direction.Right, 22);

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(4, new[]
            {
                new G2C_WorldEntityState
                {
                    EntityId = 1,
                    ConfigId = DefaultWorldConfig.PlayerConfigId,
                    ArchetypeId = DefaultWorldConfig.PlayerArchetypeId,
                    EntityTarget = DefaultWorldConfig.PlayerTarget,
                    X = 1,
                    Y = 0,
                    Direction = (int)Direction.Right,
                    HasCollider = true,
                    Blocking = true,
                    PlayerControlled = true
                }
            }, new List<long>());

            Assert.IsTrue(applied);
            Assert.AreEqual(0, ClientMoveNetworkRuntime.PendingInputCount);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(1, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(1, 0), coord);
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

            Assert.AreEqual(DebugWorldEditorSlot.Select, editor.CurrentSlot);
            Assert.AreEqual(DebugWorldEditorPaletteKind.None, editor.PaletteKind);
            Assert.AreEqual(Direction.Right, editor.BuildDirection);

            editor.SelectSlot(DebugWorldEditorSlot.PortConnector);
            editor.RotateDirection();

            Assert.AreEqual(DebugWorldEditorSlot.PortConnector, editor.CurrentSlot);
            Assert.AreEqual(DebugWorldEditorPaletteKind.BaseEntity, editor.PaletteKind);
            Assert.AreEqual(Direction.Down, editor.BuildDirection);
        }

        [Test]
        public void ClientWorldDebugEditor_SelectSlotKeepsSelectedEntity()
        {
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();

            editor.SelectEntity(42);
            editor.SelectSlot(DebugWorldEditorSlot.Blocker);

            Assert.AreEqual(DebugWorldEditorSlot.Blocker, editor.CurrentSlot);
            Assert.AreEqual(42, editor.SelectedEntityId);

            editor.SelectSlot(DebugWorldEditorSlot.Delete);

            Assert.AreEqual(42, editor.SelectedEntityId);
        }

        [Test]
        public void ClientWorldDebugEditor_SelectModeKeepsSelection()
        {
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();

            editor.SelectEntity(42);
            editor.SelectMode(DebugWorldEditorMode.Layout);

            Assert.AreEqual(DebugWorldEditorMode.Layout, editor.Mode);
            Assert.AreEqual(42, editor.SelectedEntityId);
        }

        [Test]
        public void ClientWorldDebugEditor_DebugUiHitTestBlocksMapInput()
        {
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();

            editor.SelectMode(DebugWorldEditorMode.Layout);

            Assert.IsTrue(editor.IsPointerOverDebugUi(new Vector3(32, Screen.height - 24, 0)));
            Assert.IsTrue(editor.IsPointerOverDebugUi(new Vector3(32, Screen.height - 180, 0)));
            Assert.IsFalse(editor.IsPointerOverDebugUi(new Vector3(540, Screen.height - 24, 0)));
        }

        [Test]
        public void ClientWorldDebugEditor_ContextMenuHitTestBlocksMapInput()
        {
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetField("contextMenuOpen", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, true);
            typeof(ClientWorldDebugEditor)
                .GetField("contextMenuRect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, new Rect(100, 120, 260, 360));

            Assert.IsTrue(editor.IsPointerOverDebugUi(new Vector3(120, Screen.height - 140, 0)));
            Assert.IsFalse(editor.IsPointerOverDebugUi(new Vector3(560, Screen.height - 140, 0)));
        }

        [Test]
        public void ClientWorldDebugEditor_BasePlacementKeepsGhostPalette()
        {
            ClientWorldRunner runner = CreateRunner();
            GameObject submitterObject = new GameObject("Submitter");
            var submitter = submitterObject.AddComponent<ClientMoveNetworkSubmitter>();
            typeof(ClientMoveNetworkSubmitter)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(submitter, runner);
            typeof(ClientMoveNetworkSubmitter)
                .GetField("serverAuthoritative", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(submitter, false);
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetField("networkSubmitter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, submitter);
            editor.SelectSlot(DebugWorldEditorSlot.PortConnector);

            typeof(ClientWorldDebugEditor)
                .GetMethod("ExecuteCurrentTool", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(editor, null);

            Assert.AreEqual(DebugWorldEditorPaletteKind.BaseEntity, editor.PaletteKind);
            Assert.AreEqual(DebugWorldEditorSlot.PortConnector, editor.CurrentSlot);
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
        public void DGDebugPanelController_SubmitSpawnRequestsAppliesStructureRuntimeEffects()
        {
            ClientWorldRunner runner = CreateRunner();
            GameObject submitterObject = new GameObject("Submitter");
            var submitter = submitterObject.AddComponent<ClientMoveNetworkSubmitter>();
            typeof(ClientMoveNetworkSubmitter)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(submitter, runner);
            typeof(ClientMoveNetworkSubmitter)
                .GetField("serverAuthoritative", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(submitter, false);
            GameObject panelObject = new GameObject("DebugPanel");
            var panel = panelObject.AddComponent<DGDebugPanelController>();
            typeof(DGDebugPanelController)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(panel, runner);
            typeof(DGDebugPanelController)
                .GetField("networkSubmitter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(panel, submitter);
            var request = new DebugStructureSpawnRequest("runtime_pushable", DefaultWorldConfig.BlockerConfigId, 2, 0, Direction.None, 0, 1, DirectionMask.None, false, false, false, true, DirectionMask.None, false);

            typeof(DGDebugPanelController)
                .GetMethod("SubmitSpawnRequests", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(panel, new object[] { new[] { request } });

            EntitySnapshot spawned = default;
            foreach (EntitySnapshot snapshot in runner.Context.ClientMapWorld.CreateSnapshot())
            {
                if (snapshot.X == 2 && snapshot.Y == 0)
                {
                    spawned = snapshot;
                    break;
                }
            }

            Assert.AreNotEqual(0, spawned.EntityId);
            Assert.IsTrue(runner.Context.ClientMapWorld.CoreWorld.TryGetEntity(spawned.EntityId, out GameEntity entity));
            Assert.IsTrue(runner.Context.ClientMapWorld.CoreWorld.HasComponent<PushableComponent>(entity));
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

        [Test]
        public void ClientWorldDebugEditor_SanitizesLayoutNames()
        {
            Assert.AreEqual("two_words", ClientWorldDebugEditor.SanitizeLayoutName(" two words "));
            Assert.AreEqual(string.Empty, ClientWorldDebugEditor.SanitizeLayoutName("   "));
            Assert.IsFalse(ClientWorldDebugEditor.SanitizeLayoutName("bad:name").Contains(":"));
        }

        [Test]
        public void ClientWorldDebugEditor_SaveNamedLayoutsRefreshesPaletteList()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(10, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 5, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, runner);

            editor.SelectEntity(10);
            Assert.IsTrue(editor.AddSelectedEntityToSelection());
            Assert.IsTrue(editor.SaveSelectionAs("palette_a"));
            Assert.IsTrue(editor.SaveSelectionAs("palette_b"));

            Assert.GreaterOrEqual(editor.SavedLayoutCount, 2);
            Assert.IsTrue(File.Exists(DebugLayoutPaths.NamedFilePath("palette_a")));
            Assert.IsTrue(File.Exists(DebugLayoutPaths.NamedFilePath("palette_b")));
        }

        [Test]
        public void ClientWorldDebugEditor_SaveSelectionUsesWorldRuntimeEffects()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(14, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 6, 7, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1));
            AddRuntimeEffect(runner.Context.ClientMapWorld.CoreWorld, 14, EffectKind.PortConnector, "runtime_port", DirectionMask.Down, 1);
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, runner);

            editor.SelectEntity(14);
            Assert.IsTrue(editor.AddSelectedEntityToSelection());
            Assert.IsTrue(editor.SaveSelectionAs("palette_runtime_effect"));
            Assert.IsTrue(DebugStructureBlockStorage.TryLoad(DebugLayoutPaths.NamedFilePath("palette_runtime_effect"), ClientGameConfigProviderFactory.Create(), out DebugStructureBlockDocument loaded, out IReadOnlyList<string> errors), string.Join("|", errors));

            Assert.AreEqual((int)DirectionMask.Down, loaded.Entries[0].RuntimePortLocalPorts);
        }

        [Test]
        public void ClientWorldDebugEditor_SaveSelectionUsesRuntimeEffectCallbackCache()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(16, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 8, 9, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1));
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, runner);
            var runtimeEffectRecords = (Dictionary<string, DebugStructureRuntimeEffectRecord>)typeof(ClientWorldDebugEditor)
                .GetField("runtimeEffectRecords", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(editor);
            runtimeEffectRecords["16:" + (int)RuntimeEffectKind.TemporaryPushable] = new DebugStructureRuntimeEffectRecord(16, RuntimeEffectKind.TemporaryPushable, 1, DirectionMask.None);

            editor.SelectEntity(16);
            Assert.IsTrue(editor.AddSelectedEntityToSelection());
            Assert.IsTrue(editor.SaveSelectionAs("palette_runtime_cache"));
            Assert.IsTrue(DebugStructureBlockStorage.TryLoad(DebugLayoutPaths.NamedFilePath("palette_runtime_cache"), ClientGameConfigProviderFactory.Create(), out DebugStructureBlockDocument loaded, out IReadOnlyList<string> errors), string.Join("|", errors));

            Assert.IsTrue(loaded.Entries[0].RuntimePushable);
        }

        [Test]
        public void ClientWorldDebugEditor_SaveSelectionKeepsMultipleRuntimePortDirections()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(17, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 9, 10, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1));
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, runner);
            var runtimeEffectRecords = (Dictionary<string, DebugStructureRuntimeEffectRecord>)typeof(ClientWorldDebugEditor)
                .GetField("runtimeEffectRecords", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(editor);
            runtimeEffectRecords["17:" + (int)RuntimeEffectKind.TemporaryPort + ":" + (int)DirectionMask.Left] = new DebugStructureRuntimeEffectRecord(17, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Left);
            runtimeEffectRecords["17:" + (int)RuntimeEffectKind.TemporaryPort + ":" + (int)DirectionMask.Down] = new DebugStructureRuntimeEffectRecord(17, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Down);

            editor.SelectEntity(17);
            Assert.IsTrue(editor.AddSelectedEntityToSelection());
            Assert.IsTrue(editor.SaveSelectionAs("palette_runtime_multi_port"));
            Assert.IsTrue(DebugStructureBlockStorage.TryLoad(DebugLayoutPaths.NamedFilePath("palette_runtime_multi_port"), ClientGameConfigProviderFactory.Create(), out DebugStructureBlockDocument loaded, out IReadOnlyList<string> errors), string.Join("|", errors));

            Assert.AreEqual((int)(DirectionMask.Left | DirectionMask.Down), loaded.Entries[0].RuntimePortLocalPorts);
        }

        [Test]
        public void ClientWorldDebugEditor_SplitsRuntimePortReplayByDirection()
        {
            var masks = (IReadOnlyList<DirectionMask>)typeof(ClientWorldDebugEditor)
                .GetMethod("RuntimePortMasks", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { DirectionMask.Left | DirectionMask.Right | DirectionMask.Down });

            Assert.AreEqual(3, masks.Count);
            Assert.AreEqual(DirectionMask.Left, masks[0]);
            Assert.AreEqual(DirectionMask.Right, masks[1]);
            Assert.AreEqual(DirectionMask.Down, masks[2]);
        }

        [Test]
        public void ClientWorldDebugEditor_RemovesOnlyReturnedRuntimeEffectCacheEntry()
        {
            var editor = new GameObject("DebugEditor").AddComponent<ClientWorldDebugEditor>();
            var runtimeEffectRecords = (Dictionary<string, DebugStructureRuntimeEffectRecord>)typeof(ClientWorldDebugEditor)
                .GetField("runtimeEffectRecords", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(editor);
            var runtimeEffectIds = (Dictionary<string, long>)typeof(ClientWorldDebugEditor)
                .GetField("runtimeEffectIds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(editor);
            string leftKey = "18:" + (int)RuntimeEffectKind.TemporaryPort + ":" + (int)DirectionMask.Left;
            string rightKey = "18:" + (int)RuntimeEffectKind.TemporaryPort + ":" + (int)DirectionMask.Right;
            runtimeEffectRecords[leftKey] = new DebugStructureRuntimeEffectRecord(18, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Left);
            runtimeEffectRecords[rightKey] = new DebugStructureRuntimeEffectRecord(18, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Right);
            runtimeEffectIds[leftKey] = 101;
            runtimeEffectIds[rightKey] = 102;

            typeof(ClientWorldDebugEditor)
                .GetMethod("RemoveRuntimeEffectCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(editor, new object[] { 18L, RuntimeEffectKind.TemporaryPort, 101L });

            Assert.IsFalse(runtimeEffectRecords.ContainsKey(leftKey));
            Assert.IsFalse(runtimeEffectIds.ContainsKey(leftKey));
            Assert.IsTrue(runtimeEffectRecords.ContainsKey(rightKey));
            Assert.IsTrue(runtimeEffectIds.ContainsKey(rightKey));
        }

        [Test]
        public void ClientWorldDebugEditor_LoadSavedLayoutSelectsSavedPalette()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(10, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 5, Direction.Right, true, true, false, false, 1, false, true, DirectionMask.Left | DirectionMask.Right, false, true, true, 1));
            var selection = new DebugLayoutSelectionSet();
            DebugLayoutTooling.SelectRectangle(selection, runner.Context.ClientMapWorld, new Vector2Int(4, 5), new Vector2Int(4, 5), false);
            string path = DebugLayoutPaths.NamedFilePath("palette_load");
            Assert.IsTrue(DebugLayoutTooling.TrySaveSelection(selection, "palette_load", path, out string reason), reason);
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();

            Assert.IsTrue(editor.LoadStructureBlock(path));

            Assert.AreEqual(DebugWorldEditorPaletteKind.SavedStructure, editor.PaletteKind);
            Assert.AreEqual(DebugLayoutToolState.StructureGhostPlacement, editor.ToolState);
            Assert.AreEqual(path, editor.SelectedSavedLayoutPath);
        }

        [Test]
        public void ClientWorldDebugEditor_RuntimeEffectTargetsPreferSelection()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(10, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 5, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(11, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 5, 5, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, runner);

            editor.SelectEntity(10);
            Assert.AreEqual(1, editor.RuntimeEffectTargets().Count);
            Assert.AreEqual(10, editor.RuntimeEffectTargets()[0]);

            Assert.IsTrue(editor.AddSelectedEntityToSelection());
            editor.SelectEntity(11);
            Assert.IsTrue(editor.AddSelectedEntityToSelection());

            IReadOnlyList<long> targets = editor.RuntimeEffectTargets();

            Assert.AreEqual(2, targets.Count);
            Assert.Contains(10, new List<long>(targets));
            Assert.Contains(11, new List<long>(targets));
        }

        [Test]
        public void ClientWorldDebugEditor_DeleteSelectionRemovesAllSelectedEntities()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(10, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 5, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(11, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 5, 5, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            GameObject submitterObject = new GameObject("Submitter");
            var submitter = submitterObject.AddComponent<ClientMoveNetworkSubmitter>();
            typeof(ClientMoveNetworkSubmitter)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(submitter, runner);
            typeof(ClientMoveNetworkSubmitter)
                .GetField("serverAuthoritative", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(submitter, false);
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, runner);
            typeof(ClientWorldDebugEditor)
                .GetField("networkSubmitter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, submitter);

            editor.SelectEntity(10);
            Assert.IsTrue(editor.AddSelectedEntityToSelection());
            editor.SelectEntity(11);
            Assert.IsTrue(editor.AddSelectedEntityToSelection());

            editor.DeleteSelectionOrEntity(0);

            Assert.AreEqual(0, editor.SelectionCount);
            Assert.IsFalse(runner.Context.ClientMapWorld.TryGetSnapshot(10, out _));
            Assert.IsFalse(runner.Context.ClientMapWorld.TryGetSnapshot(11, out _));
        }

        [Test]
        public void DebugLayoutSelection_ExportsStructureBlockWithStaticBaseOnly()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(10, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 5, Direction.Right, true, true, false, false, 1, false, true, DirectionMask.Left | DirectionMask.Right, false, true, true, 1));
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(11, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 5, 5, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            var selection = new DebugLayoutSelectionSet();

            Assert.AreEqual(2, DebugLayoutTooling.SelectRectangle(selection, runner.Context.ClientMapWorld, new Vector2Int(4, 5), new Vector2Int(5, 5), false));
            DebugStructureBlockDocument document = selection.ToStructureBlock("two");

            Assert.AreEqual(2, document.Entries.Count);
            Assert.AreEqual(4, document.AnchorX);
            Assert.AreEqual(5, document.AnchorY);
            Assert.AreEqual(0, document.Entries[0].OffsetX);
            Assert.AreEqual((int)(DirectionMask.Left | DirectionMask.Right), document.Entries[0].PortLocalPorts);
            Assert.IsFalse(document.Entries[0].RuntimeBlocking);
            Assert.IsFalse(document.Entries[0].RuntimePushable);
            Assert.AreEqual(0, document.Entries[0].RuntimePortLocalPorts);
        }

        [Test]
        public void DebugStructureBlockStorage_RoundTripsRuntimeComponents()
        {
            var document = new DebugStructureBlockDocument
            {
                Name = "runtime",
                Entries =
                {
                    new DebugStructureBlockEntry
                    {
                        Alias = "a",
                        ConfigId = DefaultWorldConfig.BlockerConfigId,
                        Direction = "Right",
                        RuntimeBlocking = true,
                        RuntimeAutoMove = true,
                        RuntimePushable = true,
                        RotatePivot = true,
                        RuntimePortLocalPorts = (int)(DirectionMask.Up | DirectionMask.Right),
                        RuntimeImmobile = true,
                        AutoMoveIntervalTicks = 3
                    }
                }
            };

            string json = DebugStructureBlockStorage.ToJson(document);
            bool parsed = DebugStructureBlockStorage.TryParse(json, ClientGameConfigProviderFactory.Create(), out DebugStructureBlockDocument parsedDocument, out IReadOnlyList<string> errors);
            IReadOnlyList<DebugStructureSpawnRequest> requests = DebugStructureBlockStorage.CreateSpawnRequests(parsedDocument, 7, 8);

            Assert.IsTrue(parsed, string.Join("|", errors));
            Assert.AreEqual(1, requests.Count);
            Assert.IsTrue(requests[0].RuntimeBlocking);
            Assert.IsTrue(requests[0].RuntimeAutoMove);
            Assert.IsTrue(requests[0].RuntimePushable);
            Assert.IsTrue(requests[0].RotatePivot);
            Assert.AreEqual(DirectionMask.Up | DirectionMask.Right, requests[0].RuntimePortLocalPorts);
            Assert.IsTrue(requests[0].RuntimeImmobile);
            Assert.AreEqual(3, requests[0].AutoMoveIntervalTicks);
        }

        [Test]
        public void DebugStructureBlockStorage_SerializesRuntimeEffectsFromWorldSources()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(70, new GridCoord(1, 2))));
            Assert.IsTrue(world.TryGetEntity(70, out GameEntity entity));
            var context = new ActionContext(70, 70, "runtime_port", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, 70, 0, WorldTag.SourceDebug), 70, 70, 70, 70, new ActionTarget(70, null, Direction.None), Direction.None, 0, 0, 1, 0, 70);
            var spec = new EffectSpec("runtime_port", EffectKind.PortConnector, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.Up, true, true, WorldTag.None);
            var application = new EffectApplication(context, spec, ActionTargetData.Self(70, new GridCoord(1, 2), Direction.None), 0, "runtime-port");
            new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 70, application, 0) });

            DebugStructureBlockDocument document = DebugStructureBlockStorage.FromSnapshots("runtime", world.CreateSnapshot(), world.RuntimeEffects, world.ServerTick);

            Assert.AreEqual(1, document.Entries.Count);
            Assert.AreEqual((int)DirectionMask.Up, document.Entries[0].RuntimePortLocalPorts);
            Assert.AreEqual((int)DirectionMask.Up, document.Entries[0].PortLocalPorts);
        }

        [Test]
        public void DebugStructureBlockStorage_DoesNotTreatStaticFinalComponentsAsRuntimeEffects()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(71, new GridCoord(3, 4), Direction.Right)));

            DebugStructureBlockDocument document = DebugStructureBlockStorage.FromSnapshots("static", world.CreateSnapshot(), world.RuntimeEffects, world.ServerTick);

            Assert.AreEqual(1, document.Entries.Count);
            Assert.AreEqual((int)(DirectionMask.Left | DirectionMask.Right), document.Entries[0].PortLocalPorts);
            Assert.AreEqual(0, document.Entries[0].RuntimePortLocalPorts);
            Assert.IsFalse(document.Entries[0].RuntimeBlocking);
            Assert.IsFalse(document.Entries[0].RuntimePushable);
        }

        [Test]
        public void DebugStructureBlockStorage_SerializesRotatePivotFromSnapshot()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(71, new GridCoord(2, 3))));
            Assert.IsTrue(world.TryGetEntity(71, out GameEntity entity));
            world.SetComponent(entity, new RotatePivotComponent());

            DebugStructureBlockDocument document = DebugStructureBlockStorage.FromSnapshots("pivot", world.CreateSnapshot());
            IReadOnlyList<DebugStructureSpawnRequest> requests = DebugStructureBlockStorage.CreateSpawnRequests(document, 5, 6);

            Assert.AreEqual(1, document.Entries.Count);
            Assert.IsTrue(document.Entries[0].RotatePivot);
            Assert.AreEqual(1, requests.Count);
            Assert.IsTrue(requests[0].RotatePivot);
        }

        [Test]
        public void ClientMoveNetworkRuntime_AppliesRotatePivotFromWorldState()
        {
            ClientWorldRunner runner = CreateRunner();
            ClientMoveNetworkRuntime.SetRunner(runner);
            var state = new G2C_WorldEntityState
            {
                EntityId = 81,
                ConfigId = DefaultWorldConfig.PushableBlockerConfigId,
                ArchetypeId = DefaultWorldConfig.PushableBlockerArchetypeId,
                EntityTarget = DefaultWorldConfig.BlockerTarget,
                X = 3,
                Y = 4,
                HasCollider = true,
                Blocking = true,
                Pushable = true,
                RotatePivot = true,
                CanMove = true,
                CanBePushed = true,
                AutoMoveIntervalTicks = 1
            };

            Assert.IsTrue(ClientMoveNetworkRuntime.ApplyWorldSnapshot(12, new[] { state }));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetSnapshot(81, out EntitySnapshot snapshot));
            Assert.IsTrue(snapshot.RotatePivot);
            Assert.IsTrue(runner.Context.ClientMapWorld.CoreWorld.TryGetEntity(81, out GameEntity entity));
            Assert.IsTrue(runner.Context.ClientMapWorld.CoreWorld.HasComponent<RotatePivotComponent>(entity));
        }

        [Test]
        public void DebugLayoutSelection_CreateMoveRequestsDoesNotModifyWorld()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(10, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 5, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            var selection = new DebugLayoutSelectionSet();
            DebugLayoutTooling.SelectRectangle(selection, runner.Context.ClientMapWorld, new Vector2Int(4, 5), new Vector2Int(4, 5), false);

            var requests = selection.CreateMoveRequests(new Vector2Int(9, 9), runner.Context.ClientMapWorld, out var skipped);

            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(0, skipped.Count);
            Assert.AreEqual(new Vector2Int(9, 9), requests[0].TargetCoord);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(10, out Vector2Int original));
            Assert.AreEqual(new Vector2Int(4, 5), original);
        }

        [Test]
        public void DebugLayoutSelection_CopyPreservesRelativeLayoutDirectionAndPort()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(10, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 2, Direction.Down, true, true, false, false, 1, false, true, DirectionMask.Left | DirectionMask.Right, false, true, true, 1));
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(11, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 3, 2, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            var selection = new DebugLayoutSelectionSet();
            DebugLayoutTooling.SelectRectangle(selection, runner.Context.ClientMapWorld, new Vector2Int(2, 2), new Vector2Int(3, 2), false);

            var requests = DebugLayoutTooling.CreateCopyRequests(selection, new Vector2Int(8, 8));

            Assert.AreEqual(2, requests.Count);
            Assert.AreEqual(new Vector2Int(8, 8), new Vector2Int(requests[0].X, requests[0].Y));
            Assert.AreEqual(new Vector2Int(9, 8), new Vector2Int(requests[1].X, requests[1].Y));
            Assert.AreEqual(Direction.Down, requests[0].Direction);
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right, requests[0].PortLocalPorts);
        }

        [Test]
        public void DebugLayoutSelection_CopyPreservesRuntimeEffectSources()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(12, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 4, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1));
            var context = new ActionContext(12, 12, "runtime_pushable", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, 12, 0, WorldTag.SourceDebug), 12, 12, 12, 12, new ActionTarget(12, null, Direction.None), Direction.None, 0, 0, 1, 0, 12);
            var spec = new EffectSpec("runtime_pushable", EffectKind.Pushable, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.None, true, true, WorldTag.None);
            var application = new EffectApplication(context, spec, ActionTargetData.Self(12, new GridCoord(4, 4), Direction.None), 0, "runtime-pushable");
            new CommitResolver().Resolve(runner.Context.ClientMapWorld.CoreWorld, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 12, application, 0) });
            var selection = new DebugLayoutSelectionSet();
            DebugLayoutTooling.SelectRectangle(selection, runner.Context.ClientMapWorld, new Vector2Int(4, 4), new Vector2Int(4, 4), false);

            var requests = DebugLayoutTooling.CreateCopyRequests(selection, runner.Context.ClientMapWorld, new Vector2Int(9, 9));

            Assert.AreEqual(1, requests.Count);
            Assert.IsTrue(requests[0].RuntimePushable);
        }

        [Test]
        public void DebugLayoutSelection_SaveUsesUiRuntimeEffectRecordsWithoutLocalStore()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(13, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 6, 6, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1));
            var selection = new DebugLayoutSelectionSet();
            DebugLayoutTooling.SelectRectangle(selection, runner.Context.ClientMapWorld, new Vector2Int(6, 6), new Vector2Int(6, 6), false);
            string path = Path.Combine(Application.temporaryCachePath, "ui-runtime-record.dgdebuglayout.json");
            var runtimeRecords = new[]
            {
                new DebugStructureRuntimeEffectRecord(13, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Down)
            };

            Assert.IsTrue(DebugLayoutTooling.TrySaveSelection(selection, runtimeRecords, "ui-runtime-record", path, out string reason), reason);
            Assert.IsTrue(DebugStructureBlockStorage.TryLoad(path, ClientGameConfigProviderFactory.Create(), out DebugStructureBlockDocument loaded, out IReadOnlyList<string> errors), string.Join("|", errors));

            Assert.AreEqual((int)DirectionMask.Down, loaded.Entries[0].RuntimePortLocalPorts);
            File.Delete(path);
        }

        [Test]
        public void DebugLayoutSelection_SkipsDeletedEntity()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(10, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 2, Direction.None, true, true, false, false, 1, false, true, DirectionMask.None, false, true, true, 1));
            var selection = new DebugLayoutSelectionSet();
            DebugLayoutTooling.SelectRectangle(selection, runner.Context.ClientMapWorld, new Vector2Int(2, 2), new Vector2Int(2, 2), false);
            runner.Context.ClientMapWorld.RemoveEntity(10);

            var requests = selection.CreateMoveRequests(new Vector2Int(8, 8), runner.Context.ClientMapWorld, out var skipped);

            Assert.AreEqual(0, requests.Count);
            Assert.AreEqual(1, skipped.Count);
            Assert.IsTrue(skipped[0].Contains("entity missing"));
        }

        [Test]
        public void PortDebugVisualization_UsesFinalPortMaskAndFindsConnection()
        {
            var first = new EntitySnapshot(10, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, Direction.Right, true, true, false, false, 1, false, true, DirectionMask.Right, false, true, true, 1);
            var second = new EntitySnapshot(11, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, Direction.Right, true, true, false, false, 1, false, false, DirectionMask.Left, false, true, true, 1);

            DirectionMask firstWorld = PortDebugVisualizationUtility.GetWorldPorts(first);
            var connections = PortDebugVisualizationUtility.FindConnections(new[] { first, second });

            Assert.AreEqual(DirectionMask.Right, firstWorld);
            Assert.AreEqual(1, connections.Count);
            Assert.AreEqual(10, connections[0].FromEntityId);
            Assert.AreEqual(11, connections[0].ToEntityId);
        }

        [Test]
        public void PortDebugVisualization_RuntimePortMaskChangesFinalView()
        {
            var before = new EntitySnapshot(10, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, Direction.Right, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1);
            var after = new EntitySnapshot(10, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, Direction.Right, true, true, false, false, 1, false, false, DirectionMask.Up, false, true, true, 2);

            Assert.IsFalse(PortDebugVisualizationUtility.HasPorts(before));
            Assert.IsTrue(PortDebugVisualizationUtility.HasPorts(after));
            Assert.AreEqual(DirectionMask.Up, PortDebugVisualizationUtility.GetWorldPorts(after));
        }

        [Test]
        public void ClientWorldDebugEditor_RuntimePortVisualizationUsesOneMarkerPerDirection()
        {
            ClientWorldRunner runner = CreateRunner();
            var snapshot = new EntitySnapshot(10, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1);
            runner.Context.ClientMapWorld.ApplySnapshot(snapshot);
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(editor, null);
            typeof(ClientWorldDebugEditor)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, runner);
            var runtimeEffectRecords = (Dictionary<string, DebugStructureRuntimeEffectRecord>)typeof(ClientWorldDebugEditor)
                .GetField("runtimeEffectRecords", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(editor);
            runtimeEffectRecords["10:" + (int)RuntimeEffectKind.TemporaryPort] = new DebugStructureRuntimeEffectRecord(10, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Left | DirectionMask.Right);

            typeof(ClientWorldDebugEditor)
                .GetMethod("UpdateViews", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(editor, null);

            Assert.AreEqual(2, editor.ActiveRuntimePortViewCount);
        }

        [Test]
        public void ClientWorldDebugEditor_RuntimePortVisualizationUsesWorldRuntimeEffects()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(15, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 1));
            AddRuntimeEffect(runner.Context.ClientMapWorld.CoreWorld, 15, EffectKind.PortConnector, "runtime_port_view", DirectionMask.Left | DirectionMask.Right, 1);
            GameObject gameObject = new GameObject("DebugEditor");
            var editor = gameObject.AddComponent<ClientWorldDebugEditor>();
            typeof(ClientWorldDebugEditor)
                .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(editor, null);
            typeof(ClientWorldDebugEditor)
                .GetField("runner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(editor, runner);

            typeof(ClientWorldDebugEditor)
                .GetMethod("UpdateViews", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(editor, null);

            Assert.AreEqual(2, editor.ActiveRuntimePortViewCount);
        }

        [Test]
        public void PortDebugVisualization_StructureGhostReportsInternalAndBoundaryPorts()
        {
            var document = new DebugStructureBlockDocument
            {
                Name = "ports",
                Entries =
                {
                    new DebugStructureBlockEntry { Alias = "a", ConfigId = DefaultWorldConfig.PortConnectorBlockerConfigId, OffsetX = 0, OffsetY = 0, Direction = "None", PortLocalPorts = (int)(DirectionMask.Right | DirectionMask.Up) },
                    new DebugStructureBlockEntry { Alias = "b", ConfigId = DefaultWorldConfig.PortConnectorBlockerConfigId, OffsetX = 1, OffsetY = 0, Direction = "None", PortLocalPorts = (int)DirectionMask.Left }
                }
            };

            var cells = PortDebugVisualizationUtility.BuildGhostCells(document, new Vector2Int(10, 20));

            Assert.AreEqual(2, cells.Count);
            Assert.AreEqual(new Vector2Int(10, 20), cells[0].Coord);
            Assert.IsTrue(cells[0].HasInternalConnection);
            Assert.IsTrue(cells[0].BoundaryPorts.Contains(Direction.Up));
            Assert.IsFalse(cells[0].BoundaryPorts.Contains(Direction.Right));
        }

        private static ClientWorldRunner CreateRunner()
        {
            GameObject gameObject = new GameObject("Runner");
            var world = gameObject.AddComponent<WorldBootstrap>();
            ClientWorldRunner runner = gameObject.AddComponent<ClientWorldRunner>();
            runner.Initialize(world.EnsureWorld());
            return runner;
        }

        private static void AddRuntimeEffect(GameWorld world, long entityId, EffectKind kind, string specId, DirectionMask portMask, int autoMoveIntervalTicks)
        {
            var context = new ActionContext(entityId, entityId, specId, WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, entityId, 0, WorldTag.SourceDebug), entityId, entityId, entityId, entityId, new ActionTarget(entityId, null, Direction.None), Direction.None, world.ServerTick, world.ServerTick, 1, 0, entityId);
            var spec = new EffectSpec(specId, kind, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, autoMoveIntervalTicks, portMask, true, true, WorldTag.None);
            var application = new EffectApplication(context, spec, ActionTargetData.Self(entityId, default, Direction.None), world.ServerTick, specId);
            new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, entityId, application, world.ServerTick) });
        }

        private static void DeleteLayoutIfExists(string name)
        {
            string path = DebugLayoutPaths.NamedFilePath(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            string metaPath = path + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

    }
}
