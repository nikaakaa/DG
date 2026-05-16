using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
internal sealed class ComponentTypeRegistry
{
    private readonly List<Type> types = new();

    public int GetOrAdd(Type type)
    {
        if (TryGet(type, out int id))
        {
            return id;
        }

        if (types.Count >= 64)
        {
            throw new InvalidOperationException("Component type limit exceeded");
        }

        types.Add(type);
        return types.Count - 1;
    }

    public bool TryGet(Type type, out int id)
    {
        for (int i = 0; i < types.Count; i++)
        {
            if (types[i] == type)
            {
                id = i;
                return true;
            }
        }

        id = -1;
        return false;
    }
}

}
