using System.Linq;
using DG.GameCore;
using NUnit.Framework;

namespace DG.EditorTests
{
    public sealed class IntentArbitrationTests
    {
        [Test]
        public void BehaviorIntent_UsesRegisteredDefaultsForSupportedKinds()
        {
            AssertIntentDefaults(BehaviorIntentKind.Move, WorldTag.SourcePlayer, WorldTag.AbilityMove, WorldTag.None, WorldTag.BlockPlayerMove | WorldTag.StateStunned | WorldTag.StateRooted, IntentCancelPolicy.CancelLowerPriority);
            AssertIntentDefaults(BehaviorIntentKind.Push, WorldTag.SourcePlayer, WorldTag.AbilityPlayerPush, WorldTag.None, WorldTag.StateStunned, IntentCancelPolicy.CancelLowerPriority);
            AssertIntentDefaults(BehaviorIntentKind.AutoMove, WorldTag.SourceAuto, WorldTag.AbilityAutoMove, WorldTag.None, WorldTag.None, IntentCancelPolicy.CancelLowerPriority);
            AssertIntentDefaults(BehaviorIntentKind.MechanismPush, WorldTag.SourceMechanism, WorldTag.AbilityMechanismPush, WorldTag.None, WorldTag.ImmuneMechanismPush, IntentCancelPolicy.CancelLowerPriority);
            AssertIntentDefaults(BehaviorIntentKind.DebugMove, WorldTag.SourceDebug, WorldTag.AbilityMove, WorldTag.None, WorldTag.None, IntentCancelPolicy.CancelLowerPriority);
        }

