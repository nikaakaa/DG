using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.GameCore;
using DG.Map;
using Fantasy;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class ClientAnimationLayerTests
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
        public void PlayerMoveMetadataGeneratesEvent()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(2, new[]
            {
                State(1, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 1, 0, true)
            }, new List<long>(), new[] { Metadata(1, 2, WorldDeltaMotionKind.PlayerMove, "player_move") });

            Assert.IsTrue(applied);
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(ClientAnimationMotionKind.PlayerMove, animationEvent.MotionKind);
            Assert.AreEqual(new Vector2Int(0, 0), animationEvent.FromCoord);
            Assert.AreEqual(new Vector2Int(1, 0), animationEvent.ToCoord);
            Assert.AreEqual("player_move", animationEvent.StyleId);
        }

        [Test]
        public void MechanismPushMetadataSelectsPushStyle()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 2 }, DefaultWorldConfig.PushableBlockerSpawn(2, new GridCoord(1, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(3, new[]
            {
                State(2, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true)
            }, new List<long>(), new[] { Metadata(2, 3, WorldDeltaMotionKind.MechanismPush, "mechanism_push") });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, animationEvent.MotionKind);
            Assert.AreEqual("mechanism_push", animationEvent.StyleId);
            Assert.Greater(animationEvent.Style.ScaleFeedback, 1f);
        }

        [Test]
        public void ConnectedBodyPushMetadataGeneratesEventsForEachMovedMember()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 3 }, DefaultWorldConfig.PortConnectorBlockerSpawn(3, new GridCoord(1, 0), Direction.Right));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 4 }, DefaultWorldConfig.PortConnectorBlockerSpawn(4, new GridCoord(2, 0), Direction.Left));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(13, new[]
            {
                State(3, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(4, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 3, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Left)
            }, new List<long>(), new[]
            {
                Metadata(3, 13, WorldDeltaMotionKind.MechanismPush, "mechanism_push"),
                Metadata(4, 13, WorldDeltaMotionKind.MechanismPush, "mechanism_push")
            });

            var entityIds = new List<long>();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, animationEvent.MotionKind);
                Assert.AreEqual("mechanism_push", animationEvent.StyleId);
                Assert.IsTrue(animationEvent.IsMovement);
                entityIds.Add(animationEvent.EntityId);
            }

            CollectionAssert.AreEquivalent(new[] { 3L, 4L }, entityIds);
        }

        [Test]
        public void ConnectedBodyPushPlaybackKeepsActiveAnimationForEachMovedMember()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 6 }, DefaultWorldConfig.PortConnectorBlockerSpawn(6, new GridCoord(1, 0), Direction.Right));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 7 }, DefaultWorldConfig.PortConnectorBlockerSpawn(7, new GridCoord(2, 0), Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(22, new[]
            {
                State(6, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(7, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 3, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right)
            }, new List<long>(), new[]
            {
                Metadata(6, 22, WorldDeltaMotionKind.MechanismPush, "mechanism_push"),
                Metadata(7, 22, WorldDeltaMotionKind.MechanismPush, "mechanism_push")
            });
            InvokePrivate(visuals, "LateUpdate");

            object animations = GetPrivateField(visuals, "activeAnimations");
            Assert.IsTrue((bool)animations.GetType().GetMethod("ContainsKey").Invoke(animations, new object[] { 6L }));
            Assert.IsTrue((bool)animations.GetType().GetMethod("ContainsKey").Invoke(animations, new object[] { 7L }));
            Assert.IsNotNull(visualObject.transform.Find("Entities/Entity_6"));
            Assert.IsNotNull(visualObject.transform.Find("Entities/Entity_7"));
        }

        [Test]
        public void MechanismPushMetadataGeneratesFeedbackWhenEntityDoesNotMove()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 5 }, DefaultWorldConfig.PlayerSpawn(5, 5, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(14, new[]
            {
                State(5, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 0, 0, true)
            }, new List<long>(), new[] { Metadata(5, 14, WorldDeltaMotionKind.MechanismPush, "mechanism_push", Direction.Right) });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, animationEvent.MotionKind);
            Assert.AreEqual(new Vector2Int(0, 0), animationEvent.FromCoord);
            Assert.AreEqual(new Vector2Int(0, 0), animationEvent.ToCoord);
            Assert.IsFalse(animationEvent.IsMovement);
            Assert.IsTrue(animationEvent.IsImpulse);
            Assert.AreEqual(Direction.Right, animationEvent.ImpulseDirection);
            Assert.AreEqual("mechanism_push", animationEvent.StyleId);
            Assert.Greater(animationEvent.Style.ScaleFeedback, 1f);
        }

        [Test]
        public void IntermediateConnectedBodyPushDeltasGenerateSeparateAnimationsByServerTick()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 8 }, DefaultWorldConfig.PushableBlockerSpawn(8, new GridCoord(3, 0)));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 9 }, DefaultWorldConfig.PortConnectorBlockerSpawn(9, new GridCoord(1, 0), Direction.Right));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 10 }, DefaultWorldConfig.PortConnectorBlockerSpawn(10, new GridCoord(2, 0), Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(31, new[]
            {
                State(8, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 0, false, pushable: true)
            }, new List<long>(), new[] { Metadata(8, 31, WorldDeltaMotionKind.MechanismPush, "mechanism_push", Direction.Right) });
            ClientMoveNetworkRuntime.ApplyWorldDelta(32, new[]
            {
                State(9, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(10, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 3, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right)
            }, new List<long>(), new[]
            {
                Metadata(9, 32, WorldDeltaMotionKind.MechanismPush, "mechanism_push", Direction.Right),
                Metadata(10, 32, WorldDeltaMotionKind.MechanismPush, "mechanism_push", Direction.Right)
            });

            var entityIds = new List<long>();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, animationEvent.MotionKind);
                Assert.AreEqual("mechanism_push", animationEvent.StyleId);
                Assert.IsTrue(animationEvent.IsMovement);
                entityIds.Add(animationEvent.EntityId);
            }

            CollectionAssert.AreEquivalent(new[] { 8L, 9L, 10L }, entityIds);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(8, out Vector2Int downstreamCoord));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(9, out Vector2Int firstCoord));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(10, out Vector2Int secondCoord));
            Assert.AreEqual(new Vector2Int(4, 0), downstreamCoord);
            Assert.AreEqual(new Vector2Int(2, 0), firstCoord);
            Assert.AreEqual(new Vector2Int(3, 0), secondCoord);
        }

        [Test]
        public void IntermediateConnectedBodyEntryFeedbackDoesNotInferMissingMembers()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 11 }, DefaultWorldConfig.PortConnectorBlockerSpawn(11, new GridCoord(1, 0), Direction.Right));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 12 }, DefaultWorldConfig.PortConnectorBlockerSpawn(12, new GridCoord(2, 0), Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(33, new[]
            {
                State(11, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right)
            }, new List<long>(), new[] { Metadata(11, 33, WorldDeltaMotionKind.MechanismPush, "mechanism_push", Direction.Right) });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(11, animationEvent.EntityId);
            Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, animationEvent.MotionKind);
            Assert.IsFalse(animationEvent.IsMovement);
            Assert.IsTrue(animationEvent.IsImpulse);
            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(11, out Vector2Int firstCoord));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(12, out Vector2Int secondCoord));
            Assert.AreEqual(new Vector2Int(1, 0), firstCoord);
            Assert.AreEqual(new Vector2Int(2, 0), secondCoord);
        }

        [Test]
        public void RotatePivotMetadataGeneratesMemberEventsIncludingPivot()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(101, 0, 0, Direction.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(102, 0, 1, Direction.Up));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(40, new[]
            {
                State(101, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, false, pushable: true),
                State(102, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true)
            }, new List<long>(), new[]
            {
                RotateMetadata(101, 40, WorldDeltaMotionKind.RotatePivot, "rotate_pivot", new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false),
                RotateMetadata(102, 40, WorldDeltaMotionKind.RotatePivot, "rotate_pivot", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), RotatePivotDirection.Clockwise, false)
            });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent pivotEvent));
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent memberEvent));
            Assert.AreEqual(ClientAnimationMotionKind.RotatePivot, pivotEvent.MotionKind);
            Assert.AreEqual(ClientAnimationMotionKind.RotatePivot, memberEvent.MotionKind);
            Assert.AreEqual(new Vector2Int(0, 0), pivotEvent.FromCoord);
            Assert.AreEqual(new Vector2Int(0, 0), pivotEvent.ToCoord);
            Assert.AreEqual(new Vector2Int(0, 1), memberEvent.FromCoord);
            Assert.AreEqual(new Vector2Int(1, 0), memberEvent.ToCoord);
            Assert.AreEqual(RotatePivotDirection.Clockwise, memberEvent.RotateDirection);
            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
        }

        [Test]
        public void BlockedRotateMetadataGeneratesBounceAndImpactEvents()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(111, 0, 0, Direction.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(112, 0, 1, Direction.Up));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(113, 1, 0, Direction.None));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(41, new[]
            {
                State(111, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, false, pushable: true),
                State(112, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 1, false, pushable: true),
                State(113, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true)
            }, new List<long>(), new[]
            {
                RotateMetadata(111, 111, 41, WorldDeltaMotionKind.RotatePivotBounce, "rotate_pivot_bounce", new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), RotatePivotDirection.Clockwise, true),
                RotateMetadata(112, 111, 41, WorldDeltaMotionKind.RotatePivotBounce, "rotate_pivot_bounce", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), RotatePivotDirection.Clockwise, true),
                RotateMetadata(113, 111, 41, WorldDeltaMotionKind.MechanismPush, "rotate_pivot_impact", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), RotatePivotDirection.Clockwise, false, Direction.Right)
            });

            var events = new List<ClientAnimationEvent>();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                events.Add(animationEvent);
            }

            Assert.AreEqual(3, events.Count);
            Assert.AreEqual(2, events.Count(item => item.MotionKind == ClientAnimationMotionKind.RotatePivotBounce && item.Bounce));
            Assert.IsTrue(events.Any(item => item.EntityId == 113 && item.MotionKind == ClientAnimationMotionKind.MechanismPush && item.StyleId == "rotate_pivot_impact" && item.ImpulseDirection == Direction.Right));
        }

        [Test]
        public void RotatePivotPlaybackUsesArcAndBounceReturnsToSnapshot()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(121, 0, 1, Direction.Up));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(42, new[]
            {
                State(121, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Up)
            }, new List<long>(), new[]
            {
                RotateMetadata(121, 121, 42, WorldDeltaMotionKind.RotatePivot, "rotate_pivot", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), RotatePivotDirection.Clockwise, false)
            });
            InvokePrivate(visuals, "LateUpdate");
            SetActiveAnimationProgress(visuals, 121, 0.5f);
            InvokePrivate(visuals, "LateUpdate");
            Transform view = visualObject.transform.Find("Entities/Entity_121");
            Assert.IsNotNull(view);
            Assert.Greater(view.localPosition.x, 0.5f);
            Assert.Greater(view.localPosition.y, 0.5f);
            Assert.Less(view.localPosition.y, 1.5f);

            ClientMoveNetworkRuntime.ApplyWorldDelta(43, new[]
            {
                State(121, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 1, false, pushable: true, portLocalPorts: (int)DirectionMask.Up)
            }, new List<long>(), new[]
            {
                RotateMetadata(121, 121, 43, WorldDeltaMotionKind.RotatePivotBounce, "rotate_pivot_bounce", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), RotatePivotDirection.Clockwise, true)
            });
            InvokePrivate(visuals, "LateUpdate");
            SetActiveAnimationProgress(visuals, 121, 0.5f);
            InvokePrivate(visuals, "LateUpdate");
            Assert.Greater(view.localPosition.x, 0.5f);
            Assert.Less(view.localPosition.y, 1.5f);

            SetActiveAnimationProgress(visuals, 121, 1f);
            InvokePrivate(visuals, "LateUpdate");
            Assert.AreEqual(new Vector3(0.5f, 1.5f, -0.1f), view.localPosition);
        }

        [Test]
        public void RotatePivotPlaybackKeepsConnectedBodyRigidAndLinesFollowViews()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(131, 0, 0, Direction.Right, DirectionMask.Up | DirectionMask.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(132, 0, 1, Direction.Right, DirectionMask.Down));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(133, 1, 0, Direction.Right, DirectionMask.Left));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(44, new[]
            {
                State(131, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, false, pushable: true, portLocalPorts: (int)(DirectionMask.Right | DirectionMask.Down)),
                State(132, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Left),
                State(133, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, -1, false, pushable: true, portLocalPorts: (int)DirectionMask.Up)
            }, new List<long>(), new[]
            {
                RotateMetadata(131, 131, 44, WorldDeltaMotionKind.RotatePivot, "rotate_pivot", new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false),
                RotateMetadata(132, 131, 44, WorldDeltaMotionKind.RotatePivot, "rotate_pivot", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), RotatePivotDirection.Clockwise, false),
                RotateMetadata(133, 131, 44, WorldDeltaMotionKind.RotatePivot, "rotate_pivot", new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), RotatePivotDirection.Clockwise, false)
            });
            InvokePrivate(visuals, "LateUpdate");
            SetActiveAnimationProgress(visuals, 132, 0.5f);
            SetActiveAnimationProgress(visuals, 133, 0.5f);
            InvokePrivate(visuals, "LateUpdate");

            Transform entityRoot = visualObject.transform.Find("Entities");
            Transform pivot = entityRoot.Find("Entity_131");
            Transform upper = entityRoot.Find("Entity_132");
            Transform right = entityRoot.Find("Entity_133");
            Assert.IsNotNull(pivot);
            Assert.IsNotNull(upper);
            Assert.IsNotNull(right);
            Assert.AreEqual(entityRoot, pivot.parent);
            Assert.AreEqual(entityRoot, upper.parent);
            Assert.AreEqual(entityRoot, right.parent);
            float pivotAngle = SignedAngle(pivot.localRotation.eulerAngles.z);
            Assert.Greater(Mathf.Abs(pivotAngle), 0.0001f);
            Assert.That(SignedAngle(upper.localRotation.eulerAngles.z), Is.EqualTo(pivotAngle).Within(0.0001f));
            Assert.That(SignedAngle(right.localRotation.eulerAngles.z), Is.EqualTo(pivotAngle).Within(0.0001f));
            AssertRotatedOffset(pivot.localPosition, upper.localPosition, new Vector3(0f, 1f, 0f), pivotAngle);
            AssertRotatedOffset(pivot.localPosition, right.localPosition, new Vector3(1f, 0f, 0f), pivotAngle);
            Assert.That(Vector3.Distance(upper.localPosition, right.localPosition), Is.EqualTo(Mathf.Sqrt(2f)).Within(0.0001f));

            Transform lineRoot = visualObject.transform.Find("PortConnections");
            Assert.IsNotNull(lineRoot);
            LineRenderer line = lineRoot.GetComponentInChildren<LineRenderer>();
            Assert.IsNotNull(line);
            Vector3 lineStart = line.GetPosition(0);
            Vector3 lineEnd = line.GetPosition(1);
            Assert.IsTrue(ApproximatelyAny(lineStart, pivot.localPosition, upper.localPosition, right.localPosition));
            Assert.IsTrue(ApproximatelyAny(lineEnd, pivot.localPosition, upper.localPosition, right.localPosition));
        }

        [Test]
        public void RotatePivotPlaybackUsesCurrentViewPositionAsArcStart()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(141, 0, 1, Direction.Up));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");
            InvokePrivate(visuals, "LateUpdate");
            Transform view = visualObject.transform.Find("Entities/Entity_141");
            view.localPosition = new Vector3(0.8f, 1.2f, -0.1f);

            ClientMoveNetworkRuntime.ApplyWorldDelta(45, new[]
            {
                State(141, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true)
            }, new List<long>(), new[]
            {
                RotateMetadata(141, 141, 45, WorldDeltaMotionKind.RotatePivot, "rotate_pivot", new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), RotatePivotDirection.Clockwise, false)
            });
            InvokePrivate(visuals, "LateUpdate");
            SetActiveAnimationProgress(visuals, 141, 0.5f);
            InvokePrivate(visuals, "LateUpdate");

            Vector3 pivot = new Vector3(0.5f, 0.5f, -0.1f);
            float angle = SignedAngle(view.localRotation.eulerAngles.z);
            AssertRotatedOffset(pivot, view.localPosition, new Vector3(0.3f, 0.7f, 0f), angle);
        }

        [Test]
        public void AutoMoveAndDebugDragUseDifferentStyles()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 20 }, DefaultWorldConfig.BallSpawn(20, new GridCoord(0, 0), Direction.Right, 1));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 21 }, DefaultWorldConfig.BlockerSpawn(21, new GridCoord(5, 5)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(4, new[]
            {
                State(20, DefaultWorldConfig.BallConfigId, DefaultWorldConfig.BallArchetypeId, DefaultWorldConfig.BallTarget, 1, 0, false, autoMove: true),
                State(21, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 8, 8, false)
            }, new List<long>(), new[]
            {
                Metadata(20, 4, WorldDeltaMotionKind.AutoMove, "auto_move"),
                Metadata(21, 4, WorldDeltaMotionKind.DebugDrag, "debug_drag")
            });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent first));
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent second));
            Assert.AreNotEqual(first.StyleId, second.StyleId);
            Assert.IsTrue(first.MotionKind == ClientAnimationMotionKind.AutoMove || second.MotionKind == ClientAnimationMotionKind.AutoMove);
            Assert.IsTrue(first.MotionKind == ClientAnimationMotionKind.DebugDrag || second.MotionKind == ClientAnimationMotionKind.DebugDrag);
        }

        [Test]
        public void SpawnAndRemoveGenerateEvents()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 30 }, DefaultWorldConfig.BlockerSpawn(30, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(5, new[]
            {
                State(31, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 2, false)
            }, new List<long> { 30 }, new[]
            {
                Metadata(31, 5, WorldDeltaMotionKind.Spawn, "spawn"),
                Metadata(30, 5, WorldDeltaMotionKind.Remove, "remove")
            });

            var kinds = new List<ClientAnimationMotionKind>();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                kinds.Add(animationEvent.MotionKind);
            }

            Assert.Contains(ClientAnimationMotionKind.Spawn, kinds);
            Assert.Contains(ClientAnimationMotionKind.Remove, kinds);
        }

        [Test]
        public void RepeatedFinalSnapshotDoesNotGenerateMoveEvent()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 40 }, DefaultWorldConfig.BlockerSpawn(40, new GridCoord(1, 1)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(6, new[]
            {
                State(40, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 1, false)
            }, new List<long>(), new[] { Metadata(40, 6, WorldDeltaMotionKind.PlayerMove, "player_move") });

            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
        }

        [Test]
        public void MissingMetadataGeneratesUnknownMove()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 50 }, DefaultWorldConfig.BlockerSpawn(50, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(7, new[]
            {
                State(50, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 1, false)
            }, new List<long>(), new List<G2C_WorldDeltaAnimationMetadata>());

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(ClientAnimationMotionKind.Unknown, animationEvent.MotionKind);
            Assert.AreEqual("unknown", animationEvent.StyleId);
        }

        [Test]
        public void OldTickSnapshotDoesNotGenerateRollbackAnimation()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(new EntitySnapshot(60, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 3, 0, Direction.None, true, true, false, false, 1, false, false, DirectionMask.None, false, true, true, 10));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(9, new[]
            {
                State(60, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false)
            }, new List<long>(), new[] { Metadata(60, 9, WorldDeltaMotionKind.PlayerMove, "player_move") });

            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(60, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(3, 0), coord);
        }

        [Test]
        public void VisualPlaybackDoesNotModifyClientMapWorld()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 70 }, DefaultWorldConfig.PlayerSpawn(70, 70, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            ClientMoveNetworkRuntime.ApplyWorldDelta(11, new[]
            {
                State(70, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 1, 0, true)
            }, new List<long>(), new[] { Metadata(70, 11, WorldDeltaMotionKind.PlayerMove, "player_move") });
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);

            InvokePrivate(visuals, "Awake");
            InvokePrivate(visuals, "LateUpdate");

            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(70, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(1, 0), coord);
        }

        [Test]
        public void ImpulsePushPlaybackMovesOutAndReturnsWithinDuration()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 75 }, DefaultWorldConfig.PlayerSpawn(75, 75, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            ClientMoveNetworkRuntime.ApplyWorldDelta(15, new[]
            {
                State(75, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 0, 0, true)
            }, new List<long>(), new[] { Metadata(75, 15, WorldDeltaMotionKind.MechanismPush, "mechanism_push", Direction.Right) });
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);

            InvokePrivate(visuals, "Awake");
            InvokePrivate(visuals, "LateUpdate");
            SetActiveAnimationElapsed(visuals, 75, 0.09f);
            InvokePrivate(visuals, "LateUpdate");
            Transform view = visualObject.transform.Find("Entities/Entity_75");
            Assert.IsNotNull(view);
            Vector3 start = new Vector3(0.5f, 0.5f, -0.1f);
            Assert.Greater(view.localPosition.x, start.x);

            SetActiveAnimationElapsed(visuals, 75, 0.18f);
            InvokePrivate(visuals, "LateUpdate");
            Assert.AreEqual(start, view.localPosition);
        }

        [Test]
        public void ConsecutiveServerTicksUpdateClientMapWorldBeforeAnimationFinishes()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 76 }, DefaultWorldConfig.PlayerSpawn(76, 76, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(16, new[]
            {
                State(76, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 1, 0, true)
            }, new List<long>(), new[] { Metadata(76, 16, WorldDeltaMotionKind.PlayerMove, "player_move") });
            ClientMoveNetworkRuntime.ApplyWorldDelta(17, new[]
            {
                State(76, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 2, 0, true)
            }, new List<long>(), new[] { Metadata(76, 17, WorldDeltaMotionKind.PlayerMove, "player_move") });

            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(76, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(2, 0), coord);
        }

        [Test]
        public void ConsecutiveAnimationEventsReplaceActivePlaybackForSameEntity()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 77 }, DefaultWorldConfig.PlayerSpawn(77, 77, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(18, new[]
            {
                State(77, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 1, 0, true)
            }, new List<long>(), new[] { Metadata(77, 18, WorldDeltaMotionKind.PlayerMove, "player_move") });
            InvokePrivate(visuals, "LateUpdate");
            SetActiveAnimationElapsed(visuals, 77, 0.06f);
            InvokePrivate(visuals, "LateUpdate");
            Transform view = visualObject.transform.Find("Entities/Entity_77");
            Assert.IsNotNull(view);
            float interruptedX = view.localPosition.x;
            Assert.Greater(interruptedX, 0.5f);
            Assert.Less(interruptedX, 1.5f);

            ClientMoveNetworkRuntime.ApplyWorldDelta(19, new[]
            {
                State(77, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 2, 0, true)
            }, new List<long>(), new[] { Metadata(77, 19, WorldDeltaMotionKind.PlayerMove, "player_move") });
            InvokePrivate(visuals, "LateUpdate");

            Assert.GreaterOrEqual(view.localPosition.x, interruptedX);
            Assert.Less(view.localPosition.x, 2.5f);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(77, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(2, 0), coord);
        }

        [Test]
        public void ConsecutiveAnimationPlaybackSettlesAtLatestServerDelta()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 78 }, DefaultWorldConfig.PlayerSpawn(78, 78, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(20, new[]
            {
                State(78, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 1, 0, true)
            }, new List<long>(), new[] { Metadata(78, 20, WorldDeltaMotionKind.PlayerMove, "player_move") });
            InvokePrivate(visuals, "LateUpdate");

            ClientMoveNetworkRuntime.ApplyWorldDelta(21, new[]
            {
                State(78, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 2, 0, true)
            }, new List<long>(), new[] { Metadata(78, 21, WorldDeltaMotionKind.PlayerMove, "player_move") });
            InvokePrivate(visuals, "LateUpdate");
            SetActiveAnimationElapsed(visuals, 78, 0.12f);
            InvokePrivate(visuals, "LateUpdate");

            Transform view = visualObject.transform.Find("Entities/Entity_78");
            Assert.IsNotNull(view);
            Assert.AreEqual(new Vector3(2.5f, 0.5f, -0.1f), view.localPosition);
            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(78, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(2, 0), coord);
        }

        [Test]
        public void DisabledAnimationDisplaysFinalPosition()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 80 }, DefaultWorldConfig.PlayerSpawn(80, 80, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            ClientMoveNetworkRuntime.ApplyWorldDelta(12, new[]
            {
                State(80, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 2, 0, true)
            }, new List<long>(), new[] { Metadata(80, 12, WorldDeltaMotionKind.PlayerMove, "player_move") });
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            SetPrivateField(visuals, "animationLayerEnabled", false);

            InvokePrivate(visuals, "Awake");
            InvokePrivate(visuals, "LateUpdate");
            Transform view = visualObject.transform.Find("Entities/Entity_80");

            Assert.IsNotNull(view);
            Assert.AreEqual(new Vector3(2.5f, 0.5f, -0.1f), view.localPosition);
        }

        [Test]
        public void VisualRefreshReusesEntitySpriteAndPortMaterial()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 90 }, DefaultWorldConfig.PortConnectorBlockerSpawn(90, new GridCoord(0, 0), Direction.Right));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 91 }, DefaultWorldConfig.PortConnectorBlockerSpawn(91, new GridCoord(1, 0), Direction.Left));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);

            InvokePrivate(visuals, "Awake");
            InvokePrivate(visuals, "LateUpdate");
            Transform entityView = visualObject.transform.Find("Entities/Entity_90");
            var spriteRenderer = entityView.GetComponent<SpriteRenderer>();
            var lineRenderer = entityView.GetComponent<LineRenderer>();
            Sprite sprite = spriteRenderer.sprite;
            Material material = lineRenderer.sharedMaterial;

            InvokePrivate(visuals, "LateUpdate");

            Assert.AreSame(sprite, spriteRenderer.sprite);
            Assert.AreSame(material, lineRenderer.sharedMaterial);
        }

        [Test]
        public void RemovedPortConnectionDestroysLineView()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 92 }, DefaultWorldConfig.PortConnectorBlockerSpawn(92, new GridCoord(0, 0), Direction.Right));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 93 }, DefaultWorldConfig.PortConnectorBlockerSpawn(93, new GridCoord(1, 0), Direction.Left));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);

            InvokePrivate(visuals, "Awake");
            InvokePrivate(visuals, "LateUpdate");
            object lines = GetPrivateField(visuals, "portConnectionLines");
            Assert.AreEqual(1, (int)lines.GetType().GetProperty("Count").GetValue(lines));

            runner.Context.ClientMapWorld.RemoveEntity(93);
            InvokePrivate(visuals, "LateUpdate");

            Assert.AreEqual(0, (int)lines.GetType().GetProperty("Count").GetValue(lines));
            Assert.IsNull(visualObject.transform.Find("PortConnections/PortConnection_92:93"));
        }

        [Test]
        public void AnimationStyleProviderDoesNotChangeActionSpecResults()
        {
            ClientAnimationStyleProvider provider = ClientAnimationStyleProvider.Fallback();
            Assert.IsTrue(provider.TryGet("mechanism_push", out ClientAnimationStyle style));
            Assert.AreEqual("mechanism_push", style.StyleId);
            Assert.IsTrue(provider.TryGet("rotate_pivot", out ClientAnimationStyle rotateStyle));
            Assert.IsTrue(provider.TryGet("rotate_pivot_bounce", out ClientAnimationStyle bounceStyle));
            Assert.IsTrue(provider.TryGet("rotate_pivot_impact", out ClientAnimationStyle impactStyle));
            Assert.AreEqual(0.54f, rotateStyle.DurationSeconds);
            Assert.AreEqual(0.54f, bounceStyle.DurationSeconds);
            Assert.AreEqual(0.54f, impactStyle.DurationSeconds);

            ActionSpec playerMove = ActionSpecRegistry.Default.Get("player_move");
            ActionSpec mechanismPush = ActionSpecRegistry.Default.Get("mechanism_push");

            Assert.AreEqual("player_move", playerMove.SpecId.Value);
            Assert.AreEqual("mechanism_push", mechanismPush.SpecId.Value);
            Assert.AreEqual(new ActionStrategyId("move"), playerMove.StrategyId);
            Assert.AreEqual(new ActionStrategyId("move"), mechanismPush.StrategyId);
        }

        [Test]
        public void AnimationStyleConfigSourceGeneratedAndStreamingAssetsStayAligned()
        {
            string root = FindRepositoryRoot();
            JArray source = JArray.Parse(File.ReadAllText(Path.Combine(root, "Config", "Luban", "Datas", "gamecore", "animation_style.json")));
            JArray generated = JArray.Parse(File.ReadAllText(Path.Combine(root, "Config", "Luban", "Generated", "json", "gamecore_tbanimationstyle.json")));
            JArray streaming = JArray.Parse(File.ReadAllText(Path.Combine(root, "Client", "DG_Client", "Assets", "StreamingAssets", "GameConfig", "gamecore_tbanimationstyle.json")));

            CollectionAssert.AreEquivalent(StyleIds(source), StyleIds(generated));
            CollectionAssert.AreEquivalent(StyleIds(source), StyleIds(streaming));
            Assert.IsTrue(StyleIds(streaming).Contains("unknown"));
            Assert.IsTrue(StyleIds(streaming).Contains("mechanism_push"));
            Assert.AreEqual(0.54f, StyleDuration(source, "rotate_pivot"));
            Assert.AreEqual(0.54f, StyleDuration(generated, "rotate_pivot_bounce"));
            Assert.AreEqual(0.54f, StyleDuration(streaming, "rotate_pivot_impact"));
        }

        private static ClientWorldRunner CreateRunner()
        {
            GameObject gameObject = new GameObject("Runner");
            var world = gameObject.AddComponent<WorldBootstrap>();
            ClientWorldRunner runner = gameObject.AddComponent<ClientWorldRunner>();
            runner.Initialize(world.EnsureWorld());
            return runner;
        }

        private static G2C_WorldEntityState State(long entityId, int configId, int archetypeId, int target, int x, int y, bool playerControlled, bool autoMove = false, bool pushable = false, int portLocalPorts = 0)
        {
            return new G2C_WorldEntityState
            {
                EntityId = entityId,
                ConfigId = configId,
                ArchetypeId = archetypeId,
                EntityTarget = target,
                X = x,
                Y = y,
                Direction = (int)Direction.None,
                HasCollider = true,
                Blocking = true,
                AutoMove = autoMove,
                AutoMoveIntervalTicks = autoMove ? 1 : 0,
                PlayerControlled = playerControlled,
                Pushable = pushable,
                PortLocalPorts = portLocalPorts,
                CanMove = true,
                CanBePushed = true
            };
        }

        private static G2C_WorldDeltaAnimationMetadata Metadata(long entityId, long serverTick, WorldDeltaMotionKind motionKind, string styleKey, Direction direction = Direction.None)
        {
            return new G2C_WorldDeltaAnimationMetadata
            {
                EntityId = entityId,
                ServerTick = serverTick,
                MotionKind = (int)motionKind,
                StyleKey = styleKey,
                Direction = (int)direction
            };
        }

        private static G2C_WorldDeltaAnimationMetadata RotateMetadata(long entityId, long serverTick, WorldDeltaMotionKind motionKind, string styleKey, Vector2Int pivotCoord, Vector2Int fromCoord, Vector2Int toCoord, RotatePivotDirection rotateDirection, bool bounce, Direction direction = Direction.None)
            => RotateMetadata(entityId, 101, serverTick, motionKind, styleKey, pivotCoord, fromCoord, toCoord, rotateDirection, bounce, direction);

        private static G2C_WorldDeltaAnimationMetadata RotateMetadata(long entityId, long pivotEntityId, long serverTick, WorldDeltaMotionKind motionKind, string styleKey, Vector2Int pivotCoord, Vector2Int fromCoord, Vector2Int toCoord, RotatePivotDirection rotateDirection, bool bounce, Direction direction = Direction.None)
        {
            return new G2C_WorldDeltaAnimationMetadata
            {
                EntityId = entityId,
                ServerTick = serverTick,
                MotionKind = (int)motionKind,
                StyleKey = styleKey,
                Direction = (int)direction,
                PivotEntityId = pivotEntityId,
                PivotX = pivotCoord.x,
                PivotY = pivotCoord.y,
                FromX = fromCoord.x,
                FromY = fromCoord.y,
                ToX = toCoord.x,
                ToY = toCoord.y,
                RotateDirection = (int)rotateDirection,
                Bounce = bounce,
                ImpactX = toCoord.x,
                ImpactY = toCoord.y
            };
        }

        private static EntitySnapshot StateSnapshot(long entityId, int x, int y, Direction direction)
            => StateSnapshot(entityId, x, y, direction, DirectionMask.None);

        private static EntitySnapshot StateSnapshot(long entityId, int x, int y, Direction direction, DirectionMask ports)
        {
            return new EntitySnapshot(entityId, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, x, y, direction, true, true, false, false, 1, false, true, ports, false, true, true, 1);
        }

        private static bool ApproximatelyAny(Vector3 value, params Vector3[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                Vector3 candidate = candidates[i];
                candidate.z = value.z;
                if (Vector3.Distance(value, candidate) < 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertRotatedOffset(Vector3 pivot, Vector3 target, Vector3 originalOffset, float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            Vector3 expectedOffset = new Vector3(originalOffset.x * cos - originalOffset.y * sin, originalOffset.x * sin + originalOffset.y * cos, 0f);
            Vector3 actualOffset = target - pivot;
            actualOffset.z = 0f;
            Assert.That(actualOffset.x, Is.EqualTo(expectedOffset.x).Within(0.0001f));
            Assert.That(actualOffset.y, Is.EqualTo(expectedOffset.y).Within(0.0001f));
        }

        private static float SignedAngle(float angle)
            => angle > 180f ? angle - 360f : angle;

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            return target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType()
                .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(target, null);
        }

        private static void SetActiveAnimationElapsed(ClientWorldVisuals visuals, long entityId, float elapsed)
        {
            SetActiveAnimationElapsed(GetActiveAnimation(visuals, entityId), elapsed);
        }

        private static void SetActiveAnimationProgress(ClientWorldVisuals visuals, long entityId, float normalized)
        {
            object animation = GetActiveAnimation(visuals, entityId);
            float duration = (float)animation.GetType()
                .GetProperty("DurationSeconds")
                .GetValue(animation);
            SetActiveAnimationElapsed(animation, duration * Mathf.Clamp01(normalized));
        }

        private static object GetActiveAnimation(ClientWorldVisuals visuals, long entityId)
        {
            object animations = visuals.GetType()
                .GetField("activeAnimations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(visuals);
            return animations.GetType()
                .GetProperty("Item")
                .GetValue(animations, new object[] { entityId });
        }

        private static void SetActiveAnimationElapsed(object animation, float elapsed)
        {
            animation.GetType()
                .GetField("elapsed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(animation, elapsed);
        }

        private static string FindRepositoryRoot()
        {
            string current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, "Config", "Luban")) &&
                    Directory.Exists(Path.Combine(current, "Client", "DG_Client")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            Assert.Fail("repository root not found");
            return string.Empty;
        }

        private static string[] StyleIds(JArray array)
        {
            return array.Select(item => (string)item["style_id"]).OrderBy(id => id).ToArray();
        }

        private static float StyleDuration(JArray array, string styleId)
        {
            return array.Single(item => (string)item["style_id"] == styleId)["duration_seconds"].Value<float>();
        }
    }
}
