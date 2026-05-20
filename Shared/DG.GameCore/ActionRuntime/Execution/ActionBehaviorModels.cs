using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum BehaviorStepResult
{
    Completed = 1,
    Running = 2,
    Rejected = 3,
    Failed = 4,
    Cancelled = 5
}

public enum BehaviorRunnerPhase
{
    Enter = 1,
    Tick = 2,
    Exit = 3,
    Complete = 4
}

public enum BehaviorCompletionMode
{
    CompleteAtEndTick = 1
}

public enum BehaviorIncomingPolicy
{
    RejectIncoming = 1,
    DeferIncomingUntilEnd = 2
}

public enum BehaviorClaimChannel
{
    Main = 1,
    Movement = 2,
    Status = 3,
    Interaction = 4,
    Debug = 5,
    Presentation = 6
}

public enum BehaviorClaimMode
{
    Exclusive = 1,
    Shared = 2
}

public sealed class ActionBehaviorContext
{
    public ActionBehaviorContext(GameWorld world, IReadOnlyList<ActionRequest> requests, long serverTick)
    {
        World = world;
        Requests = requests ?? Array.Empty<ActionRequest>();
        ServerTick = serverTick;
    }

    public GameWorld World { get; }
    public IReadOnlyList<ActionRequest> Requests { get; }
    public long ServerTick { get; }
}

public sealed class ActionBehaviorResult
{
    public ActionBehaviorResult(IReadOnlyList<ActionRequest> remainingRequests, IReadOnlyList<MovePlan> movePlans, IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionFact> actionFacts, IReadOnlyList<ActionBehaviorInstance> behaviorInstances, IReadOnlyList<string> reasons)
    {
        RemainingRequests = remainingRequests ?? Array.Empty<ActionRequest>();
        MovePlans = movePlans ?? Array.Empty<MovePlan>();
        ActionResults = actionResults ?? new Dictionary<long, MoveResult>();
        DeferredActions = deferredActions ?? Array.Empty<DeferredAction>();
        ActionFacts = actionFacts ?? Array.Empty<ActionFact>();
        BehaviorInstances = behaviorInstances ?? Array.Empty<ActionBehaviorInstance>();
        Reasons = reasons ?? Array.Empty<string>();
    }

    public IReadOnlyList<ActionRequest> RemainingRequests { get; }
    public IReadOnlyList<MovePlan> MovePlans { get; }
    public IReadOnlyDictionary<long, MoveResult> ActionResults { get; }
    public IReadOnlyList<DeferredAction> DeferredActions { get; }
    public IReadOnlyList<ActionFact> ActionFacts { get; }
    public IReadOnlyList<ActionBehaviorInstance> BehaviorInstances { get; }
    public IReadOnlyList<string> Reasons { get; }

    public static ActionBehaviorResult PassThrough(IReadOnlyList<ActionRequest> requests)
    {
        return new ActionBehaviorResult(requests, Array.Empty<MovePlan>(), new Dictionary<long, MoveResult>(), Array.Empty<DeferredAction>(), Array.Empty<ActionFact>(), Array.Empty<ActionBehaviorInstance>(), Array.Empty<string>());
    }
}

public sealed class BehaviorStepOutput
{
    private static readonly IReadOnlyDictionary<long, MoveResult> EmptyActionResults = new Dictionary<long, MoveResult>();

    public BehaviorStepOutput(IReadOnlyList<MovePlan> movePlans, IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<DeferredAction> deferredActions)
        : this(movePlans, Array.Empty<CommitProposal>(), Array.Empty<ActionRequest>(), actionResults, deferredActions, Array.Empty<ActionFact>(), Array.Empty<BehaviorClaimSet>(), Array.Empty<string>(), Array.Empty<string>())
    {
    }

    public BehaviorStepOutput(IReadOnlyList<MovePlan> movePlans, IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionFact> actionFacts)
        : this(movePlans, Array.Empty<CommitProposal>(), Array.Empty<ActionRequest>(), actionResults, deferredActions, actionFacts, Array.Empty<BehaviorClaimSet>(), Array.Empty<string>(), Array.Empty<string>())
    {
    }

    public BehaviorStepOutput(IReadOnlyList<MovePlan> movePlans, IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionFact> actionFacts, IReadOnlyList<string> reasons)
        : this(movePlans, Array.Empty<CommitProposal>(), Array.Empty<ActionRequest>(), actionResults, deferredActions, actionFacts, Array.Empty<BehaviorClaimSet>(), reasons, Array.Empty<string>())
    {
    }

    public BehaviorStepOutput(IReadOnlyList<MovePlan> movePlans, IReadOnlyList<CommitProposal> commitProposals, IReadOnlyList<ActionRequest> routedRequests, IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionFact> actionFacts, IReadOnlyList<string> reasons)
        : this(movePlans, commitProposals, routedRequests, actionResults, deferredActions, actionFacts, Array.Empty<BehaviorClaimSet>(), reasons, Array.Empty<string>())
    {
    }

