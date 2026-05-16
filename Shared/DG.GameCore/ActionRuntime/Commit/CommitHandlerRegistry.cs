using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class CommitHandlerRegistry
{
    private readonly Dictionary<CommitProposalId, ICommitProposalHandler> handlers = new();

    public static CommitHandlerRegistry Default { get; } = CreateDefault();

    public void Register(ICommitProposalHandler handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        if (!handler.ProposalId.IsValid)
        {
            throw new InvalidOperationException("Commit handler id is empty.");
        }

        if (handlers.ContainsKey(handler.ProposalId))
        {
            throw new InvalidOperationException("Duplicate commit handler: " + handler.ProposalId);
        }

        handlers.Add(handler.ProposalId, handler);
    }

    public ICommitProposalHandler Get(CommitProposalId id)
    {
        if (!handlers.TryGetValue(id, out ICommitProposalHandler handler))
        {
            throw new InvalidOperationException("No commit handler registered for id: " + id);
        }

        return handler;
    }

    private static CommitHandlerRegistry CreateDefault()
    {
        var registry = new CommitHandlerRegistry();
        registry.Register(new MoveEntityCommitHandler());
        registry.Register(new SetDirectionCommitHandler());
        registry.Register(new CreateEntityCommitHandler());
        registry.Register(new DeleteEntityCommitHandler());
        registry.Register(new SetAutoMoveTickCommitHandler());
        registry.Register(new AddRuntimeEffectCommitHandler());
        registry.Register(new RemoveRuntimeEffectCommitHandler());
        registry.Register(new SetComponentResultCommitHandler());
        registry.Register(new AddTagCommitHandler());
        registry.Register(new RemoveTagCommitHandler());
        registry.Register(new ClearRuntimeSourcesCommitHandler());
        return registry;
    }
}
}
