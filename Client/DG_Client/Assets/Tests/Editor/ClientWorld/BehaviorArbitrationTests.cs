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
        public void OccupancyResolver_AllowsConnectedBodyInternalOldCells()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(20, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(21, new GridCoord(1, 0), Direction.Right));
            Assert.IsTrue(world.TryGetEntity(20, out GameEntity entity));
            var planner = new RulePlanner();

            Assert.IsTrue(planner.TryPlanMove(world, new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 1, 0, entity.EntityId, Direction.Right, 1), out MovePlan plan, out PlanResult result));

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

            Assert.IsFalse(new RulePlanner().TryPlanMove(world, new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 1, 0, 30, Direction.Right, 1), out _, out PlanResult result));

            Assert.AreEqual(PlanFailureReason.BlockedCell, result.Reason);
        }

        [Test]
        public void ConflictResolver_CommitsGroupMoveAtomically()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(40, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(41, new GridCoord(1, 0), Direction.Right));
            var planner = new RulePlanner();
            Assert.IsTrue(planner.TryPlanMove(world, new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 1, 0, 40, Direction.Right, 1), out MovePlan plan, out _));

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
            Assert.IsTrue(planner.TryPlanMove(world, new BehaviorIntent(BehaviorIntentKind.Move, WorldActionPriority.Player, 1, 0, 50, Direction.Right, 1), out MovePlan firstPlan, out _));
            Assert.IsTrue(planner.TryPlanMove(world, new BehaviorIntent(BehaviorIntentKind.Move, WorldActionPriority.Player, 2, 0, 51, Direction.Down, 1), out MovePlan secondPlan, out _));

            IReadOnlyList<CommitProposalResult> results = new ConflictResolver().Resolve(world, new[] { firstPlan, secondPlan });

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results[0].Accepted);
            Assert.IsFalse(results[1].Accepted);
            Assert.AreEqual("target reserved", results[1].Reason);
        }

        [Test]
        public void IntentArbiter_RejectsConflictingDirectionsForSameBody()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(60, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(61, new GridCoord(1, 0), Direction.Right));

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 1, 0, 60, Direction.Right, 1),
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 2, 0, 61, Direction.Left, 1)
            });

            Assert.AreEqual(2, result.Items.Count);
            Assert.IsTrue(result.Items.All(item => !item.Accepted));
            Assert.IsTrue(result.Items.All(item => item.Reason == IntentArbitrationReason.ConflictingBodyIntents));
            Assert.IsTrue(result.Items.All(item => item.Message == "conflicting body intents"));
            Assert.IsTrue(world.TryGetEntity(60, out GameEntity first));
            Assert.IsTrue(world.TryGetEntity(61, out GameEntity second));
            Assert.IsTrue(world.TryGetComponent(first, out PositionComponent firstPosition));
            Assert.IsTrue(world.TryGetComponent(second, out PositionComponent secondPosition));
            Assert.AreEqual(new GridCoord(0, 0), firstPosition.Coord);
            Assert.AreEqual(new GridCoord(1, 0), secondPosition.Coord);
        }
    }
}
