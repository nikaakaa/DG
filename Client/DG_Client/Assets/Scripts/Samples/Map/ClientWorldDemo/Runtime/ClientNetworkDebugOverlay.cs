using Fantasy;
using System;
using DG.GameCore;
using System.Text;
using UnityEngine;

namespace DG.Map
{
    public sealed class ClientNetworkDebugOverlay : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private ClientMoveNetworkSubmitter networkSubmitter;
        [SerializeField] private FantasyRuntime fantasyRuntime;
        [SerializeField] private bool visible = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        private readonly StringBuilder Builder = new();
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private string lastConnectionEvent = "None";

        private void Awake()
        {
            BindReferences();
            RegisterRuntimeEvents();
        }

        private void OnDestroy()
        {
            if (fantasyRuntime == null)
            {
                return;
            }

            fantasyRuntime.onConnectComplete.RemoveListener(OnConnectComplete);
            fantasyRuntime.onConnectFail.RemoveListener(OnConnectFail);
            fantasyRuntime.onConnectDisconnect.RemoveListener(OnConnectDisconnect);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(new Rect(12f, 12f, 360f, 300f), boxStyle);
            GUILayout.Label(BuildDebugText(), labelStyle);
            GUILayout.EndArea();
        }

        public string BuildDebugText()
        {
            BindReferences();
            Builder.Clear();
            Builder.AppendLine("Client Network Debug");
            Builder.AppendLine($"Session: {FormatSessionState()}");
            Builder.AppendLine($"Ping: {FormatPing()}");
            Builder.AppendLine($"Join: {FormatJoinState()}");
            Builder.AppendLine($"Local ClientMapEntity: {FormatLocalEntity()}");
            Builder.AppendLine($"Conveyor: {FormatConveyor()}");
            Builder.AppendLine($"ClientMapWorld Entities: {FormatEntityCount()}");
            Builder.AppendLine($"Tick: {FormatTick()}");
            Builder.AppendLine($"Dirty Cells: {FormatDirtyCells()}");
            Builder.AppendLine($"Heartbeat: {FormatHeartbeat()}");
            Builder.AppendLine($"Target: {FormatTarget()}");
            Builder.AppendLine($"Focus: {Application.isFocused}  Background: {Application.runInBackground}");
            Builder.AppendLine($"Last Event: {lastConnectionEvent}");
            return Builder.ToString();
        }

        private void BindReferences()
        {
            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            if (fantasyRuntime == null)
            {
                fantasyRuntime = FindObjectOfType<FantasyRuntime>();
            }
        }

        private void RegisterRuntimeEvents()
        {
            if (fantasyRuntime == null)
            {
                return;
            }

            fantasyRuntime.onConnectComplete.RemoveListener(OnConnectComplete);
            fantasyRuntime.onConnectFail.RemoveListener(OnConnectFail);
            fantasyRuntime.onConnectDisconnect.RemoveListener(OnConnectDisconnect);
            fantasyRuntime.onConnectComplete.AddListener(OnConnectComplete);
            fantasyRuntime.onConnectFail.AddListener(OnConnectFail);
            fantasyRuntime.onConnectDisconnect.AddListener(OnConnectDisconnect);
        }

        private void EnsureStyles()
        {
            if (boxStyle != null)
            {
                return;
            }

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8)
            };
            boxStyle.normal.textColor = Color.white;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };
            labelStyle.normal.textColor = Color.white;
        }

        private string FormatSessionState()
        {
            try
            {
                return Runtime.Session != null && !Runtime.Session.IsDisposed ? "Available" : "Unavailable";
            }
            catch
            {
                return "Unavailable";
            }
        }

        private string FormatPing()
        {
            if (fantasyRuntime == null)
            {
                return "--";
            }

            try
            {
                return $"{fantasyRuntime.PingMilliseconds} ms";
            }
            catch (InvalidOperationException)
            {
                return "--";
            }
        }

        private string FormatJoinState()
        {
            if (networkSubmitter == null)
            {
                return "No Submitter";
            }

            return networkSubmitter.HasJoined ? "Joined" : "Waiting";
        }

        private string FormatLocalEntity()
        {
            long entityId = ClientMoveNetworkRuntime.HasLocalEntity ? ClientMoveNetworkRuntime.LocalEntityId : networkSubmitter != null ? networkSubmitter.LocalEntityId : 0;
            if (entityId == 0)
            {
                return "--";
            }

            if (runner != null && runner.Context != null && runner.Context.ClientMapWorld.TryGetPosition(entityId, out Vector2Int coord))
            {
                return $"{entityId} ({coord.x},{coord.y})";
            }

            return $"{entityId} (missing)";
        }

        private string FormatEntityCount()
        {
            if (runner == null || runner.Context == null)
            {
                return "--";
            }

            return runner.Context.ClientMapWorld.EntityCount.ToString();
        }

        private string FormatConveyor()
        {
            if (runner == null || runner.Context == null)
            {
                return "--";
            }

            if (runner.Context.ClientMapWorld.TryGetPosition(DefaultWorldConfig.ConveyorEntityId, out Vector2Int coord))
            {
                return $"{DefaultWorldConfig.ConveyorEntityId} ({coord.x},{coord.y})";
            }

            return "missing";
        }

        private string FormatTick()
        {
            if (runner == null || runner.Context == null)
            {
                return "--";
            }

            return runner.Context.TickIndex.ToString();
        }

        private string FormatDirtyCells()
        {
            if (runner == null || runner.Context == null)
            {
                return "--";
            }

            return runner.Context.ClientMapWorld.ChangedCells.Count.ToString();
        }

        private string FormatHeartbeat()
        {
            if (fantasyRuntime == null)
            {
                return "--";
            }

            return $"{fantasyRuntime.heartbeatInterval}ms / {fantasyRuntime.heartbeatTimeOut}ms / {fantasyRuntime.heartbeatTimeOutInterval}ms";
        }

        private string FormatTarget()
        {
            if (fantasyRuntime == null)
            {
                return "--";
            }

            return $"{fantasyRuntime.remoteIP}:{fantasyRuntime.remotePort}";
        }

        private void OnConnectComplete()
        {
            lastConnectionEvent = $"Connected {Time.realtimeSinceStartup:0.0}s";
        }

        private void OnConnectFail()
        {
            lastConnectionEvent = $"Connect Failed {Time.realtimeSinceStartup:0.0}s";
        }

        private void OnConnectDisconnect()
        {
            lastConnectionEvent = $"Disconnected {Time.realtimeSinceStartup:0.0}s";
        }
    }
}



