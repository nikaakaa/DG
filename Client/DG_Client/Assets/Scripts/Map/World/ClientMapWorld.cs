using System.Collections.Generic;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public sealed class ClientMapWorld
    {
        private readonly GameWorld coreWorld;
        private readonly Dictionary<long, ClientMapEntity> entityViews = new();
        private readonly Dictionary<long, long> entityServerTicks = new();
        private long nextEntityId = 1;

        public ClientMapWorld() : this(FallbackGameConfigProvider.Instance)
        {
        }

        public ClientMapWorld(IGameConfigProvider configProvider)
        {
            coreWorld = new GameWorld(configProvider);
        }

        public GameWorld CoreWorld => coreWorld;
        public IReadOnlyDictionary<long, ClientMapEntity> RegisteredEntities => entityViews;
        public IReadOnlyCollection<long> ChangedCells => coreWorld.ChangedCells;
        public IReadOnlyCollection<long> ChangedChunks => coreWorld.ChangedChunks;
        public int EntityCount => coreWorld.EntityCount;

        public bool AddEntity(ClientMapEntity entity, EntitySpawnSpec spawn)
        {
            if (entity == null)
            {
                return false;
            }

            if (spawn.EntityId == 0)
            {
                return false;
            }

            if (entity.EntityId == 0)
            {
                entity.EntityId = spawn.EntityId;
            }

            if (entity.EntityId != spawn.EntityId)
            {
                return false;
            }

            EnsureEntityId(entity);
            if (!coreWorld.AddOrUpdateEntity(spawn))
            {
                return false;
            }

            entityViews[entity.EntityId] = entity;
            return true;
        }

        public bool AddEntity(ClientMapEntity entity, Vector2Int worldCoord, int configId, Direction direction, long playerId, int autoMoveIntervalTicks)
        {
            if (entity == null)
            {
                return false;
            }

            EnsureEntityId(entity);
            var spawn = new EntitySpawnSpec(
                entity.EntityId,
                configId,
                MapCoordinate.ToGridCoord(worldCoord),
                direction,
                playerId,
                autoMoveIntervalTicks);
            return AddEntity(entity, spawn);
        }

        public bool ApplySnapshot(EntitySnapshot snapshot)
        {
            if (entityServerTicks.TryGetValue(snapshot.EntityId, out long currentTick) && snapshot.ServerTick < currentTick)
            {
                return true;
            }

            if (!coreWorld.ApplySnapshot(snapshot))
            {
                return false;
            }

            entityServerTicks[snapshot.EntityId] = snapshot.ServerTick;
            EnsureView(snapshot.EntityId);
            return true;
        }

        public bool ApplySpawn(EntitySpawnSpec spawn)
        {
            if (!coreWorld.AddOrUpdateEntity(spawn))
            {
                return false;
            }

            EnsureView(spawn.EntityId);
            return true;
        }

        public bool RemoveEntity(long entityId)
        {
            bool removed = coreWorld.RemoveEntity(entityId);
            entityViews.Remove(entityId);
            entityServerTicks.Remove(entityId);
            return removed;
        }

        public bool RemoveEntity(ClientMapEntity entity)
        {
            return entity != null && RemoveEntity(entity.EntityId);
        }

        public bool MoveEntity(long entityId, Vector2Int targetWorldCoord)
        {
            if (!coreWorld.TryGetEntity(entityId, out GameEntity coreEntity))
            {
                return false;
            }

            coreWorld.MoveEntity(coreEntity, MapCoordinate.ToGridCoord(targetWorldCoord));
            EnsureView(entityId);
            return true;
        }

        public bool MoveEntity(ClientMapEntity entity, Vector2Int targetWorldCoord)
        {
            return entity != null && MoveEntity(entity.EntityId, targetWorldCoord);
        }

        public bool MoveEntityOrCreate(ClientMapEntity entity, EntitySpawnSpec spawn)
        {
            if (entity == null)
            {
                return false;
            }

            if (!coreWorld.TryGetEntity(entity.EntityId, out _))
            {
                return AddEntity(entity, spawn);
            }

            return MoveEntity(entity.EntityId, MapCoordinate.ToVector2Int(spawn.Position));
        }

        public bool TryGetEntity(long entityId, out ClientMapEntity entity)
        {
            return entityViews.TryGetValue(entityId, out entity);
        }

        public bool TryGetCoreEntity(long entityId, out GameEntity entity)
        {
            return coreWorld.TryGetEntity(entityId, out entity);
        }

        public bool TryGetPosition(long entityId, out Vector2Int coord)
        {
            coord = default;
            if (!coreWorld.TryGetEntity(entityId, out GameEntity entity) ||
                !coreWorld.TryGetComponent(entity, out PositionComponent position))
            {
                return false;
            }

            coord = MapCoordinate.ToVector2Int(position.Coord);
            return true;
        }

        public bool TryGetSnapshot(long entityId, out EntitySnapshot snapshot)
        {
            snapshot = default;
            if (!coreWorld.TryGetEntity(entityId, out GameEntity entity))
            {
                return false;
            }

            snapshot = coreWorld.CreateSnapshot(entity);
            return true;
        }

        public bool TryGetServerTick(long entityId, out long serverTick)
        {
            return entityServerTicks.TryGetValue(entityId, out serverTick);
        }

        public IReadOnlyList<EntitySnapshot> CreateSnapshot()
        {
            return coreWorld.CreateSnapshot();
        }

        public WorldDelta FlushDelta()
        {
            return coreWorld.FlushDelta();
        }

        public void ClearDirty()
        {
            coreWorld.ClearSpatialDirty();
            coreWorld.FlushDelta();
        }

        private ClientMapEntity EnsureView(long entityId)
        {
            if (entityViews.TryGetValue(entityId, out ClientMapEntity found))
            {
                return found;
            }

            var entity = new ClientMapEntity { EntityId = entityId };
            entityViews.Add(entityId, entity);
            if (entityId >= nextEntityId)
            {
                nextEntityId = entityId + 1;
            }

            return entity;
        }

        private void EnsureEntityId(ClientMapEntity entity)
        {
            if (entity.EntityId != 0)
            {
                if (entity.EntityId >= nextEntityId)
                {
                    nextEntityId = entity.EntityId + 1;
                }

                return;
            }

            while (entityViews.ContainsKey(nextEntityId) || coreWorld.TryGetEntity(nextEntityId, out _))
            {
                nextEntityId++;
            }

            entity.EntityId = nextEntityId++;
        }
    }
}
