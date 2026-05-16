namespace DG.GameCore
{
public sealed class SetComponentResultCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.SetComponentResult);
    public CommitProposalKind Kind => CommitProposalKind.SetComponentResult;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplySetComponentResult(world, proposal);
}
}
