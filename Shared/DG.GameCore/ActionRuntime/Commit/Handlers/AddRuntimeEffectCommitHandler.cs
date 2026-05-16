namespace DG.GameCore
{
public sealed class AddRuntimeEffectCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.AddRuntimeEffect);
    public CommitProposalKind Kind => CommitProposalKind.AddRuntimeEffect;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyAddRuntimeEffect(world, proposal);
}
}
