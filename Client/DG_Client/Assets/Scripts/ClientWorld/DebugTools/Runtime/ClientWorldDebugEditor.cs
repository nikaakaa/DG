using DG.GameCore;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DG.Map
{
    public enum DebugWorldEditorSlot
    {
        Blocker = 0,
        Ball = 1,
        Conveyor = 2,
        PortConnector = 3,
        Select = 4,
        Delete = 5
    }

    public enum DebugWorldEditorMode
    {
        Layout = 0
    }

    public enum DebugWorldEditorPaletteKind
    {
        None = 0,
        BaseEntity = 1,
        SavedStructure = 2
    }

    public sealed class ClientWorldDebugEditor : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private ClientMoveNetworkSubmitter networkSubmitter;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private int runtimeAutoMoveIntervalTicks = 1;
        [SerializeField] private int runtimeExpireAfterTicks;
        [SerializeField] private string structureName = "debug-selection";

        private DebugWorldEditorSlot currentSlot = DebugWorldEditorSlot.Select;
        private DebugWorldEditorPaletteKind paletteKind;
        private string selectedSavedLayoutPath = string.Empty;
        private Direction buildDirection = Direction.Right;
        private Vector2Int hoveredCoord;
        private bool hasHover;
        private long selectedEntityId;
        private string lastResult = "Debug editor ready";
        private Transform hoverView;
        private Transform ghostView;
        private Transform structureGhostRoot;
        private readonly Dictionary<string, long> runtimeEffectIds = new();
        private readonly Dictionary<string, DebugStructureRuntimeEffectRecord> runtimeEffectRecords = new();
        private readonly List<Transform> structureGhostViews = new();
        private readonly List<Transform> runtimePortViews = new();
        private readonly List<Transform> selectionBoxViews = new();
        private readonly DebugLayoutSelectionSet selection = new();
        private DebugStructureBlockDocument loadedStructureBlock;
        private DebugLayoutToolState toolState;
        private bool selectingRectangle;
        private Vector2Int selectionStartCoord;
        private bool draggingEntity;
        private long draggingEntityId;
        private Vector2Int dragStartCoord;
        private DebugWorldEditorMode mode;
        private Rect debugWindowRect = new Rect(16, 12, 500, 620);
        private Vector2 layoutScroll;
        private Vector2 savedTableScroll;
        private Transform runtimePortRoot;
        private Transform selectionBoxRoot;
        private string[] savedLayoutFiles = System.Array.Empty<string>();
        private bool contextMenuOpen;
        private Rect contextMenuRect = new Rect(0, 0, 260, 360);
        private Vector2 contextMenuScroll;

        public DebugWorldEditorMode Mode => mode;
        public DebugWorldEditorSlot CurrentSlot => currentSlot;
        public DebugWorldEditorPaletteKind PaletteKind => paletteKind;
        public string SelectedSavedLayoutPath => selectedSavedLayoutPath;
        public string StructureName => structureName;
        public int SavedLayoutCount => savedLayoutFiles.Length;
        public Direction BuildDirection => buildDirection;
        public string LastResult => lastResult;
        public long SelectedEntityId => selectedEntityId;
        public int RuntimeAutoMoveIntervalTicks => runtimeAutoMoveIntervalTicks;
        public int RuntimeExpireAfterTicks => runtimeExpireAfterTicks;
        public int SelectionCount => selection.Count;
        public DebugLayoutToolState ToolState => toolState;
        public int ActiveRuntimePortViewCount { get; private set; }

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
            structureGhostRoot = new GameObject("DebugStructureGhost").transform;
            structureGhostRoot.SetParent(transform, false);
            runtimePortRoot = new GameObject("RuntimePortDebug").transform;
            runtimePortRoot.SetParent(transform, false);
            selectionBoxRoot = new GameObject("DebugSelectionBox").transform;
            selectionBoxRoot.SetParent(transform, false);
            RefreshSavedLayoutFiles();
        }

        private void Update()
        {
            UpdateHover();
            UpdateKeyboard();
            if (IsPointerOverDebugUi(Input.mousePosition))
            {
                selectingRectangle = false;
                UpdateViews();
                return;
            }

            if (Input.GetMouseButtonDown(1) && hasHover)
            {
                OpenContextMenu(Input.mousePosition);
                UpdateViews();
                return;
            }

            if (UpdatePointerDrag())
            {
                UpdateViews();
                return;
            }

            UpdateViews();

            if (!Input.GetMouseButtonDown(0) || !hasHover)
            {
                return;
            }

            ExecuteCurrentTool();
        }

        private void OnGUI()
        {
            debugWindowRect = GUI.Window(4021, debugWindowRect, DrawDebugWindow, "DG Debug");
            if (contextMenuOpen)
            {
                contextMenuRect = GUI.Window(4022, contextMenuRect, DrawContextMenuWindow, "Selection");
            }
        }

        public void SelectMode(DebugWorldEditorMode nextMode)
        {
            mode = nextMode;
            selectingRectangle = false;
        }

        public void SelectSlot(DebugWorldEditorSlot slot)
        {
            currentSlot = slot;
            if (IsBuildSlot(slot))
            {
                paletteKind = DebugWorldEditorPaletteKind.BaseEntity;
                selectedSavedLayoutPath = string.Empty;
                loadedStructureBlock = null;
                toolState = DebugLayoutToolState.SingleCellTool;
            }

            if (slot != DebugWorldEditorSlot.Delete && !IsBuildSlot(slot))
            {
                paletteKind = DebugWorldEditorPaletteKind.None;
                selectedSavedLayoutPath = string.Empty;
                draggingEntity = false;
            }

            if (slot == DebugWorldEditorSlot.Delete)
            {
                paletteKind = DebugWorldEditorPaletteKind.None;
                selectedSavedLayoutPath = string.Empty;
                loadedStructureBlock = null;
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
            draggingEntity = false;
        }

        public bool AddSelectedEntityToSelection()
        {
            if (selectedEntityId == 0 || runner == null || runner.Context == null)
            {
                lastResult = "select entity first";
                return false;
            }

            if (!runner.Context.ClientMapWorld.TryGetSnapshot(selectedEntityId, out EntitySnapshot snapshot))
            {
                lastResult = "selected entity missing";
                return false;
            }

            selection.Add(snapshot);
            toolState = DebugLayoutToolState.SelectionPreview;
            lastResult = "selection count: " + selection.Count;
            return true;
        }

        public void ClearSelection()
        {
            selection.Clear();
            toolState = DebugLayoutToolState.SingleCellTool;
            lastResult = "selection cleared";
        }

        public void SetStructureName(string value)
        {
            structureName = value ?? string.Empty;
        }

        public IReadOnlyList<long> RuntimeEffectTargets()
        {
            if (selection.Count > 0)
            {
                var targets = new List<long>();
                foreach (EntitySnapshot snapshot in selection.Snapshots())
                {
                    targets.Add(snapshot.EntityId);
                }

                return targets;
            }

            if (selectedEntityId != 0)
            {
                return new[] { selectedEntityId };
            }

            return System.Array.Empty<long>();
        }

        public bool SaveSelectionToDefault()
        {
            return SaveSelectionAs(structureName);
        }

        public bool SaveSelectionAs(string name)
        {
            string sanitizedName = SanitizeLayoutName(name);
            if (string.IsNullOrEmpty(sanitizedName))
            {
                lastResult = "save failed: name is empty";
                return false;
            }

            string path = DebugLayoutPaths.NamedFilePath(sanitizedName);
            bool saved = DebugLayoutTooling.TrySaveSelection(selection, CurrentClientMapWorld(), RuntimeEffectRecords(), sanitizedName, path, out string reason);
            lastResult = saved ? "saved " + path : "save failed: " + reason;
            if (saved)
            {
                structureName = sanitizedName;
                RefreshSavedLayoutFiles();
            }

            return saved;
        }

        public bool LoadStructureBlockFromDefault()
        {
            return LoadStructureBlock(DebugLayoutPaths.DefaultFilePath());
        }

        public bool LoadStructureBlock(string path)
        {
            bool loaded = DebugLayoutTooling.TryLoadStructureBlock(path, ClientGameConfigProviderFactory.Create(), out loadedStructureBlock, out string reason);
            toolState = loaded ? DebugLayoutToolState.StructureGhostPlacement : toolState;
            if (loaded)
            {
                currentSlot = DebugWorldEditorSlot.Select;
                paletteKind = DebugWorldEditorPaletteKind.SavedStructure;
                selectedSavedLayoutPath = path;
                toolState = DebugLayoutToolState.StructureGhostPlacement;
            }

            lastResult = loaded ? "loaded structure: " + loadedStructureBlock.Name : "load failed: " + reason;
            return loaded;
        }

        public IReadOnlyList<DebugStructureSpawnRequest> PreviewLoadedStructure(Vector2Int anchor)
        {
            if (loadedStructureBlock == null)
            {
                return new List<DebugStructureSpawnRequest>();
            }

            return DebugStructureBlockStorage.CreateSpawnRequests(loadedStructureBlock, anchor.x, anchor.y);
        }

        public void CopySelectionTo(Vector2Int targetAnchor)
        {
            SubmitSpawnRequests(DebugLayoutTooling.CreateCopyRequests(selection, CurrentClientMapWorld(), RuntimeEffectRecords(), targetAnchor));
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
                return;
            }

            IReadOnlyList<DebugBatchMoveRequest> requests = selection.CreateMoveRequests(targetAnchor, runner.Context.ClientMapWorld, out IReadOnlyList<string> skipped);
            if (requests.Count == 0)
            {
                lastResult = skipped.Count > 0 ? string.Join("|", skipped) : "selection is empty";
                return;
            }

            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
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
                    });
            }
        }

        public void DeleteSelectionOrEntity(long fallbackEntityId)
        {
            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
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
                });
            }
        }

        public void MoveEntity(long entityId, Vector2Int targetCoord)
        {
            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                return;
            }

            networkSubmitter.DebugMove(entityId, targetCoord, (success, reason) =>
            {
                lastResult = success ? $"moved {entityId} to {targetCoord}" : $"move failed: {reason}";
            });
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
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SelectMode(DebugWorldEditorMode.Layout);
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                SelectSlot(DebugWorldEditorSlot.Select);
            }
            if (Input.GetKeyDown(KeyCode.F1))
            {
                SelectMode(DebugWorldEditorMode.Layout);
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (mode == DebugWorldEditorMode.Layout)
                {
                    SelectSlot(DebugWorldEditorSlot.Blocker);
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                if (mode == DebugWorldEditorMode.Layout)
                {
                    SelectSlot(DebugWorldEditorSlot.Ball);
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                if (mode == DebugWorldEditorMode.Layout)
                {
                    SelectSlot(DebugWorldEditorSlot.Conveyor);
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha4) && mode == DebugWorldEditorMode.Layout)
            {
                SelectSlot(DebugWorldEditorSlot.PortConnector);
            }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (selection.Count > 0 || selectedEntityId != 0)
                {
                    DeleteSelectionOrEntity(selectedEntityId);
                }
                else
                {
                    SelectSlot(DebugWorldEditorSlot.Delete);
                }
            }
            if (Input.GetKeyDown(KeyCode.P))
            {
                ApplyRuntimeEffect(RuntimeEffectKind.TemporaryPort);
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                RemoveRuntimeEffect(RuntimeEffectKind.TemporaryPort);
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                contextMenuOpen = false;
                SelectSlot(DebugWorldEditorSlot.Select);
                if (toolState == DebugLayoutToolState.StructureGhostPlacement)
                {
                    loadedStructureBlock = null;
                    selectedSavedLayoutPath = string.Empty;
                    toolState = DebugLayoutToolState.SelectionPreview;
                }
            }
            if (Input.GetKeyDown(KeyCode.S) && mode == DebugWorldEditorMode.Layout)
            {
                SaveSelectionToDefault();
            }
            if (Input.GetKeyDown(KeyCode.L) && mode == DebugWorldEditorMode.Layout)
            {
                LoadStructureBlockFromDefault();
            }
            if (Input.GetKeyDown(KeyCode.C) && mode == DebugWorldEditorMode.Layout && hasHover)
            {
                CopySelectionTo(hoveredCoord);
            }
            if (Input.GetKeyDown(KeyCode.V) && mode == DebugWorldEditorMode.Layout && hasHover)
            {
                if (paletteKind == DebugWorldEditorPaletteKind.SavedStructure)
                {
                    PlaceLoadedStructure(hoveredCoord);
                }
            }
            if (Input.GetKeyDown(KeyCode.M) && mode == DebugWorldEditorMode.Layout && hasHover)
            {
                MoveSelectionTo(hoveredCoord);
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                RotateDirection();
            }
        }

        private bool UpdatePointerDrag()
        {
            if (currentSlot == DebugWorldEditorSlot.Delete || paletteKind != DebugWorldEditorPaletteKind.None || runner == null || runner.Context == null || !hasHover)
            {
                selectingRectangle = false;
                draggingEntity = false;
                return false;
            }

            if (Input.GetMouseButtonDown(0))
            {
                dragStartCoord = hoveredCoord;
                if (TryPickEntity(hoveredCoord, out long entityId))
                {
                    draggingEntity = true;
                    draggingEntityId = entityId;
                    SelectEntity(entityId);
                    lastResult = "dragging " + entityId;
                    return true;
                }

                selectingRectangle = true;
                selectionStartCoord = hoveredCoord;
                toolState = DebugLayoutToolState.Selecting;
                return true;
            }

            if (!Input.GetMouseButtonUp(0))
            {
                return selectingRectangle || draggingEntity;
            }

            if (draggingEntity)
            {
                long entityId = draggingEntityId;
                Vector2Int targetCoord = hoveredCoord;
                draggingEntity = false;
                draggingEntityId = 0;
                if (targetCoord == dragStartCoord)
                {
                    lastResult = "selected " + entityId;
                    return true;
                }

                MoveEntity(entityId, targetCoord);
                return true;
            }

            if (!selectingRectangle)
            {
                return false;
            }

            selectingRectangle = false;
            if (selectionStartCoord == hoveredCoord)
            {
                return true;
            }

            bool append = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            int added = DebugLayoutTooling.SelectRectangle(selection, runner.Context.ClientMapWorld, selectionStartCoord, hoveredCoord, append);
            toolState = DebugLayoutToolState.SelectionPreview;
            lastResult = "box selection added: " + added + " total:" + selection.Count;
            return true;
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
                bool showGhost = mode == DebugWorldEditorMode.Layout && hasHover && IsBuildSlot(currentSlot) && (loadedStructureBlock == null || toolState != DebugLayoutToolState.StructureGhostPlacement);
                ghostView.gameObject.SetActive(showGhost);
                ghostView.position = ToWorldPosition(hoveredCoord, -0.45f);
                SpriteRenderer renderer = ghostView.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = GetSlotColor(currentSlot, 0.35f);
                }
            }

            UpdateStructureGhostViews();
            UpdateRuntimePortViews();
            UpdateSelectionBoxViews();
        }

        private void UpdateStructureGhostViews()
        {
            bool showStructureGhost = hasHover && loadedStructureBlock != null && toolState == DebugLayoutToolState.StructureGhostPlacement;
            IReadOnlyList<DebugStructureGhostCell> cells = showStructureGhost ? PortDebugVisualizationUtility.BuildGhostCells(loadedStructureBlock, hoveredCoord) : new List<DebugStructureGhostCell>();
            EnsureStructureGhostCount(cells.Count);
            for (int i = 0; i < structureGhostViews.Count; i++)
            {
                Transform view = structureGhostViews[i];
                bool visible = i < cells.Count;
                view.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                DebugStructureGhostCell cell = cells[i];
                view.position = ToWorldPosition(cell.Coord, -0.5f);
                SpriteRenderer renderer = view.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.color = cell.HasInternalConnection ? new Color(0.25f, 1f, 0.9f, 0.38f) : new Color(1f, 1f, 1f, 0.28f);
                }

                LineRenderer line = view.GetComponent<LineRenderer>();
                if (line != null)
                {
                    ConfigureGhostPortLine(line, cell.BoundaryPorts);
                }
            }
        }

        private void EnsureStructureGhostCount(int count)
        {
            while (structureGhostViews.Count < count)
            {
                Transform view = CreateSquareView("DebugStructureGhostCell", new Color(1f, 1f, 1f, 0.3f), 42, 0.65f);
                view.SetParent(structureGhostRoot, true);
                LineRenderer line = view.gameObject.AddComponent<LineRenderer>();
                line.material = CreateMaterial(new Color(1f, 0.92f, 0.2f, 0.9f));
                structureGhostViews.Add(view);
            }
        }

        private void ConfigureGhostPortLine(LineRenderer line, DirectionMask boundaryPorts)
        {
            line.enabled = boundaryPorts != DirectionMask.None;
            if (!line.enabled)
            {
                return;
            }

            line.useWorldSpace = false;
            line.loop = false;
            line.widthMultiplier = 0.08f;
            line.positionCount = 2;
            line.startColor = new Color(1f, 0.92f, 0.2f, 0.9f);
            line.endColor = new Color(1f, 0.92f, 0.2f, 0.9f);
            line.sortingOrder = 43;
            if (boundaryPorts.Contains(Direction.Up) || boundaryPorts.Contains(Direction.Down))
            {
                line.SetPosition(0, new Vector3(0f, -0.45f, -0.04f));
                line.SetPosition(1, new Vector3(0f, 0.45f, -0.04f));
                return;
            }

            line.SetPosition(0, new Vector3(-0.45f, 0f, -0.04f));
            line.SetPosition(1, new Vector3(0.45f, 0f, -0.04f));
        }

        private void UpdateRuntimePortViews()
        {
            if (runner == null || runner.Context == null || runtimePortRoot == null)
            {
                EnsureRuntimePortViewCount(0);
                return;
            }

            IReadOnlyList<EntitySnapshot> snapshots = runner.Context.ClientMapWorld.CreateSnapshot();
            var markers = new List<(Vector2Int Coord, Direction Direction)>();
            for (int i = 0; i < snapshots.Count; i++)
            {
                EntitySnapshot snapshot = snapshots[i];
                DirectionMask ports = snapshot.PortLocalPorts | RuntimePortMask(runner.Context.ClientMapWorld, snapshot.EntityId);

                if (ports == DirectionMask.None)
                {
                    continue;
                }

                DirectionMask worldPorts = ports.RotateBy(snapshot.Direction);
                foreach (Direction direction in PortDebugVisualizationUtility.Directions(worldPorts))
                {
                    markers.Add((new Vector2Int(snapshot.X, snapshot.Y), direction));
                }
            }

            ActiveRuntimePortViewCount = markers.Count;
            EnsureRuntimePortViewCount(markers.Count);
            for (int i = 0; i < runtimePortViews.Count; i++)
            {
                Transform view = runtimePortViews[i];
                bool visible = i < markers.Count;
                view.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                view.position = ToWorldPosition(markers[i].Coord, -0.07f) + RuntimePortMarkerOffset(markers[i].Direction);
            }
        }

        private void EnsureRuntimePortViewCount(int count)
        {
            while (runtimePortViews.Count < count)
            {
                Transform view = CreateSquareView("RuntimePortView", new Color(1f, 0.88f, 0.08f, 0.95f), 48, 0.18f);
                view.SetParent(runtimePortRoot, true);
                runtimePortViews.Add(view);
            }

            for (int i = count; i < runtimePortViews.Count; i++)
            {
                runtimePortViews[i].gameObject.SetActive(false);
            }
        }

        private Vector3 RuntimePortMarkerOffset(Direction direction)
        {
            float offset = cellSize * 0.36f;
            return direction switch
            {
                Direction.Left => new Vector3(-offset, 0f, 0f),
                Direction.Right => new Vector3(offset, 0f, 0f),
                Direction.Up => new Vector3(0f, offset, 0f),
                Direction.Down => new Vector3(0f, -offset, 0f),
                _ => Vector3.zero
            };
        }

        private void UpdateSelectionBoxViews()
        {
            if (!selectingRectangle || !hasHover || selectionBoxRoot == null)
            {
                EnsureSelectionBoxCount(0);
                return;
            }

            int minX = Mathf.Min(selectionStartCoord.x, hoveredCoord.x);
            int maxX = Mathf.Max(selectionStartCoord.x, hoveredCoord.x);
            int minY = Mathf.Min(selectionStartCoord.y, hoveredCoord.y);
            int maxY = Mathf.Max(selectionStartCoord.y, hoveredCoord.y);
            int count = (maxX - minX + 1) * (maxY - minY + 1);
            EnsureSelectionBoxCount(count);
            int index = 0;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Transform view = selectionBoxViews[index++];
                    view.gameObject.SetActive(true);
                    view.position = ToWorldPosition(new Vector2Int(x, y), -0.55f);
                }
            }
        }

        private void EnsureSelectionBoxCount(int count)
        {
            while (selectionBoxViews.Count < count)
            {
                Transform view = CreateSquareView("DebugSelectionBoxCell", new Color(0.6f, 0.95f, 1f, 0.22f), 44, 0.9f);
                view.SetParent(selectionBoxRoot, true);
                selectionBoxViews.Add(view);
            }

            for (int i = count; i < selectionBoxViews.Count; i++)
            {
                selectionBoxViews[i].gameObject.SetActive(false);
            }
        }

        private void ExecuteCurrentTool()
        {
            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                return;
            }

            if (paletteKind == DebugWorldEditorPaletteKind.SavedStructure)
            {
                PlaceLoadedStructure(hoveredCoord);
                return;
            }

            if (paletteKind == DebugWorldEditorPaletteKind.BaseEntity && IsBuildSlot(currentSlot))
            {
                int configId = GetConfigId(currentSlot);
                Direction direction = currentSlot == DebugWorldEditorSlot.Blocker ? Direction.None : buildDirection;
                networkSubmitter.DebugSpawn(0, configId, hoveredCoord, direction, 0, 1, (success, reason, entityId) =>
                {
                    lastResult = success ? $"spawned {entityId}" : $"spawn failed: {reason}";
                });
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
                DeleteSelectionOrEntity(entityId);
                return;
            }

            SelectEntity(entityId);
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                AddSelectedEntityToSelection();
                return;
            }

            lastResult = $"selected {entityId}";
        }

        private void DrawStructureBlockPanel()
        {
            GUILayout.Label("State: " + toolState + "  Selection: " + selection.Count);
            if (hasHover && loadedStructureBlock != null)
            {
                IReadOnlyList<DebugStructureGhostCell> cells = PortDebugVisualizationUtility.BuildGhostCells(loadedStructureBlock, hoveredCoord);
                int internalConnections = 0;
                int boundaryPorts = 0;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].HasInternalConnection)
                    {
                        internalConnections++;
                    }

                    boundaryPorts += PortDebugVisualizationUtility.Directions(cells[i].BoundaryPorts).Count;
                }

                GUILayout.Label("Ghost cells:" + cells.Count + " internal:" + internalConnections + " boundary:" + boundaryPorts);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear"))
            {
                ClearSelection();
            }
            GUILayout.EndHorizontal();
        }

        private void SubmitSpawnRequests(IReadOnlyList<DebugStructureSpawnRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                lastResult = "structure is empty";
                return;
            }

            if (networkSubmitter == null)
            {
                lastResult = "debug submitter missing";
                return;
            }

            toolState = DebugLayoutToolState.SubmittingBatch;
            int successCount = 0;
            int failCount = 0;
            string lastReason = string.Empty;
            int requestCount = requests.Count;
            foreach (DebugStructureSpawnRequest request in requests)
            {
                networkSubmitter.DebugSpawn(0, request.ConfigId, new Vector2Int(request.X, request.Y), request.Direction, request.PlayerId, request.AutoMoveIntervalTicks, (success, reason, entityId) =>
                {
                    if (success)
                    {
                        successCount++;
                        ApplyStructureRuntimeEffects(entityId, request);
                    }
                    else
                    {
                        failCount++;
                        lastReason = reason;
                    }

                    lastResult = "batch spawn ok:" + successCount + " fail:" + failCount + (string.IsNullOrEmpty(lastReason) ? string.Empty : " " + lastReason);
                    if (successCount + failCount >= requestCount && paletteKind == DebugWorldEditorPaletteKind.SavedStructure && loadedStructureBlock != null)
                    {
                        toolState = DebugLayoutToolState.StructureGhostPlacement;
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
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryBlocking, 1, DirectionMask.None, 0);
            }

            if (request.RuntimeAutoMove)
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryAutoMove, request.AutoMoveIntervalTicks, DirectionMask.None, 0);
            }

            if (request.RuntimePushable)
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryPushable, 1, DirectionMask.None, 0);
            }

            if (request.RuntimePortLocalPorts != DirectionMask.None)
            {
                ApplyRuntimePortEffectsToEntity(entityId, request.RuntimePortLocalPorts);
            }

            if (request.RuntimeImmobile)
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryImmobile, 1, DirectionMask.None, 0);
            }
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

        private void ApplyRuntimePortEffectsToEntity(long entityId, DirectionMask ports)
        {
            IReadOnlyList<DirectionMask> masks = RuntimePortMasks(ports);
            for (int i = 0; i < masks.Count; i++)
            {
                ApplyRuntimeEffectToEntity(entityId, RuntimeEffectKind.TemporaryPort, 1, masks[i], 0);
            }
        }

        private static IReadOnlyList<DirectionMask> RuntimePortMasks(DirectionMask ports)
        {
            var masks = new List<DirectionMask>();
            if (ports.Contains(Direction.Left))
            {
                masks.Add(DirectionMask.Left);
            }

            if (ports.Contains(Direction.Right))
            {
                masks.Add(DirectionMask.Right);
            }

            if (ports.Contains(Direction.Up))
            {
                masks.Add(DirectionMask.Up);
            }

            if (ports.Contains(Direction.Down))
            {
                masks.Add(DirectionMask.Down);
            }

            return masks;
        }

        private void OpenContextMenu(Vector3 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            contextMenuRect.x = Mathf.Clamp(guiPosition.x, 0f, Mathf.Max(0f, Screen.width - contextMenuRect.width));
            contextMenuRect.y = Mathf.Clamp(guiPosition.y, 0f, Mathf.Max(0f, Screen.height - contextMenuRect.height));
            if (TryPickEntity(hoveredCoord, out long entityId))
            {
                SelectEntity(entityId);
            }

            contextMenuOpen = RuntimeEffectTargets().Count > 0 || entityId != 0;
            if (!contextMenuOpen)
            {
                lastResult = "no entity at cell";
            }
        }

        private void DrawContextMenuWindow(int windowId)
        {
            contextMenuScroll = GUILayout.BeginScrollView(contextMenuScroll);
            GUILayout.Label("Targets: " + RuntimeEffectTargets().Count);
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
            if (GUILayout.Button("Rotate Port Direction"))
            {
                RotateDirection();
            }

            DrawRuntimeEffectButtons("Blocking", RuntimeEffectKind.TemporaryBlocking);
            DrawRuntimeEffectButtons("AutoMove", RuntimeEffectKind.TemporaryAutoMove);
            DrawRuntimeEffectButtons("Pushable", RuntimeEffectKind.TemporaryPushable);
            DrawRuntimeEffectButtons("Port", RuntimeEffectKind.TemporaryPort);
            DrawRuntimeEffectButtons("Immobile", RuntimeEffectKind.TemporaryImmobile);

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add To Selection"))
            {
                AddSelectedEntityToSelection();
            }
            if (GUILayout.Button("Clear Selection"))
            {
                ClearSelection();
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Close"))
            {
                contextMenuOpen = false;
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, contextMenuRect.width, 22));
        }

        private void DrawDebugWindow(int windowId)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"Coord: {(hasHover ? hoveredCoord.ToString() : "-")}  Direction: {buildDirection}  Selected: {selectedEntityId}");
            GUILayout.Label(lastResult);
            GUILayout.Space(6);

            layoutScroll = GUILayout.BeginScrollView(layoutScroll);
            DrawLayoutPage();
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, debugWindowRect.width, 22));
        }

        private void DrawLayoutPage()
        {
            GUILayout.Label("Base");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Normal", GUILayout.Height(28)))
            {
                SelectSlot(DebugWorldEditorSlot.Select);
            }
            if (GUILayout.Button("Delete", GUILayout.Height(28)))
            {
                SelectSlot(DebugWorldEditorSlot.Delete);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            DrawBuildButton(DebugWorldEditorSlot.Blocker, "1 Blocker");
            DrawBuildButton(DebugWorldEditorSlot.Ball, "2 Ball");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawBuildButton(DebugWorldEditorSlot.Conveyor, "3 Conveyor");
            DrawBuildButton(DebugWorldEditorSlot.PortConnector, "4 Port Connector");
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("Palette: " + paletteKind + "  Tool: " + currentSlot);
            GUILayout.Label("Left: select/drag/box. Right: selection menu.");
            GUILayout.Label("Q normal. S save. C copy. M move. V place loaded. R rotate. Esc cancel.");
            GUILayout.Space(8);
            GUILayout.Label("Selection count: " + selection.Count);
            DrawStructureBlockPanel();
            GUILayout.Space(8);
            GUILayout.Label("Save Selection");
            GUILayout.BeginHorizontal();
            structureName = GUILayout.TextField(structureName);
            if (GUILayout.Button("Save", GUILayout.Width(72)))
            {
                SaveSelectionToDefault();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Saved");
            if (GUILayout.Button("Refresh", GUILayout.Width(78)))
            {
                RefreshSavedLayoutFiles();
            }
            GUILayout.EndHorizontal();
            if (savedLayoutFiles.Length == 0)
            {
                GUILayout.Label("No saved layout files.");
            }

            savedTableScroll = GUILayout.BeginScrollView(savedTableScroll, GUILayout.Height(150));
            for (int i = 0; i < savedLayoutFiles.Length; i++)
            {
                string file = savedLayoutFiles[i];
                DrawSavedLayoutButton(file);
            }
            GUILayout.EndScrollView();
        }

        private void DrawSavedLayoutButton(string file)
        {
            Color oldColor = GUI.color;
            GUI.color = selectedSavedLayoutPath == file && paletteKind == DebugWorldEditorPaletteKind.SavedStructure ? new Color(0.8f, 1f, 0.5f, 1f) : Color.white;
            string label = Path.GetFileNameWithoutExtension(file);
            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                LoadStructureBlock(file);
                SelectMode(DebugWorldEditorMode.Layout);
            }

            GUI.color = oldColor;
        }

        private void DrawBuildButton(DebugWorldEditorSlot slot, string label)
        {
            Color oldColor = GUI.color;
            GUI.color = currentSlot == slot && paletteKind == DebugWorldEditorPaletteKind.BaseEntity ? new Color(0.8f, 1f, 0.5f, 1f) : Color.white;
            if (GUILayout.Button(label, GUILayout.Height(30)))
            {
                SelectSlot(slot);
            }

            GUI.color = oldColor;
        }

        private void RefreshSavedLayoutFiles()
        {
            string directory = DebugLayoutPaths.DefaultDirectory();
            savedLayoutFiles = Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.dgdebuglayout.json")
                : System.Array.Empty<string>();
            System.Array.Sort(savedLayoutFiles, System.StringComparer.OrdinalIgnoreCase);
        }

        public static string SanitizeLayoutName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            var chars = trimmed.ToCharArray();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                    continue;
                }

                for (int j = 0; j < invalidChars.Length; j++)
                {
                    if (chars[i] == invalidChars[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            return new string(chars);
        }

        private void ApplyRuntimeEffect(RuntimeEffectKind kind)
        {
            if (!CanEditRuntimeEffect(out string reason))
            {
                lastResult = reason;
                return;
            }

            IReadOnlyList<long> targets = RuntimeEffectTargets();
            DirectionMask portMask = kind == RuntimeEffectKind.TemporaryPort ? DirectionToMask(buildDirection) : DirectionMask.None;
            int successCount = 0;
            int failCount = 0;
            string lastReason = string.Empty;
            for (int i = 0; i < targets.Count; i++)
            {
                long entityId = targets[i];
                long expireTick = GetRuntimeExpireTick(entityId);
                ApplyRuntimeEffectToEntity(entityId, kind, runtimeAutoMoveIntervalTicks, portMask, expireTick, (success, callbackReason) =>
                {
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        lastReason = callbackReason;
                    }

                    lastResult = "effect add " + kind + " ok:" + successCount + " fail:" + failCount + (string.IsNullOrEmpty(lastReason) ? string.Empty : " " + lastReason);
                });
            }
        }

        private void ApplyRuntimeEffectToEntity(long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portMask, long expireTick, System.Action<bool, string> completed = null)
        {
            networkSubmitter.DebugApplyRuntimeEffect(entityId, kind, autoMoveIntervalTicks, portMask, expireTick, (success, callbackReason, effectId) =>
            {
                if (success)
                {
                    string key = RuntimeEffectKey(entityId, kind, portMask);
                    runtimeEffectIds[key] = effectId;
                    runtimeEffectRecords[key] = new DebugStructureRuntimeEffectRecord(entityId, kind, autoMoveIntervalTicks, portMask);
                }

                completed?.Invoke(success, callbackReason);
            });
        }

        private void RemoveRuntimeEffect(RuntimeEffectKind kind)
        {
            if (!CanEditRuntimeEffect(out string reason))
            {
                lastResult = reason;
                return;
            }

            IReadOnlyList<long> targets = RuntimeEffectTargets();
            int successCount = 0;
            int failCount = 0;
            string lastReason = string.Empty;
            for (int i = 0; i < targets.Count; i++)
            {
                long entityId = targets[i];
                IReadOnlyList<long> effectIds = FindRuntimeEffectIds(entityId, kind);
                for (int effectIndex = 0; effectIndex < effectIds.Count; effectIndex++)
                {
                    long effectId = effectIds[effectIndex];
                    networkSubmitter.DebugRemoveRuntimeEffect(entityId, kind, effectId, (success, callbackReason, removedEffectId) =>
                    {
                        if (success)
                        {
                            successCount++;
                            RemoveRuntimeEffectCache(entityId, kind, removedEffectId);
                        }
                        else
                        {
                            failCount++;
                            lastReason = callbackReason;
                        }

                        lastResult = "effect remove " + kind + " ok:" + successCount + " fail:" + failCount + (string.IsNullOrEmpty(lastReason) ? string.Empty : " " + lastReason);
                    });
                }
            }
        }

        private bool CanEditRuntimeEffect(out string reason)
        {
            if (RuntimeEffectTargets().Count == 0)
            {
                reason = "select entity or box selection first";
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

        private static string RuntimeEffectKey(long entityId, RuntimeEffectKind kind, DirectionMask portMask)
        {
            return entityId + ":" + (int)kind + ":" + (kind == RuntimeEffectKind.TemporaryPort ? (int)portMask : 0);
        }

        private IReadOnlyList<long> FindRuntimeEffectIds(long entityId, RuntimeEffectKind kind)
        {
            var ids = new List<long>();
            foreach (KeyValuePair<string, DebugStructureRuntimeEffectRecord> pair in runtimeEffectRecords)
            {
                if (pair.Value.EntityId == entityId && pair.Value.Kind == kind && runtimeEffectIds.TryGetValue(pair.Key, out long effectId))
                {
                    ids.Add(effectId);
                }
            }

            if (ids.Count == 0)
            {
                ids.Add(0);
            }

            return ids;
        }

        private void RemoveRuntimeEffectCache(long entityId, RuntimeEffectKind kind, long removedEffectId)
        {
            if (removedEffectId != 0)
            {
                string effectKey = string.Empty;
                foreach (KeyValuePair<string, long> pair in runtimeEffectIds)
                {
                    if (pair.Value == removedEffectId)
                    {
                        effectKey = pair.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(effectKey))
                {
                    runtimeEffectIds.Remove(effectKey);
                    runtimeEffectRecords.Remove(effectKey);
                }

                return;
            }

            var keys = new List<string>();
            foreach (KeyValuePair<string, DebugStructureRuntimeEffectRecord> pair in runtimeEffectRecords)
            {
                if (pair.Value.EntityId == entityId && pair.Value.Kind == kind)
                {
                    keys.Add(pair.Key);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                runtimeEffectRecords.Remove(keys[i]);
                runtimeEffectIds.Remove(keys[i]);
            }
        }

        private ClientMapWorld CurrentClientMapWorld()
        {
            return runner != null && runner.Context != null ? runner.Context.ClientMapWorld : null;
        }

        private IReadOnlyList<DebugStructureRuntimeEffectRecord> RuntimeEffectRecords()
        {
            return new List<DebugStructureRuntimeEffectRecord>(runtimeEffectRecords.Values);
        }

        private DirectionMask RuntimePortMask(ClientMapWorld world, long entityId)
        {
            DirectionMask mask = DirectionMask.None;
            RuntimeEffectStore runtimeEffects = world?.CoreWorld.RuntimeEffects;
            long tick = world?.CoreWorld.ServerTick ?? 0;
            if (runtimeEffects != null)
            {
                foreach (RuntimeEffectInstance effect in runtimeEffects.ActiveAt(tick))
                {
                    if (effect.TargetEntityId == entityId && effect.Kind == RuntimeEffectKind.TemporaryPort)
                    {
                        mask |= effect.Spec.PortMask;
                    }
                }
            }

            foreach (DebugStructureRuntimeEffectRecord record in runtimeEffectRecords.Values)
            {
                if (record.EntityId == entityId && record.Kind == RuntimeEffectKind.TemporaryPort)
                {
                    mask |= record.PortLocalPorts;
                }
            }

            return mask;
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

        public bool IsPointerOverDebugUi(Vector3 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return debugWindowRect.Contains(guiPosition) || (contextMenuOpen && contextMenuRect.Contains(guiPosition));
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
                DebugWorldEditorSlot.Select => new Color(0.95f, 1f, 0.45f, alpha),
                _ => new Color(1f, 0.35f, 0.2f, alpha)
            };
            return color;
        }

        private static Color GetConfigColor(int configId, float alpha)
        {
            if (configId == DefaultWorldConfig.BallConfigId)
            {
                return new Color(1f, 0.88f, 0.18f, alpha);
            }

            if (configId == DefaultWorldConfig.ConveyorConfigId)
            {
                return new Color(0.35f, 1f, 0.45f, alpha);
            }

            if (configId == DefaultWorldConfig.PortConnectorBlockerConfigId)
            {
                return new Color(0.25f, 1f, 0.9f, alpha);
            }

            return new Color(1f, 0.35f, 0.2f, alpha);
        }

        private static Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Sprites/Default"));
            material.color = color;
            return material;
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
