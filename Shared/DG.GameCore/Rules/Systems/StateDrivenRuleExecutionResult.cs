using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class StateDrivenRuleExecutionResult
{
    public StateDrivenRuleExecutionResult(IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, IReadOnlyList<string> reasons)
        : this(actionResults, proposalResults, reasons, System.Array.Empty<DeferredAction>(), System.Array.Empty<ActionUnitTransition>())
    {
    }

    public StateDrivenRuleExecutionResult(IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, IReadOnlyList<string> reasons, IReadOnlyList<DeferredAction> deferredActions)
        : this(actionResults, proposalResults, reasons, deferredActions, System.Array.Empty<ActionUnitTransition>())
    {
    }

    public StateDrivenRuleExecutionResult(IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, IReadOnlyList<string> reasons, IReadOnlyList<DeferredAction> deferredActions, IReadOnlyList<ActionUnitTransition> transitions)
    {
        ActionResults = actionResults;
        ProposalResults = proposalResults;
        Reasons = reasons;
        DeferredActions = deferredActions;
        Transitions = transitions;
    }

    public IReadOnlyDictionary<long, MoveResult> ActionResults { get; }
    public IReadOnlyList<CommitProposalResult> ProposalResults { get; }
    public IReadOnlyList<string> Reasons { get; }
    public IReadOnlyList<DeferredAction> DeferredActions { get; }
    public IReadOnlyList<ActionUnitTransition> Transitions { get; }
}
}
