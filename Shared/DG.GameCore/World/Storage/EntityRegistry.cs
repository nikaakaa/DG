using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
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

}
