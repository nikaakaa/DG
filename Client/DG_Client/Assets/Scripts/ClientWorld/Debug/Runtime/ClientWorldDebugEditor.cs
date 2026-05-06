using DG.GameCore;
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

        private DebugWorldEditorSlot currentSlot;
        private Direction buildDirection = Direction.Right;
        private Vector2Int hoveredCoord;
        private bool hasHover;
        private long selectedEntityId;
        private bool dragging;
        private string lastResult = "Debug editor ready";
        private Transform hoverView;
        private Transform ghostView;

        public DebugWorldEditorSlot CurrentSlot => currentSlot;
        public Direction BuildDirection => buildDirection;
        public string LastResult => lastResult;

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