    public BehaviorStepOutput(IReadOnlyList<MovePlan> movePlans, IReadOnlyList<CommitProposal> commitProposals, IReadOnlyList<ActionRequest> routedRequests, IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionFact> actionFacts, IReadOnlyList<BehaviorClaimSet> claimChanges, IReadOnlyList<string> reasons, IReadOnlyList<string> trace)
    {
        MovePlans = movePlans ?? Array.Empty<MovePlan>();
        CommitProposals = commitProposals ?? Array.Empty<CommitProposal>();
        RoutedRequests = routedRequests ?? Array.Empty<ActionRequest>();
        ActionResults = actionResults ?? new Dictionary<long, MoveResult>();
        DeferredActions = deferredActions ?? Array.Empty<DeferredAction>();
        ActionFacts = actionFacts ?? Array.Empty<ActionFact>();
        ClaimChanges = claimChanges ?? Array.Empty<BehaviorClaimSet>();
        Reasons = reasons ?? Array.Empty<string>();
        Trace = trace ?? Array.Empty<string>();
    }

    public IReadOnlyList<MovePlan> MovePlans { get; }
    public IReadOnlyList<CommitProposal> CommitProposals { get; }
    public IReadOnlyList<ActionRequest> RoutedRequests { get; }
    public IReadOnlyDictionary<long, MoveResult> ActionResults { get; }
    public IReadOnlyList<DeferredAction> DeferredActions { get; }
    public IReadOnlyList<ActionFact> ActionFacts { get; }
    public IReadOnlyList<BehaviorClaimSet> ClaimChanges { get; }
    public IReadOnlyList<string> Reasons { get; }
    public IReadOnlyList<string> Trace { get; }

    public static BehaviorStepOutput Empty { get; } = new(Array.Empty<MovePlan>(), Array.Empty<CommitProposal>(), Array.Empty<ActionRequest>(), EmptyActionResults, Array.Empty<DeferredAction>(), Array.Empty<ActionFact>(), Array.Empty<BehaviorClaimSet>(), Array.Empty<string>(), Array.Empty<string>());
}

public readonly struct BehaviorStep
{
    public BehaviorStep(BehaviorStepResult result, BehaviorStepOutput output)
    {
        Result = result;
        Output = output ?? BehaviorStepOutput.Empty;
    }

    public BehaviorStepResult Result { get; }
    public BehaviorStepOutput Output { get; }
}

public readonly struct BehaviorRunnerContext : IBehaviorStepContext
{
    public BehaviorRunnerContext(GameWorld world, long serverTick, BehaviorRunnerPhase phase)
    {
        World = world;
        ServerTick = serverTick;
        Phase = phase;
    }

    public GameWorld World { get; }
    public long ServerTick { get; }
    public BehaviorRunnerPhase Phase { get; }
}

public sealed class BehaviorRunner
{
    private readonly IBehaviorStateMachine stateMachine;

    public BehaviorRunner(IBehaviorStateMachine stateMachine)
    {
        this.stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
    }

    public BehaviorStep Step(ActionBehaviorInstance instance, BehaviorRunnerContext context)
    {
        BehaviorStepOutput output = context.Phase switch
        {
            BehaviorRunnerPhase.Enter => stateMachine.Enter(instance, context),
            BehaviorRunnerPhase.Tick => stateMachine.Tick(instance, context),
            BehaviorRunnerPhase.Exit => stateMachine.Exit(instance, context),
            BehaviorRunnerPhase.Complete => stateMachine.Exit(instance, context),
            _ => BehaviorStepOutput.Empty
        };
        BehaviorStepResult result = ResolveStepResult(instance, context.Phase);
        return new BehaviorStep(result, output);
    }

    private static BehaviorStepResult ResolveStepResult(ActionBehaviorInstance instance, BehaviorRunnerPhase phase)
    {
        switch (instance.State)
        {
            case BehaviorInstanceState.Rejected:
                return BehaviorStepResult.Rejected;
            case BehaviorInstanceState.Failed:
                return BehaviorStepResult.Failed;
            case BehaviorInstanceState.Cancelled:
                return BehaviorStepResult.Cancelled;
        }

        return phase == BehaviorRunnerPhase.Exit || phase == BehaviorRunnerPhase.Complete
            ? BehaviorStepResult.Completed
            : BehaviorStepResult.Running;
    }
}

public sealed class CompletedActionBehaviorStateMachine : IBehaviorStateMachine
{
    private readonly BehaviorStepOutput output;

