namespace DG.GameCore
{
public readonly struct CommitProposalResult
{
    public CommitProposalResult(CommitProposal proposal, bool accepted, string reason)
    {
        Proposal = proposal;
        Accepted = accepted;
        Reason = reason;
    }

    public CommitProposal Proposal { get; }
    public bool Accepted { get; }
    public string Reason { get; }
}
}
