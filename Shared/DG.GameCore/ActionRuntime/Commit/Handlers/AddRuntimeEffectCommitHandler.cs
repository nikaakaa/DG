namespace DG.GameCore
{
public sealed class AddRuntimeEffectCommitHandler : ICommitProposalHandler
{
    public CommitProposalKind Kind => CommitProposalKind.AddRuntimeEffect;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyAddRuntimeEffect(world, proposal);
}
}