    public CompletedActionBehaviorStateMachine(BehaviorStepOutput output)
    {
        this.output = output ?? BehaviorStepOutput.Empty;
    }

    public BehaviorStepOutput Enter(ActionBehaviorInstance instance, BehaviorRunnerContext context)
    {
        return output;
    }

    public BehaviorStepOutput Tick(ActionBehaviorInstance instance, BehaviorRunnerContext context)
    {
        return BehaviorStepOutput.Empty;
    }

    public BehaviorStepOutput Exit(ActionBehaviorInstance instance, BehaviorRunnerContext context)
    {
        return output;
    }
}

public interface IBatchBehaviorRunner
{
    string BehaviorId { get; }
    bool TryResolve(ActionBehaviorContext context, out ActionBehaviorResult result);
}

public sealed class BatchBehaviorRunnerRegistry
{
    private readonly List<IBatchBehaviorRunner> runners = new();

    public BatchBehaviorRunnerRegistry()
    {
    }

    public BatchBehaviorRunnerRegistry(IEnumerable<IBatchBehaviorRunner> runners)
    {
        if (runners == null)
        {
            return;
        }

        foreach (IBatchBehaviorRunner runner in runners)
        {
            Register(runner);
        }
    }

    public static BatchBehaviorRunnerRegistry Empty { get; } = new();

