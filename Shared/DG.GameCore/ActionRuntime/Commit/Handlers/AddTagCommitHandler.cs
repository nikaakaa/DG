namespace DG.GameCore
{
public sealed class AddTagCommitHandler : ICommitProposalHandler
{
    public CommitProposalId ProposalId => new(CommitProposalKind.AddTag);
    public CommitProposalKind Kind => CommitProposalKind.AddTag;
    public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context) => CommitHandlerOperations.ApplyAddTag(world, proposal);
}
}
