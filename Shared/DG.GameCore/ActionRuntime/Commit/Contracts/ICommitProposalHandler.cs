namespace DG.GameCore
{
public interface ICommitProposalHandler
{
    CommitProposalId ProposalId { get; }
    CommitProposalKind Kind { get; }
    CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context);
}
}
