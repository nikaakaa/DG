using System;
using System.Collections.Generic;
using DG.GameCore;
using Fantasy;
using UnityEngine;

namespace DG.Map
{
    public enum ClientPlayerInputStatus
    {
        Buffered = 1,
        Replaced = 2,
        Consumed = 3,
        Expired = 4,
        Rejected = 5,
        Resolved = 6
    }

    public static class ClientMoveNetworkRuntime
    {
        public static ClientWorldRunner Runner { get; private set; }
        public static long LocalEntityId { get; private set; }
        public static bool HasLocalEntity { get; private set; }
        public static long DemoMovingEntityId { get; private set; }
        public static bool HasDemoMovingEntity { get; private set; }
        public static long LastWorldServerTick { get; private set; }
        public static string LastRuleFailureReason { get; private set; } = string.Empty;
        private static readonly Dictionary<long, PendingPlayerInput> PendingInputs = new Dictionary<long, PendingPlayerInput>();

        public static int PendingInputCount => PendingInputs.Count;

        public static void SetRunner(ClientWorldRunner runner)
        {
            Runner = runner;
        }

        public static bool ApplyMovedNotify(long entityId, int finalX, int finalY)
        {
            if (Runner == null || Runner.Context == null)
            {
                return false;
            }

            if (Runner.Context.ClientMapWorld.TryGetCoreEntity(entityId, out _))
            {
                return true;
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
            return ApplyWorldSnapshot(serverTick, entities, Array.Empty<G2C_PresentationFact>());
        }

        public static bool ApplyWorldSnapshot(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities, IReadOnlyList<G2C_PresentationFact> presentationFacts)
        {
            if (Runner == null || Runner.Context == null)
            {
                return ApplyWorldEntities(serverTick, entities);
            }

            IReadOnlyList<EntitySnapshot> before = Runner.Context.ClientMapWorld.CreateSnapshot();
            bool allApplied = ApplyWorldEntities(serverTick, entities);
            IReadOnlyList<EntitySnapshot> after = Runner.Context.ClientMapWorld.CreateSnapshot();
            Runner.Context.AnimationLayer.CaptureSnapshotApplyFacts(serverTick, before, after, ClientPresentationFactTranslator.Translate(presentationFacts));
            return allApplied;
        }

        public static bool ApplyWorldDelta(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities)
        {
            return ApplyWorldEntities(serverTick, entities);
        }

        public static bool ApplyWorldDelta(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities, IReadOnlyList<long> removedEntityIds)
        {
            return ApplyWorldDelta(serverTick, entities, removedEntityIds, Array.Empty<G2C_PresentationFact>());
        }

        public static bool ApplyWorldDelta(long serverTick, IReadOnlyList<G2C_WorldEntityState> entities, IReadOnlyList<long> removedEntityIds, IReadOnlyList<G2C_PresentationFact> presentationFacts)
        {
            if (Runner == null || Runner.Context == null)
            {
                return removedEntityIds.Count == 0 && ApplyWorldEntities(serverTick, entities);
            }

            IReadOnlyList<EntitySnapshot> before = Runner.Context.ClientMapWorld.CreateSnapshot();
            bool allApplied = ApplyRemovedEntities(serverTick, removedEntityIds);
            allApplied &= ApplyWorldEntities(serverTick, entities);
            IReadOnlyList<EntitySnapshot> after = Runner.Context.ClientMapWorld.CreateSnapshot();
            IReadOnlyList<ClientPresentationFact> convertedFacts = ClientPresentationFactTranslator.Translate(presentationFacts);
            Runner.Context.AnimationLayer.CaptureSnapshotApplyFacts(serverTick, before, after, convertedFacts);
            ClearResolvedInputs(serverTick);
            return allApplied;
        }

        public static bool ApplyWorldEntity(long serverTick, long entityId, int configId, int archetypeId, int entityTarget, int x, int y, int direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, bool playerControlled)
        {
            return ApplyWorldEntity(serverTick, entityId, configId, archetypeId, entityTarget, x, y, direction, hasCollider, blocking, bouncable, autoMove, autoMove ? 1 : 0, playerControlled, false, 0, false, true, true);
        }

        public static bool ApplyWorldEntity(long serverTick, long entityId, int configId, int archetypeId, int entityTarget, int x, int y, int direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, int autoMoveIntervalTicks, bool playerControlled, bool pushable, int portLocalPorts, bool hasMovementPermission, bool canMove, bool canBePushed)
            => ApplyWorldEntity(serverTick, entityId, configId, archetypeId, entityTarget, x, y, direction, hasCollider, blocking, bouncable, autoMove, autoMoveIntervalTicks, playerControlled, pushable, portLocalPorts, false, hasMovementPermission, canMove, canBePushed);

        public static bool ApplyWorldEntity(long serverTick, long entityId, int configId, int archetypeId, int entityTarget, int x, int y, int direction, bool hasCollider, bool blocking, bool bouncable, bool autoMove, int autoMoveIntervalTicks, bool playerControlled, bool pushable, int portLocalPorts, bool rotatePivot, bool hasMovementPermission, bool canMove, bool canBePushed)
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
                rotatePivot,
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
                    entity.RotatePivot,
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

        public static void Clear()
        {
            Runner = null;
            LocalEntityId = 0;
            HasLocalEntity = false;
            DemoMovingEntityId = 0;
            HasDemoMovingEntity = false;
            LastWorldServerTick = 0;
            LastRuleFailureReason = string.Empty;
            PendingInputs.Clear();
        }

        public static void RecordRuleFailure(string reason)
        {
            LastRuleFailureReason = reason ?? string.Empty;
        }

        public static void RecordPendingInput(long clientInputId, long entityId, long beatTick, Direction direction, long clientTick)
        {
            RecordPendingIntent(clientInputId, entityId, beatTick, direction, clientTick);
        }

        public static void RecordPendingIntent(long clientInputId, long entityId, long beatTick, Direction direction, long clientTick)
        {
            PendingInputs[clientInputId] = new PendingPlayerInput(clientInputId, entityId, beatTick, direction, clientTick, ClientInputKind.Move, ClientInputSourceKind.Player);
        }

        public static bool TryGetPendingInput(long clientInputId, out PendingPlayerInput input)
        {
            return PendingInputs.TryGetValue(clientInputId, out input);
        }

        public static void ResolvePendingInput(long clientInputId, int inputStatus)
        {
            if (inputStatus == 0)
            {
                return;
            }

            PendingInputs.Remove(clientInputId);
        }

        private static void ClearResolvedInputs(long serverTick)
        {
            if (PendingInputs.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<long, PendingPlayerInput> pair in new List<KeyValuePair<long, PendingPlayerInput>>(PendingInputs))
            {
                if (pair.Value.BeatTick <= serverTick)
                {
                    PendingInputs.Remove(pair.Key);
                }
            }
        }
    }

