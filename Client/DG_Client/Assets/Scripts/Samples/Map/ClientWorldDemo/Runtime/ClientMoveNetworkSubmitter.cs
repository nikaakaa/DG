using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using System;
using System.Collections;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public sealed class ClientMoveNetworkSubmitter : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private bool serverAuthoritative = true;
        [SerializeField] private int sessionWaitFrames = 120;

        public bool ServerAuthoritative => serverAuthoritative;
        public bool HasJoined { get; private set; }
        public long LocalEntityId { get; private set; }

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            ClientMoveNetworkRuntime.SetRunner(runner);
            EnsureDebugEditor();
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
                runner.SubmitMovement(entityId, targetCoord);
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
                Debug.LogWarning($"[ClientMove] rpc error:{response.ErrorCode}");
                return;
            }

            if (!response.Success)
            {
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
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}", response.EntityId);
                return;
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
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}");
                return;
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
                completed?.Invoke(false, $"rpc error:{response.ErrorCode}");
                return;
            }

            completed?.Invoke(response.Success, response.Reason);
            Debug.Log($"[ClientDebugRemove] success:{response.Success} entity:{response.EntityId} reason:{response.Reason}");
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

        private static bool TryGetSession(out Session session)
        {
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

        private void EnsureDebugEditor()
        {
            if (FindObjectOfType<ClientWorldDebugEditor>() != null)
            {
                return;
            }

            gameObject.AddComponent<ClientWorldDebugEditor>();
        }
    }
}