        [Test]
        public void BehaviorIntent_RejectsUnknownKind()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new BehaviorIntent((BehaviorIntentKind)999, WorldActionPriority.Player, 1, 0, 1, Direction.Right, 1));
        }

        [Test]
        public void TagSetComponent_HelperAddsAndRemovesTags()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.BlockerSpawn(1, new GridCoord(0, 0)));
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity entity));

            world.AddTag(entity, WorldTag.StateStunned);
            world.AddTag(entity, WorldTag.BlockPlayerMove);

            Assert.IsTrue(world.HasTag(entity, WorldTag.StateStunned));
            Assert.IsTrue(world.HasTag(entity, WorldTag.BlockPlayerMove));
            Assert.IsTrue(world.RemoveTag(entity, WorldTag.StateStunned));
            Assert.IsFalse(world.HasTag(entity, WorldTag.StateStunned));
            Assert.IsTrue(world.HasTag(entity, WorldTag.BlockPlayerMove));
        }

        [Test]
        public void IntentArbiter_MergesSameDirectionMechanismPush()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(10, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(11, new GridCoord(1, 0), Direction.Right));

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 1, 0, 10, Direction.Right, 1),
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 2, 0, 11, Direction.Right, 1)
            });

            Assert.AreEqual(2, result.Items.Count);
            Assert.AreEqual(1, result.Items.Count(item => item.Accepted));
            Assert.AreEqual(1, result.Items.Count(item => item.Reason == IntentArbitrationReason.MergedBodyIntent));
        }

        [Test]
        public void IntentArbiter_RejectsSamePriorityOppositeMechanismPush()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(20, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(21, new GridCoord(1, 0), Direction.Right));

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 1, 0, 20, Direction.Right, 1),
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 2, 0, 21, Direction.Left, 1)
            });

            Assert.AreEqual(2, result.Items.Count);
            Assert.IsTrue(result.Items.All(item => !item.Accepted));
            Assert.IsTrue(result.Items.All(item => item.Reason == IntentArbitrationReason.ConflictingBodyIntents));
        }

        [Test]
        public void IntentArbiter_PlayerPushInterruptsMechanismConflict()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(30, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(31, new GridCoord(1, 0), Direction.Right));

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.Push, WorldActionPriority.Player, 1, 1, 30, Direction.Right, 1),
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 2, 0, 30, Direction.Right, 1),
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 3, 0, 31, Direction.Left, 1)
            });

            Assert.AreEqual(3, result.Items.Count);
            Assert.IsTrue(result.Items.Single(item => item.Intent.SourceActionId == 1).Accepted);
            Assert.IsTrue(result.Items.Where(item => item.Intent.SourceActionId != 1).All(item => !item.Accepted));
            Assert.IsTrue(result.Items.Where(item => item.Intent.SourceActionId != 1).All(item => item.Reason == IntentArbitrationReason.InterruptedByHigherPriorityIntent));
        }

        [Test]
        public void IntentArbiter_BlocksIntentWhenAnyBodyMemberHasBlockedTag()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(40, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(41, new GridCoord(1, 0), Direction.Right));
            Assert.IsTrue(world.TryGetEntity(41, out GameEntity member));
            world.AddTag(member, WorldTag.ImmuneMechanismPush);

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.MechanismPush, WorldActionPriority.Mechanism, 1, 0, 40, Direction.Right, 1)
            });

            Assert.AreEqual(1, result.Items.Count);
            Assert.IsFalse(result.Items[0].Accepted);
            Assert.AreEqual(IntentArbitrationReason.BlockedByTag, result.Items[0].Reason);
        }

        [Test]
        public void IntentArbiter_BlocksPlayerMoveByTag()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(50, 50, new GridCoord(0, 0)));
            Assert.IsTrue(world.TryGetEntity(50, out GameEntity player));
            world.AddTag(player, WorldTag.BlockPlayerMove);

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.Move, WorldActionPriority.Player, 1, 0, 50, Direction.Right, 1)
            });

            Assert.AreEqual(1, result.Items.Count);
            Assert.IsFalse(result.Items[0].Accepted);
            Assert.AreEqual(IntentArbitrationReason.BlockedByTag, result.Items[0].Reason);
            Assert.AreEqual("blocked by tag", result.Items[0].Message);
        }

        [Test]
        public void IntentArbiter_BlocksStunnedMoveAndPlayerPush()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(60, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(61, new GridCoord(1, 0), Direction.Right));
            Assert.IsTrue(world.TryGetEntity(60, out GameEntity member));
            world.AddTag(member, WorldTag.StateStunned);

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.Move, WorldActionPriority.Player, 1, 0, 60, Direction.Right, 1),
                new BehaviorIntent(BehaviorIntentKind.Push, WorldActionPriority.Player, 2, 1, 60, Direction.Right, 1)
            });

            Assert.AreEqual(2, result.Items.Count);
            Assert.IsTrue(result.Items.All(item => !item.Accepted));
            Assert.IsTrue(result.Items.All(item => item.Reason == IntentArbitrationReason.BlockedByTag));
        }

        [Test]
        public void IntentArbiter_BlocksRootedActiveMoveOnly()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PlayerSpawn(70, 70, new GridCoord(0, 0)));
            Assert.IsTrue(world.TryGetEntity(70, out GameEntity player));
            world.AddTag(player, WorldTag.StateRooted);

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.Move, WorldActionPriority.Player, 1, 0, 70, Direction.Right, 1),
                new BehaviorIntent(BehaviorIntentKind.DebugMove, WorldActionPriority.Debug, 2, 0, 70, Direction.Right, 1, new GridCoord(1, 0))
            });

            Assert.AreEqual(2, result.Items.Count);
            Assert.IsTrue(result.Items.Single(item => item.Intent.SourceActionId == 1).Reason == IntentArbitrationReason.BlockedByTag);
            Assert.IsTrue(result.Items.Single(item => item.Intent.SourceActionId == 2).Accepted);
        }

        [Test]
        public void IntentArbiter_RejectsSameBodyDebugMovesWithDifferentTargets()
        {
            var world = new GameWorld();
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(80, new GridCoord(0, 0), Direction.Right));
            world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(81, new GridCoord(1, 0), Direction.Right));

            IntentArbitrationResult result = new IntentArbiter().Arbitrate(world, new[]
            {
                new BehaviorIntent(BehaviorIntentKind.DebugMove, WorldActionPriority.Debug, 1, 0, 80, Direction.Right, 1, new GridCoord(2, 0)),
                new BehaviorIntent(BehaviorIntentKind.DebugMove, WorldActionPriority.Debug, 2, 0, 81, Direction.Right, 1, new GridCoord(3, 0))
            });

            Assert.AreEqual(2, result.Items.Count);
            Assert.IsTrue(result.Items.All(item => !item.Accepted));
            Assert.IsTrue(result.Items.All(item => item.Reason == IntentArbitrationReason.ConflictingBodyIntents));
        }

        private static void AssertIntentDefaults(BehaviorIntentKind kind, WorldTag sourceTag, WorldTag abilityTag, WorldTag requiredTags, WorldTag blockedTags, IntentCancelPolicy cancelPolicy)
        {
            var intent = new BehaviorIntent(kind, WorldActionPriority.Player, 1, 0, 1, Direction.Right, 1);

            Assert.AreEqual(sourceTag, intent.SourceTag);
            Assert.AreEqual(abilityTag, intent.AbilityTag);
            Assert.AreEqual(requiredTags, intent.RequiredTags);
            Assert.AreEqual(blockedTags, intent.BlockedTags);
            Assert.AreEqual(cancelPolicy, intent.CancelPolicy);
        }
    }
}
