using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
internal interface IWorldDataStorage
{
    int EntityCount { get; }
    bool AddEntity(GameEntity entity);
    bool RemoveEntity(long entityId, out GameEntity entity);
    bool TryGetEntity(long entityId, out GameEntity entity);
    IReadOnlyList<GameEntity> EnumerateEntities(EntityIterationOrder order);
    IReadOnlyList<GameEntity> QueryEntities(IReadOnlyList<Type> componentTypes, EntityIterationOrder order);
    bool HasComponent<TComponent>(long entityId) where TComponent : struct;
    bool TryGetComponent<TComponent>(long entityId, out TComponent component) where TComponent : struct;
    TComponent GetComponent<TComponent>(long entityId) where TComponent : struct;
    void SetComponent<TComponent>(long entityId, TComponent component) where TComponent : struct;
    bool RemoveComponent<TComponent>(long entityId) where TComponent : struct;
}

internal sealed class IndexedWorldDataStorage : IWorldDataStorage
{
    private readonly EntityRegistry entities = new();
    private readonly ComponentTypeRegistry componentTypes = new();
    private readonly List<IComponentPool> componentPools = new();
    private readonly QueryCache queryCache = new();

    public int EntityCount => entities.Count;

    public bool AddEntity(GameEntity entity)
    {
        if (!entities.Add(entity))
        {
            return false;
        }

        queryCache.Clear();
        return true;
    }

    public bool RemoveEntity(long entityId, out GameEntity entity)
    {
        if (!entities.TryGetIndex(entityId, out int entityIndex) ||
            !entities.TryGetEntity(entityIndex, out entity))
        {
            entity = default!;
            return false;
        }

        for (int i = 0; i < componentPools.Count; i++)
        {
            componentPools[i].Remove(entityIndex);
        }

        entities.Remove(entityIndex);
        queryCache.Clear();
        return true;
    }

    public bool TryGetEntity(long entityId, out GameEntity entity)
    {
        if (entities.TryGetIndex(entityId, out int entityIndex))
        {
            return entities.TryGetEntity(entityIndex, out entity);
        }

        entity = default!;
        return false;
    }

