using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
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

}
