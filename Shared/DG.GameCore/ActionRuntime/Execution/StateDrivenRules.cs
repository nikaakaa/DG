using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class BehaviorRuntime
{
    private readonly ActionSpecRegistry actionSpecs;
    private readonly ActionRequestAdapter actionAdapter;
    private readonly BehaviorInstanceRunner behaviorRunner = new();
    private readonly StepRunner stepRunner;
    private readonly MoveBatchOrchestrator moveBatchOrchestrator;
    private readonly PrimitiveRequestDispatcher primitiveDispatcher;

    public BehaviorRuntime() : this(ActionSpecRegistry.Default)
    {
    }

    public BehaviorRuntime(ActionSpecRegistry actionSpecs)
        : this(actionSpecs, PrimitiveRunnerRegistry.Default)
    {
    }

    public BehaviorRuntime(ActionSpecRegistry actionSpecs, PrimitiveRunnerRegistry primitiveRunners)
        : this(actionSpecs, primitiveRunners, LubanGameConfigProvider.FromDirectory(GameCoreConfigPath.FindGeneratedJsonDirectory()), ActionPresentationRegistry.Default)
    {
    }

    public BehaviorRuntime(ActionSpecRegistry actionSpecs, PrimitiveRunnerRegistry primitiveRunners, IGameConfigProvider configProvider)
        : this(actionSpecs, primitiveRunners, configProvider, ActionPresentationRegistry.Default)
    {
    }

    public BehaviorRuntime(ActionSpecRegistry actionSpecs, PrimitiveRunnerRegistry primitiveRunners, IGameConfigProvider configProvider, ActionPresentationRegistry actionPresentationRegistry)
    {
        if (primitiveRunners == null) throw new ArgumentNullException(nameof(primitiveRunners));
        if (configProvider == null) throw new ArgumentNullException(nameof(configProvider));
        this.actionSpecs = actionSpecs;
        actionPresentationRegistry ??= new ActionPresentationRegistry(Array.Empty<ActionPresentationConfig>());
        actionAdapter = new ActionRequestAdapter(actionSpecs);
        BatchBehaviorRunnerRegistry batchRunners = BatchBehaviorRunnerCatalog.CreateDefault(actionSpecs);
        stepRunner = new StepRunner(actionSpecs, actionPresentationRegistry);
        PushRunner pushRunner = new PushRunner(actionSpecs);
        moveBatchOrchestrator = new MoveBatchOrchestrator(actionSpecs, batchRunners, pushRunner, stepRunner, behaviorRunner);
        primitiveDispatcher = new PrimitiveRequestDispatcher(actionSpecs, primitiveRunners, BehaviorResolver.Default, configProvider, behaviorRunner);
    }

    public int RunningBehaviorCount => behaviorRunner.RunningInstanceCount;
    public int RunningReservationCount => behaviorRunner.RunningReservationCount;
    public int ActiveBehaviorCount => RunningBehaviorCount;
    public int ActiveReservationCount => RunningReservationCount;

    public bool HasActiveSubjectReservation(long entityId)
    {
        return behaviorRunner.TryGetRunningSubject(entityId, long.MinValue, out _);
    }

    public bool HasRunningMovementClaim(long entityId, long serverTick)
    {
        return behaviorRunner.TryGetRunningSubject(entityId, BehaviorClaimChannel.Movement, serverTick, out _);
    }

    public bool HasActiveCellReservation(GridCoord cell)
    {
        return behaviorRunner.TryGetReservedCell(cell, long.MinValue, out _);
    }

    public BehaviorRuntimeTickResult Tick(GameWorld world, IReadOnlyList<WorldAction> actions, long serverTick)
    {
        var proposals = new List<CommitProposal>();
        var moveRequests = new List<ActionRequest>();
        var actionResults = new Dictionary<long, MoveResult>();
        var reasons = new List<string>();
        var deferredActions = new List<DeferredAction>();
        var actionFacts = new List<ActionFact>();
        var behaviorInstances = new List<ActionBehaviorInstance>();
        var proposalResults = new List<CommitProposalResult>();
        IReadOnlyDictionary<long, ActionSpecId> specIdsByActionId = actions.ToDictionary(action => action.ActionId, action => action.SpecId);
        IReadOnlyDictionary<long, ActionRequest> requestsByActionId = actions
            .Select(action => actionAdapter.FromWorldAction(action))
            .ToDictionary(request => request.ActionId);

        ReleaseReadyBehaviors(world, serverTick, actionResults, proposalResults, reasons, deferredActions, actionFacts);

        for (int i = 0; i < actions.Count; i++)
        {
            ActionRequest request = requestsByActionId[actions[i].ActionId];
            if (TryHandleRunningReservationRequest(world, request, serverTick, actionResults, reasons, actionFacts, behaviorInstances))
            {
                continue;
            }

            primitiveDispatcher.Route(world, request, proposals, moveRequests, actionResults, reasons, actionFacts, behaviorInstances, serverTick);
        }

        moveBatchOrchestrator.Execute(world, moveRequests, serverTick, proposals, actionResults, reasons, deferredActions, actionFacts, behaviorInstances, proposalResults, specIdsByActionId, requestsByActionId);

        return new BehaviorRuntimeTickResult(actionResults, proposalResults, reasons, deferredActions, actionFacts, behaviorInstances);
    }

    private void ReleaseReadyBehaviors(GameWorld world, long serverTick, Dictionary<long, MoveResult> actionResults, List<CommitProposalResult> proposalResults, List<string> reasons, List<DeferredAction> deferredActions, List<ActionFact> actionFacts)
    {
        IReadOnlyList<BehaviorInstanceRelease> releases = behaviorRunner.StepDue(world, serverTick);
        var releaseSpecIdsByActionId = new Dictionary<long, ActionSpecId>();
        var releaseRequestsByActionId = new Dictionary<long, ActionRequest>();
        var routedRequests = new List<ActionRequest>();
        var releaseProposals = new List<CommitProposal>();
        var releaseMovePlans = new List<MovePlan>();
        for (int i = 0; i < releases.Count; i++)
        {
            BehaviorInstanceRelease release = releases[i];
            BehaviorStepOutput output = release.Output;
            releaseSpecIdsByActionId[release.Instance.SourceActionId] = release.Instance.SpecId;
            releaseProposals.AddRange(output.CommitProposals);
            releaseMovePlans.AddRange(output.MovePlans);
            if (output.RoutedRequests.Count != 0)
            {
                for (int requestIndex = 0; requestIndex < output.RoutedRequests.Count; requestIndex++)
                {
                    ActionRequest request = output.RoutedRequests[requestIndex];
                    releaseSpecIdsByActionId[request.ActionId] = request.SpecId;
                    releaseRequestsByActionId[request.ActionId] = request;
                }

                routedRequests.AddRange(output.RoutedRequests);
            }

            foreach (KeyValuePair<long, MoveResult> pair in output.ActionResults)
            {
                actionResults[pair.Key] = pair.Value;
            }

            proposalResults.AddRange(stepRunner.ResolveMovePlans(world, output.MovePlans, behaviorRunner));
            deferredActions.AddRange(output.DeferredActions);
            actionFacts.AddRange(output.ActionFacts);
            reasons.AddRange(output.Reasons);
            reasons.Add(release.Completion ? "behavior-complete" : "behavior-scheduled-output");
        }

        if (routedRequests.Count != 0)
        {
            MoveBatchPlanResult releasePlan = moveBatchOrchestrator.Plan(world, routedRequests, serverTick, releaseProposals, actionResults, reasons, deferredActions, actionFacts, new List<ActionBehaviorInstance>());
            releaseMovePlans.AddRange(releasePlan.MovePlans);
        }

        if (releaseMovePlans.Count != 0)
        {
            proposalResults.AddRange(stepRunner.ResolveMovePlans(world, releaseMovePlans, behaviorRunner));
        }

        if (releaseProposals.Count != 0)
        {
            proposalResults.AddRange(stepRunner.ResolveCommitProposals(world, releaseProposals));
        }

        moveBatchOrchestrator.ApplyProposalResultsToActions(actionResults, proposalResults, reasons, actionFacts, new List<ActionBehaviorInstance>(), serverTick, releaseSpecIdsByActionId, releaseRequestsByActionId);
    }

    private bool TryHandleRunningReservationRequest(GameWorld world, ActionRequest request, long serverTick, Dictionary<long, MoveResult> actionResults, List<string> reasons, List<ActionFact> actionFacts, List<ActionBehaviorInstance> behaviorInstances)
    {
        if (!behaviorRunner.TryGetRunningSubject(request.EntityId, serverTick, out ActionBehaviorInstance instance))
        {
            for (int i = 0; i < request.SubjectEntityIds.Count; i++)
            {
                if (behaviorRunner.TryGetRunningSubject(request.SubjectEntityIds[i], serverTick, out instance))
                {
                    break;
                }
            }
        }

        if (instance == null && request.Target.TargetCoord.HasValue &&
            behaviorRunner.TryGetReservedCell(request.Target.TargetCoord.Value, serverTick, out ActionBehaviorInstance reserved))
        {
            instance = reserved;
        }

        if (instance == null || instance.IncomingPolicy != BehaviorIncomingPolicy.RejectIncoming)
        {
            return false;
        }

        if (instance.CreatedTick == serverTick)
        {
            return false;
        }

        string reason = behaviorRunner.TryGetRunningSubject(request.EntityId, serverTick, out _) ? "running-subject-in-flight" : "running-reservation-in-flight";
        ActionSpec spec = actionSpecs.Get(request.SpecId);
        IReadOnlyList<long> subjectIds = request.SubjectEntityIds.Count == 0 ? new[] { request.EntityId } : request.SubjectEntityIds;
        ActionBehaviorInstance rejectedInstance = ActionBehaviorInstance.Rejected(request, spec, subjectIds, serverTick, reason, "running-reservation-conflict");
        BehaviorStep rejectedStep = behaviorRunner.RunTerminal(rejectedInstance, world);
        actionFacts.AddRange(rejectedStep.Output.ActionFacts);
        behaviorInstances.Add(rejectedInstance);
        actionResults[request.ActionId] = new MoveResult(false, request.EntityId, MoveBatchOrchestrator.CurrentCoord(world, request.EntityId), request.Target.Direction, MoveErrorCode.Blocked, reason, false, default, request.ClientTick);
        reasons.Add(reason);
        return true;
    }
}
}
