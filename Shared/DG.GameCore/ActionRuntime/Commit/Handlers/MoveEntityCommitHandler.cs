namespace DG.GameCore
{
public sealed class MoveEntityCommitHandler : ICommitProposalHandler
{
    public CommitProposalKind Kind => CommitProposalKind.MoveEntity;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyMove(world, proposal, context);
}
}
