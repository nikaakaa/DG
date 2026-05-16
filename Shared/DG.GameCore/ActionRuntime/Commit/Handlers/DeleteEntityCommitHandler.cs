namespace DG.GameCore
{
public sealed class DeleteEntityCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.DeleteEntity);
    public CommitProposalKind Kind => CommitProposalKind.DeleteEntity;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyDeleteEntity(world, proposal);
}
}
