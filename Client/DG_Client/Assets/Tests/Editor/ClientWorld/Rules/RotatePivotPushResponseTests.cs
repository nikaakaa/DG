using System;
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
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.IsTrue(start.BehaviorInstances.Any(instance => instance.SourceActionId == 1 && instance.State == BehaviorInstanceState.Running));
            PresentationFact fact = ActionFactProjection.ToPresentationFacts(start.ActionFacts).Single(item => item.FactType == PresentationFactType.RotatePivotGroup);
            Assert.AreEqual(PresentationFactResultKind.Success, fact.ResultKind);
            Assert.AreEqual(world.ServerTick, fact.StartTick);
            Assert.AreEqual(world.ServerTick + 1, fact.EndTick);
            Assert.AreEqual(1, fact.EffectiveCostTicks);
            AssertPosition(world, 101, new GridCoord(1, 0));
            BehaviorRuntimeTickResult end = rules.Tick(world);
            Assert.IsTrue(end.ActionResults[1].Success);
            AssertPosition(world, 100, new GridCoord(0, 0));
            AssertPosition(world, 101, new GridCoord(0, -1));
        }

        [Test]
        public void SuccessfulRotation_RotatesMemberDirections()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            SetDirection(world, 101, Direction.Right);
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            AssertDirection(world, 101, Direction.Right);
            BehaviorRuntimeTickResult end = rules.Tick(world);
            Assert.IsTrue(end.ActionResults[1].Success);
            AssertDirection(world, 101, Direction.Down);
        }

        [Test]
        public void SuccessfulRotation_RotatesPivotDefaultPortOrientation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            AssertDirection(world, 100, Direction.Right);
            BehaviorRuntimeTickResult end = rules.Tick(world);
            Assert.IsTrue(end.ActionResults[1].Success);
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
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.AreEqual(0, start.DeferredActions.Count);
            PresentationFact startFact = ActionFactProjection.ToPresentationFacts(start.ActionFacts).Single(item => item.FactType == PresentationFactType.RotatePivotGroup);
            Assert.AreEqual(PresentationFactResultKind.Bounce, startFact.ResultKind);
            Assert.AreEqual(world.ServerTick + 1, startFact.ContactTick);
            Assert.AreEqual(world.ServerTick + 2, startFact.EndTick);
            Assert.AreEqual(2, startFact.EffectiveCostTicks);
            AssertPosition(world, 101, new GridCoord(1, 0));
            BehaviorRuntimeTickResult contact = rules.Tick(world);
            Assert.IsFalse(contact.ActionResults.ContainsKey(1));
            Assert.AreEqual(1, contact.DeferredActions.Count);
            Assert.AreEqual(201, contact.DeferredActions[0].EntityId);
            Assert.AreEqual(Direction.Down, contact.DeferredActions[0].Direction);
            PresentationFact impactFact = ActionFactProjection.ToPresentationFacts(contact.ActionFacts).Single(item => item.FactType == PresentationFactType.RotatePivotImpact);
            Assert.AreEqual(world.ServerTick, impactFact.ServerTick);
            Assert.AreEqual(world.ServerTick, contact.DeferredActions[0].CreatedTick);
            PushOriginContext context = contact.DeferredActions[0].OriginContexts.Single();
            Assert.AreEqual(101, context.ImpactMemberId);
            Assert.AreEqual(new GridCoord(1, 0), context.ImpactFrom);
            Assert.AreEqual(new GridCoord(1, -1), context.ImpactTo);
            BehaviorRuntimeTickResult result = rules.Tick(world);
            Assert.IsTrue(result.ActionResults[1].Success);
            Assert.AreEqual(0, result.DeferredActions.Count);
            AssertPosition(world, 101, new GridCoord(1, 0));
        }

        [Test]
        public void RotationSweepBlocker_DetectsRadiusTwoArcCells()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, 2, 0));
            SetPivot(world, 100);
            AddPushable(world, 201, new GridCoord(1, -1));
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 102, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.AreEqual(0, start.DeferredActions.Count);
            BehaviorRuntimeTickResult contact = rules.Tick(world);
            Assert.AreEqual(1, contact.DeferredActions.Count);
            Assert.AreEqual(201, contact.DeferredActions[0].EntityId);
            BehaviorRuntimeTickResult result = rules.Tick(world);
            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(1, 0));
            AssertPosition(world, 102, new GridCoord(2, 0));
        }

        [Test]
        public void SameDirectionTorqueContributions_RotateOnlyOnce()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down), Action(2, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.IsFalse(start.ActionResults.ContainsKey(2));
            Assert.AreEqual(2, start.BehaviorInstances.Count(instance => instance.State == BehaviorInstanceState.Running));
            BehaviorRuntimeTickResult result = rules.Tick(world);
            Assert.IsTrue(result.ActionResults[1].Success);
            Assert.IsTrue(result.ActionResults[2].Success);
            AssertPosition(world, 101, new GridCoord(0, -1));
        }

        [Test]
        public void OppositeTorqueContributions_CancelByCount()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, -1, 0));
            SetPivot(world, 100);

            BehaviorRuntimeTickResult result = Tick(world, Action(1, 101, Direction.Down), Action(2, 102, Direction.Down));

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
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Left));
            BehaviorRuntimeTickResult result = rules.Tick(world);

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
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

            BehaviorRuntimeTickResult result = Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(result.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(1, 0));
            Assert.AreEqual(0, result.DeferredActions.Count);
        }

        [Test]
        public void InternalCurrentCells_DoNotBlockRotation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, 2, 0));
            SetPivot(world, 100);
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            AssertPosition(world, 101, new GridCoord(1, 0));
            AssertPosition(world, 102, new GridCoord(2, 0));
            BehaviorRuntimeTickResult result = rules.Tick(world);
            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(0, -1));
            AssertPosition(world, 102, new GridCoord(0, -2));
        }

        [Test]
        public void DuplicateRotatedTargets_CancelRotation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, 1, 0));
            SetPivot(world, 100);

            BehaviorRuntimeTickResult result = Tick(world, Action(1, 101, Direction.Down));

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
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.AreEqual(0, start.DeferredActions.Count);
            BehaviorRuntimeTickResult contact = rules.Tick(world);
            Assert.AreEqual(2, contact.DeferredActions.Count);
            CollectionAssert.AreEqual(new[] { 201L, 202L }, contact.DeferredActions.Select(action => action.EntityId).ToArray());
            Assert.AreEqual(2, ActionFactProjection.ToPresentationFacts(contact.ActionFacts).Count(item => item.FactType == PresentationFactType.RotatePivotImpact));
            BehaviorRuntimeTickResult result = rules.Tick(world);
            Assert.IsTrue(result.ActionResults[1].Success);
            Assert.AreEqual(0, result.DeferredActions.Count);
            Assert.AreEqual(Direction.Left, contact.DeferredActions.Single(action => action.EntityId == 201).Direction);
            Assert.AreEqual(Direction.Right, contact.DeferredActions.Single(action => action.EntityId == 202).Direction);
            PushOriginContext lowerContext = contact.DeferredActions.Single(action => action.EntityId == 201).OriginContexts.Single();
            PushOriginContext upperContext = contact.DeferredActions.Single(action => action.EntityId == 202).OriginContexts.Single();
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
        public void LaterSweepContact_DoesNotReleaseImpact()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0), (102, 2, 0));
            SetPivot(world, 100);
            AddPushable(world, 201, new GridCoord(2, -1));
            AddPushable(world, 202, new GridCoord(0, -2));
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 102, Direction.Down, 4));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            IReadOnlyList<PresentationFact> startPresentationFacts = ActionFactProjection.ToPresentationFacts(start.ActionFacts);
            Assert.AreEqual(1, startPresentationFacts.Count, string.Join(",", start.ActionResults.Select(pair => pair.Key + ":" + pair.Value.Reason)));
            PresentationFact fact = startPresentationFacts.Single(item => item.FactType == PresentationFactType.RotatePivotGroup);
            while (world.ServerTick < fact.ContactTick - 1)
            {
                rules.Tick(world);
            }

            BehaviorRuntimeTickResult contact = rules.Tick(world);
            Assert.AreEqual(1, contact.DeferredActions.Count);
            Assert.AreEqual(201, contact.DeferredActions[0].EntityId);
            Assert.IsFalse(contact.DeferredActions.Any(action => action.EntityId == 202));
            Assert.Greater(fact.ContactProgress, 0d);
            Assert.Less(fact.ContactProgress, 1d);
            Assert.AreEqual(fact.ContactTick, contact.DeferredActions[0].CreatedTick);
        }

        [Test]
        public void ActiveTimedSubject_RejectsIncomingActionsUntilEndTick()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down, 3));
            BehaviorRuntimeTickResult reject = rules.Tick(world, Action(2, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.IsFalse(reject.ActionResults[2].Success);
            Assert.AreEqual("running-subject-in-flight", reject.ActionResults[2].Reason);
            Assert.IsFalse(reject.ActionResults.ContainsKey(1));
            rules.Tick(world);
            BehaviorRuntimeTickResult end = rules.Tick(world);
            Assert.IsTrue(end.ActionResults[1].Success);
            AssertPosition(world, 101, new GridCoord(0, -1));
        }

        [Test]
        public void OrdinaryMove_CreatesRunningBehaviorInstanceBeforeCompletion()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(300, 300, new GridCoord(0, 0))));
            var rules = new BehaviorRuntime();
            world.NextTick();

            BehaviorRuntimeTickResult start = rules.Tick(world, new[] { new WorldAction(1, WorldActionPriority.Player, "player_move", 300, new GridCoord(1, 0), Direction.None, 1, 0, 0, 1) }, world.ServerTick);

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.AreEqual(1, rules.ActiveBehaviorCount);
            Assert.AreEqual(1, rules.ActiveReservationCount);
            Assert.AreEqual(1, start.BehaviorInstances.Count);
            Assert.AreEqual(1, start.BehaviorInstances[0].InstanceId);
            Assert.AreEqual(BehaviorInstanceState.Running, start.BehaviorInstances[0].State);
            Assert.IsTrue(start.BehaviorInstances[0].CompletionOutput.ActionFacts.Any(fact => fact.FactType == ActionFactType.EntityMoved && fact.HasPresentationProjection));
            AssertPosition(world, 300, new GridCoord(0, 0));

            world.NextTick();
            BehaviorRuntimeTickResult end = rules.Tick(world, Array.Empty<WorldAction>(), world.ServerTick);

            Assert.IsTrue(end.ActionResults[1].Success);
            Assert.AreEqual(0, rules.ActiveBehaviorCount);
            Assert.AreEqual(0, rules.ActiveReservationCount);
            Assert.AreEqual(0, end.BehaviorInstances.Count);
            Assert.IsTrue(end.ActionFacts.Any(fact => fact.FactType == ActionFactType.EntityMoved && fact.HasPresentationProjection));
            AssertPosition(world, 300, new GridCoord(1, 0));
        }

        [Test]
        public void ActiveBehavior_CreatesSubjectAndCellReservation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            var rules = new BehaviorRuntime();
            world.NextTick();

            BehaviorRuntimeTickResult start = rules.Tick(world, new[] { Action(1, 101, Direction.Down, 3) }, world.ServerTick);

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.AreEqual(1, rules.ActiveBehaviorCount);
            Assert.AreEqual(1, rules.ActiveReservationCount);
            Assert.IsTrue(rules.HasActiveSubjectReservation(101));
            Assert.IsTrue(rules.HasActiveCellReservation(new GridCoord(1, 0)));
            Assert.IsTrue(rules.HasActiveCellReservation(new GridCoord(0, -1)));
        }

        [Test]
        public void IncomingAction_TargetingReservedCellIsRejected()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(301, 301, new GridCoord(-1, -1))));
            var rules = new RuleHarness();

            rules.Tick(world, Action(1, 101, Direction.Down, 3));
            BehaviorRuntimeTickResult reject = rules.Tick(world, new WorldAction(2, WorldActionPriority.Player, "player_move", 301, new GridCoord(0, -1), Direction.None, 2, 0, 0, 1));

            Assert.IsFalse(reject.ActionResults[2].Success);
            Assert.AreEqual("running-reservation-in-flight", reject.ActionResults[2].Reason);
            AssertPosition(world, 301, new GridCoord(-1, -1));
        }

        [Test]
        public void RotateFacts_AreActionFactsAndPresentationFactsAreProjectionOnly()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));
            BehaviorRuntimeTickResult end = rules.Tick(world);

            Assert.IsTrue(start.ActionFacts.Any(fact => fact.FactType == ActionFactType.RotateStarted && fact.HasPresentationProjection));
            Assert.IsTrue(ActionFactProjection.ToPresentationFacts(start.ActionFacts).Any(fact => fact.FactType == PresentationFactType.RotatePivotGroup));
            Assert.IsTrue(end.ActionFacts.Any(fact => fact.FactType == ActionFactType.RotateCompleted && !fact.HasPresentationProjection));
            Assert.AreEqual(0, ActionFactProjection.ToPresentationFacts(end.ActionFacts).Count);
        }

        [Test]
        public void RotateActionFacts_DeclareProjectionWithoutPresentationReverseMapping()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            ActionFact fact = start.ActionFacts.Single(item => item.FactType == ActionFactType.RotateStarted);
            Assert.AreEqual(PresentationFactType.RotatePivotGroup, fact.ProjectionFactType);
            Assert.AreEqual(PresentationFactResultKind.Success, fact.ResultKind);
            Assert.AreEqual(1, fact.SourceActionId);
            Assert.AreEqual(100, fact.PivotEntityId);
            Assert.AreEqual(new GridCoord(0, 0), fact.PivotCoord);
            Assert.AreEqual(RotatePivotDirection.Clockwise, fact.RotateDirection);
            Assert.AreEqual(2, fact.Members.Count);
            PresentationFact projection = ActionFactProjection.ToPresentationFact(fact);
            Assert.AreEqual(PresentationFactType.RotatePivotGroup, projection.FactType);
            Assert.AreEqual(PresentationFactResultKind.Success, projection.ResultKind);
        }

        [Test]
        public void BehaviorInstanceRunner_ReleasesEnterTickAndExitOutputs()
        {
            var startFact = ActionFact.WithProjection(
                ActionFactType.RotateStarted,
                5,
                PresentationFactType.RotatePivotGroup,
                PresentationFactResultKind.Success,
                10,
                0,
                100,
                new[] { 100L },
                new GridCoord(0, 0),
                new GridCoord(0, 1),
                Direction.Up,
                5,
                0,
                7,
                1d,
                2,
                100,
                new GridCoord(0, 0),
                RotatePivotDirection.Clockwise,
                Array.Empty<PresentationFactMember>(),
                Array.Empty<PresentationFactImpact>());
            var contactFact = ActionFact.WithProjection(
                ActionFactType.RotateContacted,
                6,
                PresentationFactType.RotatePivotImpact,
                PresentationFactResultKind.Impact,
                10,
                0,
                100,
                new[] { 200L },
                new GridCoord(0, 0),
                new GridCoord(0, 1),
                Direction.Up,
                5,
                6,
                7,
                0.5d,
                2,
                100,
                new GridCoord(0, 0),
                RotatePivotDirection.Clockwise,
                Array.Empty<PresentationFactMember>(),
                Array.Empty<PresentationFactImpact>());
            var completionFact = ActionFact.Internal(ActionFactType.RotateCompleted, 10);
            var unit = new ActionBehaviorInstance(
                10,
                10,
                10,
                "mechanism_push",
                "rotate-pivot",
                "rotate_pivot_runner",
                "move",
                new ActionSourceContext(ActionSourceKind.Debug, 100, 0, WorldTag.SourceDebug),
                new[] { 100L },
                BehaviorInstanceState.Running,
                string.Empty,
                BehaviorClaimSet.FromSubjects(new[] { 100L }, BehaviorIncomingPolicy.RejectIncoming),
                5,
                5,
                string.Empty,
                "test",
                5,
                7,
                2,
                2,
                BehaviorCompletionMode.CompleteAtEndTick,
                BehaviorIncomingPolicy.RejectIncoming,
                new[] { startFact },
                new[] { new BehaviorScheduledOutput(6, new BehaviorStepOutput(Array.Empty<MovePlan>(), new Dictionary<long, MoveResult>(), Array.Empty<DeferredAction>(), new[] { contactFact })) },
                new BehaviorStepOutput(Array.Empty<MovePlan>(), new Dictionary<long, MoveResult>(), Array.Empty<DeferredAction>(), new[] { completionFact }));
            var runner = new BehaviorInstanceRunner();
            var world = new GameWorld();

            BehaviorStep enter = runner.Start(unit, world);
            IReadOnlyList<BehaviorInstanceRelease> contact = runner.StepDue(world, 6);
            IReadOnlyList<BehaviorInstanceRelease> completion = runner.StepDue(world, 7);

            Assert.AreEqual(ActionFactType.RotateStarted, enter.Output.ActionFacts.Single().FactType);
            Assert.AreEqual(ActionFactType.RotateContacted, contact.Single().Output.ActionFacts.Single().FactType);
            Assert.AreEqual(ActionFactType.RotateCompleted, completion.Single().Output.ActionFacts.Single().FactType);
            Assert.AreEqual(0, runner.RunningInstanceCount);
        }

        [Test]
        public void RotateBounce_ReservesOriginalAuthorityCellUntilCompletion()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            AddPushable(world, 201, new GridCoord(1, -1));
            var rules = new BehaviorRuntime();
            world.NextTick();

            rules.Tick(world, new[] { Action(1, 101, Direction.Down) }, world.ServerTick);

            Assert.IsTrue(rules.HasActiveCellReservation(new GridCoord(1, 0)));
            world.NextTick();
            rules.Tick(world, Array.Empty<WorldAction>(), world.ServerTick);
            Assert.IsTrue(rules.HasActiveCellReservation(new GridCoord(1, 0)));
            world.NextTick();
            rules.Tick(world, Array.Empty<WorldAction>(), world.ServerTick);
            Assert.IsFalse(rules.HasActiveCellReservation(new GridCoord(1, 0)));
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
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.AreEqual(0, start.DeferredActions.Count);
            BehaviorRuntimeTickResult result = rules.Tick(world);
            Assert.AreEqual(1, result.DeferredActions.Count);
            CollectionAssert.AreEquivalent(new[] { 201L, 202L }, result.DeferredActions[0].SubjectEntityIds.ToArray());
            AssertPosition(world, 101, new GridCoord(1, 0));
        }

        [Test]
        public void BodyWithoutPivot_KeepsOrdinaryPushTranslation()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 100, Direction.Down));
            BehaviorRuntimeTickResult result = rules.Tick(world);

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 100, new GridCoord(0, -1));
            AssertPosition(world, 101, new GridCoord(1, -1));
        }

        [Test]
        public void RuntimeEffectPivot_EnablesRotateResponse()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            var spec = new EffectSpec("runtime_pivot", EffectKind.RotatePivot, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.None, true, true, WorldTag.None);
            AddEffect(world, spec, 100, 0, "pivot");
            var rules = new RuleHarness();

            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            BehaviorRuntimeTickResult result = rules.Tick(world);
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
            var rules = new RuleHarness();
            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 101, Direction.Down));

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            BehaviorRuntimeTickResult result = rules.Tick(world);
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
            var rules = new RuleHarness();
            BehaviorRuntimeTickResult start = rules.Tick(world, Action(1, 100, Direction.Down));
            BehaviorRuntimeTickResult result = rules.Tick(world);

            Assert.IsFalse(start.ActionResults.ContainsKey(1));
            Assert.IsTrue(result.ActionResults[1].Success);
            AssertPosition(world, 100, new GridCoord(0, -1));
            AssertPosition(world, 101, new GridCoord(1, -1));
        }

        [Test]
        public void DeferredRotateImpactPush_ReentersBehaviorInstancePipeline()
        {
            var world = CreateBody((100, 0, 0), (101, 1, 0));
            SetPivot(world, 100);
            AddPushable(world, 201, new GridCoord(1, -1));
            var rules = new BehaviorRuntime();
            var queue = new WorldActionQueue();
            world.NextTick();
            rules.Tick(world, new[] { Action(1, 101, Direction.Down) }, world.ServerTick);
            world.NextTick();
            BehaviorRuntimeTickResult contact = rules.Tick(world, Array.Empty<WorldAction>(), world.ServerTick);
            DeferredEnqueueResult enqueue = queue.EnqueueDeferred(contact.DeferredActions.Single());

            IReadOnlyList<WorldAction> notReady = queue.DrainReady(enqueue.Action.ReadyTick - 1);
            IReadOnlyList<WorldAction> ready = queue.DrainReady(enqueue.Action.ReadyTick);
            while (world.ServerTick < enqueue.Action.ReadyTick)
            {
                world.NextTick();
            }

            BehaviorRuntimeTickResult deferredStart = rules.Tick(world, ready, enqueue.Action.ReadyTick);

            Assert.AreEqual(0, notReady.Count);
            Assert.AreEqual(1, ready.Count);
            Assert.AreEqual(ActionSourceKind.Handoff, deferredStart.BehaviorInstances.Single().Source.Kind);
            Assert.AreEqual(ready.Single().ActionId, deferredStart.BehaviorInstances.Single().SourceActionId);
            Assert.IsFalse(deferredStart.ActionResults.ContainsKey(ready.Single().ActionId));
            Assert.Greater(rules.ActiveBehaviorCount, 0);
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

        private static BehaviorRuntimeTickResult Tick(GameWorld world, params WorldAction[] actions)
        {
            world.NextTick();
            return new BehaviorRuntime().Tick(world, actions, world.ServerTick);
        }

        private sealed class RuleHarness
        {
            private readonly BehaviorRuntime rules = new();

            public BehaviorRuntimeTickResult Tick(GameWorld world, params WorldAction[] actions)
            {
                world.NextTick();
                return rules.Tick(world, actions, world.ServerTick);
            }
        }

        private static WorldAction Action(long actionId, long entityId, Direction direction)
        {
            return Action(actionId, entityId, direction, 1);
        }

        private static WorldAction Action(long actionId, long entityId, Direction direction, int costTicks)
        {
            return new WorldAction(actionId, WorldActionPriority.Mechanism, "mechanism_push", entityId, null, direction, 0, 0, 0, costTicks);
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
