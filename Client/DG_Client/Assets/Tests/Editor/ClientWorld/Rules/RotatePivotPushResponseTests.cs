using System.Collections.Generic;
using System.Linq;
using DG.GameCore;
using NUnit.Framework;

namespace DG.EditorTests
{
    public sealed class RotatePivotPushResponseTests
    {
        [Test]
        public void UniquePivotBody_RotatesNinetyDegreesFromSinglePush()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 100, new GridCoord(0, 0));
            AssertPosition(world, 101, new GridCoord(0, -1));
        }

        [Test]
        public void SuccessfulRotation_RotatesMemberDirections()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            SetDirection(world, 101, Direction.Right);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            AssertDirection(world, 101, Direction.Down);
        }

        [Test]
        public void SuccessfulRotation_RotatesPivotDefaultPortOrientation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            AssertDirection(world, 100, Direction.Down);
            Assert.IsTrue(world.TryGetEntity(100, out GameEntity pivot));
            Assert.AreEqual(DirectionMask.Up | DirectionMask.Down, PortConnectionSystem.GetWorldPorts(world, pivot));
        }

        [Test]
        public void RotationSweepBlocker_EmitsDeferredPushEvenWhenFinalTargetIsFree()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            AddPushable(world, 201, new GridCoord(1, -1));

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            Assert.AreEqual(1, result.DeferredActions.Count);
            Assert.AreEqual(201, result.DeferredActions[0].EntityId);
            Assert.AreEqual(Direction.Down, result.DeferredActions[0].Direction);
            PushOriginContext context = result.DeferredActions[0].OriginContexts.Single();
            Assert.AreEqual(101, context.ImpactMemberId);
            Assert.AreEqual(new GridCoord(1, 0), context.ImpactFrom);
            Assert.AreEqual(new GridCoord(1, -1), context.ImpactTo);
            AssertPosition(world, 101, new GridCoord(1, 0));
        }

        [Test]
        public void RotationSweepBlocker_DetectsRadiusTwoArcCells()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, 2, 0));
            SetPivot(world, 100);
            AddPushable(world, 201, new GridCoord(1, -1));

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 102, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            Assert.AreEqual(1, result.DeferredActions.Count);
            Assert.AreEqual(201, result.DeferredActions[0].EntityId);
            AssertPosition(world, 101, new GridCoord(1, 0));
            AssertPosition(world, 102, new GridCoord(2, 0));
        }

        [Test]
        public void SameDirectionTorqueContributions_RotateOnlyOnce()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down), Action(2, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            Assert.IsTrue(result.ActionResults[2].Success);
            AssertPosition(world, 101, new GridCoord(0, -1));
        }

        [Test]
        public void OppositeTorqueContributions_CancelByCount()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, -1, 0));
            SetPivot(world, 100);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down), Action(2, 102, Direction.Down));

            Assert.IsFalse(result.ActionResults[1].Success);
            Assert.IsFalse(result.ActionResults[2].Success);
            AssertPosition(world, 101, new GridCoord(1, 0));
            AssertPosition(world, 102, new GridCoord(-1, 0));
            Assert.AreEqual(0, result.DeferredActions.Count);
        }

        [Test]
        public void PushThroughPivot_FallsBackToConnectedBodyTranslation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Left));

            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 100, new GridCoord(-1, 0));
            AssertPosition(world, 101, new GridCoord(0, 0));
            Assert.AreEqual(0, result.DeferredActions.Count);
        }

        [Test]
        public void MultiplePivots_InvalidWithoutMoveOrDeferredPush()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            SetPivot(world, 101);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(result.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(1, 0));
            Assert.AreEqual(0, result.DeferredActions.Count);
        }

        [Test]
        public void InternalCurrentCells_DoNotBlockRotation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, 2, 0));
            SetPivot(world, 100);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(0, -1));
            AssertPosition(world, 102, new GridCoord(0, -2));
        }

        [Test]
        public void DuplicateRotatedTargets_CancelRotation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, 1, 0));
            SetPivot(world, 100);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(result.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(1, 0));
            AssertPosition(world, 102, new GridCoord(1, 0));
        }

        [Test]
        public void ExternalPushableBlockers_EmitAllDeferredPushAndCancelSelf()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, -1, 0));
            SetPivot(world, 100);
            AddPushable(world, 201, new GridCoord(0, -1));
            AddPushable(world, 202, new GridCoord(0, 1));

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            Assert.AreEqual(2, result.DeferredActions.Count);
            CollectionAssert.AreEquivalent(new[] { 201L, 202L }, result.DeferredActions.Select(action => action.EntityId).ToArray());
            Assert.AreEqual(Direction.Left, result.DeferredActions.Single(action => action.EntityId == 201).Direction);
            Assert.AreEqual(Direction.Right, result.DeferredActions.Single(action => action.EntityId == 202).Direction);
            PushOriginContext lowerContext = result.DeferredActions.Single(action => action.EntityId == 201).OriginContexts.Single();
            PushOriginContext upperContext = result.DeferredActions.Single(action => action.EntityId == 202).OriginContexts.Single();
            Assert.AreEqual(PushOriginKind.RotatePivotImpact, lowerContext.Kind);
            Assert.AreEqual(101, lowerContext.ImpactMemberId);
            Assert.AreEqual(new GridCoord(1, 0), lowerContext.ImpactFrom);
            Assert.AreEqual(new GridCoord(0, -1), lowerContext.ImpactTo);
            Assert.AreEqual(201, lowerContext.BlockerEntityId);
            Assert.AreEqual(100, lowerContext.PivotEntityId);
            Assert.AreEqual(PushOriginKind.RotatePivotImpact, upperContext.Kind);
            Assert.AreEqual(102, upperContext.ImpactMemberId);
            Assert.AreEqual(new GridCoord(-1, 0), upperContext.ImpactFrom);
            Assert.AreEqual(new GridCoord(0, 1), upperContext.ImpactTo);
            Assert.AreEqual(202, upperContext.BlockerEntityId);
            Assert.AreEqual(100, upperContext.PivotEntityId);
            AssertPosition(world, 101, new GridCoord(1, 0));
            AssertPosition(world, 102, new GridCoord(-1, 0));
        }

        [Test]
        public void RotateImpactPushContext_SurvivesDeferredMerge()
        {
            var queue = new WorldActionQueue();
            var first = new PushOriginContext(PushOriginKind.RotatePivotImpact, 101, new GridCoord(1, 0), new GridCoord(0, -1), 201, 100, Direction.Right, 1);
            var second = new PushOriginContext(PushOriginKind.RotatePivotImpact, 102, new GridCoord(1, 1), new GridCoord(0, 0), 201, 100, Direction.Right, 2);

            queue.EnqueueDeferred(new DeferredAction("mechanism_push", 201, new[] { 201L }, Direction.Left, 0, 1, 1, 1, "first", new[] { first }));
            queue.EnqueueDeferred(new DeferredAction("mechanism_push", 201, new[] { 201L }, Direction.Left, 0, 1, 1, 2, "second", new[] { second }));

            WorldAction action = queue.DrainReady(1).Single();
            Assert.AreEqual(2, action.DeferredContributionCount);
            Assert.AreEqual(2, action.PushOriginContexts.Count);
            Assert.IsTrue(action.PushOriginContexts.Any(context => context.ImpactMemberId == 101));
            Assert.IsTrue(action.PushOriginContexts.Any(context => context.ImpactMemberId == 102));
        }

        [Test]
        public void ExternalConnectedBodyBlocker_BecomesWholeHandoffSubject()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            AddPort(world, 201, new GridCoord(0, -1), DirectionMask.Right);
            AddPort(world, 202, new GridCoord(1, -1), DirectionMask.Left);

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.AreEqual(1, result.DeferredActions.Count);
            CollectionAssert.AreEquivalent(new[] { 201L, 202L }, result.DeferredActions[0].SubjectEntityIds.ToArray());
            AssertPosition(world, 101, new GridCoord(1, 0));
        }

        [Test]
        public void BodyWithoutPivot_KeepsOrdinaryPushTranslation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));

            Tick(world, Action(1, 100, Direction.Down));

            AssertPosition(world, 100, new GridCoord(0, -1));
            AssertPosition(world, 101, new GridCoord(1, -1));
        }

        [Test]
        public void RuntimeEffectPivot_EnablesRotateResponse()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            var spec = new EffectSpec("runtime_pivot", EffectKind.RotatePivot, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.None, true, true, WorldTag.None);
            AddEffect(world, spec, 100, 0, "pivot");

            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(0, -1));
        }

        [Test]
        public void RuntimeEffectPivotRemoval_PreservesStaticPivotSource()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            var spec = new EffectSpec("runtime_pivot_remove", EffectKind.RotatePivot, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.None, true, true, WorldTag.None);
            RuntimeEffectInstance runtime = AddEffect(world, spec, 100, 0, "runtime");

            RemoveEffect(world, 100, runtime.Id);
            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(0, -1));
        }

        [Test]
        public void RuntimeOnlyPivotExpiration_RemovesRotateResponse()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            var spec = new EffectSpec("runtime_pivot_expire", EffectKind.RotatePivot, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOrExpire, 1, 1, DirectionMask.None, true, true, WorldTag.None);
            AddEffect(world, spec, 100, 0, "runtime");

            Assert.AreEqual(1, world.ExpireRuntimeEffects(1).Count);
            StateDrivenRuleExecutionResult result = Tick(world, Action(1, 100, Direction.Down));

            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 100, new GridCoord(0, -1));
            AssertPosition(world, 101, new GridCoord(1, -1));
        }

        private static GameWorld CreateBody(params (long id, int x, int y)[] members)
        {
            var world = new GameWorld();
            for (int i = 0; i < members.Length; i++)
            {
                AddPort(world, members[i].id, new GridCoord(members[i].x, members[i].y), DirectionMask.All);
            }

            return world;
        }

        private static void AddPort(GameWorld world, long id, GridCoord coord, DirectionMask ports)
        {
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(id, coord, Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(id, out GameEntity entity));
            world.SetComponent(entity, new PortConnectorComponent(ports));
        }

        private static void AddPushable(GameWorld world, long id, GridCoord coord)
        {
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(id, coord)));
        }

        private static void SetPivot(GameWorld world, long entityId)
        {
            Assert.IsTrue(world.TryGetEntity(entityId, out GameEntity entity));
            world.SetComponent(entity, new RotatePivotComponent());
            world.CaptureStaticComponentSources(entity);
        }

        private static void SetDirection(GameWorld world, long entityId, Direction direction)
        {
            Assert.IsTrue(world.TryGetEntity(entityId, out GameEntity entity));
            world.SetDirection(entity, direction);
        }

        private static RuntimeEffectInstance AddEffect(GameWorld world, EffectSpec spec, long entityId, long startTick, string stackKey)
        {
            var context = new ActionContext(9000, 9000, "test_effect", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, entityId, 0, WorldTag.SourceDebug), entityId, entityId, entityId, entityId, new ActionTarget(entityId, null, Direction.None), Direction.None, startTick, startTick, 1, 0, 9000);
            var application = new EffectApplication(context, spec, ActionTargetData.Self(entityId, default, Direction.None), startTick, stackKey);
            IReadOnlyList<CommitProposalResult> results = new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, context.ActionId, application, startTick) });
            Assert.IsTrue(results.Single().Accepted);
            return world.RuntimeEffects.ActiveAt(startTick).OrderByDescending(effect => effect.Id.Value).First(effect => effect.TargetEntityId == entityId && effect.Spec.EffectSpecId.Equals(spec.SpecId));
        }

        private static void RemoveEffect(GameWorld world, long entityId, RuntimeEffectId effectId)
        {
            IReadOnlyList<CommitProposalResult> results = new CommitResolver().Resolve(world, new[] { CommitProposal.RemoveRuntimeEffect(WorldActionPriority.Debug, 9001, entityId, effectId, world.ServerTick) });
            Assert.IsTrue(results.Single().Accepted);
        }

        private static StateDrivenRuleExecutionResult Tick(GameWorld world, params WorldAction[] actions)
        {
            world.NextTick();
            return new StateDrivenRuleExecutionSystem().Tick(world, actions, world.ServerTick);
        }

        private static WorldAction Action(long actionId, long entityId, Direction direction)
        {
            return new WorldAction(actionId, WorldActionPriority.Mechanism, "mechanism_push", entityId, null, direction, 0, 0, 0, 1);
        }

        private static void AssertPosition(GameWorld world, long entityId, GridCoord expected)
        {
            Assert.IsTrue(world.TryGetEntity(entityId, out GameEntity entity));
            Assert.IsTrue(world.TryGetComponent(entity, out PositionComponent position));
            Assert.AreEqual(expected, position.Coord, "entity " + entityId + " expected " + expected.X + "," + expected.Y + " actual " + position.Coord.X + "," + position.Coord.Y);
        }

        private static void AssertDirection(GameWorld world, long entityId, Direction expected)
        {
            Assert.IsTrue(world.TryGetEntity(entityId, out GameEntity entity));
            Assert.IsTrue(world.TryGetComponent(entity, out DirectionComponent direction));
            Assert.AreEqual(expected, direction.Direction);
        }
    }
}
