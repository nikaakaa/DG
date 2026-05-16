namespace DG.GameCore
{
public sealed class SetAutoMoveTickCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.SetAutoMoveTick);
    public CommitProposalKind Kind => CommitProposalKind.SetAutoMoveTick;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplySetAutoMoveTick(world, proposal);
}
}
