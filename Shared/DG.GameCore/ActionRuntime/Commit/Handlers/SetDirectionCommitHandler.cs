namespace DG.GameCore
{
public sealed class SetDirectionCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.SetDirection);
    public CommitProposalKind Kind => CommitProposalKind.SetDirection;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplySetDirection(world, proposal);
}
}
