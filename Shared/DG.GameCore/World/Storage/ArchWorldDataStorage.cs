#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Generic;
using Arch.Core;
using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;

namespace DG.GameCore
{
internal sealed class ArchWorldDataStorage : IWorldDataStorage
{
    private readonly ArchWorld world = ArchWorld.Create();
    private readonly Dictionary<long, ArchEntity> entityToArch = new();
    private readonly Dictionary<ArchEntity, long> archToEntity = new();
    private readonly Dictionary<long, GameEntity> entities = new();

    public int EntityCount => entities.Count;

    public bool AddEntity(GameEntity entity)
    {
        if (entity == null || entities.ContainsKey(entity.EntityId))
        {
            return false;
        }

        ArchEntity archEntity = world.Create();
        entityToArch.Add(entity.EntityId, archEntity);
        archToEntity.Add(archEntity, entity.EntityId);
        entities.Add(entity.EntityId, entity);
        return true;
    }

    public bool RemoveEntity(long entityId, out GameEntity entity)
    {
        if (!entities.TryGetValue(entityId, out entity) ||
            !entityToArch.TryGetValue(entityId, out ArchEntity archEntity))
        {
            entity = default!;
            return false;
        }

        if (world.IsAlive(archEntity))
        {
            world.Destroy(archEntity);
        }

        entities.Remove(entityId);
        entityToArch.Remove(entityId);
        archToEntity.Remove(archEntity);
        return true;
    }

    public bool TryGetEntity(long entityId, out GameEntity entity)
    {
        return entities.TryGetValue(entityId, out entity);
    }

    public IReadOnlyList<GameEntity> EnumerateEntities(EntityIterationOrder order)
    {
        var result = new List<GameEntity>(entities.Count);
        foreach (GameEntity entity in entities.Values)
        {
            result.Add(entity);
        }

        if (order == EntityIterationOrder.EntityId)
        {
            result.Sort(static (left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        return result;
    }

    public IReadOnlyList<GameEntity> QueryEntities(IReadOnlyList<Type> componentTypes, EntityIterationOrder order)
    {
        if (componentTypes.Count == 0)
        {
            return EnumerateEntities(order);
        }

        var archTypes = new ComponentType[componentTypes.Count];
        for (int i = 0; i < componentTypes.Count; i++)
        {
            archTypes[i] = ComponentRegistry.Add(componentTypes[i]);
        }

        var query = new QueryDescription(new Signature(archTypes));
        int count = world.CountEntities(query);
        if (count == 0)
        {
            return Array.Empty<GameEntity>();
        }

        var archEntities = new ArchEntity[count];
        world.GetEntities(query, archEntities);
        var result = new List<GameEntity>(count);
        for (int i = 0; i < archEntities.Length; i++)
        {
            if (archToEntity.TryGetValue(archEntities[i], out long entityId) &&
                entities.TryGetValue(entityId, out GameEntity entity))
            {
                result.Add(entity);
            }
        }

        if (order == EntityIterationOrder.EntityId)
        {
            result.Sort(static (left, right) => left.EntityId.CompareTo(right.EntityId));
        }

        return result;
    }

    public bool HasComponent<TComponent>(long entityId) where TComponent : struct
    {
        return entityToArch.TryGetValue(entityId, out ArchEntity archEntity) &&
            world.IsAlive(archEntity) &&
            world.Has<TComponent>(archEntity);
    }

    public bool TryGetComponent<TComponent>(long entityId, out TComponent component) where TComponent : struct
    {
        if (entityToArch.TryGetValue(entityId, out ArchEntity archEntity) &&
            world.IsAlive(archEntity) &&
            world.Has<TComponent>(archEntity))
        {
            component = world.Get<TComponent>(archEntity);
            return true;
        }

        component = default;
        return false;
    }

    public TComponent GetComponent<TComponent>(long entityId) where TComponent : struct
    {
        if (!entityToArch.TryGetValue(entityId, out ArchEntity archEntity) ||
            !world.IsAlive(archEntity))
        {
            throw new KeyNotFoundException("Entity not found: " + entityId);
        }

        return world.Get<TComponent>(archEntity);
    }

    public void SetComponent<TComponent>(long entityId, TComponent component) where TComponent : struct
    {
        if (!entityToArch.TryGetValue(entityId, out ArchEntity archEntity) ||
            !world.IsAlive(archEntity))
        {
            return;
        }

        if (world.Has<TComponent>(archEntity))
        {
            world.Set(archEntity, component);
        }
        else
        {
            world.Add(archEntity, component);
        }
    }

    public bool RemoveComponent<TComponent>(long entityId) where TComponent : struct
    {
        if (!entityToArch.TryGetValue(entityId, out ArchEntity archEntity) ||
            !world.IsAlive(archEntity) ||
            !world.Has<TComponent>(archEntity))
        {
            return false;
        }

        world.Remove<TComponent>(archEntity);
        return true;
    }
}

}
#endif
