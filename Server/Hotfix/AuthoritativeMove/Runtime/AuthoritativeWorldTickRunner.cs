using Fantasy.Async;
using Fantasy.Network;
using DG.GameCore;
using System.Linq;

namespace Fantasy;

public sealed class AuthoritativeWorldTickRunner
{
    private const int LogSampleLimit = 8;
    private readonly GameWorld World;
    private readonly AuthoritativeInputQueue InputQueue;
    private readonly AuthoritativeWorldSyncSystem SyncSystem;
    private readonly WorldActionQueue ActionQueue;
    private readonly BehaviorRuntime RuleExecutionSystem;
    private readonly PresentationFactComposer PresentationComposer;
    private readonly MoveIntentAdapter IntentAdapter = new();
    private readonly Dictionary<long, AuthoritativeMoveInput> pendingMoveInputs = new();
    private readonly int tickIntervalMs;
    private FCancellationToken? cancellationToken;
    private bool running;

    public bool FullWorldDiagnosticsEnabled { get; set; }
    public CandidateScanMode CandidateScanMode { get; set; } = CandidateScanMode.Serial;

    public AuthoritativeWorldTickRunner(GameWorld world, AuthoritativeInputQueue inputQueue, AuthoritativeWorldSyncSystem syncSystem, int tickIntervalMs)
        : this(world, inputQueue, syncSystem, tickIntervalMs, ActionSpecRegistry.Default)
    {
    }

    public AuthoritativeWorldTickRunner(GameWorld world, AuthoritativeInputQueue inputQueue, AuthoritativeWorldSyncSystem syncSystem, int tickIntervalMs, ActionSpecRegistry actionSpecs)
        : this(world, inputQueue, syncSystem, tickIntervalMs, actionSpecs, PrimitiveRunnerRegistry.Default)
    {
    }

    public AuthoritativeWorldTickRunner(GameWorld world, AuthoritativeInputQueue inputQueue, AuthoritativeWorldSyncSystem syncSystem, int tickIntervalMs, ActionSpecRegistry actionSpecs, PrimitiveRunnerRegistry primitiveRunners)
    {
        World = world;
        InputQueue = inputQueue;
        SyncSystem = syncSystem;
        ActionQueue = inputQueue.ActionQueue;
        RuleExecutionSystem = new BehaviorRuntime(actionSpecs, primitiveRunners);
        PresentationComposer = new PresentationFactComposer();
        this.tickIntervalMs = tickIntervalMs;
    }

    public bool Start(Scene scene, Func<IReadOnlyList<Session>> observerProvider)
    {
        if (running)
        {
            return false;
        }

        running = true;
        cancellationToken = FCancellationToken.ToKen;
        Run(scene, observerProvider, cancellationToken).Coroutine();
        return true;
    }

    public void Stop()
    {
        cancellationToken?.Cancel();
        cancellationToken?.Dispose();
        cancellationToken = null;
        running = false;
        InputQueue.FailPending("tick runner stopped");
        foreach (AuthoritativeMoveInput input in pendingMoveInputs.Values)
        {
            input.Complete(AuthoritativePlayerInputStatus.Rejected, new MoveResult(false, input.EntityId, default, input.Direction, MoveErrorCode.UnknownEntity, "tick runner stopped", false, default, input.ClientTick));
        }

        pendingMoveInputs.Clear();
    }

