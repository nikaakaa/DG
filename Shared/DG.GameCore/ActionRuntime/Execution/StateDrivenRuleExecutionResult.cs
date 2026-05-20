using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class BehaviorRuntimeTickResult
{
    public BehaviorRuntimeTickResult(IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, IReadOnlyList<string> reasons)
        : this(actionResults, proposalResults, reasons, System.Array.Empty<DeferredAction>())
    {
    }

    public BehaviorRuntimeTickResult(IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, IReadOnlyList<string> reasons, IReadOnlyList<DeferredAction> deferredActions)
        : this(actionResults, proposalResults, reasons, deferredActions, System.Array.Empty<ActionFact>())
    {
    }

    public BehaviorRuntimeTickResult(IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, IReadOnlyList<string> reasons, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionFact> actionFacts)
        : this(actionResults, proposalResults, reasons, deferredActions, actionFacts, System.Array.Empty<ActionBehaviorInstance>())
    {
    }

    public BehaviorRuntimeTickResult(IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, IReadOnlyList<string> reasons, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionFact> actionFacts, IReadOnlyList<ActionBehaviorInstance> behaviorInstances)
    {
        ActionResults = actionResults;
        ProposalResults = proposalResults;
        Reasons = reasons;
        DeferredActions = deferredActions;
        ActionFacts = actionFacts;
        BehaviorInstances = behaviorInstances;
    }

    public IReadOnlyDictionary<long, MoveResult> ActionResults { get; }
    public IReadOnlyList<CommitProposalResult> ProposalResults { get; }
    public IReadOnlyList<string> Reasons { get; }
    public IReadOnlyList<DeferredAction> DeferredActions { get; }
    public IReadOnlyList<ActionFact> ActionFacts { get; }
    public IReadOnlyList<ActionBehaviorInstance> BehaviorInstances { get; }
}
}
