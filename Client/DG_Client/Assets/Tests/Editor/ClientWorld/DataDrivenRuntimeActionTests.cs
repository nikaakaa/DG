using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.GameCore;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class DataDrivenRuntimeActionTests
    {
        [Test]
        public void ActionSpecRegistry_ContainsCurrentRuntimeActions()
        {
            ActionSpecRegistry registry = ActionSpecRegistry.Default;

            AssertSpec(registry, "player_move", ActionPrimitive.Move, ActionSourceKind.Player, ActionTargetRule.TargetCoordOneStep);
            AssertSpec(registry, "auto_move", ActionPrimitive.Move, ActionSourceKind.Auto, ActionTargetRule.DirectionFromComponent);
            AssertSpec(registry, "mechanism_push", ActionPrimitive.Move, ActionSourceKind.Mechanism, ActionTargetRule.DirectionFromRequest);
            AssertSpec(registry, "debug_move", ActionPrimitive.Move, ActionSourceKind.Debug, ActionTargetRule.TargetCoordAny);
            AssertSpec(registry, "debug_spawn", ActionPrimitive.Spawn, ActionSourceKind.Debug, ActionTargetRule.TargetCoordAny);
            AssertSpec(registry, "debug_remove", ActionPrimitive.Remove, ActionSourceKind.Debug, ActionTargetRule.None);
            AssertSpec(registry, "connected_body_move", ActionPrimitive.Move, ActionSourceKind.Mechanism, ActionTargetRule.DirectionFromRequest);
            Assert.AreEqual(ActionSubjectKind.ConnectedBodyIfAny, registry.Get("player_move").SubjectKind);
            Assert.AreEqual(ActionSubjectKind.ConnectedBodyIfAny, registry.Get("player_push").SubjectKind);
            Assert.AreEqual(ActionSubjectKind.ConnectedBodyIfAny, registry.Get("mechanism_push").SubjectKind);
            Assert.AreEqual(ActionSubjectKind.ConnectedBodyIfAny, registry.Get("configured_wind_push").SubjectKind);
            Assert.AreEqual(ActionSubjectKind.ConnectedBodyIfAny, registry.Get("connected_body_move").SubjectKind);
        }

        [Test]
        public void LubanActionSpecRegistry_LoadsCurrentRuntimeActions()
        {
            ActionSpecRegistry registry = LubanActionSpecRegistry.FromDirectory(GameConfigDirectory());

            AssertSpec(registry, "player_move", ActionPrimitive.Move, ActionSourceKind.Player, ActionTargetRule.TargetCoordOneStep);
            AssertSpec(registry, "auto_move", ActionPrimitive.Move, ActionSourceKind.Auto, ActionTargetRule.DirectionFromComponent);
            AssertSpec(registry, "mechanism_push", ActionPrimitive.Move, ActionSourceKind.Mechanism, ActionTargetRule.DirectionFromRequest);
            AssertSpec(registry, "debug_move", ActionPrimitive.Move, ActionSourceKind.Debug, ActionTargetRule.TargetCoordAny);
            AssertSpec(registry, "debug_spawn", ActionPrimitive.Spawn, ActionSourceKind.Debug, ActionTargetRule.TargetCoordAny);
            AssertSpec(registry, "debug_remove", ActionPrimitive.Remove, ActionSourceKind.Debug, ActionTargetRule.None);
            Assert.AreEqual(new ActionSpecId("player_push"), registry.Get("player_move").Handoff.SpecId);
            Assert.AreEqual(new ActionSpecId("mechanism_push"), registry.Get("mechanism_push").Handoff.SpecId);
            Assert.AreEqual(ActionSubjectKind.ConnectedBodyIfAny, registry.Get("mechanism_push").Handoff.SubjectKind);
        }

        [Test]
        public void WorldAction_MapsToActionRequestBySpecId()
        {
            var adapter = new ActionRequestAdapter(ActionSpecRegistry.Default);
            var player = new WorldAction(1, WorldActionPriority.Player, "player_move", 10, new GridCoord(1, 0), Direction.None, 7, 0, 0, 1);
            var auto = new WorldAction(2, WorldActionPriority.Auto, "auto_move", 20, null, Direction.None, 0, 3, 4, 1);
            var mechanism = new WorldAction(3, WorldActionPriority.Mechanism, "mechanism_push", 30, null, Direction.Right, 0, 3, 4, 1);

            ActionRequest playerRequest = adapter.FromWorldAction(player);
            ActionRequest autoRequest = adapter.FromWorldAction(auto);
            ActionRequest mechanismRequest = adapter.FromWorldAction(mechanism);

            Assert.AreEqual(new ActionSpecId("player_move"), playerRequest.SpecId);
            Assert.AreEqual(ActionSourceKind.Player, playerRequest.Source.Kind);
            Assert.AreEqual(new GridCoord(1, 0), playerRequest.Target.TargetCoord.Value);
            Assert.AreEqual(new ActionSpecId("auto_move"), autoRequest.SpecId);
            Assert.AreEqual(ActionSourceKind.Auto, autoRequest.Source.Kind);
            Assert.AreEqual(new ActionSpecId("mechanism_push"), mechanismRequest.SpecId);
            Assert.AreEqual(Direction.Right, mechanismRequest.Target.Direction);
        }

        [Test]
        public void ConfiguredMoveAction_RunsThroughExistingPipeline()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(100, 100, new GridCoord(0, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("configured_wind_push", 100, Direction.Right, world.ServerTick - 1, 1);

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.IsTrue(result.ActionResults[action.ActionId].Success);
            Assert.IsTrue(world.TryGetEntity(100, out GameEntity entity));
            Assert.IsTrue(world.TryGetComponent(entity, out PositionComponent position));
            Assert.AreEqual(new GridCoord(1, 0), position.Coord);
            Assert.AreEqual(new ActionSpecId("configured_wind_push"), action.SpecId);
        }

        [Test]
        public void ConfiguredMoveAction_UsesInjectedRegistry()
        {
            var custom = new ActionSpecRegistry(new[]
            {
                new ActionSpec("custom_slide", ActionPrimitive.Move, ActionSourceKind.Mechanism, WorldActionPriority.Debug, WorldTag.SourceMechanism, WorldTag.AbilityMechanismPush, WorldTag.None, WorldTag.None, ActionTargetRule.DirectionFromRequest, ActionBlockedPolicy.Reject, ActionConflictPolicy.ExclusiveTargetCell, ActionInterruptPolicy.HigherPriorityInterruptsLower, ActionMergePolicy.SameClaim, ActionPlanRule.MoveBody, ActionCommitRule.None)
            });
            var queue = new WorldActionQueue(custom);

            WorldAction action = queue.EnqueueConfiguredMove("custom_slide", 10, Direction.Right, 0, 1);

            Assert.AreEqual(WorldActionPriority.Debug, action.Priority);
            Assert.AreEqual(new ActionSpecId("custom_slide"), action.SpecId);
        }

        [Test]
        public void PlayerMoveConnectedBody_BlockedByExternalTargetDefersOutput()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(110, 110, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(111, new GridCoord(1, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(112, new GridCoord(2, 0))));
            Assert.IsTrue(world.TryGetEntity(110, out GameEntity player));
            world.SetComponent(player, new PortConnectorComponent(DirectionMask.Right));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(110, new GridCoord(1, 0), 1);

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.IsTrue(result.ActionResults[action.ActionId].Success);
            Assert.IsTrue(result.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(1, result.DeferredActions.Count);
            Assert.AreEqual(0, result.ActivePendingCount);
            AssertPosition(world, 110, new GridCoord(0, 0));
            AssertPosition(world, 111, new GridCoord(1, 0));
            AssertPosition(world, 112, new GridCoord(2, 0));
        }

        [Test]
        public void ActionArbiter_BuildsMoveClaimsFromRequestAndSpec()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(200, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(201, new GridCoord(1, 0), Direction.Right)));
            var request = new ActionRequest(
                10,
                "mechanism_push",
                WorldActionPriority.Mechanism,
                new ActionSourceContext(ActionSourceKind.Mechanism, 200, 0, WorldTag.SourceMechanism),
                200,
                new ActionTarget(0, null, Direction.Right),
                default,
                0,
                0,
                0);

            Assert.IsTrue(new ActionArbiter().TryBuildMoveClaims(world, request, Direction.Right, null, out var claims));

            Assert.AreEqual(1, claims.Count);
            Assert.IsTrue(claims.All(claim => claim.ConflictGroup == "movement"));
            Assert.IsTrue(claims.All(claim => claim.Mode == ActionClaimMode.Exclusive));
            Assert.IsTrue(claims.Any(claim => claim.EntityId == 200 && claim.FromCoord == new GridCoord(0, 0) && claim.ToCoord == new GridCoord(1, 0)));
            Assert.IsFalse(claims.Any(claim => claim.EntityId == 201));
        }

        [Test]
        public void ActionArbiter_MergesSameBodySameClaim()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(300, 300, new GridCoord(0, 0))));
            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 300, Direction.Right),
                MoveRequest(2, "mechanism_push", WorldActionPriority.Mechanism, 300, Direction.Right)
            }, 1);

            Assert.AreEqual(1, result.AcceptedActions.Count);
            Assert.AreEqual(1, result.RejectedActions.Count);
            Assert.AreEqual("merged body intent", result.RejectedActions[0].Reason);
        }

        [Test]
        public void ActionArbiter_RejectsSameBodyConflictingClaims()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(310, 310, new GridCoord(0, 0))));
            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 310, Direction.Right),
                MoveRequest(2, "mechanism_push", WorldActionPriority.Mechanism, 310, Direction.Left)
            }, 1);

            Assert.AreEqual(0, result.AcceptedActions.Count);
            Assert.AreEqual(2, result.RejectedActions.Count);
            Assert.IsTrue(result.RejectedActions.All(action => action.Reason == "conflicting body intents"));
        }

        [Test]
        public void PushVectorArbiter_CancelsOppositeDirectionsForSameSubject()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(312, 312, new GridCoord(0, 0))));
            var arbiter = new PushVectorArbiter(ActionSpecRegistry.Default);

            PushVectorCompositionResult result = arbiter.Compose(world, new[]
            {
                MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 312, Direction.Up),
                MoveRequest(2, "mechanism_push", WorldActionPriority.Mechanism, 312, Direction.Down)
            });

            Assert.AreEqual(0, result.Requests.Count);
            Assert.AreEqual(2, result.CancelledRequests.Count);
            Assert.AreEqual(1, result.Metadata.Count);
            Assert.IsTrue(result.Metadata[0].NetVector.IsZero);
            Assert.AreEqual(1, result.Metadata[0].PerDirectionContributionCount[Direction.Up]);
            Assert.AreEqual(1, result.Metadata[0].PerDirectionContributionCount[Direction.Down]);
            Assert.AreEqual(0, result.Metadata[0].Energy);
            Assert.IsTrue(result.Reasons.Contains("push-vector-cancelled"));
        }

        [Test]
        public void PushVectorArbiter_ComposesSameDirectionWithoutIncreasingDistance()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(313, 313, new GridCoord(0, 0))));
            var arbiter = new PushVectorArbiter(ActionSpecRegistry.Default);

            PushVectorCompositionResult result = arbiter.Compose(world, new[]
            {
                DeferredMoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 313, Direction.Up, 2, new[] { 101L, 102L })
            });

            Assert.AreEqual(1, result.Requests.Count);
            Assert.AreEqual(Direction.Up, result.Requests[0].Target.Direction);
            Assert.AreEqual(1, result.Metadata.Count);
            Assert.AreEqual(2, result.Metadata[0].TotalContributionCount);
            Assert.AreEqual(2, result.Metadata[0].PerDirectionContributionCount[Direction.Up]);
            Assert.AreEqual(2, result.Metadata[0].NetVector.Y);
            Assert.AreEqual(1, result.Metadata[0].Path.Count);
            Assert.AreEqual(2, result.Metadata[0].CausalitySamples.Count);
            Assert.AreEqual(0, result.Metadata[0].Energy);
        }

        [Test]
        public void PushVectorArbiter_DoesNotComposeDifferentSubjects()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(314, 314, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(315, 315, new GridCoord(2, 0))));
            var arbiter = new PushVectorArbiter(ActionSpecRegistry.Default);

            PushVectorCompositionResult result = arbiter.Compose(world, new[]
            {
                MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 314, Direction.Up),
                MoveRequest(2, "mechanism_push", WorldActionPriority.Mechanism, 315, Direction.Down)
            });

            Assert.AreEqual(2, result.Requests.Count);
            Assert.AreEqual(2, result.Metadata.Count);
            Assert.IsTrue(result.Metadata.All(item => !item.NetVector.IsZero));
        }

        [Test]
        public void PushVectorArbiter_SplitsDualAxisByXThenY()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(316, 316, new GridCoord(0, 0))));
            var arbiter = new PushVectorArbiter(ActionSpecRegistry.Default);

            PushVectorCompositionResult result = arbiter.Compose(world, new[]
            {
                MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 316, Direction.Right),
                MoveRequest(2, "mechanism_push", WorldActionPriority.Mechanism, 316, Direction.Up)
            });

            Assert.AreEqual(1, result.Requests.Count);
            Assert.AreEqual(Direction.Right, result.Requests[0].Target.Direction);
            Assert.AreEqual(2, result.Metadata[0].Path.Count);
            Assert.AreEqual(Direction.Right, result.Metadata[0].Path[0]);
            Assert.AreEqual(Direction.Up, result.Metadata[0].Path[1]);
            Assert.IsTrue(result.Reasons.Contains("push-vector-path-pending"));
        }

        [Test]
        public void ActionArbiter_PlayerPriorityWinsTargetClaimConflict()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(320, 320, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(321, 321, new GridCoord(2, 0))));
            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(1, "player_push", WorldActionPriority.Player, 320, Direction.Right, 1),
                MoveRequest(2, "mechanism_push", WorldActionPriority.Mechanism, 321, Direction.Left)
            }, 1);

            Assert.AreEqual(1, result.AcceptedActions.Count);
            Assert.AreEqual(1, result.RejectedActions.Count);
            Assert.AreEqual(1, result.AcceptedActions[0].Request.ActionId);
            Assert.AreEqual("target reserved", result.RejectedActions[0].Reason);
        }

        [Test]
        public void ActionArbiter_AutoBounceUsesConfiguredCommitRules()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BallSpawn(330, new GridCoord(0, 0), Direction.Right, 1)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(331, new GridCoord(1, 0))));
            var request = new ActionRequest(
                10,
                "auto_move",
                WorldActionPriority.Auto,
                new ActionSourceContext(ActionSourceKind.Auto, 330, 0, WorldTag.SourceAuto),
                330,
                new ActionTarget(0, null, Direction.None),
                default,
                0,
                0,
                0);

            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[] { request }, 1);

            Assert.AreEqual(0, result.AcceptedActions.Count);
            Assert.AreEqual(1, result.RejectedActions.Count);
            Assert.IsTrue(result.RejectedActions[0].Result.Bounced);
            Assert.AreEqual(Direction.Left, result.RejectedActions[0].Result.FinalDirection);
            Assert.IsTrue(result.CommitProposals.Any(proposal => proposal.Kind == CommitProposalKind.SetAutoMoveTick));
            Assert.IsTrue(result.CommitProposals.Any(proposal => proposal.Kind == CommitProposalKind.SetDirection && proposal.Direction == Direction.Left));
        }

        [Test]
        public void HandoffActionUnit_CreatesNextTargetUnit()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(340, 340, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(341, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(342, new GridCoord(2, 0))));
            var pending = new PendingRuleStateStore();
            PendingActionState state = pending.AddHandoffActionState(MoveRequest(20, "player_move", WorldActionPriority.Player, 340, Direction.Right), 341, Direction.Right, 0, specId: "player_push");
            var request = new ActionRequestAdapter(ActionSpecRegistry.Default).FromPendingActionState(state, 1);

            Assert.AreEqual(ActionSourceKind.Handoff, request.Source.Kind);
            Assert.AreEqual(20, request.DerivedFromUnitId);

            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[] { request }, 1);

            Assert.AreEqual(0, result.AcceptedActions.Count);
            Assert.AreEqual(0, result.RejectedActions.Count);
            Assert.AreEqual(1, result.DerivedActions.Count);
            Assert.AreEqual(ActionResultBranch.Noop, result.DerivedActions[0].Branch);
            Assert.AreEqual("bounded/deferred-output", result.DerivedActions[0].Reason);
            Assert.AreEqual(1, result.DeferredActions.Count);
            Assert.AreEqual(342, result.DeferredActions[0].EntityId);
        }

        [Test]
        public void OrdinaryPushableActionUnit_HandoffsAndOnlyTargetMoves()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(360, 360, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(361, new GridCoord(1, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(360, new GridCoord(1, 0), 1);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult third = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), first.ActionResults[action.ActionId].FinalCoord);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(1, first.DeferredActions.Count);
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            Assert.AreEqual(0, third.ActivePendingCount);
            AssertPosition(world, 360, new GridCoord(0, 0));
            AssertPosition(world, 361, new GridCoord(2, 0));
        }

        [Test]
        public void PlayerConnectedBodyActionUnit_BlockedByExternalTargetHandoffs()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(370, 370, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(371, new GridCoord(1, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(372, new GridCoord(2, 0))));
            Assert.IsTrue(world.TryGetEntity(370, out GameEntity player));
            world.SetComponent(player, new PortConnectorComponent(DirectionMask.Right));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(370, new GridCoord(1, 0), 2);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult third = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult fourth = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult fifth = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), first.ActionResults[action.ActionId].FinalCoord);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(1, first.DeferredActions.Count);
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            Assert.AreEqual(0, third.ActivePendingCount);
            Assert.AreEqual(0, fourth.ActivePendingCount);
            Assert.AreEqual(0, fifth.ActivePendingCount);
            AssertPosition(world, 370, new GridCoord(0, 0));
            AssertPosition(world, 371, new GridCoord(1, 0));
            AssertPosition(world, 372, new GridCoord(3, 0));
        }

        [Test]
        public void ConnectedBodyIfAnyPolicy_MovesAllMembersTogether()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(410, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(411, new GridCoord(1, 0), Direction.Right)));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("mechanism_push", 410, Direction.Right, world.ServerTick - 1, 1);

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.IsTrue(result.ActionResults[action.ActionId].Success);
            AssertPosition(world, 410, new GridCoord(1, 0));
            AssertPosition(world, 411, new GridCoord(2, 0));
        }

        [Test]
        public void ConnectedBodyIfAnyPolicy_BlockedMemberRejectsAllMembers()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(420, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(421, new GridCoord(1, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(422, new GridCoord(2, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("configured_wind_push", 420, Direction.Right, world.ServerTick - 1, 1);

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.IsFalse(result.ActionResults[action.ActionId].Success);
            AssertPosition(world, 420, new GridCoord(0, 0));
            AssertPosition(world, 421, new GridCoord(1, 0));
        }

        [Test]
        public void SubjectSelection_FollowsPolicyNotActionName()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(430, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(431, new GridCoord(1, 0), Direction.Right)));
            world.NextTick();

            ActionArbitrationResult first = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 430, Direction.Right)
            }, world.ServerTick);
            ActionArbitrationResult second = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(2, "configured_wind_push", WorldActionPriority.Mechanism, 430, Direction.Right)
            }, world.ServerTick);

            Assert.AreEqual(1, first.AcceptedActions.Count);
            Assert.AreEqual(1, second.AcceptedActions.Count);
            Assert.AreEqual(ActionSubjectKind.ConnectedBodyIfAny, first.AcceptedActions[0].Spec.SubjectKind);
            Assert.AreEqual(ActionSubjectKind.ConnectedBodyIfAny, second.AcceptedActions[0].Spec.SubjectKind);
            Assert.AreEqual(2, first.AcceptedActions[0].Body.Entities.Count);
            Assert.AreEqual(2, second.AcceptedActions[0].Body.Entities.Count);
            Assert.IsTrue(first.AcceptedActions[0].Claims.Any(claim => claim.EntityId == 430));
            Assert.IsTrue(first.AcceptedActions[0].Claims.Any(claim => claim.EntityId == 431));
            Assert.IsTrue(second.AcceptedActions[0].Claims.Any(claim => claim.EntityId == 430));
            Assert.IsTrue(second.AcceptedActions[0].Claims.Any(claim => claim.EntityId == 431));
        }

        [Test]
        public void LinkedPushableEntry_PushesWholeBody()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(440, 440, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(441, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(442, new GridCoord(2, 0))));
            SetPorts(world, 441, DirectionMask.Right);
            SetPorts(world, 442, DirectionMask.Left);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(440, new GridCoord(1, 0), 5);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult third = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), first.ActionResults[action.ActionId].FinalCoord);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            Assert.AreEqual(0, third.ActivePendingCount);
            AssertPosition(world, 440, new GridCoord(0, 0));
            AssertPosition(world, 441, new GridCoord(2, 0));
            AssertPosition(world, 442, new GridCoord(3, 0));
        }

        [Test]
        public void PushablePlayerEntry_PushesPlayerLinkedBody()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(4410, 4410, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(4411, 4411, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(4412, new GridCoord(2, 0), Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(4411, out GameEntity pushedPlayer));
            world.SetComponent(pushedPlayer, new PushableComponent());
            world.SetComponent(pushedPlayer, new PortConnectorComponent(DirectionMask.Right));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(4410, new GridCoord(1, 0), 6);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), first.ActionResults[action.ActionId].FinalCoord);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            AssertPosition(world, 4410, new GridCoord(0, 0));
            AssertPosition(world, 4411, new GridCoord(2, 0));
            AssertPosition(world, 4412, new GridCoord(3, 0));
        }

        [Test]
        public void LinkedPushableMembers_DoNotSplitIntoOrdinaryPushChain()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(445, 445, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(446, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(447, new GridCoord(2, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(448, new GridCoord(3, 0))));
            SetPorts(world, 446, DirectionMask.Right);
            SetPorts(world, 447, DirectionMask.Left | DirectionMask.Right);
            SetPorts(world, 448, DirectionMask.Left);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(445, new GridCoord(1, 0), 6);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            Assert.AreEqual(0, first.ActivePendingCount);
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult third = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), first.ActionResults[action.ActionId].FinalCoord);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            Assert.AreEqual(0, third.ActivePendingCount);
            AssertPosition(world, 445, new GridCoord(0, 0));
            AssertPosition(world, 446, new GridCoord(2, 0));
            AssertPosition(world, 447, new GridCoord(3, 0));
            AssertPosition(world, 448, new GridCoord(4, 0));
        }

        [Test]
        public void ConnectedBodyMultiContact_CollectsDistinctExternalContacts()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(510, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(511, new GridCoord(0, 1), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(512, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(513, new GridCoord(1, 1))));
            SetPorts(world, 510, DirectionMask.All);
            SetPorts(world, 511, DirectionMask.All);
            Assert.IsTrue(world.TryGetEntity(510, out GameEntity root));
            Assert.IsTrue(new BodyResolver().TryResolve(world, root, out BehaviorBody body, out _));
            var claims = new[]
            {
                new ActionClaim(1, body.BodyId, 510, ActionClaimKind.BodyMove, new GridCoord(0, 0), new GridCoord(1, 0), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism),
                new ActionClaim(1, body.BodyId, 511, ActionClaimKind.BodyMove, new GridCoord(0, 1), new GridCoord(1, 1), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism)
            };

            IReadOnlyList<ExternalPushContact> contacts = new BodyCapabilityResolver().FindExternalPushContacts(world, claims, body);

            Assert.AreEqual(2, contacts.Count);
            Assert.AreEqual(512, contacts[0].BlockerEntityId);
            Assert.AreEqual(513, contacts[1].BlockerEntityId);
        }

        [Test]
        public void ContactSet_ExcludesInternalBodyAndKeepsDistinctContacts()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(520, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(521, new GridCoord(1, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(522, new GridCoord(2, 0))));
            SetPorts(world, 520, DirectionMask.Right);
            SetPorts(world, 521, DirectionMask.Left);
            Assert.IsTrue(world.TryGetEntity(520, out GameEntity root));
            Assert.IsTrue(new BodyResolver().TryResolve(world, root, out BehaviorBody body, out _));
            var claims = new[]
            {
                new ActionClaim(1, body.BodyId, 520, ActionClaimKind.BodyMove, new GridCoord(0, 0), new GridCoord(1, 0), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism),
                new ActionClaim(1, body.BodyId, 521, ActionClaimKind.BodyMove, new GridCoord(1, 0), new GridCoord(2, 0), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism),
                new ActionClaim(1, body.BodyId, 520, ActionClaimKind.BodyMove, new GridCoord(0, 0), new GridCoord(2, 0), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism)
            };

            IReadOnlyList<ExternalPushContact> contacts = new BodyCapabilityResolver().FindExternalPushContacts(world, claims, body);

            Assert.AreEqual(2, contacts.Count);
            Assert.AreEqual(522, contacts[0].BlockerEntityId);
            Assert.AreEqual(522, contacts[1].BlockerEntityId);
        }

        [Test]
        public void MultiContactPush_CreatesTwoDeferredOutputs()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(530, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(531, new GridCoord(0, 1), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(532, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(533, new GridCoord(1, 1))));
            SetPorts(world, 530, DirectionMask.All);
            SetPorts(world, 531, DirectionMask.All);
            AddMechanismAbility(world, 530, 531);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("mechanism_push", 530, Direction.Right, world.ServerTick - 1, 1);

            StateDrivenRuleExecutionResult first = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(2, first.DeferredActions.Count);
            Assert.IsTrue(first.DeferredActions.All(output => output.CausalityId == action.ActionId));
            Assert.IsTrue(first.DeferredActions.All(output => output.Direction == Direction.Right));
        }

        [Test]
        public void MultiContactPush_DeduplicatesChildUnitsByResolvedSubject()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(570, new GridCoord(0, 0), Direction.None)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(571, new GridCoord(0, 1), Direction.None)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(572, new GridCoord(1, 0), Direction.None)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(573, new GridCoord(1, 1), Direction.None)));
            SetPorts(world, 570, DirectionMask.Up | DirectionMask.Down);
            SetPorts(world, 571, DirectionMask.Up | DirectionMask.Down);
            SetPorts(world, 572, DirectionMask.Up | DirectionMask.Down);
            SetPorts(world, 573, DirectionMask.Up | DirectionMask.Down);
            RefreshStaticPorts(world, 570, 571, 572, 573);
            AddMechanismAbility(world, 570, 571);
            Assert.IsTrue(world.TryGetEntity(570, out GameEntity root));
            Assert.IsTrue(new BodyResolver().TryResolve(world, root, out BehaviorBody body, out _));
            var claims = new[]
            {
                new ActionClaim(1, body.BodyId, 570, ActionClaimKind.BodyMove, new GridCoord(0, 0), new GridCoord(1, 0), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism),
                new ActionClaim(1, body.BodyId, 571, ActionClaimKind.BodyMove, new GridCoord(0, 1), new GridCoord(1, 1), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism)
            };
            IReadOnlyList<ExternalPushContact> contacts = new BodyCapabilityResolver().FindExternalPushContacts(world, claims, body);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("mechanism_push", 570, Direction.Right, world.ServerTick - 1, 1);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.AreEqual(2, contacts.Count);
            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsFalse(first.Reasons.Contains("push chain cycle"));
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(1, first.DeferredActions.Count);
            Assert.AreEqual(0, first.ActivePendingCount);
            world.NextTick();
            for (int i = 0; i < first.DeferredActions.Count; i++)
            {
                queue.EnqueueDeferred(first.DeferredActions[i]);
            }
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.AreEqual(0, second.ActivePendingCount);
            AssertPosition(world, 570, new GridCoord(0, 0));
            AssertPosition(world, 571, new GridCoord(0, 1));
            AssertPosition(world, 572, new GridCoord(2, 0));
            AssertPosition(world, 573, new GridCoord(2, 1));
        }

        [Test]
        public void MultiContactPush_MovesLargeDownstreamSubjectAsOneChildUnit()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(580, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(581, new GridCoord(1, 0), Direction.Right)));
            AddMechanismAbility(world, 580, 581);
            for (int i = 0; i < 9; i++)
            {
                long entityId = 590 + i;
                Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(entityId, new GridCoord(i, 1), Direction.Right)));
            }

            Assert.IsTrue(world.TryGetEntity(580, out GameEntity root));
            Assert.IsTrue(new BodyResolver().TryResolve(world, root, out BehaviorBody sourceBody, out _));
            Assert.IsTrue(world.TryGetEntity(590, out GameEntity targetRoot));
            Assert.IsTrue(new BodyResolver().TryResolve(world, targetRoot, out BehaviorBody targetBody, out _));
            Assert.AreEqual(9, targetBody.Entities.Count);
            var claims = new[]
            {
                new ActionClaim(1, sourceBody.BodyId, 580, ActionClaimKind.BodyMove, new GridCoord(0, 0), new GridCoord(0, 1), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism),
                new ActionClaim(1, sourceBody.BodyId, 581, ActionClaimKind.BodyMove, new GridCoord(1, 0), new GridCoord(1, 1), "movement", ActionClaimMode.Exclusive, WorldActionPriority.Mechanism)
            };
            IReadOnlyList<ExternalPushContact> contacts = new BodyCapabilityResolver().FindExternalPushContacts(world, claims, sourceBody);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("mechanism_push", 580, Direction.Up, world.ServerTick - 1, 1);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.AreEqual(2, contacts.Count);
            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(1, first.DeferredActions.Count);
            Assert.AreEqual(0, first.ActivePendingCount);
            world.NextTick();
            for (int i = 0; i < first.DeferredActions.Count; i++)
            {
                queue.EnqueueDeferred(first.DeferredActions[i]);
            }
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            AssertPosition(world, 580, new GridCoord(0, 0));
            AssertPosition(world, 581, new GridCoord(1, 0));
            for (int i = 0; i < 9; i++)
            {
                long entityId = 590 + i;
                AssertPosition(world, entityId, new GridCoord(i, 2));
            }
        }

        [Test]
        public void SplitMiddleContacts_DoNotCreateCycleWhenTheyReachSameDownstreamBody()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(610, 610, new GridCoord(1, -1))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(620, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(621, new GridCoord(1, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(622, new GridCoord(2, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(623, new GridCoord(0, 1), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(624, new GridCoord(2, 1), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(625, new GridCoord(0, 2), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(626, new GridCoord(1, 2), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(627, new GridCoord(2, 2), Direction.Right)));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(610, new GridCoord(1, 0), 33);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult third = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult fourth = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(1, -1), first.ActionResults[action.ActionId].FinalCoord);
            Assert.IsFalse(first.Reasons.Contains("push chain cycle"));
            Assert.IsFalse(second.Reasons.Contains("push chain cycle"));
            Assert.IsFalse(third.Reasons.Contains("push chain cycle"));
            AssertPosition(world, 610, new GridCoord(1, -1));
            AssertPosition(world, 620, new GridCoord(0, 0));
            AssertPosition(world, 621, new GridCoord(1, 0));
            AssertPosition(world, 622, new GridCoord(2, 0));
            AssertPosition(world, 623, new GridCoord(0, 1));
            AssertPosition(world, 624, new GridCoord(2, 1));
            AssertPosition(world, 625, new GridCoord(0, 3));
            AssertPosition(world, 626, new GridCoord(1, 3));
            AssertPosition(world, 627, new GridCoord(2, 3));
        }

        [Test]
        public void SplitMiddleContacts_WithExtraTopMember_DoesNotFailAsPendingDuplicate()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(630, 630, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(631, new GridCoord(1, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(632, new GridCoord(2, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(633, new GridCoord(3, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(634, new GridCoord(4, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(635, new GridCoord(4, 1), Direction.Up)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(636, new GridCoord(4, 2), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(637, new GridCoord(1, 1), Direction.Up)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(638, new GridCoord(1, 2), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(639, new GridCoord(2, 2), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(640, new GridCoord(3, 2), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(641, new GridCoord(2, 3), Direction.Up)));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(630, new GridCoord(1, 0), 34);
            var system = new StateDrivenRuleExecutionSystem();
            var reasons = new List<string>();

            for (int i = 0; i < 8; i++)
            {
                StateDrivenRuleExecutionResult result = i == 0
                    ? TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick))
                    : TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
                reasons.AddRange(result.Reasons);
                if (result.ActionResults.TryGetValue(action.ActionId, out MoveResult moveResult))
                {
                    Assert.IsTrue(moveResult.Success, string.Join(",", reasons));
                    return;
                }

                if (queue.Count == 0)
                {
                    break;
                }

                world.NextTick();
            }

            Assert.Fail(string.Join(",", reasons));
        }

        [Test]
        public void MultiContactPush_AllChildrenSucceedBeforeOwnerCompletes()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(540, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(541, new GridCoord(0, 1), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(542, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(543, new GridCoord(1, 1))));
            SetPorts(world, 540, DirectionMask.All);
            SetPorts(world, 541, DirectionMask.All);
            AddMechanismAbility(world, 540, 541);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("mechanism_push", 540, Direction.Right, world.ServerTick - 1, 1);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult third = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            Assert.AreEqual(0, third.ActivePendingCount);
            AssertPosition(world, 540, new GridCoord(0, 0));
            AssertPosition(world, 541, new GridCoord(0, 1));
            AssertPosition(world, 542, new GridCoord(2, 0));
            AssertPosition(world, 543, new GridCoord(2, 1));
        }

        [Test]
        public void MultiContactPush_DeferredFailureDoesNotFailSource()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(550, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(551, new GridCoord(0, 1), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(552, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(553, new GridCoord(1, 1))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(554, new GridCoord(2, 1))));
            SetPorts(world, 550, DirectionMask.All);
            SetPorts(world, 551, DirectionMask.All);
            AddMechanismAbility(world, 550, 551);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("mechanism_push", 550, Direction.Right, world.ServerTick - 1, 1);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(second.ActionResults.Values.Any(result => !result.Success));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            AssertPosition(world, 552, new GridCoord(2, 0));
            AssertPosition(world, 553, new GridCoord(1, 1));
            AssertPosition(world, 550, new GridCoord(0, 0));
            AssertPosition(world, 551, new GridCoord(0, 1));
        }

        [Test]
        public void MultiContactPush_NonPushableContactRejectsWholeBlockedStep()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(560, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(561, new GridCoord(0, 1), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(562, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(563, new GridCoord(1, 1))));
            SetPorts(world, 560, DirectionMask.All);
            SetPorts(world, 561, DirectionMask.All);
            AddMechanismAbility(world, 560, 561);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueueConfiguredMove("mechanism_push", 560, Direction.Right, world.ServerTick - 1, 1);

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.IsFalse(result.ActionResults[action.ActionId].Success);
            Assert.AreEqual(0, result.ActivePendingCount);
            AssertPosition(world, 560, new GridCoord(0, 0));
            AssertPosition(world, 561, new GridCoord(0, 1));
            AssertPosition(world, 562, new GridCoord(1, 0));
            AssertPosition(world, 563, new GridCoord(1, 1));
        }

        [Test]
        public void LinkedNonPushableEntry_RejectsPush()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(450, 450, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(451, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(452, new GridCoord(2, 0))));
            SetPorts(world, 451, DirectionMask.Right);
            SetPorts(world, 452, DirectionMask.Left);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(450, new GridCoord(1, 0), 6);

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.IsFalse(result.ActionResults[action.ActionId].Success);
            Assert.AreEqual(0, result.ActivePendingCount);
            AssertPosition(world, 450, new GridCoord(0, 0));
            AssertPosition(world, 451, new GridCoord(1, 0));
            AssertPosition(world, 452, new GridCoord(2, 0));
        }

        [Test]
        public void LinkedBody_DoesNotPropagatePushableComponent()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(460, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(461, new GridCoord(1, 0))));
            SetPorts(world, 460, DirectionMask.Right);
            SetPorts(world, 461, DirectionMask.Left);
            Assert.IsTrue(world.TryGetEntity(461, out GameEntity nonPushable));

            Assert.IsTrue(new BodyResolver().TryResolve(world, nonPushable, out BehaviorBody body, out _));

            Assert.AreEqual(BehaviorBodyKind.PortConnected, body.Kind);
            Assert.IsFalse(world.HasComponent<PushableComponent>(nonPushable));
        }

        [Test]
        public void LinkedBody_MovementPermissionRejectsWholeBody()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(470, 470, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(471, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(472, new GridCoord(2, 0))));
            SetPorts(world, 471, DirectionMask.Right);
            SetPorts(world, 472, DirectionMask.Left);
            Assert.IsTrue(world.TryGetEntity(472, out GameEntity blockedMember));
            world.SetComponent(blockedMember, new MovementPermissionComponent(false, true));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(470, new GridCoord(1, 0), 7);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.IsFalse(second.ActionResults.Values.Any(result => result.Success));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            AssertPosition(world, 471, new GridCoord(1, 0));
            AssertPosition(world, 472, new GridCoord(2, 0));
        }

        [Test]
        public void LinkedBody_ExternalBlockerRejectsWholeBody()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(480, 480, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(481, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(482, new GridCoord(2, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(483, new GridCoord(3, 0))));
            SetPorts(world, 481, DirectionMask.Right);
            SetPorts(world, 482, DirectionMask.Left);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(480, new GridCoord(1, 0), 8);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.IsFalse(second.ActionResults.Values.Any(result => result.Success));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            AssertPosition(world, 481, new GridCoord(1, 0));
            AssertPosition(world, 482, new GridCoord(2, 0));
            AssertPosition(world, 483, new GridCoord(3, 0));
        }

        [Test]
        public void PlayerMoveConnectedBodyPolicy_MovesPlayerBodyTogether()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(430, 430, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(431, new GridCoord(1, 0), Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(430, out GameEntity player));
            world.SetComponent(player, new PortConnectorComponent(DirectionMask.Right));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(430, new GridCoord(1, 0), 9);

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);

            Assert.IsTrue(result.ActionResults[action.ActionId].Success);
            Assert.AreEqual(0, result.ActivePendingCount);
            AssertPosition(world, 430, new GridCoord(1, 0));
            AssertPosition(world, 431, new GridCoord(2, 0));
        }

        [Test]
        public void DeferredPushFailure_DoesNotFailSourceAction()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(380, 380, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(381, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(382, new GridCoord(2, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(380, new GridCoord(1, 0), 3);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.IsFalse(second.ActionResults.Values.Any(result => result.Success));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(0, second.ActivePendingCount);
            AssertPosition(world, 380, new GridCoord(0, 0));
            AssertPosition(world, 381, new GridCoord(1, 0));
        }

        [Test]
        public void NestedPushableBlock_DeferredOutputsUntilOnlyTailMoves()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(390, 390, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(391, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(392, new GridCoord(2, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(390, new GridCoord(1, 0), 4);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult third = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult fourth = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult fifth = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(0, second.ActivePendingCount);
            Assert.AreEqual(0, third.ActivePendingCount);
            Assert.AreEqual(0, fourth.ActivePendingCount);
            Assert.AreEqual(0, fifth.ActivePendingCount);
            AssertPosition(world, 390, new GridCoord(0, 0));
            AssertPosition(world, 391, new GridCoord(1, 0));
            AssertPosition(world, 392, new GridCoord(3, 0));
        }

        [Test]
        public void ConnectedBodyChain_DeferredOutputsUntilOnlyTailBodyMoves()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(490, 490, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(491, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(492, new GridCoord(2, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(493, new GridCoord(3, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(494, new GridCoord(4, 0))));
            SetPorts(world, 491, DirectionMask.Right);
            SetPorts(world, 492, DirectionMask.Left);
            SetPorts(world, 493, DirectionMask.Right);
            SetPorts(world, 494, DirectionMask.Left);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(490, new GridCoord(1, 0), 9);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult third = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult fourth = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult fifth = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(new GridCoord(0, 0), first.ActionResults[action.ActionId].FinalCoord);
            Assert.AreEqual(0, third.ActivePendingCount);
            Assert.AreEqual(0, fourth.ActivePendingCount);
            Assert.AreEqual(0, fifth.ActivePendingCount);
            AssertPosition(world, 490, new GridCoord(0, 0));
            AssertPosition(world, 491, new GridCoord(1, 0));
            AssertPosition(world, 492, new GridCoord(2, 0));
            AssertPosition(world, 493, new GridCoord(4, 0));
            AssertPosition(world, 494, new GridCoord(5, 0));
        }

        [Test]
        public void PushBlockedPath_DoesNotCreatePendingChildHandoff()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(720, 720, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(721, new GridCoord(1, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(720, new GridCoord(1, 0), 41);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.AreEqual(1, first.DeferredActions.Count);
            AssertPosition(world, 720, new GridCoord(0, 0));
            AssertPosition(world, 721, new GridCoord(1, 0));
        }

        [Test]
        public void DeferredPushOutput_ReentersQueueAfterCost()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(730, 730, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(731, new GridCoord(1, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(730, new GridCoord(1, 0), 42);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(1, first.DeferredActions.Count);
            Assert.AreEqual(0, second.ActivePendingCount);
            AssertPosition(world, 730, new GridCoord(0, 0));
            AssertPosition(world, 731, new GridCoord(2, 0));
        }

        [Test]
        public void DeferredOutputPolicy_EquivalentSpecsProduceEquivalentOutput()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(740, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(741, new GridCoord(0, 1), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(742, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(743, new GridCoord(1, 1))));
            AddMechanismAbility(world, 740);
            AddMechanismAbility(world, 741);
            world.NextTick();
            var registry = new ActionSpecRegistry(new[]
            {
                new ActionSpec("policy_a", ActionPrimitive.Move, ActionSourceKind.Mechanism, WorldActionPriority.Mechanism, WorldTag.SourceMechanism, WorldTag.AbilityMechanismPush, WorldTag.None, WorldTag.None, ActionTargetRule.DirectionFromRequest, ActionBlockedPolicy.StartPushIfPushable, ActionConflictPolicy.ExclusiveTargetCell, ActionInterruptPolicy.HigherPriorityInterruptsLower, ActionMergePolicy.SameClaim, ActionPlanRule.MoveBody, ActionCommitRule.None, ActionSubjectKind.ConnectedBodyIfAny, new ActionHandoffSpec(ActionHandoffPolicy.Configured, "configured_wind_push", ActionSubjectKind.ConnectedBodyIfAny)),
                new ActionSpec("policy_b", ActionPrimitive.Move, ActionSourceKind.Mechanism, WorldActionPriority.Mechanism, WorldTag.SourceMechanism, WorldTag.AbilityMechanismPush, WorldTag.None, WorldTag.None, ActionTargetRule.DirectionFromRequest, ActionBlockedPolicy.StartPushIfPushable, ActionConflictPolicy.ExclusiveTargetCell, ActionInterruptPolicy.HigherPriorityInterruptsLower, ActionMergePolicy.SameClaim, ActionPlanRule.MoveBody, ActionCommitRule.None, ActionSubjectKind.ConnectedBodyIfAny, new ActionHandoffSpec(ActionHandoffPolicy.Configured, "configured_wind_push", ActionSubjectKind.ConnectedBodyIfAny)),
                ActionSpecRegistry.Default.Get("configured_wind_push")
            });

            StateDrivenRuleExecutionResult first = new StateDrivenRuleExecutionSystem(registry).Tick(world, new[]
            {
                new WorldAction(1, WorldActionPriority.Mechanism, "policy_a", 740, null, Direction.Right, 0, world.ServerTick - 1, world.ServerTick, 1),
                new WorldAction(2, WorldActionPriority.Mechanism, "policy_b", 741, null, Direction.Right, 0, world.ServerTick - 1, world.ServerTick, 1)
            }, world.ServerTick);

            Assert.AreEqual(2, first.DeferredActions.Count);
            Assert.IsTrue(first.DeferredActions.All(output => output.SpecId.Equals(new ActionSpecId("configured_wind_push"))));
            Assert.IsTrue(first.DeferredActions.All(output => output.Direction == Direction.Right));
        }

        [Test]
        public void SurgingPushDevice_WithoutStrengthPolicyEmitsPeriodicDeferredOutput()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(750, 750, new GridCoord(-1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(751, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(752, new GridCoord(1, 0))));
            SetPorts(world, 751, DirectionMask.All);
            AddMechanismAbility(world, 751);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(750, new GridCoord(0, 0), 43);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(1, first.DeferredActions.Count);
            Assert.AreEqual(1, first.DeferredActions[0].CostTicks);
            Assert.AreEqual(first.DeferredActions[0].CreatedTick + 1, first.DeferredActions[0].ReadyTick);
            Assert.AreEqual(0, first.ActivePendingCount);
            Assert.IsFalse(first.Reasons.Contains("push chain cycle"));
            Assert.IsFalse(second.Reasons.Contains("push chain cycle"));
        }

        [Test]
        public void PushSourceAction_DoesNotWaitForDownstreamDeferredResult()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(760, 760, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(761, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(762, new GridCoord(2, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(760, new GridCoord(1, 0), 44);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));
            world.NextTick();
            StateDrivenRuleExecutionResult second = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsTrue(first.ActionResults[action.ActionId].Success);
            Assert.IsTrue(first.Reasons.Contains("bounded/deferred-output"));
            Assert.IsFalse(second.ActionResults.Values.Any(result => result.Success));
            Assert.AreEqual(0, first.ActivePendingCount);
            AssertPosition(world, 760, new GridCoord(0, 0));
            AssertPosition(world, 761, new GridCoord(1, 0));
        }

        [Test]
        public void StateDrivenPushVector_CancelsOppositeSameTickOutput()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(780, 780, new GridCoord(0, 0))));
            world.NextTick();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult result = system.Tick(world, new[]
            {
                new WorldAction(1, WorldActionPriority.Mechanism, "mechanism_push", 780, null, Direction.Up, 0, world.ServerTick - 1, world.ServerTick, 1),
                new WorldAction(2, WorldActionPriority.Mechanism, "mechanism_push", 780, null, Direction.Down, 0, world.ServerTick - 1, world.ServerTick, 1)
            }, world.ServerTick);

            Assert.AreEqual(2, result.ActionResults.Count);
            Assert.IsTrue(result.ActionResults.Values.All(item => !item.Success && item.Reason == "push-vector-cancelled"));
            Assert.IsTrue(result.Reasons.Contains("push-vector-cancelled"));
            AssertPosition(world, 780, new GridCoord(0, 0));
        }

        [Test]
        public void StateDrivenPushVector_ComposedSingleAxisStillUsesCollisionValidation()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(781, 781, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(782, new GridCoord(0, 1))));
            world.NextTick();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult result = system.Tick(world, new[]
            {
                new WorldAction(1, WorldActionPriority.Mechanism, "mechanism_push", 781, null, Direction.Up, 0, world.ServerTick - 1, world.ServerTick, 1),
                new WorldAction(2, WorldActionPriority.Mechanism, "mechanism_push", 781, null, Direction.Up, 0, world.ServerTick - 1, world.ServerTick, 1)
            }, world.ServerTick);

            Assert.AreEqual(2, result.ActionResults.Count);
            Assert.IsFalse(result.ActionResults[1].Success);
            Assert.AreEqual("blocked cell", result.ActionResults[1].Reason);
            Assert.IsFalse(result.ActionResults[2].Success);
            Assert.AreEqual("blocked cell", result.ActionResults[2].Reason);
            AssertPosition(world, 781, new GridCoord(0, 0));
            AssertPosition(world, 782, new GridCoord(0, 1));
        }

        [Test]
        public void StateDrivenPushVector_DualAxisDoesNotJumpPastBlockedStep()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(783, 783, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(784, new GridCoord(1, 0))));
            world.NextTick();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult result = system.Tick(world, new[]
            {
                new WorldAction(1, WorldActionPriority.Mechanism, "mechanism_push", 783, null, Direction.Right, 0, world.ServerTick - 1, world.ServerTick, 1),
                new WorldAction(2, WorldActionPriority.Mechanism, "mechanism_push", 783, null, Direction.Up, 0, world.ServerTick - 1, world.ServerTick, 1)
            }, world.ServerTick);

            Assert.IsTrue(result.ActionResults.ContainsKey(1));
            Assert.IsFalse(result.ActionResults[1].Success);
            AssertPosition(world, 783, new GridCoord(0, 0));
            AssertPosition(world, 784, new GridCoord(1, 0));
        }

        [Test]
        public void DeferredQueue_MergesEquivalentDeferredWithDifferentCausality()
        {
            var queue = new WorldActionQueue();
            var firstDeferred = new DeferredAction("player_push", 800, new[] { 800L }, Direction.Up, 20, 21, 1, 1001, "1001:20:Up:800");
            var secondDeferred = new DeferredAction("player_push", 800, new[] { 800L }, Direction.Up, 19, 21, 1, 1002, "1002:19:Up:800");

            DeferredEnqueueResult first = queue.EnqueueDeferred(firstDeferred);
            DeferredEnqueueResult second = queue.EnqueueDeferred(secondDeferred);
            IReadOnlyList<WorldAction> ready = queue.DrainReady(21);

            Assert.IsTrue(first.Enqueued);
            Assert.IsFalse(second.Enqueued);
            Assert.AreSame(first.Action, second.Action);
            Assert.AreEqual(2, second.ContributionCount);
            Assert.AreEqual(1, ready.Count);
            Assert.AreEqual(2, ready[0].DeferredContributionCount);
            CollectionAssert.Contains(ready[0].DeferredCausalitySamples.ToArray(), 1001);
            CollectionAssert.Contains(ready[0].DeferredCausalitySamples.ToArray(), 1002);
        }

        [Test]
        public void DeferredQueue_DoesNotMergeOppositeDirections()
        {
            var queue = new WorldActionQueue();

            DeferredEnqueueResult up = queue.EnqueueDeferred(new DeferredAction("player_push", 810, new[] { 810L }, Direction.Up, 30, 31, 1, 1101, "up"));
            DeferredEnqueueResult down = queue.EnqueueDeferred(new DeferredAction("player_push", 810, new[] { 810L }, Direction.Down, 30, 31, 1, 1102, "down"));
            IReadOnlyList<WorldAction> ready = queue.DrainReady(31);

            Assert.IsTrue(up.Enqueued);
            Assert.IsTrue(down.Enqueued);
            Assert.AreEqual(2, ready.Count);
            Assert.IsTrue(ready.Any(action => action.Direction == Direction.Up));
            Assert.IsTrue(ready.Any(action => action.Direction == Direction.Down));
        }

        [Test]
        public void DeferredQueue_DoesNotMergeDifferentSubjects()
        {
            var queue = new WorldActionQueue();

            DeferredEnqueueResult leftSubject = queue.EnqueueDeferred(new DeferredAction("player_push", 820, new[] { 820L }, Direction.Up, 40, 41, 1, 1201, "left"));
            DeferredEnqueueResult rightSubject = queue.EnqueueDeferred(new DeferredAction("player_push", 821, new[] { 821L }, Direction.Up, 40, 41, 1, 1202, "right"));
            IReadOnlyList<WorldAction> ready = queue.DrainReady(41);

            Assert.IsTrue(leftSubject.Enqueued);
            Assert.IsTrue(rightSubject.Enqueued);
            Assert.AreEqual(2, ready.Count);
            Assert.IsTrue(ready.Any(action => action.EntityId == 820));
            Assert.IsTrue(ready.Any(action => action.EntityId == 821));
        }

        [Test]
        public void ClosedLoopWithoutExternalOutput_CompletesWithoutDeferredOutput()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(770, 770, new GridCoord(-1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(771, new GridCoord(0, 0))));
            SetPorts(world, 771, DirectionMask.All);
            AddMechanismAbility(world, 771);
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(770, new GridCoord(0, 0), 45);
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = TickAndEnqueueDeferred(system, world, queue, queue.DrainReady(world.ServerTick));

            Assert.IsFalse(first.ActionResults[action.ActionId].Success);
            Assert.AreEqual(0, first.DeferredActions.Count);
            Assert.AreEqual(0, first.ActivePendingCount);
        }

        [Test]
        public void PushChainCycleGuard_RejectsPartialSubjectOverlap()
        {
            var pending = new PendingRuleStateStore();
            PendingActionState state = pending.AddHandoffActionState(MoveRequest(31, "player_move", WorldActionPriority.Player, 700, Direction.Right), 701, Direction.Right, 0, 1, "player_push", new[] { 701L, 702L });

            bool added = state.TryAddChildUnits(MoveRequest(state.ReadyUnit.ActionUnitId, "player_push", WorldActionPriority.Player, 701, Direction.Right, state.StateId), new[]
            {
                new PendingChildUnitRequest(703, "player_push", new[] { 702L, 703L })
            }, Direction.Right, 1, out string reason);

            Assert.IsFalse(added);
            Assert.AreEqual("push chain cycle", reason);
            Assert.AreEqual(PendingRuleStatus.Active, state.Status);
        }

        [Test]
        public void PendingSiblingBatch_UsesFilteredChildrenWhenSubjectAlreadyReady()
        {
            var pending = new PendingRuleStateStore();
            PendingActionState state = pending.AddHandoffActionState(MoveRequest(41, "player_move", WorldActionPriority.Player, 610, Direction.Right), 611, Direction.Right, 0, 1, "player_push", new[] { 611L, 612L });

            bool added = state.TryAddSiblingUnitsToReadyBatch(new[]
            {
                new PendingChildUnitRequest(611, "player_push", new[] { 611L, 612L }),
                new PendingChildUnitRequest(612, "player_push", new[] { 611L, 612L })
            }, Direction.Right, 1, out string reason);

            Assert.IsTrue(added, reason);
            Assert.AreEqual(1, state.Units.Count);
            Assert.AreEqual(0, state.PushContactBatches.Count);
        }

        [Test]
        public void RulePlanner_CreatesPlanFromAcceptedClaims()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(350, new GridCoord(0, 0), Direction.Right)));
            ActionArbitrationResult arbitration = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 350, Direction.Right)
            }, 1);

            Assert.AreEqual(1, arbitration.AcceptedActions.Count);
            Assert.IsTrue(new RulePlanner().TryPlanMove(world, arbitration.AcceptedActions[0], out MovePlan plan, out PlanResult result));
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, plan.Members.Count);
            Assert.IsTrue(plan.Members.Any(member => member.EntityId == 350 && member.To == new GridCoord(1, 0)));
        }

        [Test]
        public void CoreExecutionAndArbitration_DoNotBranchOnOrdinaryActionKinds()
        {
            string root = RepositoryRoot();
            string execution = File.ReadAllText(Path.Combine(root, "Shared", "DG.GameCore", "Rules", "Execution", "StateDrivenRules.cs"));
            string actions = File.ReadAllText(Path.Combine(root, "Shared", "DG.GameCore", "Rules", "Actions", "ActionSpecs.cs"));
            string pending = File.ReadAllText(Path.Combine(root, "Shared", "DG.GameCore", "Rules", "Pending", "PendingRuleStates.cs"));
            string removedDefaultRegistryFactory = "private static ActionSpecRegistry " + "CreateDefault()";
            int createDefault = actions.IndexOf(removedDefaultRegistryFactory, StringComparison.Ordinal);
            string runtimeActions = createDefault >= 0 ? actions.Substring(0, createDefault) : actions;

            string removedActionKindName = "WorldAction" + "Kind";
            Assert.IsFalse(execution.Contains(removedActionKindName));
            Assert.IsFalse(actions.Contains(removedActionKindName));
            Assert.IsFalse(execution.Contains("ProcessPushableBlock"));
            Assert.IsFalse(execution.Contains("ProcessBounceBlock"));
            Assert.IsFalse(execution.Contains("FindBlockingForBodyMove"));
            Assert.IsFalse(execution.Contains("IntentArbiter"));
            Assert.IsFalse(execution.Contains("ProcessSpawn"));
            Assert.IsFalse(execution.Contains("ProcessRemove"));
            Assert.IsFalse(actions.Contains("BehaviorIntentKind"));
            Assert.IsFalse(actions.Contains("ToBehaviorIntent"));
            Assert.IsFalse(actions.Contains("ToMoveIntent"));
            Assert.IsFalse(runtimeActions.Contains("\"player_push\""));
            Assert.IsFalse(runtimeActions.Contains("\"connected_body_move\""));
            Assert.IsFalse(pending.Contains("\"player_push\""));
        }

        [Test]
        public void PendingHandoffState_DoesNotExposeParentRetryApi()
        {
            string root = RepositoryRoot();
            string pending = File.ReadAllText(Path.Combine(root, "Shared", "DG.GameCore", "Rules", "Pending", "PendingRuleStates.cs"));

            Assert.IsFalse(pending.Contains("ReadyToRetry"));
            Assert.IsFalse(pending.Contains("BeginDerivedAction"));
            Assert.IsFalse(pending.Contains("AddDerivedActionState"));
            Assert.IsFalse(pending.Contains("WaitingForDerived"));
            Assert.IsFalse(pending.Contains("RetryCount"));
        }

        private static ActionRequest MoveRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, long entityId, Direction direction, long sourceStateId = 0)
        {
            ActionSpec spec = ActionSpecRegistry.Default.Get(specId);
            return new ActionRequest(
                actionId,
                specId,
                priority,
                new ActionSourceContext(spec.DefaultSource, entityId, sourceStateId, spec.SourceTag),
                entityId,
                new ActionTarget(0, null, direction),
                default,
                0,
                0,
                0);
        }

        private static ActionRequest DeferredMoveRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, long entityId, Direction direction, int contributionCount, IReadOnlyList<long> causalitySamples)
        {
            ActionSpec spec = ActionSpecRegistry.Default.Get(specId);
            return new ActionRequest(
                actionId,
                specId,
                priority,
                new ActionSourceContext(spec.DefaultSource, entityId, 0, spec.SourceTag),
                entityId,
                new ActionTarget(0, null, direction),
                default,
                0,
                0,
                0,
                0,
                0,
                contributionCount,
                causalitySamples);
        }

        private static void AssertSpec(ActionSpecRegistry registry, ActionSpecId id, ActionPrimitive primitive, ActionSourceKind source, ActionTargetRule targetRule)
        {
            Assert.IsTrue(registry.TryGet(id, out ActionSpec spec));
            Assert.AreEqual(primitive, spec.Primitive);
            Assert.AreEqual(source, spec.DefaultSource);
            Assert.AreEqual(targetRule, spec.TargetRule);
        }

        private static void AssertPosition(GameWorld world, long entityId, GridCoord expected)
        {
            Assert.IsTrue(world.TryGetEntity(entityId, out GameEntity entity));
            Assert.IsTrue(world.TryGetComponent(entity, out PositionComponent position));
            Assert.AreEqual(expected, position.Coord);
        }

        private static StateDrivenRuleExecutionResult TickAndEnqueueDeferred(StateDrivenRuleExecutionSystem system, GameWorld world, WorldActionQueue queue, IReadOnlyList<WorldAction> actions)
        {
            StateDrivenRuleExecutionResult result = system.Tick(world, actions, world.ServerTick);
            for (int i = 0; i < result.DeferredActions.Count; i++)
            {
                queue.EnqueueDeferred(result.DeferredActions[i]);
            }

            return result;
        }

        private static void SetPorts(GameWorld world, long entityId, DirectionMask ports)
        {
            Assert.IsTrue(world.TryGetEntity(entityId, out GameEntity entity));
            world.SetComponent(entity, new PortConnectorComponent(ports));
        }

        private static void RefreshStaticPorts(GameWorld world, params long[] entityIds)
        {
            for (int i = 0; i < entityIds.Length; i++)
            {
                Assert.IsTrue(world.TryGetEntity(entityIds[i], out GameEntity entity));
                world.CaptureStaticComponentSources(entity);
            }

            world.ResolveComponentResults();
        }

        private static void AddMechanismAbility(GameWorld world, params long[] entityIds)
        {
            for (int i = 0; i < entityIds.Length; i++)
            {
                Assert.IsTrue(world.TryGetEntity(entityIds[i], out GameEntity entity));
                world.AddTag(entity, WorldTag.AbilityMechanismPush);
            }
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }

        private static string GameConfigDirectory()
        {
            return Path.Combine(Application.streamingAssetsPath, "GameConfig");
        }
    }
}