    public WorldDelta Tick(IReadOnlyList<Session> observers)
    {
        long serverTick = World.NextTick();
        World.ExpireRuntimeEffects(serverTick);
        IReadOnlyList<AuthoritativeMoveInput> inputs = InputQueue.DrainMoves(serverTick);
        for (int i = 0; i < inputs.Count; i++)
        {
            AuthoritativeMoveInput input = inputs[i];
            if (!IntentAdapter.TryResolveMoveTarget(World, input.Intent, out GridCoord targetCoord, out MoveResult rejectResult))
            {
                input.Complete(AuthoritativePlayerInputStatus.Rejected, rejectResult);
                continue;
            }

            WorldAction action = ActionQueue.EnqueuePlayerMove(input.EntityId, targetCoord, input.ClientTick);
            pendingMoveInputs[action.ActionId] = input;
        }

        EnqueueAutoMoveActions(serverTick);
        EnqueuePushOnEnterActions(serverTick);
        IReadOnlyList<WorldAction> actions = ActionQueue.DrainReady(serverTick);
        IReadOnlyCollection<long> preRuleTouchedEntityIds = CollectPreRuleTouchedEntityIds(inputs, actions);
        IReadOnlyDictionary<long, string> beforeSnapshot = FullWorldDiagnosticsEnabled ? CreateFullEntitySnapshot() : CreateEntitySnapshot(preRuleTouchedEntityIds);
        LogRuleTickInput(serverTick, inputs, actions, beforeSnapshot);
        BehaviorRuntimeTickResult ruleResult = RuleExecutionSystem.Tick(World, actions, serverTick);
        RecordPresentationFacts(serverTick, actions, ruleResult);
        IReadOnlyCollection<long> postRuleTouchedEntityIds = CollectPostRuleTouchedEntityIds(actions, ruleResult);
        IReadOnlyDictionary<long, string> afterSnapshot = FullWorldDiagnosticsEnabled ? CreateFullEntitySnapshot() : CreateEntitySnapshot(postRuleTouchedEntityIds);
        LogRuleTickResult(serverTick, actions, ruleResult, beforeSnapshot, afterSnapshot);
        EnqueueDeferredActions(ruleResult.DeferredActions);
        foreach (KeyValuePair<long, AuthoritativeMoveInput> pair in pendingMoveInputs.ToArray())
        {
            if (ruleResult.ActionResults.TryGetValue(pair.Key, out MoveResult result))
            {
                pair.Value.Complete(AuthoritativePlayerInputStatus.Resolved, result);
                pendingMoveInputs.Remove(pair.Key);
            }
        }
        IReadOnlyList<AuthoritativeDebugActionInput> debugInputs = InputQueue.DrainDebugInputs(ruleResult.ActionResults.Keys.ToArray());
        for (int i = 0; i < debugInputs.Count; i++)
        {
            AuthoritativeDebugActionInput input = debugInputs[i];
            if (ruleResult.ActionResults.TryGetValue(input.Action.ActionId, out MoveResult result))
            {
                input.Complete(result);
            }
        }
        return SyncSystem.BroadcastDelta(observers);
    }

    public WorldDelta Tick()
    {
        return Tick(Array.Empty<Session>());
    }

    private void EnqueueDeferredActions(IReadOnlyList<DeferredAction> deferredActions)
    {
        if (deferredActions.Count == 0)
        {
            return;
        }

        var results = new List<DeferredEnqueueResult>(deferredActions.Count);
        for (int i = 0; i < deferredActions.Count; i++)
        {
            DeferredAction deferred = deferredActions[i];
            results.Add(ActionQueue.EnqueueDeferred(deferred));
        }

        int enqueuedCount = results.Count(result => result.Enqueued);
        int mergedCount = results.Count - enqueuedCount;
        string sampleText = string.Join(" | ", results
            .GroupBy(result => result.EquivalenceKey)
            .OrderByDescending(group => group.Max(result => result.ContributionCount))
            .ThenBy(group => group.Key)
            .Take(LogSampleLimit)
            .Select(group =>
                "key:" + group.Key +
                " action:" + group.First().Action.ActionId +
                " enqueued:" + group.Any(result => result.Enqueued) +
                " contribution:" + group.Max(result => result.ContributionCount) +
                " samples:[" + string.Join(",", group.First().Action.DeferredCausalitySamples) + "]"));

        TryLogInfo("[AuthoritativeWorldTickRunner] enqueue deferred summary raw:{0} enqueued:{1} merged:{2} samples:{3}", deferredActions.Count, enqueuedCount, mergedCount, sampleText.Length == 0 ? "none" : sampleText);
    }

