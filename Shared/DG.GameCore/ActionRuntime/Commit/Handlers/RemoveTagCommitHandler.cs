namespace DG.GameCore
{
public sealed class RemoveTagCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.RemoveTag);
    public CommitProposalKind Kind => CommitProposalKind.RemoveTag;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyRemoveTag(world, proposal);
}
}