    public void Register(IBatchBehaviorRunner runner)
    {
        if (runner == null)
        {
            throw new ArgumentNullException(nameof(runner));
        }

        if (string.IsNullOrWhiteSpace(runner.BehaviorId))
        {
            throw new InvalidOperationException("Batch behavior runner id is empty.");
        }

        if (runners.Any(item => string.Equals(item.BehaviorId, runner.BehaviorId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Duplicate batch behavior runner id: " + runner.BehaviorId);
        }

        runners.Add(runner);
    }

    public ActionBehaviorResult Resolve(GameWorld world, IReadOnlyList<ActionRequest> requests, long serverTick)
    {
        IReadOnlyList<ActionRequest> remaining = requests ?? Array.Empty<ActionRequest>();
        var movePlans = new List<MovePlan>();
        var actionResults = new Dictionary<long, MoveResult>();
        var deferredActions = new List<DeferredAction>();
        var actionFacts = new List<ActionFact>();
        var behaviorInstances = new List<ActionBehaviorInstance>();
        var reasons = new List<string>();
        for (int i = 0; i < runners.Count; i++)
        {
            if (remaining.Count == 0)
            {
                break;
            }

            if (!runners[i].TryResolve(new ActionBehaviorContext(world, remaining, serverTick), out ActionBehaviorResult result))
            {
                continue;
            }

            remaining = result.RemainingRequests;
            movePlans.AddRange(result.MovePlans);
            foreach (KeyValuePair<long, MoveResult> pair in result.ActionResults)
            {
                actionResults[pair.Key] = pair.Value;
            }

            deferredActions.AddRange(result.DeferredActions);
            actionFacts.AddRange(result.ActionFacts);
            behaviorInstances.AddRange(result.BehaviorInstances);
            reasons.AddRange(result.Reasons);
        }

        return new ActionBehaviorResult(remaining, movePlans, actionResults, deferredActions, actionFacts, behaviorInstances, reasons);
    }
}

public readonly struct ResourceKey : IEquatable<ResourceKey>
{
    public ResourceKey(string value)
    {
        Value = value ?? string.Empty;
    }

    public string Value { get; }
    public bool IsValid => !string.IsNullOrEmpty(Value);

    public bool Equals(ResourceKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is ResourceKey other && Equals(other);
    public override int GetHashCode() => Value == null ? 0 : Value.GetHashCode();
    public override string ToString() => Value;
}

public sealed class BehaviorClaimSet
{
    public BehaviorClaimSet(IReadOnlyList<long> subjectEntityIds, IReadOnlyList<GridCoord> cells, IReadOnlyList<ResourceKey> resources, BehaviorIncomingPolicy incomingPolicy)
        : this(subjectEntityIds, cells, resources, incomingPolicy, BehaviorClaimChannel.Movement, BehaviorClaimMode.Exclusive)
    {
    }

    public BehaviorClaimSet(IReadOnlyList<long> subjectEntityIds, IReadOnlyList<GridCoord> cells, IReadOnlyList<ResourceKey> resources, BehaviorIncomingPolicy incomingPolicy, BehaviorClaimChannel channel, BehaviorClaimMode mode)
    {
        SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? Array.Empty<long>() : subjectEntityIds.Distinct().OrderBy(id => id).ToArray();
        Cells = cells == null || cells.Count == 0 ? Array.Empty<GridCoord>() : cells.Distinct().OrderBy(coord => coord.X).ThenBy(coord => coord.Y).ToArray();
        Resources = resources == null || resources.Count == 0 ? Array.Empty<ResourceKey>() : resources.Where(resource => resource.IsValid).Distinct().OrderBy(resource => resource.Value).ToArray();
        IncomingPolicy = incomingPolicy;
        Channel = channel;
        Mode = mode;
    }

    public IReadOnlyList<long> SubjectEntityIds { get; }
    public IReadOnlyList<GridCoord> Cells { get; }
    public IReadOnlyList<ResourceKey> Resources { get; }
    public BehaviorIncomingPolicy IncomingPolicy { get; }
    public BehaviorClaimChannel Channel { get; }
    public BehaviorClaimMode Mode { get; }

    public static BehaviorClaimSet FromSubjects(IReadOnlyList<long> subjectEntityIds, BehaviorIncomingPolicy incomingPolicy)
    {
        return new BehaviorClaimSet(subjectEntityIds, Array.Empty<GridCoord>(), Array.Empty<ResourceKey>(), incomingPolicy);
    }

    public static BehaviorClaimSet FromSubjects(IReadOnlyList<long> subjectEntityIds, BehaviorIncomingPolicy incomingPolicy, BehaviorClaimChannel channel, BehaviorClaimMode mode)
    {
        return new BehaviorClaimSet(subjectEntityIds, Array.Empty<GridCoord>(), Array.Empty<ResourceKey>(), incomingPolicy, channel, mode);
    }

    public BehaviorClaimSet WithCells(IReadOnlyList<GridCoord> cells)
    {
        return new BehaviorClaimSet(SubjectEntityIds, cells, Resources, IncomingPolicy, Channel, Mode);
    }

    public bool ConflictsWith(BehaviorClaimSet other)
    {
        if (other == null ||
            Channel != other.Channel ||
            (Mode == BehaviorClaimMode.Shared && other.Mode == BehaviorClaimMode.Shared))
        {
            return false;
        }

        return SubjectEntityIds.Any(other.SubjectEntityIds.Contains) ||
            Cells.Any(other.Cells.Contains) ||
            Resources.Any(other.Resources.Contains);
    }
}

public sealed class ActionBehaviorInstance
{
    public ActionBehaviorInstance(long instanceId, long sourceActionId, long ownerActionId, ActionSpecId specId, string behaviorId, string runnerId, string primitive, ActionSourceContext source, IReadOnlyList<long> subjectEntityIds, BehaviorInstanceState state, string stateData, BehaviorClaimSet reservation, long createdTick, long updatedTick, string runtimePayload, string traceContext, long startTick, long endTick, int baseCostTicks, int effectiveCostTicks, BehaviorCompletionMode completionMode, BehaviorIncomingPolicy incomingPolicy, IReadOnlyList<ActionFact> startActionFacts, IReadOnlyList<BehaviorScheduledOutput> scheduledOutputs, BehaviorStepOutput completionOutput)
        : this(instanceId, sourceActionId, ownerActionId, specId, behaviorId, runnerId, primitive, source, subjectEntityIds, state, stateData, reservation, createdTick, updatedTick, runtimePayload, traceContext, startTick, endTick, baseCostTicks, effectiveCostTicks, completionMode, incomingPolicy, startActionFacts, scheduledOutputs, completionOutput, null)
    {
    }

    public ActionBehaviorInstance(long instanceId, long sourceActionId, long ownerActionId, ActionSpecId specId, string behaviorId, string runnerId, string primitive, ActionSourceContext source, IReadOnlyList<long> subjectEntityIds, BehaviorInstanceState state, string stateData, BehaviorClaimSet reservation, long createdTick, long updatedTick, string runtimePayload, string traceContext, long startTick, long endTick, int baseCostTicks, int effectiveCostTicks, BehaviorCompletionMode completionMode, BehaviorIncomingPolicy incomingPolicy, IReadOnlyList<ActionFact> startActionFacts, IReadOnlyList<BehaviorScheduledOutput> scheduledOutputs, BehaviorStepOutput completionOutput, Func<GameWorld, long, BehaviorStepOutput>? completionOutputFactory)
    {
        InstanceId = instanceId;
        SourceActionId = sourceActionId == 0 ? ownerActionId : sourceActionId;
        OwnerActionId = ownerActionId;
        SpecId = specId;
        BehaviorId = behaviorId ?? string.Empty;
        RunnerId = runnerId ?? string.Empty;
        Primitive = primitive ?? string.Empty;
        Source = source;
        SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? Array.Empty<long>() : subjectEntityIds.Distinct().OrderBy(id => id).ToArray();
        State = state;
        StateData = stateData ?? string.Empty;
        Reservation = reservation ?? BehaviorClaimSet.FromSubjects(SubjectEntityIds, BehaviorIncomingPolicy.RejectIncoming);
        CreatedTick = createdTick;
        UpdatedTick = updatedTick;
        RuntimePayload = runtimePayload ?? string.Empty;
        TraceContext = traceContext ?? string.Empty;
        StartTick = startTick;
        EndTick = endTick;
        BaseCostTicks = Math.Max(1, baseCostTicks);
        EffectiveCostTicks = Math.Max(1, effectiveCostTicks);
        CompletionMode = completionMode;
        IncomingPolicy = incomingPolicy;
        StartActionFacts = startActionFacts ?? Array.Empty<ActionFact>();
        ScheduledOutputs = scheduledOutputs == null || scheduledOutputs.Count == 0
            ? Array.Empty<BehaviorScheduledOutput>()
            : scheduledOutputs.OrderBy(output => output.ReleaseTick).ToArray();
        CompletionOutput = completionOutput ?? BehaviorStepOutput.Empty;
        CompletionOutputFactory = completionOutputFactory;
    }

    public long InstanceId { get; }
    public long SourceActionId { get; }
    public long OwnerActionId { get; }
    public ActionSpecId SpecId { get; }
    public string BehaviorId { get; }
    public string RunnerId { get; }
    public string Primitive { get; }
    public ActionSourceContext Source { get; }
    public IReadOnlyList<long> SubjectEntityIds { get; }
    public BehaviorInstanceState State { get; }
    public BehaviorInstanceState CurrentState => State;
    public string StateData { get; }
    public BehaviorClaimSet Reservation { get; }
    public BehaviorClaimSet ClaimSet => Reservation;
    public long CreatedTick { get; }
    public long UpdatedTick { get; }
    public string RuntimePayload { get; }
    public string TraceContext { get; }
    public long StartTick { get; }
    public long EndTick { get; }
    public int BaseCostTicks { get; }
    public int EffectiveCostTicks { get; }
    public BehaviorCompletionMode CompletionMode { get; }
    public BehaviorIncomingPolicy IncomingPolicy { get; }
    public IReadOnlyList<ActionFact> StartActionFacts { get; }
    public IReadOnlyList<BehaviorScheduledOutput> ScheduledOutputs { get; }
    public BehaviorStepOutput CompletionOutput { get; }
    public Func<GameWorld, long, BehaviorStepOutput>? CompletionOutputFactory { get; }

    public static ActionBehaviorInstance Running(ActionRequest request, ActionSpec spec, IReadOnlyList<long> subjectEntityIds, BehaviorClaimSet reservation, long serverTick, BehaviorStepOutput completionOutput, string traceContext)
    {
        int costTicks = Math.Max(1, request.RuntimeParams.CostTicks);
        return new ActionBehaviorInstance(request.ActionId, request.ActionId, request.OwnerActionId, request.SpecId, spec.SpecId.Value, "step_runner", ActionPrimitiveNames.NameOf(spec.Primitive), request.Source, subjectEntityIds, BehaviorInstanceState.Running, string.Empty, reservation, serverTick, serverTick, string.Empty, traceContext, serverTick, serverTick + costTicks, costTicks, costTicks, BehaviorCompletionMode.CompleteAtEndTick, BehaviorIncomingPolicy.RejectIncoming, Array.Empty<ActionFact>(), Array.Empty<BehaviorScheduledOutput>(), completionOutput);
    }

    public static ActionBehaviorInstance Running(ActionRequest request, ActionSpec spec, IReadOnlyList<long> subjectEntityIds, BehaviorClaimSet reservation, long serverTick, Func<GameWorld, long, BehaviorStepOutput> completionOutputFactory, string traceContext)
    {
        int costTicks = Math.Max(1, request.RuntimeParams.CostTicks);
        return new ActionBehaviorInstance(request.ActionId, request.ActionId, request.OwnerActionId, request.SpecId, spec.SpecId.Value, "step_runner", ActionPrimitiveNames.NameOf(spec.Primitive), request.Source, subjectEntityIds, BehaviorInstanceState.Running, string.Empty, reservation, serverTick, serverTick, string.Empty, traceContext, serverTick, serverTick + costTicks, costTicks, costTicks, BehaviorCompletionMode.CompleteAtEndTick, BehaviorIncomingPolicy.RejectIncoming, Array.Empty<ActionFact>(), Array.Empty<BehaviorScheduledOutput>(), BehaviorStepOutput.Empty, completionOutputFactory);
    }

    public static ActionBehaviorInstance Rejected(ActionRequest request, ActionSpec spec, IReadOnlyList<long> subjectEntityIds, long serverTick, string reason, string traceContext)
    {
        IReadOnlyList<long> subjects = subjectEntityIds == null || subjectEntityIds.Count == 0
            ? new[] { request.EntityId }
            : subjectEntityIds;
        ActionFact rejectedFact = ActionFact.BehaviorRejected(serverTick, request.ActionId, subjects);
        return new ActionBehaviorInstance(request.ActionId, request.ActionId, request.OwnerActionId, request.SpecId, spec.SpecId.Value, "rejected_runner", ActionPrimitiveNames.NameOf(spec.Primitive), request.Source, subjects, BehaviorInstanceState.Rejected, reason ?? string.Empty, BehaviorClaimSet.FromSubjects(subjects, BehaviorIncomingPolicy.RejectIncoming), serverTick, serverTick, string.Empty, traceContext, serverTick, serverTick, 1, 1, BehaviorCompletionMode.CompleteAtEndTick, BehaviorIncomingPolicy.RejectIncoming, new[] { rejectedFact }, Array.Empty<BehaviorScheduledOutput>(), BehaviorStepOutput.Empty);
    }

    public static ActionBehaviorInstance Cancelled(ActionRequest request, ActionSpec spec, IReadOnlyList<long> subjectEntityIds, long serverTick, string reason, string traceContext)
    {
        IReadOnlyList<long> subjects = subjectEntityIds == null || subjectEntityIds.Count == 0
            ? new[] { request.EntityId }
            : subjectEntityIds;
        ActionFact cancelledFact = ActionFact.BehaviorCancelled(serverTick, request.ActionId, subjects);
        return new ActionBehaviorInstance(request.ActionId, request.ActionId, request.OwnerActionId, request.SpecId, spec.SpecId.Value, "cancelled_runner", ActionPrimitiveNames.NameOf(spec.Primitive), request.Source, subjects, BehaviorInstanceState.Cancelled, reason ?? string.Empty, BehaviorClaimSet.FromSubjects(subjects, BehaviorIncomingPolicy.RejectIncoming), serverTick, serverTick, string.Empty, traceContext, serverTick, serverTick, 1, 1, BehaviorCompletionMode.CompleteAtEndTick, BehaviorIncomingPolicy.RejectIncoming, new[] { cancelledFact }, Array.Empty<BehaviorScheduledOutput>(), BehaviorStepOutput.Empty);
    }

    public static ActionBehaviorInstance Failed(ActionRequest request, ActionSpec spec, IReadOnlyList<long> subjectEntityIds, long serverTick, string reason, string traceContext)
    {
        IReadOnlyList<long> subjects = subjectEntityIds == null || subjectEntityIds.Count == 0
            ? new[] { request.EntityId }
            : subjectEntityIds;
        ActionFact failedFact = ActionFact.BehaviorFailed(serverTick, request.ActionId, subjects);
        return new ActionBehaviorInstance(request.ActionId, request.ActionId, request.OwnerActionId, request.SpecId, spec.SpecId.Value, "failed_runner", ActionPrimitiveNames.NameOf(spec.Primitive), request.Source, subjects, BehaviorInstanceState.Failed, reason ?? string.Empty, BehaviorClaimSet.FromSubjects(subjects, BehaviorIncomingPolicy.RejectIncoming), serverTick, serverTick, string.Empty, traceContext, serverTick, serverTick, 1, 1, BehaviorCompletionMode.CompleteAtEndTick, BehaviorIncomingPolicy.RejectIncoming, new[] { failedFact }, Array.Empty<BehaviorScheduledOutput>(), BehaviorStepOutput.Empty);
    }

    public static ActionBehaviorInstance Completed(ActionRequest request, ActionSpec spec, IReadOnlyList<long> subjectEntityIds, BehaviorClaimSet reservation, long serverTick, string traceContext)
    {
        IReadOnlyList<long> subjects = subjectEntityIds == null || subjectEntityIds.Count == 0
            ? new[] { request.EntityId }
            : subjectEntityIds;
        BehaviorClaimSet claims = reservation ?? BehaviorClaimSet.FromSubjects(subjects, BehaviorIncomingPolicy.RejectIncoming);
        return new ActionBehaviorInstance(request.ActionId, request.ActionId, request.OwnerActionId, request.SpecId, spec.SpecId.Value, "primitive_runner", ActionPrimitiveNames.NameOf(spec.Primitive), request.Source, subjects, BehaviorInstanceState.Completed, string.Empty, claims, serverTick, serverTick, string.Empty, traceContext, serverTick, serverTick, 1, 1, BehaviorCompletionMode.CompleteAtEndTick, BehaviorIncomingPolicy.RejectIncoming, Array.Empty<ActionFact>(), Array.Empty<BehaviorScheduledOutput>(), BehaviorStepOutput.Empty);
    }
}

public interface IBehaviorStateMachine
{
    BehaviorStepOutput Enter(ActionBehaviorInstance instance, BehaviorRunnerContext context);
    BehaviorStepOutput Tick(ActionBehaviorInstance instance, BehaviorRunnerContext context);
    BehaviorStepOutput Exit(ActionBehaviorInstance instance, BehaviorRunnerContext context);
}

public sealed class ActionBehaviorStateMachine : IBehaviorStateMachine
{
    private readonly Dictionary<long, HashSet<int>> releasedScheduledOutputIndexes = new();

    public BehaviorStepOutput Enter(ActionBehaviorInstance instance, BehaviorRunnerContext context)
    {
        return new BehaviorStepOutput(Array.Empty<MovePlan>(), new Dictionary<long, MoveResult>(), Array.Empty<DeferredAction>(), instance.StartActionFacts);
    }

    public BehaviorStepOutput Tick(ActionBehaviorInstance instance, BehaviorRunnerContext context)
    {
        if (instance.ScheduledOutputs.Count == 0)
        {
            return BehaviorStepOutput.Empty;
        }

        bool hasDueOutput = false;
        releasedScheduledOutputIndexes.TryGetValue(instance.InstanceId, out HashSet<int> released);
        for (int outputIndex = 0; outputIndex < instance.ScheduledOutputs.Count; outputIndex++)
        {
            if ((released == null || !released.Contains(outputIndex)) &&
                instance.ScheduledOutputs[outputIndex].ReleaseTick <= context.ServerTick)
            {
                hasDueOutput = true;
                break;
            }
        }

        if (!hasDueOutput)
        {
            return BehaviorStepOutput.Empty;
        }

        if (released == null)
        {
            released = new HashSet<int>();
            releasedScheduledOutputIndexes.Add(instance.InstanceId, released);
        }

        var movePlans = new List<MovePlan>();
        var actionResults = new Dictionary<long, MoveResult>();
        var deferredActions = new List<DeferredAction>();
        var actionFacts = new List<ActionFact>();
        for (int outputIndex = 0; outputIndex < instance.ScheduledOutputs.Count; outputIndex++)
        {
            if (released.Contains(outputIndex))
            {
                continue;
            }

            BehaviorScheduledOutput scheduled = instance.ScheduledOutputs[outputIndex];
            if (scheduled.ReleaseTick > context.ServerTick)
            {
                continue;
            }

            released.Add(outputIndex);
            BehaviorStepOutput output = scheduled.Output;
            movePlans.AddRange(output.MovePlans);
            foreach (KeyValuePair<long, MoveResult> pair in output.ActionResults)
            {
                actionResults[pair.Key] = pair.Value;
            }

            deferredActions.AddRange(output.DeferredActions);
            actionFacts.AddRange(output.ActionFacts);
        }

        return movePlans.Count == 0 && actionResults.Count == 0 && deferredActions.Count == 0 && actionFacts.Count == 0
            ? BehaviorStepOutput.Empty
            : new BehaviorStepOutput(movePlans, actionResults, deferredActions, actionFacts);
    }

    public BehaviorStepOutput Exit(ActionBehaviorInstance instance, BehaviorRunnerContext context)
    {
        releasedScheduledOutputIndexes.Remove(instance.InstanceId);
        return instance.CompletionOutputFactory == null
            ? instance.CompletionOutput
            : instance.CompletionOutputFactory(context.World, context.ServerTick);
    }
}

public class RunningBehaviorInstanceStore
{
    private readonly Dictionary<long, ActionBehaviorInstance> byInstance = new();
    private readonly Dictionary<SubjectClaimKey, ActionBehaviorInstance> bySubject = new();
    private readonly Dictionary<CellClaimKey, ActionBehaviorInstance> byCell = new();
    private readonly Dictionary<ResourceClaimKey, ActionBehaviorInstance> byResource = new();

    public int Count => byInstance.Count;

    public void Add(ActionBehaviorInstance instance)
    {
        byInstance[instance.InstanceId] = instance;
        for (int i = 0; i < instance.Reservation.SubjectEntityIds.Count; i++)
        {
            bySubject[new SubjectClaimKey(instance.Reservation.SubjectEntityIds[i], instance.Reservation.Channel)] = instance;
        }

        for (int i = 0; i < instance.Reservation.Cells.Count; i++)
        {
            byCell[new CellClaimKey(instance.Reservation.Cells[i], instance.Reservation.Channel)] = instance;
        }

        for (int i = 0; i < instance.Reservation.Resources.Count; i++)
        {
            byResource[new ResourceClaimKey(instance.Reservation.Resources[i], instance.Reservation.Channel)] = instance;
        }
    }

    public bool TryGetBySubject(long entityId, out ActionBehaviorInstance instance) => TryGetBySubject(entityId, BehaviorClaimChannel.Movement, out instance);
    public bool TryGetBySubject(long entityId, BehaviorClaimChannel channel, out ActionBehaviorInstance instance) => bySubject.TryGetValue(new SubjectClaimKey(entityId, channel), out instance);
    public bool TryGetByCell(GridCoord cell, out ActionBehaviorInstance instance) => TryGetByCell(cell, BehaviorClaimChannel.Movement, out instance);
    public bool TryGetByCell(GridCoord cell, BehaviorClaimChannel channel, out ActionBehaviorInstance instance) => byCell.TryGetValue(new CellClaimKey(cell, channel), out instance);
    public bool TryGetByResource(ResourceKey resource, out ActionBehaviorInstance instance) => TryGetByResource(resource, BehaviorClaimChannel.Movement, out instance);
    public bool TryGetByResource(ResourceKey resource, BehaviorClaimChannel channel, out ActionBehaviorInstance instance) => byResource.TryGetValue(new ResourceClaimKey(resource, channel), out instance);
    public bool TryGet(long instanceId, out ActionBehaviorInstance instance) => byInstance.TryGetValue(instanceId, out instance);
    public bool Get(long instanceId, out ActionBehaviorInstance instance) => TryGet(instanceId, out instance);
    public bool QueryByEntity(long entityId, BehaviorClaimChannel channel, out ActionBehaviorInstance instance) => TryGetBySubject(entityId, channel, out instance);
    public bool QueryByCell(GridCoord cell, BehaviorClaimChannel channel, out ActionBehaviorInstance instance) => TryGetByCell(cell, channel, out instance);
    public bool QueryByResource(ResourceKey resource, BehaviorClaimChannel channel, out ActionBehaviorInstance instance) => TryGetByResource(resource, channel, out instance);
    public IReadOnlyList<ActionBehaviorInstance> StepDue(long serverTick)
    {
        return byInstance.Values
            .Where(instance => instance.EndTick <= serverTick)
            .OrderBy(instance => instance.EndTick)
            .ThenBy(instance => instance.InstanceId)
            .ToArray();
    }

    public void Release(long instanceId)
    {
        if (!byInstance.TryGetValue(instanceId, out ActionBehaviorInstance instance))
        {
            return;
        }

        byInstance.Remove(instanceId);
        for (int i = 0; i < instance.Reservation.SubjectEntityIds.Count; i++)
        {
            bySubject.Remove(new SubjectClaimKey(instance.Reservation.SubjectEntityIds[i], instance.Reservation.Channel));
        }

        for (int i = 0; i < instance.Reservation.Cells.Count; i++)
        {
            byCell.Remove(new CellClaimKey(instance.Reservation.Cells[i], instance.Reservation.Channel));
        }

        for (int i = 0; i < instance.Reservation.Resources.Count; i++)
        {
            byResource.Remove(new ResourceClaimKey(instance.Reservation.Resources[i], instance.Reservation.Channel));
        }
    }

    public bool Remove(long instanceId)
    {
        if (!byInstance.ContainsKey(instanceId))
        {
            return false;
        }

        Release(instanceId);
        return true;
    }

    public bool ReleaseByInstance(long instanceId)
    {
        return Remove(instanceId);
    }

    private readonly struct SubjectClaimKey : System.IEquatable<SubjectClaimKey>
    {
        public SubjectClaimKey(long entityId, BehaviorClaimChannel channel)
        {
            EntityId = entityId;
            Channel = channel;
        }

        public long EntityId { get; }
        public BehaviorClaimChannel Channel { get; }
        public bool Equals(SubjectClaimKey other) => EntityId == other.EntityId && Channel == other.Channel;
        public override bool Equals(object obj) => obj is SubjectClaimKey other && Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(EntityId, Channel);
    }

    private readonly struct CellClaimKey : System.IEquatable<CellClaimKey>
    {
        public CellClaimKey(GridCoord cell, BehaviorClaimChannel channel)
        {
            Cell = cell;
            Channel = channel;
        }

        public GridCoord Cell { get; }
        public BehaviorClaimChannel Channel { get; }
        public bool Equals(CellClaimKey other) => Cell.Equals(other.Cell) && Channel == other.Channel;
        public override bool Equals(object obj) => obj is CellClaimKey other && Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(Cell, Channel);
    }

    private readonly struct ResourceClaimKey : System.IEquatable<ResourceClaimKey>
    {
        public ResourceClaimKey(ResourceKey resource, BehaviorClaimChannel channel)
        {
            Resource = resource;
            Channel = channel;
        }

        public ResourceKey Resource { get; }
        public BehaviorClaimChannel Channel { get; }
        public bool Equals(ResourceClaimKey other) => Resource.Equals(other.Resource) && Channel == other.Channel;
        public override bool Equals(object obj) => obj is ResourceClaimKey other && Equals(other);
        public override int GetHashCode() => System.HashCode.Combine(Resource, Channel);
    }
}

public sealed class ActiveReservationStore : RunningBehaviorInstanceStore
{
}
}
