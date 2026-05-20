using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class PrimitiveRunnerRegistry
{
    private readonly Dictionary<RunnerId, Action<PrimitiveRunnerContext>> dispatchers = new();

    public static PrimitiveRunnerRegistry Default => PrimitiveRunnerCatalog.CreateDefault();

    public void Register(RunnerId runnerId, Action<PrimitiveRunnerContext> dispatcher)
    {
        if (!runnerId.IsValid)
        {
            throw new InvalidOperationException("Runner id is empty.");
        }

        if (dispatcher == null)
        {
            throw new ArgumentNullException(nameof(dispatcher));
        }

        if (dispatchers.ContainsKey(runnerId))
        {
            throw new InvalidOperationException("Duplicate runner id: " + runnerId);
        }

        dispatchers.Add(runnerId, dispatcher);
    }

    public Action<PrimitiveRunnerContext> Get(BehaviorDefinition definition)
    {
        if (!definition.IsValid)
        {
            throw new InvalidOperationException("Behavior definition is invalid.");
        }

        if (!dispatchers.TryGetValue(definition.RunnerId, out Action<PrimitiveRunnerContext> dispatcher))
        {
            throw new InvalidOperationException("No runner registered for id '" + definition.RunnerId + "' (behavior '" + definition.BehaviorId + "').");
        }

        return dispatcher;
    }

    public bool Contains(RunnerId runnerId)
    {
        return dispatchers.ContainsKey(runnerId);
    }
}
}