    private void RecordPresentationFacts(long serverTick, IReadOnlyList<WorldAction> actions, BehaviorRuntimeTickResult result)
    {
        IReadOnlyList<PresentationFact> facts = PresentationComposer.Compose(serverTick, actions, result);
        for (int i = 0; i < facts.Count; i++)
        {
            World.AddPresentationFact(facts[i]);
        }
    }

    private void LogRuleTickInput(long serverTick, IReadOnlyList<AuthoritativeMoveInput> inputs, IReadOnlyList<WorldAction> actions, IReadOnlyDictionary<long, string> beforeSnapshot)
    {
        if (inputs.Count == 0 && actions.Count == 0)
        {
            return;
        }

        string inputText = inputs.Count == 0
            ? "none"
            : string.Join(" | ", inputs.Select(input =>
                "entity:" + input.EntityId +
                " beat:" + input.BeatTick +
                " consume:" + input.ConsumeTick +
                " dir:" + input.Direction +
                " status:" + input.Status +
                " input:" + input.ClientInputId +
                " seq:" + input.SubmitSequence +
                " clientTick:" + input.ClientTick +
                " before:" + SnapshotText(beforeSnapshot, input.EntityId)));
        string actionText = actions.Count == 0
            ? "none"
            : string.Join(" | ", actions.Select(action =>
                "action:" + action.ActionId +
                " spec:" + action.SpecId +
                " entity:" + action.EntityId +
                " target:" + CoordText(action.TargetCoord) +
                " dir:" + action.Direction +
                " ready:" + action.ReadyTick +
                " cost:" + action.CostTicks +
                " causality:" + action.CausalityId +
                " dedupe:" + action.DedupeKey +
                " before:" + SnapshotText(beforeSnapshot, action.EntityId)));

        TryLogInfo("[AuthoritativeWorldTickRunner] rule tick input tick:{0} pendingMoves:{1} actions:{2} inputs:{3} actionList:{4}", serverTick, inputs.Count, actions.Count, inputText, actionText);
    }

    private void LogRuleTickResult(long serverTick, IReadOnlyList<WorldAction> actions, BehaviorRuntimeTickResult result, IReadOnlyDictionary<long, string> beforeSnapshot, IReadOnlyDictionary<long, string> afterSnapshot)
    {
        if (actions.Count == 0 &&
            result.ActionResults.Count == 0 &&
            result.DeferredActions.Count == 0 &&
            result.ProposalResults.Count == 0 &&
            result.Reasons.Count == 0)
        {
            return;
        }

        string actionResultText = result.ActionResults.Count == 0
            ? "none"
            : string.Join(" | ", result.ActionResults.OrderBy(pair => pair.Key).Select(pair =>
                "action:" + pair.Key +
                " success:" + pair.Value.Success +
                " final:(" + pair.Value.FinalCoord.X + "," + pair.Value.FinalCoord.Y + ")" +
                " dir:" + pair.Value.FinalDirection +
                " error:" + pair.Value.ErrorCode +
                " reason:" + pair.Value.Reason));
        string deferredText = result.DeferredActions.Count == 0
            ? "none"
            : string.Join(" | ", result.DeferredActions.Take(LogSampleLimit).Select(deferred =>
                "spec:" + deferred.SpecId +
                " entity:" + deferred.EntityId +
                " subject:[" + string.Join(",", deferred.SubjectEntityIds.OrderBy(entityId => entityId)) + "]" +
                " dir:" + deferred.Direction +
                " created:" + deferred.CreatedTick +
                " ready:" + deferred.ReadyTick +
                " cost:" + deferred.CostTicks +
                " causality:" + deferred.CausalityId +
                " dedupe:" + deferred.DedupeKey +
                " before:" + SnapshotText(beforeSnapshot, deferred.EntityId) +
                " after:" + SnapshotText(afterSnapshot, deferred.EntityId))) +
                (result.DeferredActions.Count > LogSampleLimit ? " | omitted:" + (result.DeferredActions.Count - LogSampleLimit) : string.Empty);
        string proposalText = result.ProposalResults.Count == 0
            ? "none"
            : string.Join(" | ", result.ProposalResults.Select(item =>
                "kind:" + item.Proposal.Kind +
                " action:" + item.Proposal.SourceActionId +
                " state:" + item.Proposal.SourceStateId +
                " entity:" + item.Proposal.EntityId +
                " from:(" + item.Proposal.From.X + "," + item.Proposal.From.Y + ")" +
                " to:(" + item.Proposal.To.X + "," + item.Proposal.To.Y + ")" +
                " dir:" + item.Proposal.Direction +
                " accepted:" + item.Accepted +
                " reason:" + item.Reason +
                " before:" + SnapshotText(beforeSnapshot, item.Proposal.EntityId) +
                " after:" + SnapshotText(afterSnapshot, item.Proposal.EntityId)));
        string reasonText = result.Reasons.Count == 0 ? "none" : string.Join(" | ", result.Reasons);
        string touchedText = BuildTouchedSnapshot(actions, result, beforeSnapshot, afterSnapshot);

        TryLogInfo("[AuthoritativeWorldTickRunner] rule tick result tick:{0} actionResults:{1} deferred:{2} proposals:{3} reasons:{4} touched:{5}", serverTick, actionResultText, deferredText, proposalText, reasonText, touchedText);
    }

