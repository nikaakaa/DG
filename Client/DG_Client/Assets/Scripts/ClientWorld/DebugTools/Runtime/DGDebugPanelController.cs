using System;
using System.Collections.Generic;
using System.Linq;
using DG.GameCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DG.Map
{
    public enum DGDebugPanelMode
    {
        EntityManage = 0,
        EntityGenerate = 1
    }

    public enum DGDebugPanelTool
    {
        Blocker = 0,
        Ball = 1,
        Conveyor = 2,
        PortConnector = 3,
        RotatePivot = 4,
        Select = 5,
        Drag = 6,
        Delete = 7
    }

    public sealed class DGDebugPanelController : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private ClientMoveNetworkSubmitter networkSubmitter;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private KeyCode toggleKey = KeyCode.F4;

        private RectTransform rootRect;
        private RectTransform topArea;
        private Button closeButton;
        private Button entityManageButton;
        private Button entityGenerateButton;
        private RectTransform contentRoot;
        private RectTransform generateActionsRoot;
        private RectTransform manageActionsRoot;
        private Button blockerButton;
        private Button ballButton;
        private Button conveyorButton;
        private Button portConnectorButton;
        private Button rotatePivotButton;
        private Button rotateButton;
        private Button dragButton;
        private Button deleteButton;
        private Button addImmuneMechanismPushButton;
        private Button removeImmuneMechanismPushButton;
        private Button addBlockPlayerMoveButton;
        private Button removeBlockPlayerMoveButton;
        private CanvasGroup canvasGroup;
        private DGDebugPanelMode mode = DGDebugPanelMode.EntityGenerate;
        private DGDebugPanelTool currentTool;
        private Direction buildDirection = Direction.Right;
        private Vector2Int hoveredCoord;
        private bool hasHover;
        private long selectedEntityId;
        private bool dragging;
        private string lastResult = "debug panel ready";
        private Transform hoverView;
        private Transform ghostView;
        private bool visible = true;
        private bool draggingPanel;
        private Vector2 dragStartMouse;
        private Vector2 dragStartAnchored;
        private readonly DebugLayoutSelectionSet selection = new();
        private readonly DebugRuntimeLayoutManager layoutManager = new();
        private DebugLayoutToolState toolState;

        public DGDebugPanelMode Mode => mode;
        public DGDebugPanelTool CurrentTool => currentTool;
        public Direction BuildDirection => buildDirection;
        public string LastResult => lastResult;
        public bool Visible => visible;
        public long SelectedEntityId => selectedEntityId;
        public int SelectionCount => selection.Count;
        public DebugLayoutToolState ToolState => toolState;
        public string CurrentLayoutPath => layoutManager.CurrentLayoutPath;
        public string SaveTargetName => layoutManager.SaveTargetName;
        public string LastSavedPath => layoutManager.LastSavedPath;
        public string LastLayoutOperationResult => layoutManager.LastLayoutOperationResult;

        private void Awake()
        {
            rootRect = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            BindSceneServices();
            BindPrefabParts();
            hoverView = CreateSquareView("DebugHover", new Color(1f, 1f, 1f, 0.25f), 40, 1f);
            ghostView = CreateSquareView("DebugGhost", new Color(1f, 1f, 1f, 0.35f), 41, 0.7f);
            RefreshLayoutFiles();
            Rebuild();
            SetVisible(true);
        }

        private void OnDestroy()
        {
            if (hoverView != null)
            {
                Destroy(hoverView.gameObject);
            }

            if (ghostView != null)
            {
                Destroy(ghostView.gameObject);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                SetVisible(!visible);
            }

            UpdatePanelDrag();
            UpdateHover();
            UpdateKeyboard();
            UpdateViews();

            if (!visible || IsPointerOverUi())
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && hasHover)
            {
                ExecuteCurrentTool();
                Rebuild();
            }
        }

        public void SetVisible(bool value)
        {
            visible = value;
            canvasGroup.alpha = value ? 1f : 0f;
            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }

        public void SetMode(DGDebugPanelMode value)
        {
            mode = value;
            Rebuild();
        }

        public void SelectTool(DGDebugPanelTool tool)
        {
            currentTool = tool;
            if (tool != DGDebugPanelTool.Drag)
            {
                dragging = false;
                if (tool != DGDebugPanelTool.Select)
                {
                    selectedEntityId = 0;
                }
            }

            lastResult = "tool: " + tool;
            Rebuild();
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
            lastResult = "direction: " + buildDirection;
            Rebuild();
        }

        public void SelectEntity(long entityId)
        {
            selectedEntityId = entityId;
            dragging = false;
            lastResult = "selected " + entityId;
            Rebuild();
        }

        public bool AddSelectedEntityToSelection()
        {
            if (selectedEntityId == 0 || runner == null || runner.Context == null)
            {
                lastResult = "select entity first";
                Rebuild();
                return false;
            }

            if (!runner.Context.ClientMapWorld.TryGetSnapshot(selectedEntityId, out EntitySnapshot snapshot))
            {
                lastResult = "selected entity missing";
                Rebuild();
                return false;
            }

            selection.Add(snapshot);
            toolState = DebugLayoutToolState.SelectionPreview;
            lastResult = "selection count: " + selection.Count;
            Rebuild();
            return true;
        }

        public void ClearSelection()
        {
            selection.Clear();
            toolState = DebugLayoutToolState.SingleCellTool;
            lastResult = "selection cleared";
            Rebuild();
        }

        public bool SaveSelectionToDefault()
        {
            return SaveSelectionAs(layoutManager.SaveTargetName);
        }

        public bool SaveSelectionAs(string name)
        {
            ClientMapWorld world = runner != null && runner.Context != null ? runner.Context.ClientMapWorld : null;
            bool saved = layoutManager.SaveAs(selection, world, name, ClientGameConfigProviderFactory.Create());
            if (saved)
            {
                toolState = DebugLayoutToolState.StructureGhostPlacement;
            }

            lastResult = layoutManager.LastLayoutOperationResult;
            Rebuild();
            return saved;
        }

        public bool OverwriteCurrentLayout()
        {
            ClientMapWorld world = runner != null && runner.Context != null ? runner.Context.ClientMapWorld : null;
            bool saved = layoutManager.OverwriteCurrent(selection, world, ClientGameConfigProviderFactory.Create());
            lastResult = layoutManager.LastLayoutOperationResult;
            Rebuild();
            return saved;
        }

        public bool RenameCurrentLayout(string name)
        {
            bool renamed = layoutManager.RenameCurrent(name, ClientGameConfigProviderFactory.Create());
            lastResult = layoutManager.LastLayoutOperationResult;
            Rebuild();
            return renamed;
        }

        public bool DeleteCurrentLayout()
        {
            bool deleted = layoutManager.DeleteCurrent(ClientGameConfigProviderFactory.Create());
            if (deleted)
            {
                toolState = DebugLayoutToolState.SingleCellTool;
            }

            lastResult = layoutManager.LastLayoutOperationResult;
            Rebuild();
            return deleted;
        }

        public void RefreshLayoutFiles()
        {
            layoutManager.Refresh(ClientGameConfigProviderFactory.Create());
            if (layoutManager.LoadedStructureBlock == null && toolState == DebugLayoutToolState.StructureGhostPlacement)
            {
                toolState = DebugLayoutToolState.SingleCellTool;
            }

            lastResult = layoutManager.LastLayoutOperationResult;
            Rebuild();
        }

        public bool LoadStructureBlockFromDefault()
        {
            return LoadStructureBlock(DebugLayoutPaths.DefaultFilePath());
        }

        public bool LoadStructureBlock(string path)
        {
            bool loaded = layoutManager.Load(path, ClientGameConfigProviderFactory.Create());
            if (loaded)
            {
                toolState = DebugLayoutToolState.StructureGhostPlacement;
            }

            lastResult = layoutManager.LastLayoutOperationResult;
            Rebuild();
            return loaded;
        }

        public IReadOnlyList<DebugStructureSpawnRequest> PreviewLoadedStructure(Vector2Int anchor)
        {
            if (layoutManager.LoadedStructureBlock == null)
            {
                return Array.Empty<DebugStructureSpawnRequest>();
            }

            return DebugStructureBlockStorage.CreateSpawnRequests(layoutManager.LoadedStructureBlock, anchor.x, anchor.y);
        }

        public void CopySelectionTo(Vector2Int targetAnchor)
        {
            ClientMapWorld world = runner != null && runner.Context != null ? runner.Context.ClientMapWorld : null;
            SubmitSpawnRequests(DebugLayoutTooling.CreateCopyRequests(selection, world, targetAnchor));
        }

        public void PlaceLoadedStructure(Vector2Int targetAnchor)
        {
            SubmitSpawnRequests(PreviewLoadedStructure(targetAnchor));
        }

        public void MoveSelectionTo(Vector2Int targetAnchor)
        {
            if (runner == null || runner.Context == null)
            {
                lastResult = "runner unavailable";
                Rebuild();
                return;
            }

            IReadOnlyList<DebugBatchMoveRequest> requests = selection.CreateMoveRequests(targetAnchor, runner.Context.ClientMapWorld, out IReadOnlyList<string> skipped);
            if (requests.Count == 0)
            {
                lastResult = skipped.Count > 0 ? string.Join("|", skipped) : "selection is empty";
                Rebuild();
                return;
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                Rebuild();
                return;
            }

            toolState = DebugLayoutToolState.SubmittingBatch;
            int successCount = 0;
            int failCount = skipped.Count;
            string lastReason = skipped.Count > 0 ? skipped[skipped.Count - 1] : string.Empty;
            foreach (DebugBatchMoveRequest request in requests)
            {
                networkSubmitter.DebugMove(request.EntityId, request.TargetCoord, (success, reason) =>
                {
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        lastReason = reason;
                    }

                    lastResult = "batch move ok:" + successCount + " fail:" + failCount + (string.IsNullOrEmpty(lastReason) ? string.Empty : " " + lastReason);
                    Rebuild();
                });
            }
        }

        public void DeleteSelectionOrEntity(long fallbackEntityId)
        {
            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                Rebuild();
                return;
            }

            IReadOnlyList<EntitySnapshot> snapshots = selection.Snapshots();
            if (snapshots.Count > 0)
            {
                DeleteEntities(snapshots);
                return;
            }

            if (fallbackEntityId == 0)
            {
                lastResult = "select entity first";
                Rebuild();
                return;
            }

            networkSubmitter.DebugRemove(fallbackEntityId, (success, reason) =>
            {
                if (success)
                {
                    selection.Remove(fallbackEntityId);
                    if (selectedEntityId == fallbackEntityId)
                    {
                        selectedEntityId = 0;
                    }
                }

                lastResult = success ? "removed " + fallbackEntityId : "remove failed: " + reason;
                Rebuild();
            });
        }

        private void DeleteEntities(IReadOnlyList<EntitySnapshot> snapshots)
        {
            int successCount = 0;
            int failCount = 0;
            string lastReason = string.Empty;
            for (int i = 0; i < snapshots.Count; i++)
            {
                long entityId = snapshots[i].EntityId;
                networkSubmitter.DebugRemove(entityId, (success, reason) =>
                {
                    if (success)
                    {
                        successCount++;
                        selection.Remove(entityId);
                        if (selectedEntityId == entityId)
                        {
                            selectedEntityId = 0;
                        }
                    }
                    else
                    {
                        failCount++;
                        lastReason = reason;
                    }

                    lastResult = "batch delete ok:" + successCount + " fail:" + failCount + (string.IsNullOrEmpty(lastReason) ? string.Empty : " " + lastReason);
                    Rebuild();
                });
            }
        }

        private void SubmitSpawnRequests(IReadOnlyList<DebugStructureSpawnRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                lastResult = "structure is empty";
                Rebuild();
                return;
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                Rebuild();
                return;
            }

            toolState = DebugLayoutToolState.SubmittingBatch;
            int successCount = 0;
            int failCount = 0;
            string lastReason = string.Empty;
            foreach (DebugStructureSpawnRequest request in requests)
            {
                networkSubmitter.DebugSpawn(0, request.ConfigId, new Vector2Int(request.X, request.Y), request.Direction, request.PlayerId, request.AutoMoveIntervalTicks, request.RotatePivot, (success, reason, entityId) =>
                {
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        lastReason = reason;
                    }

                    lastResult = "batch spawn ok:" + successCount + " fail:" + failCount + (string.IsNullOrEmpty(lastReason) ? string.Empty : " " + lastReason);
                    Rebuild();
                    if (success)
                    {
                        ApplyStructureRuntimeEffects(entityId, request);
                    }
                });
            }
        }

        private void ApplyStructureRuntimeEffects(long entityId, DebugStructureSpawnRequest request)
        {
            if (entityId == 0 || !request.HasRuntimeEffects || networkSubmitter == null)
            {
                return;
            }

            if (request.RuntimeBlocking)
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryBlocking, 1, DirectionMask.None);
            }

            if (request.RuntimeAutoMove)
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryAutoMove, request.AutoMoveIntervalTicks, DirectionMask.None);
            }

            if (request.RuntimePushable)
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryPushable, 1, DirectionMask.None);
            }

            if (request.RuntimePortLocalPorts != DirectionMask.None)
            {
                ApplyRuntimePortEffectsToEntity(entityId, request.RuntimePortLocalPorts);
            }

            if (request.RuntimeImmobile)
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryImmobile, 1, DirectionMask.None);
            }
        }

        private void ApplyRuntimePortEffectsToEntity(long entityId, DirectionMask ports)
        {
            if (ports.Contains(Direction.Left))
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Left);
            }

            if (ports.Contains(Direction.Right))
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Right);
            }

            if (ports.Contains(Direction.Up))
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Up);
            }

            if (ports.Contains(Direction.Down))
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryPort, 1, DirectionMask.Down);
            }
        }

        private void ApplyRuntimeEffectToEntity(long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portMask)
        {
            networkSubmitter.DebugApplyRuntimeEffect(entityId, kind, autoMoveIntervalTicks, portMask, 0, (success, reason, _) =>
            {
                if (!success)
                {
                    lastResult = "runtime effect failed: " + reason;
                    Rebuild();
                }
            });
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

        private void BindSceneServices()
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
        }

        private void BindPrefabParts()
        {
            topArea = FindChild<RectTransform>("TopArea");
            closeButton = FindChild<Button>("CloseButton");
            entityManageButton = FindChild<Button>("EntityMana_Btn");
            entityGenerateButton = FindChild<Button>("EntityGen_Btn");
            Transform runtimeList = FindDeep(transform, "RuntimeList");
            Transform content = runtimeList != null ? runtimeList : FindDeep(transform, "Content");
            contentRoot = content as RectTransform;
            generateActionsRoot = FindDeep(transform, "GenerateActions") as RectTransform;
            manageActionsRoot = FindDeep(transform, "ManageActions") as RectTransform;
            blockerButton = FindChild<Button>("Blocker_Btn");
            ballButton = FindChild<Button>("Ball_Btn");
            conveyorButton = FindChild<Button>("Conveyor_Btn");
            portConnectorButton = FindChild<Button>("PortConnector_Btn");
            rotatePivotButton = FindChild<Button>("RotatePivot_Btn");
            rotateButton = FindChild<Button>("Rotate_Btn");
            dragButton = FindChild<Button>("Drag_Btn");
            deleteButton = FindChild<Button>("Delete_Btn");
            addImmuneMechanismPushButton = FindChild<Button>("AddImmuneMechanismPush_Btn");
            removeImmuneMechanismPushButton = FindChild<Button>("RemoveImmuneMechanismPush_Btn");
            addBlockPlayerMoveButton = FindChild<Button>("AddBlockPlayerMove_Btn");
            removeBlockPlayerMoveButton = FindChild<Button>("RemoveBlockPlayerMove_Btn");

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            if (entityManageButton != null)
            {
                entityManageButton.onClick.RemoveAllListeners();
                entityManageButton.onClick.AddListener(() => SetMode(DGDebugPanelMode.EntityManage));
            }

            if (entityGenerateButton != null)
            {
                entityGenerateButton.onClick.RemoveAllListeners();
                entityGenerateButton.onClick.AddListener(() => SetMode(DGDebugPanelMode.EntityGenerate));
            }

            BindButton(blockerButton, () => SelectTool(DGDebugPanelTool.Blocker));
            BindButton(ballButton, () => SelectTool(DGDebugPanelTool.Ball));
            BindButton(conveyorButton, () => SelectTool(DGDebugPanelTool.Conveyor));
            BindButton(portConnectorButton, () => SelectTool(DGDebugPanelTool.PortConnector));
            BindButton(rotatePivotButton, () => SelectTool(DGDebugPanelTool.RotatePivot));
            BindButton(rotateButton, RotateDirection);
            BindButton(dragButton, () => SelectTool(DGDebugPanelTool.Drag));
            BindButton(deleteButton, () => SelectTool(DGDebugPanelTool.Delete));
            BindButton(addImmuneMechanismPushButton, () => ApplyTag(WorldTag.ImmuneMechanismPush, true));
            BindButton(removeImmuneMechanismPushButton, () => ApplyTag(WorldTag.ImmuneMechanismPush, false));
            BindButton(addBlockPlayerMoveButton, () => ApplyTag(WorldTag.BlockPlayerMove, true));
            BindButton(removeBlockPlayerMoveButton, () => ApplyTag(WorldTag.BlockPlayerMove, false));
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void Close()
        {
            SetVisible(false);
        }

        private void UpdatePanelDrag()
        {
            if (!visible || topArea == null || rootRect == null)
            {
                draggingPanel = false;
                return;
            }

            if (Input.GetMouseButtonDown(0) &&
                RectTransformUtility.RectangleContainsScreenPoint(topArea, Input.mousePosition, null))
            {
                draggingPanel = true;
                dragStartMouse = Input.mousePosition;
                dragStartAnchored = rootRect.anchoredPosition;
            }

            if (Input.GetMouseButtonUp(0))
            {
                draggingPanel = false;
            }

            if (draggingPanel && Input.GetMouseButton(0))
            {
                Vector2 delta = (Vector2)Input.mousePosition - dragStartMouse;
                rootRect.anchoredPosition = dragStartAnchored + delta;
            }
        }

        private void UpdateHover()
        {
            hasHover = TryPickCell(Input.mousePosition, out hoveredCoord);
        }

        private void UpdateKeyboard()
        {
            if (!visible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SelectTool(DGDebugPanelTool.Blocker);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SelectTool(DGDebugPanelTool.Ball);
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SelectTool(DGDebugPanelTool.Conveyor);
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SelectTool(DGDebugPanelTool.PortConnector);
            }
            if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                SelectTool(DGDebugPanelTool.RotatePivot);
            }
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                SelectTool(DGDebugPanelTool.Select);
            }
            if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                SelectTool(DGDebugPanelTool.Drag);
            }
            if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                SelectTool(DGDebugPanelTool.Delete);
            }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (selection.Count > 0 || selectedEntityId != 0)
                {
                    DeleteSelectionOrEntity(selectedEntityId);
                }
                else
                {
                    SelectTool(DGDebugPanelTool.Delete);
                }
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
                hoverView.gameObject.SetActive(visible && hasHover);
                hoverView.position = ToWorldPosition(hoveredCoord, -0.35f);
            }

            if (ghostView != null)
            {
                bool showGhost = visible && hasHover && IsBuildTool(currentTool);
                ghostView.gameObject.SetActive(showGhost);
                ghostView.position = ToWorldPosition(hoveredCoord, -0.45f);
                SpriteRenderer renderer = ghostView.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = GetToolColor(currentTool, 0.35f);
                }
            }
        }

        private void ExecuteCurrentTool()
        {
            if (currentTool == DGDebugPanelTool.Select)
            {
                if (!TryPickEntity(hoveredCoord, out long pickedEntityId))
                {
                    lastResult = "no entity at cell";
                    return;
                }

                SelectEntity(pickedEntityId);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    AddSelectedEntityToSelection();
                }
                return;
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                return;
            }

            if (IsBuildTool(currentTool))
            {
                int configId = GetConfigId(currentTool);
                Direction direction = currentTool == DGDebugPanelTool.Blocker ? Direction.None : buildDirection;
                int interval = currentTool == DGDebugPanelTool.Ball ? 1 : 1;
                networkSubmitter.DebugSpawn(0, configId, hoveredCoord, direction, 0, interval, currentTool == DGDebugPanelTool.RotatePivot, (success, reason, entityId) =>
                {
                    lastResult = success ? "spawned " + entityId : "spawn failed: " + reason;
                    Rebuild();
                });
                return;
            }

            if (currentTool == DGDebugPanelTool.Drag && dragging)
            {
                long movingEntityId = selectedEntityId;
                Vector2Int targetCoord = hoveredCoord;
                networkSubmitter.DebugMove(movingEntityId, targetCoord, (success, reason) =>
                {
                    lastResult = success ? "moved " + movingEntityId + " to " + targetCoord : "move failed: " + reason;
                    Rebuild();
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
            if (currentTool == DGDebugPanelTool.Delete)
            {
                DeleteSelectionOrEntity(entityId);
                return;
            }

            if (currentTool == DGDebugPanelTool.Drag)
            {
                dragging = true;
                lastResult = "selected " + entityId;
            }
        }

        private bool TryPickEntity(Vector2Int coord, out long entityId)
        {
            entityId = 0;
            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

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

        private void Rebuild()
        {
            if (contentRoot == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }

            if (generateActionsRoot != null)
            {
                generateActionsRoot.gameObject.SetActive(mode == DGDebugPanelMode.EntityGenerate);
            }

            if (manageActionsRoot != null)
            {
                manageActionsRoot.gameObject.SetActive(mode == DGDebugPanelMode.EntityManage);
            }

            AddHeader(mode == DGDebugPanelMode.EntityGenerate ? "Entity Generate" : "Entity Manage");
            AddText("Tool: " + currentTool + "   Direction: " + buildDirection);
            AddText("Hover: " + (hasHover ? hoveredCoord.ToString() : "-") + "   Selected: " + (selectedEntityId == 0 ? "-" : selectedEntityId.ToString()));
            AddText("State: " + toolState + "   Selection: " + selection.Count);
            AddText("Result: " + lastResult);

            if (mode == DGDebugPanelMode.EntityGenerate)
            {
                BuildGeneratePage();
            }
            else
            {
                BuildManagePage();
            }
        }

        private void BuildGeneratePage()
        {
            if (generateActionsRoot == null)
            {
                AddButton("1 Blocker", () => SelectTool(DGDebugPanelTool.Blocker));
                AddButton("2 Ball", () => SelectTool(DGDebugPanelTool.Ball));
                AddButton("3 Conveyor", () => SelectTool(DGDebugPanelTool.Conveyor));
                AddButton("4 Port Connector", () => SelectTool(DGDebugPanelTool.PortConnector));
                AddButton("5 Rotate Pivot", () => SelectTool(DGDebugPanelTool.RotatePivot));
                AddButton("Rotate R: " + buildDirection, RotateDirection);
            }
            AddHeader("Luban EntityArchetypes");

            try
            {
                IGameConfigProvider provider = ClientGameConfigProviderFactory.Create();
                foreach (EntityArchetype archetype in provider.GetEntityArchetypes().OrderBy(item => item.ConfigId))
                {
                    string components = string.Join(",", archetype.Components.Select(item => item.ToString()));
                    string tags = string.Join(",", archetype.Tags);
                    AddText(archetype.ConfigId + " target:" + archetype.EntityTarget + " comps:" + components + " tags:" + tags);
                }
            }
            catch (Exception ex)
            {
                AddText("config load failed: " + ex.Message);
            }
        }

        private void BuildManagePage()
        {
            if (manageActionsRoot == null)
            {
                AddButton("6 Select", () => SelectTool(DGDebugPanelTool.Select));
                AddButton("7 Drag / Move", () => SelectTool(DGDebugPanelTool.Drag));
                AddButton("8 Delete", () => SelectTool(DGDebugPanelTool.Delete));
                AddButton("Add ImmuneMechanismPush", () => ApplyTag(WorldTag.ImmuneMechanismPush, true));
                AddButton("Remove ImmuneMechanismPush", () => ApplyTag(WorldTag.ImmuneMechanismPush, false));
                AddButton("Add BlockPlayerMove", () => ApplyTag(WorldTag.BlockPlayerMove, true));
                AddButton("Remove BlockPlayerMove", () => ApplyTag(WorldTag.BlockPlayerMove, false));
            }
            else
            {
                AddButton("6 Select", () => SelectTool(DGDebugPanelTool.Select));
            }
            AddHeader("Structure Block");
            AddButton("Add Selected To Structure", () => AddSelectedEntityToSelection());
            AddButton("Clear Structure Selection", ClearSelection);
            if (hasHover)
            {
                AddButton("Copy Selection Here", () => CopySelectionTo(hoveredCoord));
                AddButton("Place Loaded Here", () => PlaceLoadedStructure(hoveredCoord));
                AddButton("Move Selection Here", () => MoveSelectionTo(hoveredCoord));
            }

            if (hasHover && layoutManager.LoadedStructureBlock != null)
            {
                IReadOnlyList<DebugStructureGhostCell> cells = PortDebugVisualizationUtility.BuildGhostCells(layoutManager.LoadedStructureBlock, hoveredCoord);
                int boundaryPorts = 0;
                for (int i = 0; i < cells.Count; i++)
                {
                    boundaryPorts += PortDebugVisualizationUtility.Directions(cells[i].BoundaryPorts).Count;
                }

                AddText("Ghost cells:" + cells.Count + " boundary ports:" + boundaryPorts);
            }
            BuildLayoutManagerPanel();
            AddHeader("World Snapshots");

            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            if (runner == null || runner.Context == null)
            {
                AddText("runner unavailable");
                return;
            }

            foreach (EntitySnapshot snapshot in runner.Context.ClientMapWorld.CreateSnapshot())
            {
                DirectionMask worldPorts = PortDebugVisualizationUtility.GetWorldPorts(snapshot);
                string port = snapshot.PortLocalPorts == DirectionMask.None ? "-" : snapshot.PortLocalPorts + " world:" + worldPorts;
                AddText(snapshot.EntityId + " config:" + snapshot.ConfigId + " (" + snapshot.X + "," + snapshot.Y + ") dir:" + snapshot.Direction + " pivot:" + snapshot.RotatePivot + " port:" + port);
            }
        }

        private void BuildLayoutManagerPanel()
        {
            AddHeader("Layouts");
            AddInputField("SaveTargetInput", layoutManager.SaveTargetName, value =>
            {
                layoutManager.SetSaveTargetName(value);
                lastResult = "save target: " + layoutManager.SaveTargetName;
                Rebuild();
            });
            AddText("Save target: " + layoutManager.SaveTargetName);
            AddText("Current loaded: " + DebugLayoutPaths.RelativePath(layoutManager.CurrentLayoutPath));
            AddText("Last saved: " + DebugLayoutPaths.RelativePath(layoutManager.LastSavedPath));
            AddText("Selection: " + selection.Count + "   Result: " + layoutManager.LastLayoutOperationResult);
            AddButton("另存为输入名: " + layoutManager.SaveTargetName, () => SaveSelectionAs(layoutManager.SaveTargetName));
            AddButton("覆盖当前加载", () => OverwriteCurrentLayout());
            AddButton("重命名当前加载为输入名", () => RenameCurrentLayout(layoutManager.SaveTargetName));
            AddButton("删除当前加载", () => DeleteCurrentLayout());
            AddButton("刷新布局列表", RefreshLayoutFiles);

            IReadOnlyList<DebugLayoutFileMetadata> files = layoutManager.LayoutFiles;
            if (files.Count == 0)
            {
                AddText("No layout files.");
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                DebugLayoutFileMetadata file = files[i];
                string mark = string.Equals(file.Path, layoutManager.CurrentLayoutPath, StringComparison.OrdinalIgnoreCase) ? "* " : string.Empty;
                string status = file.Valid ? "ok" : "bad";
                string label = mark + file.ShortName + "  count:" + file.EntryCount + "  " + file.LastWriteTime.ToString("MM-dd HH:mm") + "  " + status;
                AddButton(label, () => LoadStructureBlock(file.Path));
                if (!file.Valid)
                {
                    AddText("  " + file.Reason);
                }
            }
        }

        private void ApplyTag(WorldTag tag, bool enabled)
        {
            if (selectedEntityId == 0)
            {
                lastResult = "select entity first";
                Rebuild();
                return;
            }

            if (networkSubmitter == null)
            {
                networkSubmitter = FindObjectOfType<ClientMoveNetworkSubmitter>();
            }

            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                Rebuild();
                return;
            }

            long entityId = selectedEntityId;
            networkSubmitter.DebugSetTag(entityId, tag, enabled, (success, reason) =>
            {
                lastResult = success ? "tag " + tag + " " + enabled + " on " + entityId : "tag failed: " + reason;
                Rebuild();
            });
        }

        private void AddHeader(string text)
        {
            TextMeshProUGUI label = CreateText(text, 18, FontStyles.Bold);
            label.color = new Color(0.92f, 0.96f, 1f, 1f);
        }

        private void AddText(string text)
        {
            CreateText(text, 14, FontStyles.Normal);
        }

        private void AddButton(string text, UnityEngine.Events.UnityAction action)
        {
            GameObject row = CreateRow("Button_" + text);
            Image image = row.AddComponent<Image>();
            image.color = new Color(0.17f, 0.2f, 0.24f, 0.95f);
            Button button = row.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            GameObject labelObject = new GameObject("Text");
            labelObject.transform.SetParent(row.transform, false);
            var rect = labelObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10, 2);
            rect.offsetMax = new Vector2(-10, -2);
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
        }

        private void AddInputField(string name, string value, UnityEngine.Events.UnityAction<string> action)
        {
            GameObject row = CreateRow(name);
            Image image = row.AddComponent<Image>();
            image.color = new Color(0.08f, 0.1f, 0.13f, 0.95f);
            TMP_InputField input = row.AddComponent<TMP_InputField>();
            input.targetGraphic = image;

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(row.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 2);
            textRect.offsetMax = new Vector2(-10, -2);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            input.textComponent = text;
            input.text = value ?? string.Empty;
            input.onEndEdit.AddListener(action);
        }

        private TextMeshProUGUI CreateText(string text, int fontSize, FontStyles style)
        {
            GameObject row = CreateRow("Text_" + text);
            TextMeshProUGUI label = row.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.enableWordWrapping = true;
            label.color = new Color(0.86f, 0.9f, 0.94f, 1f);
            return label;
        }

        private GameObject CreateRow(string name)
        {
            GameObject row = new GameObject(name);
            row.transform.SetParent(contentRoot, false);
            var rect = row.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 34f);
            var layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 30f;
            layout.preferredHeight = 34f;
            return row;
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private T FindChild<T>(string childName) where T : Component
        {
            Transform found = FindDeep(transform, childName);
            return found != null ? found.GetComponent<T>() : null;
        }

        private static Transform FindDeep(Transform current, string childName)
        {
            if (current.name == childName)
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                Transform found = FindDeep(current.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool IsBuildTool(DGDebugPanelTool tool)
        {
            return tool == DGDebugPanelTool.Blocker ||
                tool == DGDebugPanelTool.Ball ||
                tool == DGDebugPanelTool.Conveyor ||
                tool == DGDebugPanelTool.PortConnector ||
                tool == DGDebugPanelTool.RotatePivot;
        }

        private static int GetConfigId(DGDebugPanelTool tool)
        {
            return tool switch
            {
                DGDebugPanelTool.Ball => DefaultWorldConfig.BallConfigId,
                DGDebugPanelTool.Conveyor => DefaultWorldConfig.ConveyorConfigId,
                DGDebugPanelTool.PortConnector => DefaultWorldConfig.PortConnectorBlockerConfigId,
                DGDebugPanelTool.RotatePivot => DefaultWorldConfig.PushableBlockerConfigId,
                _ => DefaultWorldConfig.BlockerConfigId
            };
        }

        private static Color GetToolColor(DGDebugPanelTool tool, float alpha)
        {
            return tool switch
            {
                DGDebugPanelTool.Ball => new Color(1f, 0.88f, 0.18f, alpha),
                DGDebugPanelTool.Conveyor => new Color(0.35f, 1f, 0.45f, alpha),
                DGDebugPanelTool.PortConnector => new Color(0.5f, 0.65f, 1f, alpha),
                DGDebugPanelTool.RotatePivot => new Color(1f, 0.55f, 0.95f, alpha),
                DGDebugPanelTool.Delete => new Color(1f, 0.2f, 0.2f, alpha),
                DGDebugPanelTool.Select => new Color(0.95f, 1f, 0.45f, alpha),
                DGDebugPanelTool.Drag => new Color(0.6f, 0.8f, 1f, alpha),
                _ => new Color(1f, 0.35f, 0.2f, alpha)
            };
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
