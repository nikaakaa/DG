namespace DG.GameCore
{
public interface ICommitProposalHandler
{
    CommitProposalKind Kind { get; }
    CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context);
}
}
