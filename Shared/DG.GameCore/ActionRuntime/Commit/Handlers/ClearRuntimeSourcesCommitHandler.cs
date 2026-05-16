namespace DG.GameCore
{
public sealed class ClearRuntimeSourcesCommitHandler : ICommitProposalHandler
{
    public CommitProposalKind Kind => CommitProposalKind.ClearRuntimeSources;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyClearRuntimeSources(world, proposal);
}
}
