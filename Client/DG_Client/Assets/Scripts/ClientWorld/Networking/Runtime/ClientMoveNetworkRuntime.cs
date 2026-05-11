using System;
using System.Collections.Generic;
using DG.GameCore;
using Fantasy;
using UnityEngine;

namespace DG.Map
{
    public static class ClientMoveNetworkRuntime
    {
        public static ClientWorldRunner Runner { get; private set; }
        public static long LocalEntityId { get; private set; }
        public static bool HasLocalEntity { get; private set; }
        public static long DemoMovingEntityId { get; private set; }
        public static bool HasDemoMovingEntity { get; private set; }
        public static long LastWorldServerTick { get; private set; }
        public static int ActivePendingStateCount { get; private set; } = -1;
        public static string LastRuleFailureReason { get; private set; } = string.Empty;

        public static void SetRunner(ClientWorldRunner runner)
        {
            Runner = runner;
        }

        public static bool ApplyMovedNotify(long entityId, int finalX, int finalY)
        {
            if (Runner == null)
            {
                return false;
            }

            return Runner.ApplyServerMovementOrCreate(entityId, new Vector2Int(finalX, finalY), DefaultWorldConfig.PlayerConfigId, Direction.None, entityId, 1);
        }

        public static bool ApplyJoinedPlayer(long entityId, int currentX, int currentY)
        {
            if (!ApplyMovedNotify(entityId, currentX, currentY))
            {
                return false;
            }

            LocalEntityId = entityId;
            HasLocalEntity = true;
            return true;
        }

        public static bool ApplyWorldSnapshot(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities)
        {
            return ApplyWorldSnapshot(serverTick, entities, Array.Empty<G2C_WorldDeltaAnimationMetadata>());
        }

        public static bool ApplyWorldSnapshot(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities, IReadOnlyList<G2C_WorldDeltaAnimationMetadata> animationMetadata)
        {
            if (Runner == null || Runner.Context == null)
            {
                return ApplyWorldEntities(serverTick, entities);
            }

            IReadOnlyList<EntitySnapshot> before = Runner.Context.ClientMapWorld.CreateSnapshot();
            bool allApplied = ApplyWorldEntities(serverTick, entities);
            IReadOnlyList<EntitySnapshot> after = Runner.Context.ClientMapWorld.CreateSnapshot();
            Runner.Context.AnimationLayer.CaptureSnapshotApply(serverTick, before, after, ConvertAnimationMetadata(animationMetadata));
            return allApplied;
        }

        public static bool ApplyWorldDelta(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities)
        {
            return ApplyWorldEntities(serverTick, entities);
        }

        public static bool ApplyWorldDelta(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities, IReadOnlyList<long> removedEntityIds)
        {
            return ApplyWorldDelta(serverTick, entities, removedEntityIds, Array.Empty<G2C_WorldDeltaAnimationMetadata>());
        }

        public static bool ApplyWorldDelta(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities, IReadOnlyList<long> removedEntityIds, IReadOnlyList<G2C_WorldDeltaAnimationMetadata> animationMetadata)
        {
            if (Runner == null || Runner.Context == null)
            {
                return removedEntityIds.Count == 0 && ApplyWorldEntities(serverTick, entities);
            }

            IReadOnlyList<EntitySnapshot> before = Runner.Context.ClientMapWorld.CreateSnapshot();
            bool allApplied = ApplyRemovedEntities(serverTick, removedEntityIds);
            allApplied &= ApplyWorldEntities(serverTick, entities);
            IReadOnlyList<EntitySnapshot> after = Runner.Context.ClientMapWorld.CreateSnapshot();
            IReadOnlyList<ClientAnimationMetadata> convertedMetadata = ConvertAnimationMetadata(animationMetadata);
            Runner.Context.AnimationLayer.CaptureSnapshotApply(serverTick, before, after, convertedMetadata);
            return allApplied;
        }

        public static bool ApplyWorldEntity(long serverTick, long entityId, int configId, int archetypeId, int entityTarget, int x, int y, int direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, bool playerControlled)
        {
            return ApplyWorldEntity(serverTick, entityId, configId, archetypeId, entityTarget, x, y, direction, hasCollider, blocking, bouncable, autoMove, autoMove ? 1 : 0, playerControlled, false, 0, false, true, true);
        }

