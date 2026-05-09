using System;
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
        public void LegacyWorldAction_MapsToActionRequest()
        {
            var adapter = new ActionRequestAdapter(ActionSpecRegistry.Default);
            var player = new WorldAction(1, WorldActionPriority.Player, WorldActionKind.PlayerMove, 10, new GridCoord(1, 0), Direction.None, 7, 0, 0, 1);
            var auto = new WorldAction(2, WorldActionPriority.Auto, WorldActionKind.AutoMove, 20, null, Direction.None, 0, 3, 4, 1);
            var mechanism = new WorldAction(3, WorldActionPriority.Mechanism, WorldActionKind.MechanismPush, 30, null, Direction.Right, 0, 3, 4, 1);

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

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), new PendingRuleStateStore(), world.ServerTick);

            Assert.IsTrue(result.ActionResults[action.ActionId].Success);
            Assert.IsTrue(world.TryGetEntity(100, out GameEntity entity));
            Assert.IsTrue(world.TryGetComponent(entity, out PositionComponent position));
            Assert.AreEqual(new GridCoord(1, 0), position.Coord);
        }

        [Test]
        public void PlayerMoveConnectedBody_BlockedByExternalTargetHandoffs()
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
            var pending = new PendingRuleStateStore();

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);

            Assert.IsFalse(result.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(result.Reasons.Contains("handoff"));
            Assert.AreEqual(1, pending.ActiveCount);
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
            }, new PendingRuleStateStore(), 1);

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
            }, new PendingRuleStateStore(), 1);

            Assert.AreEqual(0, result.AcceptedActions.Count);
            Assert.AreEqual(2, result.RejectedActions.Count);
            Assert.IsTrue(result.RejectedActions.All(action => action.Reason == "conflicting body intents"));
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
            }, new PendingRuleStateStore(), 1);

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

            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[] { request }, new PendingRuleStateStore(), 1);

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
            PendingActionState state = pending.AddHandoffActionState(MoveRequest(20, "player_move", WorldActionPriority.Player, 340, Direction.Right), 341, Direction.Right, 0);
            var request = new ActionRequestAdapter(ActionSpecRegistry.Default).FromPendingActionState(state, 1);

            Assert.AreEqual(ActionSourceKind.Handoff, request.Source.Kind);
            Assert.AreEqual(20, request.DerivedFromUnitId);

            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[] { request }, pending, 1);

            Assert.AreEqual(0, result.AcceptedActions.Count);
            Assert.AreEqual(0, result.RejectedActions.Count);
            Assert.AreEqual(1, result.DerivedActions.Count);
            Assert.AreEqual(ActionResultBranch.Handoff, result.DerivedActions[0].Branch);
            Assert.AreEqual("handoff", result.DerivedActions[0].Reason);
            Assert.AreEqual(PendingRuleStatus.Active, state.Status);
            Assert.AreEqual(1, pending.ActiveCount);
            Assert.IsTrue(pending.ActionStates.Any(item => item.Status == PendingRuleStatus.Active && item.ActiveHandoffEntityId == 342));
            Assert.AreEqual(2, state.Units.Count);
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
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult third = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsTrue(second.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), second.ActionResults[action.ActionId].FinalCoord);
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
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult third = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult fourth = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult fifth = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsTrue(second.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), second.ActionResults[action.ActionId].FinalCoord);
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

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), new PendingRuleStateStore(), world.ServerTick);

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

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), new PendingRuleStateStore(), world.ServerTick);

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
            }, new PendingRuleStateStore(), world.ServerTick);
            ActionArbitrationResult second = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(2, "configured_wind_push", WorldActionPriority.Mechanism, 430, Direction.Right)
            }, new PendingRuleStateStore(), world.ServerTick);

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
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult third = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsTrue(second.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), second.ActionResults[action.ActionId].FinalCoord);
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
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsTrue(second.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), second.ActionResults[action.ActionId].FinalCoord);
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
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            Assert.AreEqual(1, pending.ActiveCount);
            Assert.IsTrue(pending.HasActiveActionInvolving(446));
            Assert.IsTrue(pending.HasActiveActionInvolving(447));
            Assert.IsTrue(pending.HasActiveActionInvolving(448));
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult third = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsTrue(second.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), second.ActionResults[action.ActionId].FinalCoord);
            Assert.AreEqual(0, second.ActivePendingCount);
            Assert.AreEqual(0, third.ActivePendingCount);
            AssertPosition(world, 445, new GridCoord(0, 0));
            AssertPosition(world, 446, new GridCoord(2, 0));
            AssertPosition(world, 447, new GridCoord(3, 0));
            AssertPosition(world, 448, new GridCoord(4, 0));
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
            var pending = new PendingRuleStateStore();

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);

            Assert.IsFalse(result.ActionResults[action.ActionId].Success);
            Assert.AreEqual(0, pending.ActiveCount);
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
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsFalse(second.ActionResults[action.ActionId].Success);
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
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsFalse(second.ActionResults[action.ActionId].Success);
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
            var pending = new PendingRuleStateStore();

            StateDrivenRuleExecutionResult result = new StateDrivenRuleExecutionSystem().Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);

            Assert.IsTrue(result.ActionResults[action.ActionId].Success);
            Assert.AreEqual(0, pending.ActiveCount);
            AssertPosition(world, 430, new GridCoord(1, 0));
            AssertPosition(world, 431, new GridCoord(2, 0));
        }

        [Test]
        public void DerivedActionFailure_FailsOwnerAction()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(380, 380, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(381, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(382, new GridCoord(2, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(380, new GridCoord(1, 0), 3);
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsFalse(second.ActionResults[action.ActionId].Success);
            Assert.AreEqual(0, second.ActivePendingCount);
            AssertPosition(world, 380, new GridCoord(0, 0));
            AssertPosition(world, 381, new GridCoord(1, 0));
        }

        [Test]
        public void NestedPushableBlock_HandoffsUntilOnlyTailMoves()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(390, 390, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(391, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(392, new GridCoord(2, 0))));
            world.NextTick();
            var queue = new WorldActionQueue();
            WorldAction action = queue.EnqueuePlayerMove(390, new GridCoord(1, 0), 4);
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult third = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult fourth = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult fifth = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsFalse(second.ActionResults.ContainsKey(action.ActionId));
            Assert.AreEqual(1, second.ActivePendingCount);
            Assert.IsTrue(third.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), third.ActionResults[action.ActionId].FinalCoord);
            Assert.AreEqual(0, third.ActivePendingCount);
            Assert.AreEqual(0, fourth.ActivePendingCount);
            Assert.AreEqual(0, fifth.ActivePendingCount);
            AssertPosition(world, 390, new GridCoord(0, 0));
            AssertPosition(world, 391, new GridCoord(1, 0));
            AssertPosition(world, 392, new GridCoord(3, 0));
        }

        [Test]
        public void ConnectedBodyChain_HandoffsUntilOnlyTailBodyMoves()
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
            var pending = new PendingRuleStateStore();
            var system = new StateDrivenRuleExecutionSystem();

            StateDrivenRuleExecutionResult first = system.Tick(world, queue.DrainReady(world.ServerTick), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult second = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult third = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult fourth = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);
            world.NextTick();
            StateDrivenRuleExecutionResult fifth = system.Tick(world, Array.Empty<WorldAction>(), pending, world.ServerTick);

            Assert.IsFalse(first.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(first.Reasons.Contains("handoff"));
            Assert.IsFalse(second.ActionResults.ContainsKey(action.ActionId));
            Assert.IsTrue(third.ActionResults[action.ActionId].Success);
            Assert.AreEqual(new GridCoord(0, 0), third.ActionResults[action.ActionId].FinalCoord);
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
        public void PushChainCycleGuard_RejectsRepeatedSubject()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(500, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(501, new GridCoord(0, 0))));
            var pending = new PendingRuleStateStore();
            PendingActionState state = pending.AddHandoffActionState(MoveRequest(30, "player_move", WorldActionPriority.Player, 500, Direction.Right), 501, Direction.Right, 0);
            var request = new ActionRequestAdapter(ActionSpecRegistry.Default).FromPendingActionState(state, 1);

            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[] { request }, pending, 1);

            Assert.AreEqual(1, result.RejectedActions.Count);
            Assert.AreEqual("push chain cycle", result.RejectedActions[0].Reason);
        }

        [Test]
        public void PushChainDepthGuard_RejectsTooLongChain()
        {
            var pending = new PendingRuleStateStore();
            PendingActionState state = pending.AddHandoffActionState(MoveRequest(40, "player_move", WorldActionPriority.Player, 600, Direction.Right), 601, Direction.Right, 0);
            string reason = string.Empty;

            for (int i = 0; i < PendingActionState.MaxChainDepth; i++)
            {
                PendingActionUnit unit = state.ReadyUnit;
                var request = MoveRequest(unit.ActionUnitId, unit.SpecId, unit.Priority, unit.EntityId, unit.Direction, state.StateId);
                bool added = state.TryAddChildUnit(request, 700 + i, Direction.Right, i + 1, "player_push", new[] { 700L + i }, out reason);
                if (!added)
                {
                    break;
                }
            }

            Assert.AreEqual("push chain depth exceeded", reason);
        }

        [Test]
        public void RulePlanner_CreatesPlanFromAcceptedClaims()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(350, new GridCoord(0, 0), Direction.Right)));
            ActionArbitrationResult arbitration = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 350, Direction.Right)
            }, new PendingRuleStateStore(), 1);

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

            Assert.IsFalse(execution.Contains("WorldActionKind.PlayerMove"));
            Assert.IsFalse(execution.Contains("WorldActionKind.AutoMove"));
            Assert.IsFalse(execution.Contains("WorldActionKind.MechanismPush"));
            Assert.IsFalse(execution.Contains("WorldActionKind.DebugMove"));
            Assert.IsFalse(execution.Contains("ProcessPushableBlock"));
            Assert.IsFalse(execution.Contains("ProcessBounceBlock"));
            Assert.IsFalse(execution.Contains("FindBlockingForBodyMove"));
            Assert.IsFalse(execution.Contains("IntentArbiter"));
            Assert.IsFalse(execution.Contains("ProcessSpawn"));
            Assert.IsFalse(execution.Contains("ProcessRemove"));
            Assert.IsFalse(actions.Contains("BehaviorIntentKind"));
            Assert.IsFalse(actions.Contains("ToBehaviorIntent"));
            Assert.IsFalse(actions.Contains("ToMoveIntent"));
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

        private static void SetPorts(GameWorld world, long entityId, DirectionMask ports)
        {
            Assert.IsTrue(world.TryGetEntity(entityId, out GameEntity entity));
            world.SetComponent(entity, new PortConnectorComponent(ports));
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }
    }
}
