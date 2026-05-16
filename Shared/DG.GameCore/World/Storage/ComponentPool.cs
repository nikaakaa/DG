using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
internal interface IComponentPool
{
    bool Remove(int entityIndex);
}

internal sealed class ComponentPool<TComponent> : IComponentPool where TComponent : struct
{
    private readonly List<int> entityIndexes = new();
    private readonly List<TComponent> values = new();
    private int[] sparse = Array.Empty<int>();

    public void Set(int entityIndex, TComponent component)
    {
        EnsureSparse(entityIndex);
        int row = sparse[entityIndex];
        if (row >= 0)
        {
            values[row] = component;
            return;
        }

        sparse[entityIndex] = values.Count;
        entityIndexes.Add(entityIndex);
        values.Add(component);
    }

    public bool TryGet(int entityIndex, out TComponent component)
    {
        if (entityIndex >= 0 &&
            entityIndex < sparse.Length &&
            sparse[entityIndex] >= 0)
        {
            component = values[sparse[entityIndex]];
            return true;
        }

        component = default;
        return false;
    }

    public TComponent Get(int entityIndex)
    {
        if (!TryGet(entityIndex, out TComponent component))
        {
            throw new KeyNotFoundException("Component not found: " + typeof(TComponent).Name);
        }

        return component;
    }

    public bool Remove(int entityIndex)
    {
        if (entityIndex < 0 ||
            entityIndex >= sparse.Length)
        {
            return false;
        }

        int row = sparse[entityIndex];
        if (row < 0)
        {
            return false;
        }

        int lastRow = values.Count - 1;
        int lastEntityIndex = entityIndexes[lastRow];
        values[row] = values[lastRow];
        entityIndexes[row] = lastEntityIndex;
        sparse[lastEntityIndex] = row;
        values.RemoveAt(lastRow);
        entityIndexes.RemoveAt(lastRow);
        sparse[entityIndex] = -1;
        return true;
    }

    private void EnsureSparse(int entityIndex)
    {
        if (entityIndex < sparse.Length)
        {
            return;
        }

        int oldLength = sparse.Length;
        int newLength = sparse.Length == 0 ? 4 : sparse.Length;
        while (newLength <= entityIndex)
        {
            newLength *= 2;
        }

        Array.Resize(ref sparse, newLength);
        for (int i = oldLength; i < sparse.Length; i++)
        {
            sparse[i] = -1;
        }
    }
}

}