        public static bool ApplyWorldEntity(long serverTick, long entityId, int configId, int archetypeId, int entityTarget, int x, int y, int direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, int autoMoveIntervalTicks, bool playerControlled, bool pushable, int portLocalPorts, bool hasMovementPermission, bool canMove, bool canBePushed)
        {
            if (Runner == null || Runner.Context == null)
            {
                Debug.LogWarning($"[ClientWorldState] current ClientMapWorld unavailable entity:{entityId} config:{configId} serverTick:{serverTick}");
                return false;
            }

            var snapshot = new EntitySnapshot(
                entityId,
                configId,
                archetypeId,
                entityTarget,
                x,
                y,
                (Direction)direction,
                hasCollider,
                blocking,
                bouncable,
                autoMove,
                autoMoveIntervalTicks,
                playerControlled,
                pushable,
                (DirectionMask)portLocalPorts,
                hasMovementPermission,
                canMove,
                canBePushed,
                serverTick);

            if (!Runner.ApplyServerSnapshot(snapshot))
            {
                return false;
            }

            LastWorldServerTick = Math.Max(LastWorldServerTick, serverTick);
            if (bouncable || autoMove)
            {
                DemoMovingEntityId = entityId;
                HasDemoMovingEntity = true;
            }

            return true;
        }

        private static bool ApplyWorldEntities(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities)
        {
            bool allApplied = true;
            for (int i = 0; i < entities.Count; i++)
            {
                G2C_WorldEntityState entity = entities[i];
                allApplied &= ApplyWorldEntity(
                    serverTick,
                    entity.EntityId,
                    entity.ConfigId,
                    entity.ArchetypeId,
                    entity.EntityTarget,
                    entity.X,
                    entity.Y,
                    entity.Direction,
                    entity.HasCollider,
                    entity.Blocking,
                    entity.Bouncable,
                    entity.AutoMove,
                    entity.AutoMoveIntervalTicks,
                    entity.PlayerControlled,
                    entity.Pushable,
                    entity.PortLocalPorts,
                    entity.HasMovementPermission,
                    entity.CanMove,
                    entity.CanBePushed);
            }

            return allApplied;
        }

        private static bool ApplyRemovedEntities(long serverTick, IReadOnlyList<long> removedEntityIds)
        {
            if (Runner == null || Runner.Context == null)
            {
                return removedEntityIds.Count == 0;
            }

            for (int i = 0; i < removedEntityIds.Count; i++)
            {
                Runner.Context.ClientMapWorld.RemoveEntity(removedEntityIds[i]);
            }

            if (removedEntityIds.Count > 0)
            {
                LastWorldServerTick = Math.Max(LastWorldServerTick, serverTick);
            }

            return true;
        }

        private static IReadOnlyList<ClientAnimationMetadata> ConvertAnimationMetadata(IReadOnlyList<G2C_WorldDeltaAnimationMetadata> metadata)
        {
            if (metadata == null || metadata.Count == 0)
            {
                return Array.Empty<ClientAnimationMetadata>();
            }

            var result = new List<ClientAnimationMetadata>(metadata.Count);
            for (int i = 0; i < metadata.Count; i++)
            {
                G2C_WorldDeltaAnimationMetadata item = metadata[i];
                result.Add(new ClientAnimationMetadata(
                    item.EntityId,
                    item.ServerTick,
                    ConvertMotionKind(item.MotionKind),
                    item.StyleKey,
                    (Direction)item.Direction));
            }

            return result;
        }

        private static ClientAnimationMotionKind ConvertMotionKind(int motionKind)
        {
            return motionKind switch
            {
                (int)WorldDeltaMotionKind.PlayerMove => ClientAnimationMotionKind.PlayerMove,
                (int)WorldDeltaMotionKind.MechanismPush => ClientAnimationMotionKind.MechanismPush,
                (int)WorldDeltaMotionKind.AutoMove => ClientAnimationMotionKind.AutoMove,
                (int)WorldDeltaMotionKind.DebugDrag => ClientAnimationMotionKind.DebugDrag,
                (int)WorldDeltaMotionKind.Spawn => ClientAnimationMotionKind.Spawn,
                (int)WorldDeltaMotionKind.Remove => ClientAnimationMotionKind.Remove,
                _ => ClientAnimationMotionKind.Unknown
            };
        }

        public static void Clear()
        {
            Runner = null;
            LocalEntityId = 0;
            HasLocalEntity = false;
            DemoMovingEntityId = 0;
            HasDemoMovingEntity = false;
            LastWorldServerTick = 0;
            ActivePendingStateCount = -1;
            LastRuleFailureReason = string.Empty;
        }

        public static void RecordRuleFailure(string reason)
        {
            LastRuleFailureReason = reason ?? string.Empty;
        }
    }
}
