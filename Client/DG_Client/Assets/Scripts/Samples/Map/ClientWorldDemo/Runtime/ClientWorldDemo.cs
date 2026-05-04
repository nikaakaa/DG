using System;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public sealed class ClientWorldDemo : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private ClientMoveNetworkSubmitter networkSubmitter;
        [SerializeField] private long localEntityId = 1;
        [SerializeField] private bool autoMove;

        private long playerEntityId;

        private void Start()
        {
            Application.runInBackground = true;

            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            ClientMoveNetworkRuntime.SetRunner(runner);
            if (networkSubmitter != null && networkSubmitter.ServerAuthoritative)
            {
                networkSubmitter.JoinWorld(entity => playerEntityId = entity.EntityId);
                return;
            }

            playerEntityId = ResolveLocalEntityId();
            runner.Context.ClientMapWorld.AddEntity(
                new ClientMapEntity { EntityId = playerEntityId },
                DefaultWorldConfig.PlayerSpawn(playerEntityId, playerEntityId, MapCoordinate.ToGridCoord(Vector2Int.zero)));
            runner.Context.ClientMapWorld.ClearDirty();
        }

        private void Update()
        {
            if (runner == null || playerEntityId == 0 || !runner.Context.ClientMapWorld.TryGetPosition(playerEntityId, out Vector2Int coord))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                Submit(coord + Vector2Int.right);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                Submit(coord + Vector2Int.left);
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                Submit(coord + Vector2Int.up);
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                Submit(coord + Vector2Int.down);
            }

            if (!autoMove)
            {
                return;
            }

            Submit(coord + Vector2Int.right);
        }

        private void Submit(Vector2Int targetCoord)
        {
            if (networkSubmitter != null && networkSubmitter.ServerAuthoritative)
            {
                networkSubmitter.Submit(playerEntityId, targetCoord, runner.Context.TickIndex);
                return;
            }

            runner.SubmitMovement(playerEntityId, targetCoord);
        }

        private long ResolveLocalEntityId()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--entityId=", StringComparison.OrdinalIgnoreCase) && long.TryParse(arg.Substring("--entityId=".Length), out long inlineId))
                {
                    Debug.Log($"[ClientWorldDemo] debug entity override:{inlineId}");
                    return inlineId;
                }

                if (string.Equals(arg, "--entityId", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && long.TryParse(args[i + 1], out long nextId))
                {
                    Debug.Log($"[ClientWorldDemo] debug entity override:{nextId}");
                    return nextId;
                }
            }

            return localEntityId;
        }
    }
}
