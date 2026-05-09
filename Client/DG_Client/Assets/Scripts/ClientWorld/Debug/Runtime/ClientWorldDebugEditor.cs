using DG.GameCore;
using System.Collections.Generic;
using UnityEngine;

namespace DG.Map
{
    public enum DebugWorldEditorSlot
    {
        Blocker = 0,
        Ball = 1,
        Conveyor = 2,
        PortConnector = 3,
        Drag = 4,
        Delete = 5
    }

    public sealed class ClientWorldDebugEditor : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private ClientMoveNetworkSubmitter networkSubmitter;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private int runtimeAutoMoveIntervalTicks = 1;
        [SerializeField] private int runtimeExpireAfterTicks;

        private DebugWorldEditorSlot currentSlot;
        private Direction buildDirection = Direction.Right;
        private Vector2Int hoveredCoord;
        private bool hasHover;
        private long selectedEntityId;
        private bool dragging;
        private string lastResult = "Debug editor ready";
        private Transform hoverView;
        private Transform ghostView;
        private readonly Dictionary<string, long> runtimeEffectIds = new();

        public DebugWorldEditorSlot CurrentSlot => currentSlot;
        public Direction BuildDirection => buildDirection;
        public string LastResult => lastResult;
        public long SelectedEntityId => selectedEntityId;
        public int RuntimeAutoMoveIntervalTicks => runtimeAutoMoveIntervalTicks;
        public int RuntimeExpireAfterTicks => runtimeExpireAfterTicks;

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            hoverView = CreateSquareView("DebugHover", new Color(1f, 1f, 1f, 0.25f), 40, 1f);
            ghostView = CreateSquareView("DebugGhost", new Color(1f, 1f, 1f, 0.35f), 41, 0.7f);
        }

        private void Update()
        {
            UpdateHover();
            UpdateKeyboard();
            UpdateViews();

            if (!Input.GetMouseButtonDown(0) || !hasHover)
            {
                return;
            }

            ExecuteCurrentTool();
        }

        private void OnGUI()
        {
            const int slotWidth = 108;
            const int slotHeight = 42;
            int totalWidth = slotWidth * 6;
            int startX = (Screen.width - totalWidth) / 2;
            int y = Screen.height - slotHeight - 18;

            DrawSlot(startX, y, slotWidth, slotHeight, DebugWorldEditorSlot.Blocker, "1 Block");
            DrawSlot(startX + slotWidth, y, slotWidth, slotHeight, DebugWorldEditorSlot.Ball, "2 Ball");
            DrawSlot(startX + slotWidth * 2, y, slotWidth, slotHeight, DebugWorldEditorSlot.Conveyor, "3 Belt");
            DrawSlot(startX + slotWidth * 3, y, slotWidth, slotHeight, DebugWorldEditorSlot.PortConnector, "4 Port");
            DrawSlot(startX + slotWidth * 4, y, slotWidth, slotHeight, DebugWorldEditorSlot.Drag, "5 Drag");
            DrawSlot(startX + slotWidth * 5, y, slotWidth, slotHeight, DebugWorldEditorSlot.Delete, "6 Delete");

            GUI.Label(new Rect(18, Screen.height - 74, 560, 24), $"Coord: {(hasHover ? hoveredCoord.ToString() : "-")}  Direction: {buildDirection}  Selected: {selectedEntityId}");
            GUI.Label(new Rect(18, Screen.height - 48, 820, 24), lastResult);
            DrawRuntimeEffectPanel();
        }

        public void SelectSlot(DebugWorldEditorSlot slot)
        {
            currentSlot = slot;
            if (slot != DebugWorldEditorSlot.Drag)
            {
                dragging = false;
                selectedEntityId = 0;
            }
        }

        public void RotateDirection()
        {
            buildDirection = buildDirection switch
            {
                Direction.Right => Direction.Down,
                Direction.Down => Direction.Left,
                Direction.Left => Direction.Up,
                _ => Direction.Right
            };
        }

        public void SelectEntity(long entityId)
        {
            selectedEntityId = entityId;
            dragging = false;
        }

        public void SetRuntimeAutoMoveIntervalTicks(int value)
        {
            runtimeAutoMoveIntervalTicks = Mathf.Max(1, value);
        }

        public void SetRuntimeExpireAfterTicks(int value)
        {
            runtimeExpireAfterTicks = Mathf.Max(0, value);
        }

        public bool TryPickCell(Vector3 screenPosition, out Vector2Int coord)
        {
            coord = default;
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z));
            coord = new Vector2Int(Mathf.FloorToInt(world.x / cellSize), Mathf.FloorToInt(world.y / cellSize));
            return true;
        }

        private void UpdateHover()
        {
            hasHover = TryPickCell(Input.mousePosition, out hoveredCoord);
        }

        private void UpdateKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SelectSlot(DebugWorldEditorSlot.Blocker);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SelectSlot(DebugWorldEditorSlot.Ball);
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SelectSlot(DebugWorldEditorSlot.Conveyor);
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SelectSlot(DebugWorldEditorSlot.PortConnector);
            }
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                SelectSlot(DebugWorldEditorSlot.Drag);
            }
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                SelectSlot(DebugWorldEditorSlot.Delete);
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                RotateDirection();
            }
        }

        private void UpdateViews()
        {
            if (hoverView != null)
            {
                hoverView.gameObject.SetActive(hasHover);
                hoverView.position = ToWorldPosition(hoveredCoord, -0.35f);
            }

            if (ghostView != null)
            {
                bool showGhost = hasHover && IsBuildSlot(currentSlot);
                ghostView.gameObject.SetActive(showGhost);
                ghostView.position = ToWorldPosition(hoveredCoord, -0.45f);
                SpriteRenderer renderer = ghostView.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = GetSlotColor(currentSlot, 0.35f);
                }
            }
        }

        private void ExecuteCurrentTool()
        {
            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                return;
            }

            if (IsBuildSlot(currentSlot))
            {
                int configId = GetConfigId(currentSlot);
                Direction direction = currentSlot == DebugWorldEditorSlot.Blocker ? Direction.None : buildDirection;
                networkSubmitter.DebugSpawn(0, configId, hoveredCoord, direction, 0, 1, (success, reason, entityId) =>
                {
                    lastResult = success ? $"spawned {entityId}" : $"spawn failed: {reason}";
                });
                return;
            }

            if (currentSlot == DebugWorldEditorSlot.Drag && dragging)
            {
                long movingEntityId = selectedEntityId;
                Vector2Int targetCoord = hoveredCoord;
                networkSubmitter.DebugMove(movingEntityId, targetCoord, (success, reason) =>
                {
                    lastResult = success ? $"moved {movingEntityId} to {targetCoord}" : $"move failed: {reason}";
                });
                dragging = false;
                return;
            }

            if (!TryPickEntity(hoveredCoord, out long entityId))
            {
                lastResult = "no entity at cell";
                return;
            }

            selectedEntityId = entityId;
            if (currentSlot == DebugWorldEditorSlot.Delete)
            {
                networkSubmitter.DebugRemove(entityId, (success, reason) =>
                {
                    lastResult = success ? $"removed {entityId}" : $"remove failed: {reason}";
                });
                return;
            }

            if (currentSlot == DebugWorldEditorSlot.Drag && !dragging)
            {
                dragging = true;
                selectedEntityId = entityId;
                lastResult = $"selected {entityId}";
            }
        }

        private void DrawRuntimeEffectPanel()
        {
            const int width = 300;
            Rect panel = new Rect(Screen.width - width - 16, 16, width, 500);
            GUI.Box(panel, "Runtime Effects");
            GUILayout.BeginArea(new Rect(panel.x + 12, panel.y + 28, panel.width - 24, panel.height - 40));
            GUILayout.Label("Selected: " + (selectedEntityId == 0 ? "-" : selectedEntityId.ToString()));
            GUILayout.Label("Final: " + DescribeSelectedEntity());

            GUILayout.BeginHorizontal();
            GUILayout.Label("Auto interval", GUILayout.Width(95));
            if (GUILayout.Button("-", GUILayout.Width(28)))
            {
                SetRuntimeAutoMoveIntervalTicks(runtimeAutoMoveIntervalTicks - 1);
            }
            GUILayout.Label(runtimeAutoMoveIntervalTicks.ToString(), GUILayout.Width(32));
            if (GUILayout.Button("+", GUILayout.Width(28)))
            {
                SetRuntimeAutoMoveIntervalTicks(runtimeAutoMoveIntervalTicks + 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Expire +ticks", GUILayout.Width(95));
            if (GUILayout.Button("-", GUILayout.Width(28)))
            {
                SetRuntimeExpireAfterTicks(runtimeExpireAfterTicks - 1);
            }
            GUILayout.Label(runtimeExpireAfterTicks == 0 ? "none" : runtimeExpireAfterTicks.ToString(), GUILayout.Width(44));
            if (GUILayout.Button("+", GUILayout.Width(28)))
            {
                SetRuntimeExpireAfterTicks(runtimeExpireAfterTicks + 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Port direction: " + buildDirection);
            DrawRuntimeEffectButtons("Blocking", RuntimeEffectKind.TemporaryBlocking);
            DrawRuntimeEffectButtons("AutoMove", RuntimeEffectKind.TemporaryAutoMove);
            DrawRuntimeEffectButtons("Pushable", RuntimeEffectKind.TemporaryPushable);
            DrawRuntimeEffectButtons("Port", RuntimeEffectKind.TemporaryPort);
            DrawRuntimeEffectButtons("Immobile", RuntimeEffectKind.TemporaryImmobile);
            GUILayout.Space(8);
            GUILayout.Label("Last: " + lastResult);
            GUILayout.EndArea();
        }

        private void DrawRuntimeEffectButtons(string label, RuntimeEffectKind kind)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(78));
            if (GUILayout.Button("Add", GUILayout.Width(72)))
            {
                ApplyRuntimeEffect(kind);
            }
            if (GUILayout.Button("Remove", GUILayout.Width(86)))
            {
                RemoveRuntimeEffect(kind);
            }
            GUILayout.EndHorizontal();
        }

        private void ApplyRuntimeEffect(RuntimeEffectKind kind)
        {
            if (!CanEditRuntimeEffect(out string reason))
            {
                lastResult = reason;
                return;
            }

            long entityId = selectedEntityId;
            DirectionMask portMask = kind == RuntimeEffectKind.TemporaryPort ? DirectionToMask(buildDirection) : DirectionMask.None;
            long expireTick = GetRuntimeExpireTick(entityId);
            networkSubmitter.DebugApplyRuntimeEffect(entityId, kind, runtimeAutoMoveIntervalTicks, portMask, expireTick, (success, callbackReason, effectId) =>
            {
                if (success)
                {
                    runtimeEffectIds[RuntimeEffectKey(entityId, kind)] = effectId;
                    lastResult = "effect add " + kind + " id:" + effectId;
                    return;
                }

                lastResult = "effect add failed: " + callbackReason;
            });
        }

        private void RemoveRuntimeEffect(RuntimeEffectKind kind)
        {
            if (!CanEditRuntimeEffect(out string reason))
            {
                lastResult = reason;
                return;
            }

            long entityId = selectedEntityId;
            runtimeEffectIds.TryGetValue(RuntimeEffectKey(entityId, kind), out long effectId);
            networkSubmitter.DebugRemoveRuntimeEffect(entityId, kind, effectId, (success, callbackReason, removedEffectId) =>
            {
                if (success)
                {
                    runtimeEffectIds.Remove(RuntimeEffectKey(entityId, kind));
                    lastResult = "effect remove " + kind + " id:" + removedEffectId;
                    return;
                }

                lastResult = "effect remove failed: " + callbackReason;
            });
        }

        private bool CanEditRuntimeEffect(out string reason)
        {
            if (selectedEntityId == 0)
            {
                reason = "select entity first";
                return false;
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            if (networkSubmitter == null)
            {
                reason = "debug submitter missing";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private long GetRuntimeExpireTick(long entityId)
        {
            if (runtimeExpireAfterTicks <= 0 || runner == null || runner.Context == null)
            {
                return 0;
            }

            if (runner.Context.ClientMapWorld.TryGetServerTick(entityId, out long serverTick))
            {
                return serverTick + runtimeExpireAfterTicks;
            }

            return runtimeExpireAfterTicks;
        }

        private string DescribeSelectedEntity()
        {
            if (selectedEntityId == 0 || runner == null || runner.Context == null)
            {
                return "-";
            }

            if (!runner.Context.ClientMapWorld.TryGetSnapshot(selectedEntityId, out EntitySnapshot snapshot))
            {
                return "missing";
            }

            string port = snapshot.PortLocalPorts == DirectionMask.None ? "-" : snapshot.PortLocalPorts.ToString();
            string move = snapshot.HasMovementPermission ? " move:" + snapshot.CanMove + "/" + snapshot.CanBePushed : string.Empty;
            return "B:" + snapshot.Blocking +
                " A:" + snapshot.AutoMove +
                " P:" + snapshot.Pushable +
                " Port:" + port +
                move;
        }

        private static DirectionMask DirectionToMask(Direction direction)
        {
            return direction switch
            {
                Direction.Up => DirectionMask.Up,
                Direction.Down => DirectionMask.Down,
                Direction.Left => DirectionMask.Left,
                Direction.Right => DirectionMask.Right,
                _ => DirectionMask.None
            };
        }

        private static string RuntimeEffectKey(long entityId, RuntimeEffectKind kind)
        {
            return entityId + ":" + (int)kind;
        }

        private bool TryPickEntity(Vector2Int coord, out long entityId)
        {
            entityId = 0;
            if (runner == null || runner.Context == null)
            {
                return false;
            }

            foreach (EntitySnapshot snapshot in runner.Context.ClientMapWorld.CreateSnapshot())
            {
                if (snapshot.X == coord.x && snapshot.Y == coord.y)
                {
                    entityId = snapshot.EntityId;
                    return true;
                }
            }

            return false;
        }

        private void DrawSlot(int x, int y, int width, int height, DebugWorldEditorSlot slot, string label)
        {
            Color oldColor = GUI.color;
            GUI.color = currentSlot == slot ? new Color(0.8f, 1f, 0.5f, 1f) : Color.white;
            if (GUI.Button(new Rect(x + 4, y, width - 8, height), label))
            {
                SelectSlot(slot);
            }

            GUI.color = oldColor;
        }

        private static bool IsBuildSlot(DebugWorldEditorSlot slot)
        {
            return slot == DebugWorldEditorSlot.Blocker ||
                slot == DebugWorldEditorSlot.Ball ||
                slot == DebugWorldEditorSlot.Conveyor ||
                slot == DebugWorldEditorSlot.PortConnector;
        }

        private static int GetConfigId(DebugWorldEditorSlot slot)
        {
            return slot switch
            {
                DebugWorldEditorSlot.Ball => DefaultWorldConfig.BallConfigId,
                DebugWorldEditorSlot.Conveyor => DefaultWorldConfig.ConveyorConfigId,
                DebugWorldEditorSlot.PortConnector => DefaultWorldConfig.PortConnectorBlockerConfigId,
                _ => DefaultWorldConfig.BlockerConfigId
            };
        }

        private static Color GetSlotColor(DebugWorldEditorSlot slot, float alpha)
        {
            Color color = slot switch
            {
                DebugWorldEditorSlot.Ball => new Color(1f, 0.88f, 0.18f, alpha),
                DebugWorldEditorSlot.Conveyor => new Color(0.35f, 1f, 0.45f, alpha),
                DebugWorldEditorSlot.PortConnector => new Color(0.5f, 0.65f, 1f, alpha),
                DebugWorldEditorSlot.Delete => new Color(1f, 0.2f, 0.2f, alpha),
                DebugWorldEditorSlot.Drag => new Color(0.6f, 0.8f, 1f, alpha),
                _ => new Color(1f, 0.35f, 0.2f, alpha)
            };
            return color;
        }

        private Vector3 ToWorldPosition(Vector2Int coord, float z)
        {
            return new Vector3((coord.x + 0.5f) * cellSize, (coord.y + 0.5f) * cellSize, z);
        }

        private static Transform CreateSquareView(string name, Color color, int sortingOrder, float scale)
        {
            GameObject view = new GameObject(name);
            SpriteRenderer renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            view.transform.localScale = Vector3.one * scale;
            return view.transform;
        }

        private static Sprite CreateSquareSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
