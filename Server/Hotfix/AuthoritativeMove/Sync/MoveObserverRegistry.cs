namespace Fantasy;

public sealed class MoveObserverRegistry<TObserver> where TObserver : class
{
    private readonly Dictionary<TObserver, long> Observers = new();
    private readonly Dictionary<long, TObserver> Owners = new();

    public int ObserverCount => Observers.Count;

    public int OwnerCount => Owners.Count;

    public void RegisterObserver(TObserver observer, long entityId)
    {
        Observers[observer] = entityId;
    }

    public void RefreshOwner(long entityId, TObserver observer)
    {
        Owners[entityId] = observer;
    }

    public void RemoveObserver(TObserver observer)
    {
        Observers.Remove(observer);
        List<long>? removeKeys = null;
        foreach (KeyValuePair<long, TObserver> pair in Owners)
        {
            if (!ReferenceEquals(pair.Value, observer))
            {
                continue;
            }

            removeKeys ??= new List<long>();
            removeKeys.Add(pair.Key);
        }

        if (removeKeys == null)
        {
            return;
        }

        foreach (long key in removeKeys)
        {
            Owners.Remove(key);
        }
    }

    public void RemoveOwner(long entityId)
    {
        Owners.Remove(entityId);
    }

    public IReadOnlyList<TObserver> EnumerateAvailable(Func<TObserver, bool> isAvailable)
    {
        var result = new List<TObserver>();
        List<TObserver>? removeKeys = null;
        foreach (TObserver observer in Observers.Keys)
        {
            if (isAvailable(observer))
            {
                result.Add(observer);
                continue;
            }

            removeKeys ??= new List<TObserver>();
            removeKeys.Add(observer);
        }

        if (removeKeys == null)
        {
            return result;
        }

        foreach (TObserver observer in removeKeys)
        {
            RemoveObserver(observer);
        }

        return result;
    }

    public IReadOnlyList<long> EnumerateObservedEntityIds()
    {
        return Observers.Values.Distinct().ToArray();
    }
}
