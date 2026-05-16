using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
internal sealed class EntityIdIndex
{
    private readonly List<long> ids = new();
    private readonly List<int> indexes = new();

    public void Set(long entityId, int index)
    {
        int found = ids.BinarySearch(entityId);
        if (found >= 0)
        {
            indexes[found] = index;
            return;
        }

        int insert = ~found;
        ids.Insert(insert, entityId);
        indexes.Insert(insert, index);
    }

    public bool TryGet(long entityId, out int index)
    {
        int found = ids.BinarySearch(entityId);
        if (found >= 0)
        {
            index = indexes[found];
            return true;
        }

        index = -1;
        return false;
    }

    public bool Remove(long entityId)
    {
        int found = ids.BinarySearch(entityId);
        if (found < 0)
        {
            return false;
        }

        ids.RemoveAt(found);
        indexes.RemoveAt(found);
        return true;
    }
}

}
