using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
internal interface IComponentStore
{
    bool Has(long entityId);
    bool Remove(long entityId);
}

internal sealed class ComponentStore<TComponent> : IComponentStore where TComponent : struct
{
    private readonly Dictionary<long, TComponent> components = new();

    public void Set(long entityId, TComponent component)
    {
        components[entityId] = component;
    }

    public bool Has(long entityId)
    {
        return components.ContainsKey(entityId);
    }

    public bool TryGet(long entityId, out TComponent component)
    {
        return components.TryGetValue(entityId, out component);
    }

    public TComponent Get(long entityId)
    {
        return components[entityId];
    }

    public bool Remove(long entityId)
    {
        return components.Remove(entityId);
    }
}
}

