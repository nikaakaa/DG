namespace DG.GameCore
{
public sealed class ClearRuntimeSourcesCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.ClearRuntimeSources);
    public CommitProposalKind Kind => CommitProposalKind.ClearRuntimeSources;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyClearRuntimeSources(world, proposal);
}
}
