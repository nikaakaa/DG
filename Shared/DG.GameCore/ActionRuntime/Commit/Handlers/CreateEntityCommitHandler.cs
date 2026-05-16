namespace DG.GameCore
{
public sealed class CreateEntityCommitHandler : ICommitProposalHandler
{
    public CommitProposalKind Kind => CommitProposalKind.CreateEntity;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyCreateEntity(world, proposal);
}
}