    public IReadOnlyList<GameEntity> EnumerateEntities(EntityIterationOrder order)
    {
        IReadOnlyList<int> indexes = entities.AliveIndexes(order);
        var result = new List<GameEntity>(indexes.Count);
        for (int i = 0; i < indexes.Count; i++)
        {
            if (entities.TryGetEntity(indexes[i], out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        return result;
    }

    public IReadOnlyList<GameEntity> QueryEntities(IReadOnlyList<Type> componentTypes, EntityIterationOrder order)
    {
        if (componentTypes.Count == 0)
        {
            return EnumerateEntities(order);
        }

        ulong requiredMask = 0;
        for (int i = 0; i < componentTypes.Count; i++)
        {
            if (!this.componentTypes.TryGet(componentTypes[i], out int componentTypeId))
            {
                return Array.Empty<GameEntity>();
            }

            requiredMask |= 1UL << componentTypeId;
        }

        IReadOnlyList<int> indexes = queryCache.GetOrBuild(requiredMask, order, entities);
        var result = new List<GameEntity>(indexes.Count);
        for (int i = 0; i < indexes.Count; i++)
        {
            if (entities.TryGetEntity(indexes[i], out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        return result;
    }

    public bool HasComponent<TComponent>(long entityId) where TComponent : struct
    {
        if (!entities.TryGetIndex(entityId, out int entityIndex) ||
            !componentTypes.TryGet(typeof(TComponent), out int componentTypeId))
        {
            return false;
        }

        return entities.HasComponent(entityIndex, componentTypeId);
    }

    public bool TryGetComponent<TComponent>(long entityId, out TComponent component) where TComponent : struct
    {
        if (entities.TryGetIndex(entityId, out int entityIndex) &&
            TryGetPool(out ComponentPool<TComponent> pool))
        {
            return pool.TryGet(entityIndex, out component);
        }

        component = default;
        return false;
    }

    public TComponent GetComponent<TComponent>(long entityId) where TComponent : struct
    {
        if (!entities.TryGetIndex(entityId, out int entityIndex))
        {
            throw new KeyNotFoundException("Entity not found: " + entityId);
        }

        return GetPool<TComponent>().Get(entityIndex);
    }

    public void SetComponent<TComponent>(long entityId, TComponent component) where TComponent : struct
    {
        if (!entities.TryGetIndex(entityId, out int entityIndex))
        {
            return;
        }

        int componentTypeId = componentTypes.GetOrAdd(typeof(TComponent));
        GetPool<TComponent>().Set(entityIndex, component);
        entities.SetComponent(entityIndex, componentTypeId);
        queryCache.Clear();
    }

    public bool RemoveComponent<TComponent>(long entityId) where TComponent : struct
    {
        if (!entities.TryGetIndex(entityId, out int entityIndex) ||
            !componentTypes.TryGet(typeof(TComponent), out int componentTypeId) ||
            !TryGetPool(out ComponentPool<TComponent> pool) ||
            !pool.Remove(entityIndex))
        {
            return false;
        }

        entities.RemoveComponent(entityIndex, componentTypeId);
        queryCache.Clear();
        return true;
    }

    private ComponentPool<TComponent> GetPool<TComponent>() where TComponent : struct
    {
        int componentTypeId = componentTypes.GetOrAdd(typeof(TComponent));
        while (componentPools.Count <= componentTypeId)
        {
            componentPools.Add(null!);
        }

        if (componentPools[componentTypeId] == null)
        {
            componentPools[componentTypeId] = new ComponentPool<TComponent>();
        }

        return (ComponentPool<TComponent>)componentPools[componentTypeId];
    }

    private bool TryGetPool<TComponent>(out ComponentPool<TComponent> pool) where TComponent : struct
    {
        if (!componentTypes.TryGet(typeof(TComponent), out int componentTypeId) ||
            componentTypeId >= componentPools.Count ||
            componentPools[componentTypeId] == null)
        {
            pool = default!;
            return false;
        }

        pool = (ComponentPool<TComponent>)componentPools[componentTypeId];
        return true;
    }
}

internal sealed class EntityRegistry
{
    private readonly List<EntityRecord> records = new();
    private readonly List<int> freeIndexes = new();
    private readonly EntityIdIndex entityIds = new();
    private int count;

    public int Count => count;

    public bool Add(GameEntity entity)
    {
        if (entityIds.TryGet(entity.EntityId, out _))
        {
            return false;
        }

        int index;
        if (freeIndexes.Count > 0)
        {
            int last = freeIndexes.Count - 1;
            index = freeIndexes[last];
            freeIndexes.RemoveAt(last);
            EntityRecord record = records[index];
            record.Entity = entity;
            record.EntityId = entity.EntityId;
            record.ComponentMask = 0;
            record.Alive = true;
            records[index] = record;
        }
        else
        {
            index = records.Count;
            records.Add(new EntityRecord(entity.EntityId, entity, 1, true, 0));
        }

        count++;
        entityIds.Set(entity.EntityId, index);
        return true;
    }

    public bool Remove(int entityIndex)
    {
        if (entityIndex < 0 ||
            entityIndex >= records.Count ||
            !records[entityIndex].Alive)
        {
            return false;
        }

        EntityRecord record = records[entityIndex];
        long entityId = record.EntityId;
        record.Entity = null!;
        record.EntityId = 0;
        record.Generation++;
        record.ComponentMask = 0;
        record.Alive = false;
        records[entityIndex] = record;
        freeIndexes.Add(entityIndex);
        entityIds.Remove(entityId);
        count--;
        return true;
    }

    public bool TryGetIndex(long entityId, out int entityIndex)
    {
        return entityIds.TryGet(entityId, out entityIndex);
    }

    public bool TryGetEntity(int entityIndex, out GameEntity entity)
    {
        if (entityIndex >= 0 &&
            entityIndex < records.Count &&
            records[entityIndex].Alive)
        {
            entity = records[entityIndex].Entity;
            return true;
        }

        entity = default!;
        return false;
    }

    public void SetComponent(int entityIndex, int componentTypeId)
    {
        EntityRecord record = records[entityIndex];
        record.ComponentMask |= 1UL << componentTypeId;
        records[entityIndex] = record;
    }

    public void RemoveComponent(int entityIndex, int componentTypeId)
    {
        EntityRecord record = records[entityIndex];
        record.ComponentMask &= ~(1UL << componentTypeId);
        records[entityIndex] = record;
    }

    public bool HasComponent(int entityIndex, int componentTypeId)
    {
        return entityIndex >= 0 &&
            entityIndex < records.Count &&
            records[entityIndex].Alive &&
            (records[entityIndex].ComponentMask & (1UL << componentTypeId)) != 0;
    }

    public IReadOnlyList<int> AliveIndexes(EntityIterationOrder order)
    {
        var result = new List<int>(count);
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].Alive)
            {
                result.Add(i);
            }
        }

        if (order == EntityIterationOrder.EntityId)
        {
            result.Sort((left, right) => records[left].EntityId.CompareTo(records[right].EntityId));
        }

        return result;
    }

    public IReadOnlyList<int> Match(ulong requiredMask, EntityIterationOrder order)
    {
        var result = new List<int>();
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].Alive && (records[i].ComponentMask & requiredMask) == requiredMask)
            {
                result.Add(i);
            }
        }

        if (order == EntityIterationOrder.EntityId)
        {
            result.Sort((left, right) => records[left].EntityId.CompareTo(records[right].EntityId));
        }

        return result;
    }
}

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

internal struct EntityRecord
{
    public EntityRecord(long entityId, GameEntity entity, int generation, bool alive, ulong componentMask)
    {
        EntityId = entityId;
        Entity = entity;
        Generation = generation;
        Alive = alive;
        ComponentMask = componentMask;
    }

    public long EntityId;
    public GameEntity Entity;
    public int Generation;
    public bool Alive;
    public ulong ComponentMask;
}

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
