using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public sealed class ClientMoveNetworkSubmitter : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private FantasyRuntime fantasyRuntime;
        [SerializeField] private bool serverAuthoritative = true;
        [SerializeField] private int sessionWaitFrames = 600;

        public bool ServerAuthoritative => serverAuthoritative;
        public bool HasJoined { get; private set; }
        public long LocalEntityId { get; private set; }

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            if (fantasyRuntime == null)
            {
                fantasyRuntime = FindObjectOfType<FantasyRuntime>();
            }

            ClientMoveNetworkRuntime.SetRunner(runner);
        }

        public void RegisterObserver(ClientMapEntity entity)
        {
            if (!serverAuthoritative)
            {
                return;
            }

            StartCoroutine(RegisterObserverWhenReady(entity.EntityId));
        }

        public void JoinWorld(Action<ClientMapEntity> joined)
        {
            if (!serverAuthoritative)
            {
                return;
            }

            StartCoroutine(JoinWorldWhenReady(joined));
        }

        public void Submit(long entityId, Vector2Int targetCoord, int clientTick)
        {
            if (!serverAuthoritative)
            {
                Debug.LogWarning("[ClientMove] move ignored because server authoritative mode is disabled");
                return;
            }

            if (!HasJoined)
            {
                Debug.LogWarning("[ClientJoinWorld] move ignored before join");
                return;
            }

            SubmitAsync(entityId, targetCoord, clientTick).Coroutine();
        }

        public void DebugSpawn(long entityId, int configId, Vector2Int coord, Direction direction, long playerId, int autoMoveIntervalTicks, Action<bool, string, long> completed)
        {
            if (!CanSubmitDebugRequest(out string reason))
            {
                completed?.Invoke(false, reason, entityId);
                return;
            }

            DebugSpawnAsync(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks, completed).Coroutine();
        }

        public void DebugMove(long entityId, Vector2Int targetCoord, Action<bool, string> completed)
        {
            if (!CanSubmitDebugRequest(out string reason))
            {
                completed?.Invoke(false, reason);
                return;
            }

            DebugMoveAsync(entityId, targetCoord, completed).Coroutine();
        }

        public void DebugRemove(long entityId, Action<bool, string> completed)
        {
            if (!CanSubmitDebugRequest(out string reason))
            {
                completed?.Invoke(false, reason);
                return;
            }

            DebugRemoveAsync(entityId, completed).Coroutine();
        }

        public void DebugSetTag(long entityId, WorldTag tag, bool enabled, Action<bool, string> completed)
        {
            if (!serverAuthoritative)
            {
                completed?.Invoke(TrySetLocalTag(entityId, tag, enabled, out string reason), reason);
                return;
            }

            if (!CanSubmitDebugRequest(out string submitReason))
            {
                completed?.Invoke(false, submitReason);
                return;
            }

            DebugSetTagAsync(entityId, tag, enabled, completed).Coroutine();
        }

        public void DebugApplyRuntimeEffect(long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portMask, long expireTick, Action<bool, string, long> completed)
        {
            if (!serverAuthoritative)
            {
                completed?.Invoke(TryApplyLocalRuntimeEffect(entityId, kind, autoMoveIntervalTicks, portMask, expireTick, out long effectId, out string reason), reason, effectId);
                return;
            }

            if (!CanSubmitDebugRequest(out string submitReason))
            {
                completed?.Invoke(false, submitReason, 0);
                return;
            }

            DebugApplyRuntimeEffectAsync(entityId, kind, autoMoveIntervalTicks, portMask, expireTick, completed).Coroutine();
        }

        public void DebugRemoveRuntimeEffect(long entityId, RuntimeEffectKind kind, long runtimeEffectId, Action<bool, string, long> completed)
        {
            if (!serverAuthoritative)
            {
                completed?.Invoke(TryRemoveLocalRuntimeEffect(entityId, kind, new RuntimeEffectId(runtimeEffectId), out long removedEffectId, out string reason), reason, removedEffectId);
                return;
            }

            if (!CanSubmitDebugRequest(out string submitReason))
            {
                completed?.Invoke(false, submitReason, 0);
                return;
            }

            DebugRemoveRuntimeEffectAsync(entityId, kind, runtimeEffectId, completed).Coroutine();
        }

        public bool CanSubmitDebugRequest(out string reason)
        {
            if (!serverAuthoritative)
            {
                reason = "server authoritative disabled";
                return false;
            }

            if (!TryGetSession(out _))
            {
                reason = "session unavailable";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private IEnumerator JoinWorldWhenReady(Action<ClientMapEntity> joined)
        {
            for (int i = 0; i < sessionWaitFrames; i++)
            {
                if (TryGetSession(out _))
                {
                    JoinWorldAsync(joined).Coroutine();
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning("[ClientJoinWorld] session unavailable");
        }

        private IEnumerator RegisterObserverWhenReady(long entityId)
        {
            for (int i = 0; i < sessionWaitFrames; i++)
            {
                if (TryGetSession(out _))
                {
                    RegisterObserverAsync(entityId).Coroutine();
                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning("[ClientMoveObserver] session unavailable");
        }

        private async FTask SubmitAsync(long entityId, Vector2Int targetCoord, int clientTick)
        {
            if (!TryGetSession(out Session session))
            {
                Debug.LogWarning("[ClientMove] session unavailable");
                return;
            }

            G2C_MoveResponse response = await session.C2G_MoveRequest(entityId, targetCoord.x, targetCoord.y, clientTick);
            if (response.ErrorCode != 0)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure($"rpc error:{response.ErrorCode}");
                Debug.LogWarning($"[ClientMove] rpc error:{response.ErrorCode}");
                return;
            }

            if (!response.Success)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure(response.Reason);
                Debug.LogWarning($"[ClientMove] rejected ClientMapEntity:{response.EntityId} error:{response.MoveErrorCode} reason:{response.Reason}");
                return;
            }

            Vector2Int finalCoord = new Vector2Int(response.FinalX, response.FinalY);
            if (!runner.ApplyServerMovementOrCreate(response.EntityId, finalCoord, DefaultWorldConfig.PlayerConfigId, Direction.None, response.EntityId, 1))
            {
                Debug.LogWarning($"[ClientMove] apply failed ClientMapEntity:{response.EntityId} final:{finalCoord}");
                return;
            }

            Debug.Log($"[ClientMove] applied ClientMapEntity:{response.EntityId} final:{finalCoord} tick:{response.ClientTick}");
        }

        private async FTask DebugSpawnAsync(long entityId, int configId, Vector2Int coord, Direction direction, long playerId, int autoMoveIntervalTicks, Action<bool, string, long> completed)
        {
            if (!TryGetSession(out Session session))
            {
                completed?.Invoke(false, "session unavailable", entityId);
                return;
            }

            G2C_DebugSpawnEntityResponse response = await session.C2G_DebugSpawnEntityRequest(entityId, configId, coord.x, coord.y, (int)direction, playerId, autoMoveIntervalTicks);
            if (response.ErrorCode != 0)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure($"rpc error:{response.ErrorCode}");
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}", response.EntityId);
                return;
            }

            if (!response.Success)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure(response.Reason);
            }

            completed?.Invoke(response.Success, response.Reason, response.EntityId);
            Debug.Log($"[ClientDebugSpawn] success:{response.Success} entity:{response.EntityId} reason:{response.Reason}");
        }

        private async FTask DebugMoveAsync(long entityId, Vector2Int targetCoord, Action<bool, string> completed)
        {
            if (!TryGetSession(out Session session))
            {
                completed?.Invoke(false, "session unavailable");
                return;
            }

            G2C_DebugMoveEntityResponse response = await session.C2G_DebugMoveEntityRequest(entityId, targetCoord.x, targetCoord.y);
            if (response.ErrorCode != 0)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure($"rpc error:{response.ErrorCode}");
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}");
                return;
            }

            if (!response.Success)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure(response.Reason);
            }

            completed?.Invoke(response.Success, response.Reason);
            Debug.Log($"[ClientDebugMove] success:{response.Success} entity:{response.EntityId} final:({response.FinalX},{response.FinalY}) reason:{response.Reason}");
        }

        private async FTask DebugRemoveAsync(long entityId, Action<bool, string> completed)
        {
            if (!TryGetSession(out Session session))
            {
                completed?.Invoke(false, "session unavailable");
                return;
            }

            G2C_DebugRemoveEntityResponse response = await session.C2G_DebugRemoveEntityRequest(entityId);
            if (response.ErrorCode != 0)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure($"rpc error:{response.ErrorCode}");
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}");
                return;
            }

            if (!response.Success)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure(response.Reason);
            }

            completed?.Invoke(response.Success, response.Reason);
            Debug.Log($"[ClientDebugRemove] success:{response.Success} entity:{response.EntityId} reason:{response.Reason}");
        }

        private async FTask DebugSetTagAsync(long entityId, WorldTag tag, bool enabled, Action<bool, string> completed)
        {
            if (!TryGetSession(out Session session))
            {
                completed?.Invoke(false, "session unavailable");
                return;
            }

            G2C_DebugSetEntityTagResponse response = await session.C2G_DebugSetEntityTagRequest(entityId, (int)tag, enabled);
            if (response.ErrorCode != 0)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure($"rpc error:{response.ErrorCode}");
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}");
                return;
            }

            if (!response.Success)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure(response.Reason);
            }

            completed?.Invoke(response.Success, response.Reason);
            Debug.Log($"[ClientDebugTag] success:{response.Success} entity:{response.EntityId} tag:{tag} enabled:{response.Enabled} reason:{response.Reason}");
        }

        private async FTask DebugApplyRuntimeEffectAsync(long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portMask, long expireTick, Action<bool, string, long> completed)
        {
            if (!TryGetSession(out Session session))
            {
                completed?.Invoke(false, "session unavailable", 0);
                return;
            }

            G2C_DebugApplyRuntimeEffectResponse response = await session.C2G_DebugApplyRuntimeEffectRequest(entityId, (int)kind, autoMoveIntervalTicks, (int)portMask, expireTick);
            if (response.ErrorCode != 0)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure($"rpc error:{response.ErrorCode}");
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}", 0);
                return;
            }

            if (!response.Success)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure(response.Reason);
            }

            completed?.Invoke(response.Success, response.Reason, response.RuntimeEffectId);
            Debug.Log($"[ClientDebugEffectAdd] success:{response.Success} entity:{response.EntityId} effect:{kind} effectId:{response.RuntimeEffectId} reason:{response.Reason}");
        }

        private async FTask DebugRemoveRuntimeEffectAsync(long entityId, RuntimeEffectKind kind, long runtimeEffectId, Action<bool, string, long> completed)
        {
            if (!TryGetSession(out Session session))
            {
                completed?.Invoke(false, "session unavailable", 0);
                return;
            }

            G2C_DebugRemoveRuntimeEffectResponse response = await session.C2G_DebugRemoveRuntimeEffectRequest(entityId, (int)kind, runtimeEffectId);
            if (response.ErrorCode != 0)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure($"rpc error:{response.ErrorCode}");
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}", 0);
                return;
            }

            if (!response.Success)
            {
                ClientMoveNetworkRuntime.RecordRuleFailure(response.Reason);
            }

            completed?.Invoke(response.Success, response.Reason, response.RuntimeEffectId);
            Debug.Log($"[ClientDebugEffectRemove] success:{response.Success} entity:{response.EntityId} effect:{kind} effectId:{response.RuntimeEffectId} reason:{response.Reason}");
        }

        private async FTask RegisterObserverAsync(long entityId)
        {
            if (!TryGetSession(out Session session))
            {
                Debug.LogWarning("[ClientMoveObserver] session unavailable");
                return;
            }

            G2C_RegisterMoveObserverResponse response = await session.C2G_RegisterMoveObserverRequest(entityId);
            if (response.ErrorCode != 0)
            {
                Debug.LogWarning($"[ClientMoveObserver] rpc error:{response.ErrorCode}");
                return;
            }

            if (!response.Success)
            {
                Debug.LogWarning($"[ClientMoveObserver] rejected ClientMapEntity:{response.EntityId} reason:{response.Reason}");
                return;
            }

            Vector2Int currentCoord = new Vector2Int(response.CurrentX, response.CurrentY);
            if (!runner.ApplyServerMovementOrCreate(response.EntityId, currentCoord, DefaultWorldConfig.PlayerConfigId, Direction.None, response.EntityId, 1))
            {
                Debug.LogWarning($"[ClientMoveObserver] apply failed ClientMapEntity:{response.EntityId} current:{currentCoord}");
                return;
            }

            Debug.Log($"[ClientMoveObserver] registered ClientMapEntity:{response.EntityId} current:{currentCoord}");
        }

        private async FTask JoinWorldAsync(Action<ClientMapEntity> joined)
        {
            if (!TryGetSession(out Session session))
            {
                Debug.LogWarning("[ClientJoinWorld] session unavailable");
                return;
            }

            G2C_JoinWorldResponse response = await session.C2G_JoinWorldRequest();
            if (response.ErrorCode != 0)
            {
                Debug.LogWarning($"[ClientJoinWorld] rpc error:{response.ErrorCode}");
                return;
            }

            if (!response.Success)
            {
                Debug.LogWarning($"[ClientJoinWorld] rejected reason:{response.Reason}");
                return;
            }

            if (!ClientMoveNetworkRuntime.ApplyJoinedPlayer(response.EntityId, response.CurrentX, response.CurrentY))
            {
                Debug.LogWarning($"[ClientJoinWorld] apply failed ClientMapEntity:{response.EntityId} current:({response.CurrentX},{response.CurrentY})");
                return;
            }

            HasJoined = true;
            LocalEntityId = response.EntityId;
            if (runner.Context.ClientMapWorld.TryGetEntity(LocalEntityId, out ClientMapEntity entity))
            {
                joined?.Invoke(entity);
            }

            Debug.Log($"[ClientJoinWorld] joined ClientMapEntity:{response.EntityId} current:({response.CurrentX},{response.CurrentY})");
        }

        private bool TryGetSession(out Session session)
        {
            if (fantasyRuntime == null)
            {
                fantasyRuntime = FindObjectOfType<FantasyRuntime>();
            }

            if (fantasyRuntime != null)
            {
                try
                {
                    session = fantasyRuntime.Session;
                    if (session != null && !session.IsDisposed)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            try
            {
                session = Runtime.Session;
                return session != null && !session.IsDisposed;
            }
            catch
            {
                session = null;
                return false;
            }
        }

        private bool TrySetLocalTag(long entityId, WorldTag tag, bool enabled, out string reason)
        {
            reason = string.Empty;
            if (runner == null || runner.Context == null)
            {
                reason = "runner unavailable";
                return false;
            }

            GameWorld world = runner.Context.ClientMapWorld.CoreWorld;
            if (!world.TryGetEntity(entityId, out GameEntity entity))
            {
                reason = "entity not found";
                return false;
            }

            if (enabled)
            {
                world.AddTag(entity, tag);
            }
            else
            {
                world.RemoveTag(entity, tag);
            }

            world.MarkDirty(entityId);
            return true;
        }

        private bool TryApplyLocalRuntimeEffect(long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portMask, long expireTick, out long effectId, out string reason)
        {
            effectId = 0;
            if (!TryGetLocalWorldEntity(entityId, out GameWorld world, out _, out reason))
            {
                return false;
            }

            if (!TryCreateRuntimeEffectSpec(world, entityId, kind, autoMoveIntervalTicks, portMask, expireTick, out RuntimeEffectSpec spec, out reason))
            {
                return false;
            }

            RuntimeEffectInstance instance = world.AddRuntimeEffect(spec);
            effectId = instance.Id.Value;
            reason = string.Empty;
            return true;
        }

        private bool TryRemoveLocalRuntimeEffect(long entityId, RuntimeEffectKind kind, RuntimeEffectId requestedEffectId, out long removedEffectId, out string reason)
        {
            removedEffectId = 0;
            if (!TryGetLocalWorldEntity(entityId, out GameWorld world, out _, out reason))
            {
                return false;
            }

            RuntimeEffectId effectId = requestedEffectId.IsValid ? FindLocalRuntimeEffect(world, entityId, kind, requestedEffectId) : FindLocalRuntimeEffect(world, entityId, kind);
            if (!effectId.IsValid)
            {
                reason = "runtime effect not found";
                return false;
            }

            if (!world.RemoveRuntimeEffect(effectId))
            {
                reason = "runtime effect not found";
                return false;
            }

            removedEffectId = effectId.Value;
            reason = string.Empty;
            return true;
        }

        private bool TryGetLocalWorldEntity(long entityId, out GameWorld world, out GameEntity entity, out string reason)
        {
            world = null;
            entity = default;
            if (runner == null || runner.Context == null)
            {
                reason = "runner unavailable";
                return false;
            }

            world = runner.Context.ClientMapWorld.CoreWorld;
            if (!world.TryGetEntity(entityId, out entity))
            {
                reason = "entity not found";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool TryCreateRuntimeEffectSpec(GameWorld world, long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portMask, long expireTick, out RuntimeEffectSpec spec, out string reason)
        {
            if (kind == RuntimeEffectKind.TemporaryBlocking)
            {
                spec = RuntimeEffectSpec.Blocking(entityId, world.ServerTick, expireTick);
                reason = string.Empty;
                return true;
            }

            if (kind == RuntimeEffectKind.TemporaryAutoMove)
            {
                spec = RuntimeEffectSpec.AutoMove(entityId, autoMoveIntervalTicks <= 0 ? 1 : autoMoveIntervalTicks, world.ServerTick, expireTick);
                reason = string.Empty;
                return true;
            }

            if (kind == RuntimeEffectKind.TemporaryPushable)
            {
                spec = RuntimeEffectSpec.Pushable(entityId, world.ServerTick, expireTick);
                reason = string.Empty;
                return true;
            }

            if (kind == RuntimeEffectKind.TemporaryPort)
            {
                if (portMask == DirectionMask.None)
                {
                    spec = default;
                    reason = "invalid port mask";
                    return false;
                }

                spec = RuntimeEffectSpec.Port(entityId, portMask, world.ServerTick, expireTick);
                reason = string.Empty;
                return true;
            }

            if (kind == RuntimeEffectKind.TemporaryImmobile)
            {
                spec = RuntimeEffectSpec.Immobile(entityId, world.ServerTick, expireTick);
                reason = string.Empty;
                return true;
            }

            spec = default;
            reason = "invalid runtime effect kind";
            return false;
        }

        private static RuntimeEffectId FindLocalRuntimeEffect(GameWorld world, long entityId, RuntimeEffectKind kind)
        {
            IReadOnlyList<RuntimeEffectInstance> active = world.RuntimeEffects.ActiveAt(world.ServerTick);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                RuntimeEffectInstance effect = active[i];
                if (effect.TargetEntityId == entityId && effect.Kind == kind)
                {
                    return effect.Id;
                }
            }

            return default;
        }

        private static RuntimeEffectId FindLocalRuntimeEffect(GameWorld world, long entityId, RuntimeEffectKind kind, RuntimeEffectId requestedEffectId)
        {
            IReadOnlyList<RuntimeEffectInstance> active = world.RuntimeEffects.ActiveAt(world.ServerTick);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                RuntimeEffectInstance effect = active[i];
                if (effect.Id.Equals(requestedEffectId) && effect.TargetEntityId == entityId && effect.Kind == kind)
                {
                    return effect.Id;
                }
            }

            return default;
        }

    }
}



