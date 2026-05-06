using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class StateDrivenRuleExecutionResult
{
    public StateDrivenRuleExecutionResult(IReadOnlyDictionary<long, MoveResult> actionResults, IReadOnlyList<CommitProposalResult> proposalResults, int activePendingCount, IReadOnlyList<string> reasons)
    {
        ActionResults = actionResults;
        ProposalResults = proposalResults;
        ActivePendingCount = activePendingCount;
        Reasons = reasons;
    }

    public IReadOnlyDictionary<long, MoveResult> ActionResults { get; }
    public IReadOnlyList<CommitProposalResult> ProposalResults { get; }
    public int ActivePendingCount { get; }
    public IReadOnlyList<string> Reasons { get; }
}
}