    private string BuildTouchedSnapshot(IReadOnlyList<WorldAction> actions, BehaviorRuntimeTickResult result, IReadOnlyDictionary<long, string> beforeSnapshot, IReadOnlyDictionary<long, string> afterSnapshot)
    {
        var ids = new HashSet<long>();
        for (int i = 0; i < actions.Count; i++)
        {
            ids.Add(actions[i].EntityId);
        }

        foreach (DeferredAction deferred in result.DeferredActions)
        {
            ids.Add(deferred.EntityId);
            for (int i = 0; i < deferred.SubjectEntityIds.Count; i++)
            {
                ids.Add(deferred.SubjectEntityIds[i]);
            }
        }

        foreach (CommitProposalResult proposalResult in result.ProposalResults)
        {
            ids.Add(proposalResult.Proposal.EntityId);
        }

        if (ids.Count == 0)
        {
            return "none";
        }

        return string.Join(" | ", ids.OrderBy(id => id).Take(LogSampleLimit).Select(id => "entity:" + id + " before:" + SnapshotText(beforeSnapshot, id) + " after:" + SnapshotText(afterSnapshot, id))) +
            (ids.Count > LogSampleLimit ? " | omitted:" + (ids.Count - LogSampleLimit) : string.Empty);
    }

    private IReadOnlyCollection<long> CollectPreRuleTouchedEntityIds(IReadOnlyList<AuthoritativeMoveInput> inputs, IReadOnlyList<WorldAction> actions)
    {
        var ids = new HashSet<long>();
        for (int i = 0; i < inputs.Count; i++)
        {
            ids.Add(inputs[i].EntityId);
        }

        AddActionEntityIds(actions, ids);
        World.RecordTouchedDiagnostics(ids);
        return ids;
    }

    private IReadOnlyCollection<long> CollectPostRuleTouchedEntityIds(IReadOnlyList<WorldAction> actions, BehaviorRuntimeTickResult result)
    {
        var ids = new HashSet<long>();
        AddActionEntityIds(actions, ids);
        foreach (DeferredAction deferred in result.DeferredActions)
        {
            ids.Add(deferred.EntityId);
            for (int i = 0; i < deferred.SubjectEntityIds.Count; i++)
            {
                ids.Add(deferred.SubjectEntityIds[i]);
            }
        }

        foreach (CommitProposalResult proposalResult in result.ProposalResults)
        {
            ids.Add(proposalResult.Proposal.EntityId);
        }

        IReadOnlyList<DirtyChange> dirty = World.PeekDirtyChanges();
        for (int i = 0; i < dirty.Count; i++)
        {
            ids.Add(dirty[i].EntityId);
        }

        World.RecordTouchedDiagnostics(ids);
        return ids;
    }

