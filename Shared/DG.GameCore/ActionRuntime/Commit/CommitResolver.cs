using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class CommitResolver
{
    private readonly CommitHandlerRegistry handlers;

    public CommitResolver() : this(CommitHandlerRegistry.Default)
    {
    }

    public CommitResolver(CommitHandlerRegistry handlers)
    {
        this.handlers = handlers ?? throw new System.ArgumentNullException(nameof(handlers));
    }

    public IReadOnlyList<CommitProposalResult> Resolve(GameWorld world, IReadOnlyList<CommitProposal> proposals)
    {
        var results = new List<CommitProposalResult>();
        var movedEntities = new HashSet<long>();
        var occupiedTargets = new HashSet<GridCoord>();
        IReadOnlyDictionary<long, HashSet<long>> moveGroups = BuildMoveGroups(proposals);
        IReadOnlyList<CommitProposal> ordered = proposals
            .OrderBy(proposal => proposal.Priority)
            .ThenBy(proposal => proposal.SourceActionId)
            .ThenBy(proposal => proposal.SourceStateId)
            .ThenBy(proposal => proposal.EntityId)
            .ToArray();
        var context = new CommitResolveContext(movedEntities, occupiedTargets, moveGroups);

        for (int i = 0; i < ordered.Count; i++)
        {
            CommitProposal proposal = ordered[i];
            CommitProposalResult result = handlers.Get(proposal.Kind).Apply(world, proposal, context);
            results.Add(result);
        }

        return results;
    }

    private static IReadOnlyDictionary<long, HashSet<long>> BuildMoveGroups(IReadOnlyList<CommitProposal> proposals)
    {
        return proposals
            .Where(proposal => proposal.Kind == CommitProposalKind.MoveEntity && proposal.SourceStateId != 0)
            .GroupBy(proposal => proposal.SourceStateId)
            .ToDictionary(group => group.Key, group => new HashSet<long>(group.Select(proposal => proposal.EntityId)));
    }
}
}
