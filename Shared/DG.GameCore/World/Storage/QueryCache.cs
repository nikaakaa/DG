using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
internal sealed class QueryCache
{
    private readonly List<QueryCacheEntry> entries = new();

    public IReadOnlyList<int> GetOrBuild(ulong requiredMask, EntityIterationOrder order, EntityRegistry entities)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].RequiredMask == requiredMask && entries[i].Order == order)
            {
                return entries[i].EntityIndexes;
            }
        }

        IReadOnlyList<int> indexes = entities.Match(requiredMask, order);
        entries.Add(new QueryCacheEntry(requiredMask, order, indexes));
        return indexes;
    }

    public void Clear()
    {
        entries.Clear();
    }
}

internal readonly struct QueryCacheEntry
{
    public QueryCacheEntry(ulong requiredMask, EntityIterationOrder order, IReadOnlyList<int> entityIndexes)
    {
        RequiredMask = requiredMask;
        Order = order;
        EntityIndexes = entityIndexes;
    }

    public ulong RequiredMask { get; }
    public EntityIterationOrder Order { get; }
    public IReadOnlyList<int> EntityIndexes { get; }
}
}