    public readonly struct PendingPlayerInput
    {
        public PendingPlayerInput(long clientInputId, long entityId, long beatTick, Direction direction, long clientTick)
            : this(clientInputId, entityId, beatTick, direction, clientTick, ClientInputKind.Move, ClientInputSourceKind.Player)
        {
        }

        public PendingPlayerInput(long clientInputId, long entityId, long beatTick, Direction direction, long clientTick, ClientInputKind inputKind, ClientInputSourceKind sourceKind)
        {
            ClientInputId = clientInputId;
            EntityId = entityId;
            BeatTick = beatTick;
            Direction = direction;
            ClientTick = clientTick;
            InputKind = inputKind;
            SourceKind = sourceKind;
        }

        public long ClientInputId { get; }
        public long EntityId { get; }
        public long BeatTick { get; }
        public Direction Direction { get; }
        public long ClientTick { get; }
        public ClientInputKind InputKind { get; }
        public ClientInputSourceKind SourceKind { get; }
    }

    public enum ClientInputKind
    {
        None = 0,
        Move = 1,
        Wait = 2,
        Attack = 3,
        Interact = 4,
        Skill = 5,
        Cancel = 6
    }

    public enum ClientInputSourceKind
    {
        None = 0,
        Player = 1,
        Debug = 2,
        AI = 3,
        Replay = 4,
        Script = 5
    }
}
