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
        public void PlayerMoveFactGeneratesPlayback()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 1 }, DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(2, new[]
            {
                State(1, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 1, 0, true)
            }, new List<long>(), new[] { Fact(1, 2, PresentationFactType.EntityMoved) });

            Assert.IsTrue(applied);
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(ClientAnimationMotionKind.PlayerMove, animationEvent.MotionKind);
            Assert.AreEqual(new Vector2Int(0, 0), animationEvent.FromCoord);
            Assert.AreEqual(new Vector2Int(1, 0), animationEvent.ToCoord);
            Assert.AreEqual("player_move", animationEvent.StyleId);
        }

        [Test]
        public void MechanismPushFactSelectsPushStyle()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 2 }, DefaultWorldConfig.PushableBlockerSpawn(2, new GridCoord(1, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(3, new[]
            {
                State(2, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true)
            }, new List<long>(), new[] { Fact(2, 3, PresentationFactType.EntityPushed) });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, animationEvent.MotionKind);
            Assert.AreEqual("mechanism_push", animationEvent.StyleId);
            Assert.Greater(animationEvent.Style.ScaleFeedback, 1f);
        }

        [Test]
        public void ConnectedBodyPushFactGeneratesPlaybacksForEachMovedMember()
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
                Fact(3, 13, PresentationFactType.EntityPushed),
                Fact(4, 13, PresentationFactType.EntityPushed)
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
        public void BodyMovedFactGeneratesSingleTranslateGroupTrack()
        {
            var planner = new ClientPresentationPlaybackPlanner(ClientAnimationStyleProvider.Fallback());
            var before = new Dictionary<long, EntitySnapshot>
            {
                [206] = StateSnapshot(206, 1, 0, Direction.Right, DirectionMask.Right),
                [207] = StateSnapshot(207, 2, 0, Direction.Right, DirectionMask.Left)
            };
            var after = new Dictionary<long, EntitySnapshot>
            {
                [206] = StateSnapshot(206, 2, 0, Direction.Right, DirectionMask.Right),
                [207] = StateSnapshot(207, 3, 0, Direction.Right, DirectionMask.Left)
            };
            ClientPresentationFact fact = ClientPresentationFactTranslator.Translate(new[]
            {
                BodyMovedFact(61, 8801, Direction.Right, new[]
                {
                    BodyMovedMember(206, new Vector2Int(1, 0), new Vector2Int(2, 0), Direction.Right, Direction.Right, DirectionMask.Right, DirectionMask.Right),
                    BodyMovedMember(207, new Vector2Int(2, 0), new Vector2Int(3, 0), Direction.Right, Direction.Right, DirectionMask.Left, DirectionMask.Left)
                })
            })[0];

            ClientPresentationPlaybackPlan plan = planner.Plan(61, before, after, new[] { fact });

            Assert.AreEqual(1, plan.BehaviorPlans.Count);
            Assert.AreEqual(0, plan.BehaviorPlans[0].EntityTracks.Count);
            Assert.AreEqual(1, plan.BehaviorPlans[0].GroupTracks.Count);
            Assert.AreEqual(RigidBodyGroupMotionKind.Translate, plan.BehaviorPlans[0].GroupTracks[0].MotionKind);
            CollectionAssert.AreEquivalent(new[] { 206L, 207L }, plan.BehaviorPlans[0].GroupTracks[0].Members.Select(item => item.EntityId).ToArray());
        }

        [Test]
        public void BodyMovedDeltaQueuesTranslateGroupForEntireConnectedBody()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(506, 1, 0, Direction.Right, DirectionMask.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(507, 2, 0, Direction.Right, DirectionMask.Left));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(62, new[]
            {
                State(506, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(507, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 3, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Left)
            }, new List<long>(), new[]
            {
                BodyMovedFact(62, 62001, Direction.Right, new[]
                {
                    BodyMovedMember(506, new Vector2Int(1, 0), new Vector2Int(2, 0), Direction.Right, Direction.Right, DirectionMask.Right, DirectionMask.Right),
                    BodyMovedMember(507, new Vector2Int(2, 0), new Vector2Int(3, 0), Direction.Right, Direction.Right, DirectionMask.Left, DirectionMask.Left)
                })
            });

            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeueTranslateGroup(out TranslateGroupPlaybackPlan plan));
            CollectionAssert.AreEquivalent(new[] { 506L, 507L }, plan.Members.Select(item => item.EntityId).ToArray());
            Assert.AreEqual(Direction.Right, plan.Direction);
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
                Fact(6, 22, PresentationFactType.EntityPushed),
                Fact(7, 22, PresentationFactType.EntityPushed)
            });
            InvokePrivate(visuals, "LateUpdate");

            object animations = GetPrivateField(visuals, "activeAnimations");
            Assert.IsTrue((bool)animations.GetType().GetMethod("ContainsKey").Invoke(animations, new object[] { 6L }));
            Assert.IsTrue((bool)animations.GetType().GetMethod("ContainsKey").Invoke(animations, new object[] { 7L }));
            Assert.IsNotNull(visualObject.transform.Find("Entities/Entity_6"));
            Assert.IsNotNull(visualObject.transform.Find("Entities/Entity_7"));
        }

        [Test]
        public void MechanismPushFactGeneratesFeedbackWhenEntityDoesNotMove()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 5 }, DefaultWorldConfig.PlayerSpawn(5, 5, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(14, new[]
            {
                State(5, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 0, 0, true)
            }, new List<long>(), new[] { Fact(5, 14, PresentationFactType.EntityPushed, Direction.Right) });

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
        public void SourcePushFeedbackPlaysWhenSourceDoesNotMoveAndPushesAnotherEntity()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 501 }, DefaultWorldConfig.PlayerSpawn(501, 501, new GridCoord(0, 0)));
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 502 }, DefaultWorldConfig.PushableBlockerSpawn(502, new GridCoord(1, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            G2C_PresentationFact sourceFeedback = Fact(501, 24, PresentationFactType.EntityPushed, Direction.Right);
            sourceFeedback.SourceActionId = 24001;
            G2C_PresentationFact pushedEntity = Fact(502, 24, PresentationFactType.EntityPushed, Direction.Right);
            pushedEntity.SourceActionId = 24002;
            ClientMoveNetworkRuntime.ApplyWorldDelta(24, new[]
            {
                State(501, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 0, 0, true),
                State(502, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true)
            }, new List<long>(), new[] { sourceFeedback, pushedEntity });

            var events = new List<ClientAnimationEvent>();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                events.Add(animationEvent);
            }

            ClientAnimationEvent feedback = events.Single(item => item.EntityId == 501);
            Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, feedback.MotionKind);
            Assert.IsFalse(feedback.IsMovement);
            Assert.IsTrue(feedback.IsImpulse);
            Assert.AreEqual(Direction.Right, feedback.ImpulseDirection);
            Assert.IsTrue(events.Any(item => item.EntityId == 502 && item.IsMovement));
        }

        [Test]
        public void NoOpBodyMovedDeltaQueuesGroupFeedbackWithSourceFeedback()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(511, 0, 0, Direction.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(512, 1, 0, Direction.Right, DirectionMask.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(513, 2, 0, Direction.Right, DirectionMask.Left));
            ClientMoveNetworkRuntime.SetRunner(runner);

            G2C_PresentationFact sourceFeedback = Fact(511, 64, PresentationFactType.EntityPushed, Direction.Right);
            sourceFeedback.SourceActionId = 64001;
            ClientMoveNetworkRuntime.ApplyWorldDelta(64, new[]
            {
                State(511, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 0, 0, true),
                State(512, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(513, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Left)
            }, new List<long>(), new[]
            {
                sourceFeedback,
                BodyMovedFact(64, 64002, Direction.Right, new[]
                {
                    BodyMovedMember(512, new Vector2Int(1, 0), new Vector2Int(1, 0), Direction.Right, Direction.Right, DirectionMask.Right, DirectionMask.Right),
                    BodyMovedMember(513, new Vector2Int(2, 0), new Vector2Int(2, 0), Direction.Right, Direction.Right, DirectionMask.Left, DirectionMask.Left)
                })
            });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent feedback));
            Assert.AreEqual(511, feedback.EntityId);
            Assert.IsTrue(feedback.IsImpulse);
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeueTranslateGroup(out TranslateGroupPlaybackPlan plan));
            CollectionAssert.AreEquivalent(new[] { 512L, 513L }, plan.Members.Select(item => item.EntityId).ToArray());
            Assert.IsTrue(plan.Members.All(item => item.FromCoord == item.ToCoord));
        }

        [Test]
        public void NoOpBodyMovedDeltaKeepsTailEntityPushAnimation()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(522, 1, 0, Direction.Right, DirectionMask.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(523, 2, 0, Direction.Right, DirectionMask.Left));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(524, 3, 0, Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);

            G2C_PresentationFact tailMove = Fact(524, 66, PresentationFactType.EntityPushed, Direction.Right);
            tailMove.SourceActionId = 66002;
            ClientMoveNetworkRuntime.ApplyWorldDelta(66, new[]
            {
                State(522, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(523, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Left),
                State(524, DefaultWorldConfig.PushableBlockerConfigId, DefaultWorldConfig.PushableBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 4, 0, false, pushable: true)
            }, new List<long>(), new[]
            {
                BodyMovedFact(66, 66001, Direction.Right, new[]
                {
                    BodyMovedMember(522, new Vector2Int(1, 0), new Vector2Int(1, 0), Direction.Right, Direction.Right, DirectionMask.Right, DirectionMask.Right),
                    BodyMovedMember(523, new Vector2Int(2, 0), new Vector2Int(2, 0), Direction.Right, Direction.Right, DirectionMask.Left, DirectionMask.Left)
                }),
                tailMove
            });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent tailAnimation));
            Assert.AreEqual(524, tailAnimation.EntityId);
            Assert.IsTrue(tailAnimation.IsMovement);
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeueTranslateGroup(out TranslateGroupPlaybackPlan plan));
            CollectionAssert.AreEquivalent(new[] { 522L, 523L }, plan.Members.Select(item => item.EntityId).ToArray());
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
            }, new List<long>(), new[] { Fact(8, 31, PresentationFactType.EntityPushed, Direction.Right) });
            ClientMoveNetworkRuntime.ApplyWorldDelta(32, new[]
            {
                State(9, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(10, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 3, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right)
            }, new List<long>(), new[]
            {
                Fact(9, 32, PresentationFactType.EntityPushed, Direction.Right),
                Fact(10, 32, PresentationFactType.EntityPushed, Direction.Right)
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
            }, new List<long>(), new[] { Fact(11, 33, PresentationFactType.EntityPushed, Direction.Right) });

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
        public void RotatePivotFactGeneratesSingleGroupPlaybackPlan()
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
                RotateGroupFact(40, PresentationFactType.RotatePivotGroup, 101, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(101, new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    RotateMember(102, new Vector2Int(0, 1), new Vector2Int(1, 0))
                })
            });

            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeueRotateGroup(out RotatePivotGroupPlaybackPlan groupPlan));
            Assert.AreEqual(2, groupPlan.Members.Count);
            Assert.AreEqual(new Vector2Int(0, 0), groupPlan.Members[0].FromCoord);
            Assert.AreEqual(new Vector2Int(0, 0), groupPlan.Members[0].ToCoord);
            Assert.AreEqual(new Vector2Int(0, 1), groupPlan.Members[1].FromCoord);
            Assert.AreEqual(new Vector2Int(1, 0), groupPlan.Members[1].ToCoord);
            Assert.AreEqual(RotatePivotDirection.Clockwise, groupPlan.RotateDirection);
            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeueRotateGroup(out _));
        }

        [Test]
        public void RotatePivotFactGeneratesSingleBehaviorGroupTrack()
        {
            var planner = new ClientPresentationPlaybackPlanner(ClientAnimationStyleProvider.Fallback());
            var before = new Dictionary<long, EntitySnapshot>
            {
                [201] = StateSnapshot(201, 0, 0, Direction.Right),
                [202] = StateSnapshot(202, 0, 1, Direction.Up)
            };
            var after = new Dictionary<long, EntitySnapshot>
            {
                [201] = StateSnapshot(201, 0, 0, Direction.Right),
                [202] = StateSnapshot(202, 1, 0, Direction.Up)
            };
            ClientPresentationFact fact = ClientPresentationFactTranslator.Translate(new[]
            {
                RotateGroupFact(52, PresentationFactType.RotatePivotGroup, 201, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(201, new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    RotateMember(202, new Vector2Int(0, 1), new Vector2Int(1, 0))
                }, 520, 523)
            })[0];

            ClientPresentationPlaybackPlan plan = planner.Plan(52, before, after, new[] { fact });

            Assert.AreEqual(1, plan.BehaviorPlans.Count);
            Assert.AreEqual(0, plan.BehaviorPlans[0].EntityTracks.Count);
            Assert.AreEqual(1, plan.BehaviorPlans[0].GroupTracks.Count);
            Assert.AreEqual(RigidBodyGroupMotionKind.RotateAroundPivot, plan.BehaviorPlans[0].GroupTracks[0].MotionKind);
            Assert.AreEqual(520, plan.BehaviorPlans[0].StartTick);
            Assert.AreEqual(523, plan.BehaviorPlans[0].EndTick);
        }

        [Test]
        public void BehaviorPlansMergeBySourceActionAndTickWindow()
        {
            var planner = new ClientPresentationPlaybackPlanner(ClientAnimationStyleProvider.Fallback());
            var before = new Dictionary<long, EntitySnapshot>
            {
                [203] = StateSnapshot(203, 0, 0, Direction.Right),
                [204] = StateSnapshot(204, 2, 0, Direction.Right)
            };
            var after = new Dictionary<long, EntitySnapshot>
            {
                [203] = StateSnapshot(203, 1, 0, Direction.Right),
                [204] = StateSnapshot(204, 3, 0, Direction.Right)
            };
            G2C_PresentationFact first = Fact(203, 53, PresentationFactType.EntityPushed);
            G2C_PresentationFact second = Fact(204, 53, PresentationFactType.EntityPushed);
            first.SourceActionId = 7001;
            second.SourceActionId = 7001;
            first.StartTick = 10;
            second.StartTick = 10;
            first.EndTick = 12;
            second.EndTick = 12;

            ClientPresentationPlaybackPlan plan = planner.Plan(53, before, after, ClientPresentationFactTranslator.Translate(new[] { first, second }));

            Assert.AreEqual(1, plan.BehaviorPlans.Count);
            Assert.AreEqual(2, plan.BehaviorPlans[0].EntityTracks.Count);
            CollectionAssert.AreEquivalent(new[] { 203L, 204L }, plan.BehaviorPlans[0].EntityTracks.Select(item => item.EntityId).ToArray());
        }

        [Test]
        public void BlockedRotateFactGeneratesBounceAndImpactPlayback()
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
                RotateGroupFact(41, PresentationFactType.RotatePivotGroup, 111, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, true, new[]
                {
                    RotateMember(111, new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    RotateMember(112, new Vector2Int(0, 1), new Vector2Int(1, 0))
                }),
                RotateFact(113, 111, 41, new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), RotatePivotDirection.Clockwise, false, Direction.Right)
            });

            var events = new List<ClientAnimationEvent>();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                events.Add(animationEvent);
            }

            Assert.AreEqual(1, events.Count);
            Assert.IsTrue(events.Any(item => item.EntityId == 113 && item.MotionKind == ClientAnimationMotionKind.MechanismPush && item.StyleId == "pivot.impact" && item.ImpulseDirection == Direction.Right));
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeueRotateGroup(out RotatePivotGroupPlaybackPlan groupPlan));
            Assert.IsTrue(groupPlan.Bounce);
            Assert.AreEqual(2, groupPlan.Members.Count);
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
                RotateGroupFact(42, PresentationFactType.RotatePivotGroup, 121, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(121, new Vector2Int(0, 1), new Vector2Int(1, 0))
                })
            });
            InvokePrivate(visuals, "LateUpdate");
            SetRotateGroupProgress(visuals, 0.5f);
            InvokePrivate(visuals, "LateUpdate");
            Transform view = FindEntityView(visualObject, 121);
            Assert.IsNotNull(view);
            Vector3 groupSpacePosition = visualObject.transform.InverseTransformPoint(view.position);
            Assert.Greater(groupSpacePosition.x, 0.5f);
            Assert.Greater(groupSpacePosition.y, 0.5f);
            Assert.Less(groupSpacePosition.y, 1.5f);

            ClientMoveNetworkRuntime.ApplyWorldDelta(43, new[]
            {
                State(121, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 1, false, pushable: true, portLocalPorts: (int)DirectionMask.Up)
            }, new List<long>(), new[]
            {
                RotateGroupFact(43, PresentationFactType.RotatePivotGroup, 121, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, true, new[]
                {
                    RotateMember(121, new Vector2Int(0, 1), new Vector2Int(1, 0))
                })
            });
            InvokePrivate(visuals, "LateUpdate");
            SetRotateGroupProgress(visuals, 0.5f);
            InvokePrivate(visuals, "LateUpdate");
            groupSpacePosition = visualObject.transform.InverseTransformPoint(view.position);
            Assert.Greater(groupSpacePosition.x, 0.5f);
            Assert.Less(groupSpacePosition.y, 1.5f);

            SetRotateGroupProgress(visuals, 1f);
            InvokePrivate(visuals, "LateUpdate");
            Assert.AreEqual(new Vector3(0.5f, 1.5f, -0.1f), view.localPosition);
        }

        [Test]
        public void RotatePivotBounceRestoresToAuthoritativeSnapshotNotFactTarget()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(123, 0, 1, Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(431, new[]
            {
                State(123, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 1, false, pushable: true, portLocalPorts: (int)DirectionMask.Up)
            }, new List<long>(), new[]
            {
                RotateGroupFact(431, PresentationFactType.RotatePivotGroup, 123, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, true, new[]
                {
                    RotateMember(123, new Vector2Int(0, 1), new Vector2Int(1, 0))
                })
            });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeueRotateGroup(out RotatePivotGroupPlaybackPlan groupPlan));
            Assert.AreEqual(new Vector2Int(0, 1), groupPlan.Members[0].ToCoord);

            runner.Context.AnimationLayer.Clear();
            ClientMoveNetworkRuntime.ApplyWorldDelta(432, new[]
            {
                State(123, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 1, false, pushable: true, portLocalPorts: (int)DirectionMask.Up)
            }, new List<long>(), new[]
            {
                RotateGroupFact(432, PresentationFactType.RotatePivotGroup, 123, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, true, new[]
                {
                    RotateMember(123, new Vector2Int(0, 1), new Vector2Int(1, 0))
                })
            });
            InvokePrivate(visuals, "LateUpdate");
            SetRotateGroupProgress(visuals, 1f);
            InvokePrivate(visuals, "LateUpdate");

            Transform view = FindEntityView(visualObject, 123);
            Assert.IsNotNull(view);
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
                RotateGroupFact(44, PresentationFactType.RotatePivotGroup, 131, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(131, new Vector2Int(0, 0), new Vector2Int(0, 0), fromDirection: Direction.Right, fromPortLocalPorts: DirectionMask.Up | DirectionMask.Right),
                    RotateMember(132, new Vector2Int(0, 1), new Vector2Int(1, 0), fromDirection: Direction.Right, fromPortLocalPorts: DirectionMask.Down),
                    RotateMember(133, new Vector2Int(1, 0), new Vector2Int(0, -1), fromDirection: Direction.Right, fromPortLocalPorts: DirectionMask.Left)
                }),
                Fact(131, 44, PresentationFactType.EntityPushed),
                Fact(132, 44, PresentationFactType.EntityPushed),
                Fact(133, 44, PresentationFactType.EntityPushed)
            });
            InvokePrivate(visuals, "LateUpdate");
            SetRotateGroupProgress(visuals, 0.5f);
            InvokePrivate(visuals, "LateUpdate");

            Transform entityRoot = visualObject.transform.Find("Entities");
            Transform groupRoot = visualObject.transform.Find("RotatePivotGroups").GetChild(0);
            Transform pivot = groupRoot.Find("Entity_131");
            Transform upper = groupRoot.Find("Entity_132");
            Transform right = groupRoot.Find("Entity_133");
            Assert.IsNotNull(pivot);
            Assert.IsNotNull(upper);
            Assert.IsNotNull(right);
            Assert.AreEqual(groupRoot, pivot.parent);
            Assert.AreEqual(groupRoot, upper.parent);
            Assert.AreEqual(groupRoot, right.parent);
            float pivotAngle = SignedAngle(groupRoot.localRotation.eulerAngles.z);
            Assert.Greater(Mathf.Abs(pivotAngle), 0.0001f);
            AssertRotatedPosition(WorldLocal(visualObject, upper), new Vector2Int(0, 1), new Vector2Int(0, 0), pivotAngle);
            AssertRotatedPosition(WorldLocal(visualObject, right), new Vector2Int(1, 0), new Vector2Int(0, 0), pivotAngle);
            AssertRotatedOffset(WorldLocal(visualObject, pivot), WorldLocal(visualObject, upper), new Vector3(0f, 1f, 0f), pivotAngle);
            AssertRotatedOffset(WorldLocal(visualObject, pivot), WorldLocal(visualObject, right), new Vector3(1f, 0f, 0f), pivotAngle);
            Assert.That(Vector3.Distance(WorldLocal(visualObject, upper), WorldLocal(visualObject, right)), Is.EqualTo(Mathf.Sqrt(2f)).Within(0.0001f));
            AssertPortLineLocal(upper.GetComponent<LineRenderer>(), true);
            AssertPortLineLocal(right.GetComponent<LineRenderer>(), false);

            Transform lineRoot = visualObject.transform.Find("PortConnections");
            Assert.IsNotNull(lineRoot);
            LineRenderer line = lineRoot.GetComponentInChildren<LineRenderer>();
            Assert.IsNotNull(line);
            Vector3 lineStart = line.GetPosition(0);
            Vector3 lineEnd = line.GetPosition(1);
            Assert.IsTrue(ApproximatelyAny(lineStart, WorldLocal(visualObject, pivot), WorldLocal(visualObject, upper), WorldLocal(visualObject, right)));
            Assert.IsTrue(ApproximatelyAny(lineEnd, WorldLocal(visualObject, pivot), WorldLocal(visualObject, upper), WorldLocal(visualObject, right)));
        }

        [Test]
        public void BodyMovedPlaybackTranslatesMembersAndLinesTogether()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(216, 1, 0, Direction.Right, DirectionMask.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(217, 2, 0, Direction.Right, DirectionMask.Left));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(62, new[]
            {
                State(216, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(217, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 3, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Left)
            }, new List<long>(), new[]
            {
                BodyMovedFact(62, 8802, Direction.Right, new[]
                {
                    BodyMovedMember(216, new Vector2Int(1, 0), new Vector2Int(2, 0), Direction.Right, Direction.Right, DirectionMask.Right, DirectionMask.Right),
                    BodyMovedMember(217, new Vector2Int(2, 0), new Vector2Int(3, 0), Direction.Right, Direction.Right, DirectionMask.Left, DirectionMask.Left)
                })
            });
            InvokePrivate(visuals, "LateUpdate");
            SetRotateGroupProgress(visuals, 0.5f);
            InvokePrivate(visuals, "LateUpdate");

            Transform first = FindEntityView(visualObject, 216);
            Transform second = FindEntityView(visualObject, 217);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.That(WorldLocal(visualObject, first).x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(WorldLocal(visualObject, second).x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(Vector3.Distance(WorldLocal(visualObject, first), WorldLocal(visualObject, second)), Is.EqualTo(1f).Within(0.0001f));

            Transform lineRoot = visualObject.transform.Find("PortConnections");
            Assert.IsNotNull(lineRoot);
            LineRenderer line = lineRoot.GetComponentInChildren<LineRenderer>();
            Assert.IsNotNull(line);
            Assert.IsTrue(ApproximatelyAny(line.GetPosition(0), WorldLocal(visualObject, first), WorldLocal(visualObject, second)));
            Assert.IsTrue(ApproximatelyAny(line.GetPosition(1), WorldLocal(visualObject, first), WorldLocal(visualObject, second)));

            object animations = GetPrivateField(visuals, "activeAnimations");
            Assert.IsFalse((bool)animations.GetType().GetMethod("ContainsKey").Invoke(animations, new object[] { 216L }));
            Assert.IsFalse((bool)animations.GetType().GetMethod("ContainsKey").Invoke(animations, new object[] { 217L }));
        }

        [Test]
        public void NoOpBodyMovedPlaybackUsesPushProbeMotion()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(226, 1, 0, Direction.Right, DirectionMask.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(227, 2, 0, Direction.Right, DirectionMask.Left));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(65, new[]
            {
                State(226, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Right),
                State(227, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Left)
            }, new List<long>(), new[]
            {
                BodyMovedFact(65, 8803, Direction.Right, new[]
                {
                    BodyMovedMember(226, new Vector2Int(1, 0), new Vector2Int(1, 0), Direction.Right, Direction.Right, DirectionMask.Right, DirectionMask.Right),
                    BodyMovedMember(227, new Vector2Int(2, 0), new Vector2Int(2, 0), Direction.Right, Direction.Right, DirectionMask.Left, DirectionMask.Left)
                })
            });
            InvokePrivate(visuals, "LateUpdate");
            SetRotateGroupProgress(visuals, 0.5f);
            InvokePrivate(visuals, "LateUpdate");

            Transform first = FindEntityView(visualObject, 226);
            Transform second = FindEntityView(visualObject, 227);
            Assert.That(WorldLocal(visualObject, first).x, Is.GreaterThan(1.5f));
            Assert.That(WorldLocal(visualObject, second).x, Is.GreaterThan(2.5f));

            SetRotateGroupProgress(visuals, 1f);
            InvokePrivate(visuals, "LateUpdate");
            Assert.That(WorldLocal(visualObject, first).x, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(WorldLocal(visualObject, second).x, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void RotatePivotGroupSuppressesOrdinaryMemberPresentationInSameDelta()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(135, 0, 0, Direction.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(136, 0, 1, Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(445, new[]
            {
                State(135, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, false, pushable: true),
                State(136, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true)
            }, new List<long>(), new[]
            {
                RotateGroupFact(445, PresentationFactType.RotatePivotGroup, 135, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(135, new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    RotateMember(136, new Vector2Int(0, 1), new Vector2Int(1, 0))
                }),
                Fact(135, 445, PresentationFactType.EntityPushed),
                Fact(136, 445, PresentationFactType.EntityPushed)
            });

            var events = new List<ClientAnimationEvent>();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                events.Add(animationEvent);
            }

            Assert.AreEqual(0, events.Count);
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeueRotateGroup(out RotatePivotGroupPlaybackPlan groupPlan));
            CollectionAssert.AreEquivalent(new[] { 135L, 136L }, groupPlan.Members.Select(item => item.EntityId).ToArray());
        }

        [Test]
        public void ActiveGroupOwnershipSuppressesLaterOrdinaryAnimation()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(137, 0, 1, Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(446, new[]
            {
                State(137, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true)
            }, new List<long>(), new[]
            {
                RotateGroupFact(446, PresentationFactType.RotatePivotGroup, 137, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(137, new Vector2Int(0, 1), new Vector2Int(1, 0))
                }, 446, 456)
            });
            InvokePrivate(visuals, "LateUpdate");

            ClientMoveNetworkRuntime.ApplyWorldDelta(447, new[]
            {
                State(137, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 0, false, pushable: true)
            }, new List<long>(), new[] { Fact(137, 447, PresentationFactType.EntityPushed) });
            InvokePrivate(visuals, "LateUpdate");

            object animations = GetPrivateField(visuals, "activeAnimations");
            Assert.IsFalse((bool)animations.GetType().GetMethod("ContainsKey").Invoke(animations, new object[] { 137L }));
        }

        [Test]
        public void RotatePivotPayloadTicksOverridePlaybackDuration()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(141, 0, 0, Direction.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(142, 0, 1, Direction.Up));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(45, new[]
            {
                State(141, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, false, pushable: true),
                State(142, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true)
            }, new List<long>(), new[]
            {
                RotateGroupFact(45, PresentationFactType.RotatePivotGroup, 141, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(141, new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    RotateMember(142, new Vector2Int(0, 1), new Vector2Int(1, 0))
                }, 45, 48)
            });

            var durations = new List<float>();
            while (runner.Context.AnimationLayer.TryDequeueRotateGroup(out RotatePivotGroupPlaybackPlan groupPlan))
            {
                durations.Add(groupPlan.Style.DurationSeconds);
            }

            Assert.AreEqual(1, durations.Count);
            Assert.That(durations[0], Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void RotatePivotBounceUsesContactTickAsPhaseBoundary()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(143, 0, 1, Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(451, new[]
            {
                State(143, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 1, false, pushable: true, portLocalPorts: (int)DirectionMask.Up)
            }, new List<long>(), new[]
            {
                RotateGroupFact(451, PresentationFactType.RotatePivotGroup, 143, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, true, new[]
                {
                    RotateMember(143, new Vector2Int(0, 1), new Vector2Int(1, 0))
                }, 10, 14, 12)
            });
            InvokePrivate(visuals, "LateUpdate");

            SetRotateGroupProgress(visuals, 0.5f);
            InvokePrivate(visuals, "LateUpdate");
            Transform groupRoot = visualObject.transform.Find("RotatePivotGroups").GetChild(0);
            Assert.That(SignedAngle(groupRoot.localRotation.eulerAngles.z), Is.EqualTo(-90f).Within(0.0001f));

            SetRotateGroupProgress(visuals, 0.75f);
            InvokePrivate(visuals, "LateUpdate");
            Assert.That(SignedAngle(groupRoot.localRotation.eulerAngles.z), Is.EqualTo(-11.25f).Within(0.0001f));
        }

        [Test]
        public void UnknownRotatePivotFactDoesNotFallbackToSharedArcPlayback()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(151, 0, 0, Direction.Right, DirectionMask.Up | DirectionMask.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(152, 0, 1, Direction.Right, DirectionMask.Down));
            ClientMoveNetworkRuntime.SetRunner(runner);
            GameObject visualObject = new GameObject("Visuals");
            var visuals = visualObject.AddComponent<ClientWorldVisuals>();
            SetPrivateField(visuals, "runner", runner);
            InvokePrivate(visuals, "Awake");

            ClientMoveNetworkRuntime.ApplyWorldDelta(46, new[]
            {
                State(151, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, false, pushable: true, portLocalPorts: (int)(DirectionMask.Right | DirectionMask.Down)),
                State(152, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true, portLocalPorts: (int)DirectionMask.Left)
            }, new List<long>(), new[]
            {
                RotateGroupFact(46, PresentationFactType.Unknown, 151, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(151, new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    RotateMember(152, new Vector2Int(0, 1), new Vector2Int(1, 0))
                })
            });
            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
            InvokePrivate(visuals, "LateUpdate");
            Assert.AreEqual(new Vector3(0.5f, 0.5f, -0.1f), visualObject.transform.Find("Entities/Entity_151").localPosition);
            Assert.AreEqual(new Vector3(1.5f, 0.5f, -0.1f), visualObject.transform.Find("Entities/Entity_152").localPosition);
        }

        [Test]
        public void RotatePivotGroupDoesNotInferSubjectsOutsidePayload()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(161, 0, 0, Direction.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(162, 0, 1, Direction.Right));
            runner.Context.ClientMapWorld.ApplySnapshot(StateSnapshot(163, 1, 0, Direction.Right));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(47, new[]
            {
                State(161, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 0, false, pushable: true),
                State(162, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 0, false, pushable: true),
                State(163, DefaultWorldConfig.PortConnectorBlockerConfigId, DefaultWorldConfig.PortConnectorBlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, -1, false, pushable: true)
            }, new List<long>(), new[]
            {
                RotateGroupFact(47, PresentationFactType.RotatePivotGroup, 161, new Vector2Int(0, 0), RotatePivotDirection.Clockwise, false, new[]
                {
                    RotateMember(161, new Vector2Int(0, 0), new Vector2Int(0, 0)),
                    RotateMember(162, new Vector2Int(0, 1), new Vector2Int(1, 0))
                })
            });

            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeueRotateGroup(out RotatePivotGroupPlaybackPlan groupPlan));
            CollectionAssert.AreEquivalent(new[] { 161L, 162L }, groupPlan.Members.Select(item => item.EntityId).ToArray());
            CollectionAssert.DoesNotContain(groupPlan.Members.Select(item => item.EntityId).ToArray(), 163L);
        }

        [Test]
        public void RepeatedPresentationFactIdIsIgnored()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 164 }, DefaultWorldConfig.PlayerSpawn(164, 164, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);
            G2C_PresentationFact presentationFact = Fact(164, 48, PresentationFactType.EntityMoved);
            presentationFact.FactId = 9001;

            ClientMoveNetworkRuntime.ApplyWorldDelta(48, new[]
            {
                State(164, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 1, 0, true)
            }, new List<long>(), new[] { presentationFact });
            ClientMoveNetworkRuntime.ApplyWorldDelta(48, new[]
            {
                State(164, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 2, 0, true)
            }, new List<long>(), new[] { presentationFact });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(164, animationEvent.EntityId);
            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
        }

        [Test]
        public void FactOnlyDeltaWithoutSnapshotStillQueues()
        {
            ClientWorldRunner runner = CreateRunner();
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(49, System.Array.Empty<G2C_WorldEntityState>(), new List<long>(), new[]
            {
                Fact(165, 49, PresentationFactType.EntityPushed, Direction.Up)
            });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent));
            Assert.AreEqual(165, animationEvent.EntityId);
            Assert.AreEqual(ClientAnimationMotionKind.MechanismPush, animationEvent.MotionKind);
            Assert.AreEqual(Direction.Up, animationEvent.ImpulseDirection);
            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
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
                Fact(20, 4, PresentationFactType.EntityMoved),
                Fact(21, 4, PresentationFactType.EntityMoved)
            });

            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent first));
            Assert.IsTrue(runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent second));
            Assert.AreNotEqual(first.StyleId, second.StyleId);
            Assert.IsTrue(first.MotionKind == ClientAnimationMotionKind.AutoMove || second.MotionKind == ClientAnimationMotionKind.AutoMove);
            Assert.IsTrue(first.MotionKind == ClientAnimationMotionKind.DebugDrag || second.MotionKind == ClientAnimationMotionKind.DebugDrag);
        }

        [Test]
        public void SpawnAndRemoveGeneratePlayback()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 30 }, DefaultWorldConfig.BlockerSpawn(30, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(5, new[]
            {
                State(31, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 2, 2, false)
            }, new List<long> { 30 }, new[]
            {
                Fact(31, 5, PresentationFactType.EntitySpawned),
                Fact(30, 5, PresentationFactType.EntityRemoved)
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
        public void RepeatedFinalSnapshotDoesNotGenerateMovePlayback()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 40 }, DefaultWorldConfig.BlockerSpawn(40, new GridCoord(1, 1)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(6, new[]
            {
                State(40, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 1, 1, false)
            }, new List<long>(), new[] { Fact(40, 6, PresentationFactType.EntityMoved) });

            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
        }

        [Test]
        public void MissingFactDoesNotGeneratePresentationPlayback()
        {
            ClientWorldRunner runner = CreateRunner();
            runner.Context.ClientMapWorld.AddEntity(new ClientMapEntity { EntityId = 50 }, DefaultWorldConfig.BlockerSpawn(50, new GridCoord(0, 0)));
            ClientMoveNetworkRuntime.SetRunner(runner);

            ClientMoveNetworkRuntime.ApplyWorldDelta(7, new[]
            {
                State(50, DefaultWorldConfig.BlockerConfigId, DefaultWorldConfig.BlockerArchetypeId, DefaultWorldConfig.BlockerTarget, 0, 1, false)
            }, new List<long>(), new List<G2C_PresentationFact>());

            Assert.IsFalse(runner.Context.AnimationLayer.TryDequeue(out _));
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
            }, new List<long>(), new[] { Fact(60, 9, PresentationFactType.EntityMoved) });

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
            }, new List<long>(), new[] { Fact(70, 11, PresentationFactType.EntityMoved) });
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
            }, new List<long>(), new[] { Fact(75, 15, PresentationFactType.EntityPushed, Direction.Right) });
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
            }, new List<long>(), new[] { Fact(76, 16, PresentationFactType.EntityMoved) });
            ClientMoveNetworkRuntime.ApplyWorldDelta(17, new[]
            {
                State(76, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 2, 0, true)
            }, new List<long>(), new[] { Fact(76, 17, PresentationFactType.EntityMoved) });

            Assert.IsTrue(runner.Context.ClientMapWorld.TryGetPosition(76, out Vector2Int coord));
            Assert.AreEqual(new Vector2Int(2, 0), coord);
        }

        [Test]
        public void ConsecutiveAnimationFactsReplaceActivePlaybackForSameEntity()
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
            }, new List<long>(), new[] { Fact(77, 18, PresentationFactType.EntityMoved) });
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
            }, new List<long>(), new[] { Fact(77, 19, PresentationFactType.EntityMoved) });
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
            }, new List<long>(), new[] { Fact(78, 20, PresentationFactType.EntityMoved) });
            InvokePrivate(visuals, "LateUpdate");

            ClientMoveNetworkRuntime.ApplyWorldDelta(21, new[]
            {
                State(78, DefaultWorldConfig.PlayerConfigId, DefaultWorldConfig.PlayerArchetypeId, DefaultWorldConfig.PlayerTarget, 2, 0, true)
            }, new List<long>(), new[] { Fact(78, 21, PresentationFactType.EntityMoved) });
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
            }, new List<long>(), new[] { Fact(80, 12, PresentationFactType.EntityMoved) });
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
            Assert.IsTrue(provider.TryGet("pivot.impact", out ClientAnimationStyle impactStyle));
            Assert.AreEqual(0.54f, rotateStyle.DurationSeconds);
            Assert.AreEqual(0.54f, bounceStyle.DurationSeconds);
            Assert.AreEqual(0.54f, impactStyle.DurationSeconds);

            ActionSpec playerMove = ActionSpecRegistry.Default.Get("player_move");
            ActionSpec mechanismPush = ActionSpecRegistry.Default.Get("mechanism_push");

            Assert.AreEqual("player_move", playerMove.SpecId.Value);
            Assert.AreEqual("mechanism_push", mechanismPush.SpecId.Value);
            Assert.AreEqual(ActionPrimitive.Move, playerMove.Primitive);
            Assert.AreEqual(ActionPrimitive.Move, mechanismPush.Primitive);
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
            Assert.AreEqual(0.54f, StyleDuration(streaming, "pivot.impact"));
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

        private static G2C_PresentationFact Fact(long entityId, long serverTick, PresentationFactType factType, Direction direction = Direction.None)
        {
            var item = new G2C_PresentationFact
            {
                FactId = serverTick * 1000 + entityId,
                ServerTick = serverTick,
                FactType = (int)factType,
                ResultKind = (int)PresentationFactResultKind.Success,
                SourceEntityId = entityId,
                Direction = (int)direction
            };
            item.SubjectEntityIds.Add(entityId);
            return item;
        }

        private static G2C_PresentationFact RotateFact(long entityId, long serverTick, Vector2Int pivotCoord, Vector2Int fromCoord, Vector2Int toCoord, RotatePivotDirection rotateDirection, bool bounce, Direction direction = Direction.None, Direction fromDirection = Direction.None, DirectionMask fromPortLocalPorts = DirectionMask.None)
            => RotateFact(entityId, 101, serverTick, pivotCoord, fromCoord, toCoord, rotateDirection, bounce, direction, fromDirection, fromPortLocalPorts);

        private static G2C_PresentationFact RotateFact(long entityId, long pivotEntityId, long serverTick, Vector2Int pivotCoord, Vector2Int fromCoord, Vector2Int toCoord, RotatePivotDirection rotateDirection, bool bounce, Direction direction = Direction.None, Direction fromDirection = Direction.None, DirectionMask fromPortLocalPorts = DirectionMask.None)
        {
            var item = new G2C_PresentationFact
            {
                FactId = serverTick * 1000 + entityId,
                ServerTick = serverTick,
                FactType = (int)PresentationFactType.RotatePivotImpact,
                ResultKind = (int)PresentationFactResultKind.Impact,
                SourceEntityId = pivotEntityId,
                FromX = fromCoord.x,
                FromY = fromCoord.y,
                ToX = toCoord.x,
                ToY = toCoord.y,
                Direction = (int)direction,
                PivotEntityId = pivotEntityId,
                PivotX = pivotCoord.x,
                PivotY = pivotCoord.y,
                RotateDirection = (int)rotateDirection
            };
            item.SubjectEntityIds.Add(entityId);
            item.Impacts.Add(new G2C_PresentationFactImpact
            {
                BlockerEntityId = entityId,
                ImpactMemberId = entityId,
                ImpactFromX = fromCoord.x,
                ImpactFromY = fromCoord.y,
                ImpactToX = toCoord.x,
                ImpactToY = toCoord.y,
                PushDirection = (int)direction
            });
            return item;
        }

        private static G2C_PresentationFact RotateGroupFact(long serverTick, PresentationFactType factType, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, bool bounce, IReadOnlyList<RotateMemberSpec> members)
            => RotateGroupFact(serverTick, factType, pivotEntityId, pivotCoord, rotateDirection, bounce, members, serverTick, serverTick);

        private static G2C_PresentationFact RotateGroupFact(long serverTick, PresentationFactType factType, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, bool bounce, IReadOnlyList<RotateMemberSpec> members, long startTick, long endTick)
            => RotateGroupFact(serverTick, factType, pivotEntityId, pivotCoord, rotateDirection, bounce, members, startTick, endTick, 0);

        private static G2C_PresentationFact RotateGroupFact(long serverTick, PresentationFactType factType, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, bool bounce, IReadOnlyList<RotateMemberSpec> members, long startTick, long endTick, long contactTick)
        {
            var item = new G2C_PresentationFact
            {
                FactId = serverTick * 100000 + pivotEntityId,
                ServerTick = serverTick,
                FactType = (int)factType,
                ResultKind = (int)(bounce ? PresentationFactResultKind.Bounce : PresentationFactResultKind.Success),
                SourceEntityId = pivotEntityId,
                FromX = members[0].FromCoord.x,
                FromY = members[0].FromCoord.y,
                ToX = members[0].ToCoord.x,
                ToY = members[0].ToCoord.y,
                StartTick = startTick,
                ContactTick = contactTick,
                EndTick = endTick,
                PivotEntityId = pivotEntityId,
                PivotX = pivotCoord.x,
                PivotY = pivotCoord.y,
                RotateDirection = (int)rotateDirection
            };

            for (int i = 0; i < members.Count; i++)
            {
                RotateMemberSpec member = members[i];
                item.SubjectEntityIds.Add(member.EntityId);
                item.Members.Add(new G2C_PresentationFactMember
                {
                    EntityId = member.EntityId,
                    FromX = member.FromCoord.x,
                    FromY = member.FromCoord.y,
                    ToX = member.ToCoord.x,
                    ToY = member.ToCoord.y,
                    FromDirection = (int)member.FromDirection,
                    ToDirection = (int)member.Direction,
                    FromPortLocalPorts = (int)member.FromPortLocalPorts
                });
            }

            return item;
        }

        private static G2C_PresentationFact BodyMovedFact(long serverTick, long sourceActionId, Direction direction, IReadOnlyList<RotateMemberSpec> members)
        {
            var item = new G2C_PresentationFact
            {
                FactId = serverTick * 100000 + sourceActionId,
                ServerTick = serverTick,
                FactType = (int)PresentationFactType.BodyMoved,
                ResultKind = (int)PresentationFactResultKind.Success,
                SourceActionId = sourceActionId,
                SourceEntityId = members[0].EntityId,
                FromX = members[0].FromCoord.x,
                FromY = members[0].FromCoord.y,
                ToX = members[0].ToCoord.x,
                ToY = members[0].ToCoord.y,
                Direction = (int)direction,
                StartTick = serverTick,
                EndTick = serverTick + 1
            };

            for (int i = 0; i < members.Count; i++)
            {
                RotateMemberSpec member = members[i];
                item.SubjectEntityIds.Add(member.EntityId);
                item.Members.Add(new G2C_PresentationFactMember
                {
                    EntityId = member.EntityId,
                    FromX = member.FromCoord.x,
                    FromY = member.FromCoord.y,
                    ToX = member.ToCoord.x,
                    ToY = member.ToCoord.y,
                    FromDirection = (int)member.FromDirection,
                    ToDirection = (int)member.Direction,
                    FromPortLocalPorts = (int)member.FromPortLocalPorts,
                    ToPortLocalPorts = (int)member.ToPortLocalPorts
                });
            }

            return item;
        }

        private static RotateMemberSpec BodyMovedMember(long entityId, Vector2Int fromCoord, Vector2Int toCoord, Direction direction, Direction fromDirection, DirectionMask fromPortLocalPorts, DirectionMask toPortLocalPorts)
        {
            return new RotateMemberSpec(entityId, fromCoord, toCoord, direction, fromDirection, fromPortLocalPorts, toPortLocalPorts);
        }

        private static RotateMemberSpec RotateMember(long entityId, Vector2Int fromCoord, Vector2Int toCoord, Direction direction = Direction.None, Direction fromDirection = Direction.None, DirectionMask fromPortLocalPorts = DirectionMask.None)
        {
            return new RotateMemberSpec(entityId, fromCoord, toCoord, direction, fromDirection, fromPortLocalPorts, DirectionMask.None);
        }

        private readonly struct RotateMemberSpec
        {
            public RotateMemberSpec(long entityId, Vector2Int fromCoord, Vector2Int toCoord, Direction direction, Direction fromDirection, DirectionMask fromPortLocalPorts, DirectionMask toPortLocalPorts)
            {
                EntityId = entityId;
                FromCoord = fromCoord;
                ToCoord = toCoord;
                Direction = direction;
                FromDirection = fromDirection;
                FromPortLocalPorts = fromPortLocalPorts;
                ToPortLocalPorts = toPortLocalPorts;
            }

            public long EntityId { get; }
            public Vector2Int FromCoord { get; }
            public Vector2Int ToCoord { get; }
            public Direction Direction { get; }
            public Direction FromDirection { get; }
            public DirectionMask FromPortLocalPorts { get; }
            public DirectionMask ToPortLocalPorts { get; }
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

        private static Transform FindEntityView(GameObject root, long entityId)
        {
            Transform direct = root.transform.Find("Entities/Entity_" + entityId);
            if (direct != null)
            {
                return direct;
            }

            Transform groupRoot = root.transform.Find("RotatePivotGroups");
            if (groupRoot == null)
            {
                return null;
            }

            return groupRoot.GetComponentsInChildren<Transform>().FirstOrDefault(item => item.name == "Entity_" + entityId);
        }

        private static Vector3 WorldLocal(GameObject root, Transform view)
        {
            Vector3 position = root.transform.InverseTransformPoint(view.position);
            position.z = -0.1f;
            return position;
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

        private static void AssertRotatedPosition(Vector3 actual, Vector2Int fromCoord, Vector2Int pivotCoord, float angleDegrees)
        {
            Vector3 pivot = CellCenter(pivotCoord);
            Vector3 from = CellCenter(fromCoord);
            Vector3 offset = from - pivot;
            AssertRotatedOffset(pivot, actual, offset, angleDegrees);
        }

        private static Vector3 CellCenter(Vector2Int coord)
            => new(coord.x + 0.5f, coord.y + 0.5f, -0.1f);

        private static void AssertPortLineLocal(LineRenderer line, bool vertical)
        {
            Assert.IsNotNull(line);
            Assert.IsTrue(line.enabled);
            Vector3 start = line.GetPosition(0);
            Vector3 end = line.GetPosition(1);
            if (vertical)
            {
                Assert.That(start.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(end.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.Less(start.y, end.y);
                return;
            }

            Assert.That(start.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(end.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.Less(start.x, end.x);
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

        private static void SetRotateGroupProgress(ClientWorldVisuals visuals, float normalized)
        {
            object scheduler = typeof(ClientWorldVisuals)
                .GetField("playbackScheduler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(visuals);
            object groups = scheduler.GetType()
                .GetProperty("GroupTracks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .GetValue(scheduler);
            object enumerator = groups.GetType().GetMethod("GetEnumerator").Invoke(groups, null);
            bool hasItem = (bool)enumerator.GetType().GetMethod("MoveNext").Invoke(enumerator, null);
            Assert.IsTrue(hasItem);
            object pair = enumerator.GetType().GetProperty("Current").GetValue(enumerator);
            object runtime = pair.GetType().GetProperty("Value").GetValue(pair);
            float duration = (float)runtime.GetType().GetProperty("DurationSeconds").GetValue(runtime);
            runtime.GetType().GetMethod("SetElapsedForTests").Invoke(runtime, new object[] { duration * Mathf.Clamp01(normalized) });
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
