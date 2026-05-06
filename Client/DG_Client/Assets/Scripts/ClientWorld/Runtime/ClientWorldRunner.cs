using System;
using System.Collections.Generic;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public sealed class ClientWorldRunner : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;
        [SerializeField] private float fixedTickInterval = 0.1f;
        [SerializeField] private int maxTicksPerFrame = 4;

        private readonly List<IClientSystem> Systems = new();
        private float accumulator;

        public ClientWorldContext Context { get; private set; }
        public DirtyFlushSystem DirtyFlushSystem { get; private set; }
        public float FixedTickInterval => fixedTickInterval;
        public int MaxTicksPerFrame => maxTicksPerFrame;

        private void Awake()
        {
            if (worldBootstrap == null)
            {
                worldBootstrap = GetComponent<WorldBootstrap>();
            }

            if (worldBootstrap == null)
            {
                worldBootstrap = FindObjectOfType<WorldBootstrap>();
            }

            if (worldBootstrap == null)
            {
                worldBootstrap = gameObject.AddComponent<WorldBootstrap>();
            }

            Initialize(worldBootstrap.EnsureWorld());
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        public void Initialize(ClientMapWorld clientMapWorld)
        {
            if (clientMapWorld == null)
            {
                throw new ArgumentNullException(nameof(clientMapWorld));
            }

            Context = new ClientWorldContext(clientMapWorld);
            DirtyFlushSystem = new DirtyFlushSystem();
            Systems.Clear();
            Systems.Add(DirtyFlushSystem);
            accumulator = 0f;
        }

        public int Advance(float deltaTime)
        {
            if (Context == null)
            {
                return 0;
            }

            if (fixedTickInterval <= 0f)
            {
                return 0;
            }

            accumulator += deltaTime;
            int tickCount = 0;
            int tickLimit = Mathf.Max(1, maxTicksPerFrame);

            while (accumulator >= fixedTickInterval && tickCount < tickLimit)
            {
                TickOnce(fixedTickInterval);
                accumulator -= fixedTickInterval;
                tickCount++;
            }

            if (tickCount == tickLimit && accumulator >= fixedTickInterval)
            {
                accumulator = 0f;
            }

            return tickCount;
        }

        public bool ApplyServerMovement(long entityId, Vector2Int finalCoord)
        {
            if (Context == null)
            {
                return false;
            }

            return Context.ClientMapWorld.MoveEntity(entityId, finalCoord);
        }

        public bool ApplyServerMovementOrCreate(long entityId, Vector2Int finalCoord, int configId, Direction direction, long playerId, int autoMoveIntervalTicks)
        {
            if (Context == null)
            {
                return false;
            }

            if (!Context.ClientMapWorld.TryGetCoreEntity(entityId, out _))
            {
                var entity = new ClientMapEntity { EntityId = entityId };
                var spawn = new EntitySpawnSpec(
                    entityId,
                    configId,
                    MapCoordinate.ToGridCoord(finalCoord),
                    direction,
                    playerId,
                    autoMoveIntervalTicks);
                return Context.ClientMapWorld.AddEntity(entity, spawn);
            }

            return Context.ClientMapWorld.MoveEntity(entityId, finalCoord);
        }

        public bool ApplyServerSnapshot(EntitySnapshot snapshot)
        {
            if (Context == null)
            {
                return false;
            }

            return Context.ClientMapWorld.ApplySnapshot(snapshot);
        }

        private void TickOnce(float deltaTime)
        {
            Context.AdvanceTick(deltaTime);
            for (int i = 0; i < Systems.Count; i++)
            {
                Systems[i].Tick(Context, deltaTime);
            }
        }
    }
}
