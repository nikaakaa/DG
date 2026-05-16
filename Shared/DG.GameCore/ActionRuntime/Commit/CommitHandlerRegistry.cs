using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class CommitHandlerRegistry
{
    private readonly Dictionary<CommitProposalKind, ICommitProposalHandler> handlers = new();

    public static CommitHandlerRegistry Default { get; } = CreateDefault();

    public void Register(ICommitProposalHandler handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        if (handlers.ContainsKey(handler.Kind))
        {
            throw new InvalidOperationException("Duplicate commit handler: " + handler.Kind);
        }

        handlers.Add(handler.Kind, handler);
    }

    public ICommitProposalHandler Get(CommitProposalKind kind)
    {
        if (!handlers.TryGetValue(kind, out ICommitProposalHandler handler))
        {
            throw new InvalidOperationException("No commit handler registered for kind: " + kind);
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
