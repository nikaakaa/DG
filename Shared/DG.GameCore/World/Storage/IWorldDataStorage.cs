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

}
