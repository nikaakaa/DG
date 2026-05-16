using System.Collections.Generic;
using System.Linq;
using DG.GameCore;
using NUnit.Framework;

namespace DG.EditorTests
{
    public sealed class BehaviorArbitrationTests
    {
        [Test]
        public void BodyResolver_ReturnsSingleBodyForNonPortEntity()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.BlockerSpawn(1, new GridCoord(0, 0)));
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity entity));

            Assert.IsTrue(new BodyResolver().TryResolve(world, entity, out BehaviorBody body, out _));

            Assert.AreEqual(BehaviorBodyKind.SingleEntity, body.Kind);
            Assert.AreEqual(1, body.Entities.Count);
            Assert.AreEqual(1, body.Entities[0].EntityId);
        }

        [Test]
        public void BodyResolver_ReturnsMatchedPortGroupWithoutMismatchedNeighbor()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(10, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(11, new GridCoord(1, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(12, new GridCoord(2, 0), Direction.Down));
            Assert.IsTrue(world.TryGetEntity(10, out GameEntity entity));

            Assert.IsTrue(new BodyResolver().TryResolve(world, entity, out BehaviorBody body, out _));

            Assert.AreEqual(BehaviorBodyKind.PortConnected, body.Kind);
            Assert.AreEqual(2, body.Entities.Count);
            Assert.IsTrue(body.Entities.Any(member => member.EntityId == 10));
            Assert.IsTrue(body.Entities.Any(member => member.EntityId == 11));
            Assert.IsFalse(body.Entities.Any(member => member.EntityId == 12));
        }

        [Test]
        public void BodyResolver_ResolvesPortCycleAsFlatConnectedBody()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(13, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(14, new GridCoord(1, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(15, new GridCoord(1, 1), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(16, new GridCoord(0, 1), Direction.Right));
            SetPorts(world, 13, DirectionMask.Left | DirectionMask.Right | DirectionMask.Up | DirectionMask.Down);
            SetPorts(world, 14, DirectionMask.Left | DirectionMask.Right | DirectionMask.Up | DirectionMask.Down);
            SetPorts(world, 15, DirectionMask.Left | DirectionMask.Right | DirectionMask.Up | DirectionMask.Down);
            SetPorts(world, 16, DirectionMask.Left | DirectionMask.Right | DirectionMask.Up | DirectionMask.Down);
            Assert.IsTrue(world.TryGetEntity(15, out GameEntity entity));

            Assert.IsTrue(new BodyResolver().TryResolve(world, entity, out BehaviorBody body, out _));

            Assert.AreEqual(BehaviorBodyKind.PortConnected, body.Kind);
            Assert.AreEqual(13, body.RootEntityId);
            Assert.AreEqual(4, body.Entities.Count);
            CollectionAssert.AreEqual(new[] { 13L, 14L, 15L, 16L }, body.Entities.Select(member => member.EntityId).ToArray());
        }

        [Test]
        public void OccupancyResolver_AllowsConnectedBodyInternalOldCells()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(20, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(21, new GridCoord(1, 0), Direction.Right));
            Assert.IsTrue(world.TryGetEntity(20, out GameEntity entity));
            var planner = new RulePlanner();

            Assert.IsTrue(planner.TryPlanMove(world, MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, entity.EntityId, Direction.Right), Direction.Right, null, 1, out MovePlan plan, out PlanResult result));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(2, plan.Members.Count);
            Assert.IsTrue(plan.Members.Any(member => member.EntityId == 20 && member.To == new GridCoord(1, 0)));
            Assert.IsTrue(plan.Members.Any(member => member.EntityId == 21 && member.To == new GridCoord(2, 0)));
        }

        [Test]
        public void OccupancyResolver_RejectsConnectedBodyExternalBlocker()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(30, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(31, new GridCoord(1, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.BlockerSpawn(32, new GridCoord(2, 0)));

            Assert.IsFalse(new RulePlanner().TryPlanMove(world, MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 30, Direction.Right), Direction.Right, null, 1, out _, out PlanResult result));

            Assert.AreEqual(PlanFailureReason.BlockedCell, result.Reason);
        }

        [Test]
        public void ConflictResolver_CommitsGroupMoveAtomically()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(40, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(41, new GridCoord(1, 0), Direction.Right));
            var planner = new RulePlanner();
            Assert.IsTrue(planner.TryPlanMove(world, MoveRequest(1, "mechanism_push", WorldActionPriority.Mechanism, 40, Direction.Right), Direction.Right, null, 1, out MovePlan plan, out _));

            IReadOnlyList<CommitProposalResult> results = new ConflictResolver().Resolve(world, new[] { plan });

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(result => result.Accepted));
            Assert.IsTrue(world.TryGetEntity(40, out GameEntity first));
            Assert.IsTrue(world.TryGetEntity(41, out GameEntity second));
            Assert.IsTrue(world.TryGetComponent(first, out PositionComponent firstPosition));
            Assert.IsTrue(world.TryGetComponent(second, out PositionComponent secondPosition));
            Assert.AreEqual(new GridCoord(1, 0), firstPosition.Coord);
            Assert.AreEqual(new GridCoord(2, 0), secondPosition.Coord);
        }

        [Test]
        public void ConflictResolver_RejectsSecondPlanTargetingReservedCell()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(50, 50, new GridCoord(0, 0)));
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(51, 51, new GridCoord(1, 1)));
            var planner = new RulePlanner();
            Assert.IsTrue(planner.TryPlanMove(world, MoveRequest(1, "player_move", WorldActionPriority.Player, 50, Direction.Right), Direction.Right, null, 1, out MovePlan firstPlan, out _));
            Assert.IsTrue(planner.TryPlanMove(world, MoveRequest(2, "player_move", WorldActionPriority.Player, 51, Direction.Down), Direction.Down, null, 1, out MovePlan secondPlan, out _));

            IReadOnlyList<CommitProposalResult> results = new ConflictResolver().Resolve(world, new[] { firstPlan, secondPlan });

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results[0].Accepted);
            Assert.IsFalse(results[1].Accepted);
            Assert.AreEqual("target reserved", results[1].Reason);
        }

        [Test]
        public void ActionArbiter_RejectsConflictingDirectionsForSameBody()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(60, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(61, new GridCoord(1, 0), Direction.Right));

            ActionArbitrationResult result = new ActionArbiter().ArbitrateMoves(world, new[]
            {
                MoveRequest(1, "connected_body_move", WorldActionPriority.Mechanism, 60, Direction.Right),
                MoveRequest(2, "connected_body_move", WorldActionPriority.Mechanism, 61, Direction.Left)
            }, 1);

            Assert.AreEqual(0, result.AcceptedActions.Count);
            Assert.AreEqual(2, result.RejectedActions.Count);
            Assert.IsTrue(result.RejectedActions.All(item => item.Reason == "conflicting body intents"));
            Assert.IsTrue(world.TryGetEntity(60, out GameEntity first));
            Assert.IsTrue(world.TryGetEntity(61, out GameEntity second));
            Assert.IsTrue(world.TryGetComponent(first, out PositionComponent firstPosition));
            Assert.IsTrue(world.TryGetComponent(second, out PositionComponent secondPosition));
            Assert.AreEqual(new GridCoord(0, 0), firstPosition.Coord);
            Assert.AreEqual(new GridCoord(1, 0), secondPosition.Coord);
        }

        private static void SetPorts(GameWorld world, long entityId, DirectionMask ports)
        {
            Assert.IsTrue(world.TryGetEntity(entityId, out GameEntity entity));
            world.SetComponent(entity, new PortConnectorComponent(ports));
        }

        private static ActionRequest MoveRequest(long actionId, ActionSpecId specId, WorldActionPriority priority, long entityId, Direction direction)
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
                0);
        }
    }
}
