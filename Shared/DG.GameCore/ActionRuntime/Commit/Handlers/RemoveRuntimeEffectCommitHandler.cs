namespace DG.GameCore
{
public sealed class RemoveRuntimeEffectCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.RemoveRuntimeEffect);
    public CommitProposalKind Kind => CommitProposalKind.RemoveRuntimeEffect;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyRemoveRuntimeEffect(world, proposal);
}
}