    private static void AddActionEntityIds(IReadOnlyList<WorldAction> actions, HashSet<long> ids)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            ids.Add(actions[i].EntityId);
            for (int subjectIndex = 0; subjectIndex < actions[i].SubjectEntityIds.Count; subjectIndex++)
            {
                ids.Add(actions[i].SubjectEntityIds[subjectIndex]);
            }
        }
    }

    private IReadOnlyDictionary<long, string> CreateEntitySnapshot(IReadOnlyCollection<long> entityIds)
    {
        var result = new Dictionary<long, string>();
        foreach (long entityId in entityIds)
        {
            if (!World.TryGetEntity(entityId, out GameEntity entity))
            {
                continue;
            }

            EntitySnapshot snapshot = World.CreateSnapshot(entity);
            result[snapshot.EntityId] = FormatSnapshot(snapshot);
        }

        return result;
    }

    private IReadOnlyDictionary<long, string> CreateFullEntitySnapshot()
    {
        return World.CreateSnapshot().ToDictionary(
            snapshot => snapshot.EntityId,
            FormatSnapshot);
    }

    private static string FormatSnapshot(EntitySnapshot snapshot)
    {
        return "cfg:" + snapshot.ConfigId +
            " pos:(" + snapshot.X + "," + snapshot.Y + ")" +
            " dir:" + snapshot.Direction +
            " push:" + snapshot.Pushable +
            " move:" + snapshot.CanMove +
            " bePushed:" + snapshot.CanBePushed +
            " ports:" + snapshot.PortLocalPorts;
    }

    private static string SnapshotText(IReadOnlyDictionary<long, string> snapshot, long entityId)
    {
        return snapshot.TryGetValue(entityId, out string? value) && value != null ? value : "missing";
    }

    private static string CoordText(GridCoord? coord)
    {
        return coord.HasValue ? "(" + coord.Value.X + "," + coord.Value.Y + ")" : "none";
    }

    private static void TryLogInfo(string message, params object[] args)
    {
        try
        {
            Log.Info(message, args);
        }
        catch (NullReferenceException)
        {
        }
    }

    private async FTask Run(Scene scene, Func<IReadOnlyList<Session>> observerProvider, FCancellationToken token)
    {
        while (!token.IsCancel && running)
        {
            bool completed = await FTask.Wait(scene, tickIntervalMs, token);
            if (!completed)
            {
                break;
            }

            Tick(observerProvider());
        }
    }

    private void EnqueueAutoMoveActions(long serverTick)
    {
        IReadOnlyList<AutoMoveActionCandidate> candidates = World.CollectAutoMoveCandidates(serverTick, CandidateScanMode);
        for (int i = 0; i < candidates.Count; i++)
        {
            AutoMoveActionCandidate candidate = candidates[i];
            ActionQueue.EnqueueAutoMove(candidate.EntityId, candidate.CreatedTick, candidate.CostTicks);
        }

        World.RecordCandidateCommits(candidates.Count);
    }

    private void EnqueuePushOnEnterActions(long serverTick)
    {
        IReadOnlyList<PushOnEnterActionCandidate> candidates = World.CollectPushOnEnterCandidates(serverTick, CandidateScanMode);
        var moved = new HashSet<long>();
        int committed = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            PushOnEnterActionCandidate candidate = candidates[i];
            if (!moved.Add(candidate.SubjectEntityId) ||
                ActionQueue.HasPendingMove(candidate.SubjectEntityId, candidate.SpecId, candidate.Direction) ||
                RuleExecutionSystem.HasRunningMovementClaim(candidate.SubjectEntityId, serverTick))
            {
                continue;
            }

            ActionQueue.EnqueueConfiguredMove(candidate.SpecId, candidate.SubjectEntityId, candidate.Direction, candidate.CreatedTick, candidate.CostTicks);
            committed++;
        }

        World.RecordCandidateCommits(committed);
    }

}
