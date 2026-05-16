namespace DG.GameCore
{
public sealed class SetDirectionCommitHandler : ICommitProposalHandler
{
    public CommitProposalKind Kind => CommitProposalKind.SetDirection;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplySetDirection(world, proposal);
}
}
