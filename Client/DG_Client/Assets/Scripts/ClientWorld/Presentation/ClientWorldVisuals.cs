using System.Collections.Generic;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public sealed class ClientWorldVisuals : MonoBehaviour
    {
        [SerializeField] private ClientWorldRunner runner;
        [SerializeField] private int gridRadius = 6;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Color gridColor = new Color(0.25f, 0.35f, 0.45f, 1f);
        [SerializeField] private Color playerColor = new Color(0.1f, 0.8f, 1f, 1f);
        [SerializeField] private Color ballColor = new Color(1f, 0.88f, 0.18f, 1f);
        [SerializeField] private Color blockerColor = new Color(1f, 0.35f, 0.2f, 1f);
        [SerializeField] private Color conveyorColor = new Color(0.35f, 1f, 0.45f, 1f);
        [SerializeField] private Color portConnectorColor = new Color(0.25f, 1f, 0.9f, 1f);
        [SerializeField] private Color portLineColor = new Color(0.02f, 0.14f, 0.14f, 1f);
        [SerializeField] private bool animationLayerEnabled = true;
        private readonly Dictionary<long, Transform> EntityViews = new();
        private readonly Dictionary<long, ActiveEntityAnimation> activeAnimations = new();
        private readonly Dictionary<long, ClientAnimationEvent> latestDrainedEvents = new();
        private readonly Dictionary<string, SharedRotateAnimationGroup> rotateAnimationGroups = new();
        private readonly Dictionary<string, LineRenderer> portConnectionLines = new();
        private readonly HashSet<long> seenEntityIds = new();
        private readonly List<long> removedEntityIds = new();
        private readonly HashSet<string> seenPortConnectionKeys = new();
        private readonly List<string> removedPortConnectionKeys = new();
        private Transform entityRoot;
        private Transform portConnectionRoot;
        private Texture2D squareTexture;
        private Sprite squareSprite;
        private Material gridMaterial;
        private Material portLineMaterial;
        public string LastAnimationSummary => runner != null && runner.Context != null ? runner.Context.AnimationLayer.RecentSummary : string.Empty;

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            entityRoot = new GameObject("Entities").transform;
            entityRoot.SetParent(transform, false);
            portConnectionRoot = new GameObject("PortConnections").transform;
            portConnectionRoot.SetParent(transform, false);
            squareSprite = CreateSquareSprite(out squareTexture);
            gridMaterial = CreateMaterial(gridColor);
            portLineMaterial = CreateMaterial(portLineColor);
            DrawGrid();
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<long, Transform> pair in EntityViews)
            {
                if (pair.Value != null)
                {
                    DestroyImmediateOrDeferred(pair.Value.gameObject);
                }
            }

            foreach (KeyValuePair<string, LineRenderer> pair in portConnectionLines)
            {
                if (pair.Value != null)
                {
                    DestroyImmediateOrDeferred(pair.Value.gameObject);
                }
            }

            EntityViews.Clear();
            activeAnimations.Clear();
            latestDrainedEvents.Clear();
            rotateAnimationGroups.Clear();
            portConnectionLines.Clear();
            DestroyAsset(squareSprite);
            DestroyAsset(squareTexture);
            DestroyAsset(gridMaterial);
            DestroyAsset(portLineMaterial);
            squareSprite = null;
            squareTexture = null;
            gridMaterial = null;
            portLineMaterial = null;
        }

        private void LateUpdate()
        {
            if (runner == null || runner.Context == null)
            {
                return;
            }

            IReadOnlyList<EntitySnapshot> snapshots = runner.Context.ClientMapWorld.CreateSnapshot();
            float animationDeltaTime = Application.isPlaying ? Time.deltaTime : 0f;
            runner.Context.AnimationLayer.Enabled = animationLayerEnabled;
            DrainAnimationEvents();
            PrepareRotateAnimationGroups(animationDeltaTime);
            seenEntityIds.Clear();
            for (int i = 0; i < snapshots.Count; i++)
            {
                EntitySnapshot snapshot = snapshots[i];
                seenEntityIds.Add(snapshot.EntityId);
                Transform view = GetOrCreateEntityView(snapshot.EntityId);
                ConfigureEntityView(view, snapshot, animationDeltaTime);
            }

            RemoveMissingViews(seenEntityIds);
            ConfigurePortConnections(snapshots);
            CleanupUnusedRotateAnimationGroups();
        }

        private Transform GetOrCreateEntityView(long entityId)
        {
            if (EntityViews.TryGetValue(entityId, out Transform found))
            {
                return found;
            }

            GameObject view = new GameObject($"Entity_{entityId}");
            view.transform.SetParent(entityRoot, false);
            SpriteRenderer spriteRenderer = view.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = squareSprite;
            spriteRenderer.color = playerColor;
            spriteRenderer.sortingOrder = 10;
            view.AddComponent<LineRenderer>();
            view.transform.localScale = Vector3.one * (cellSize * 0.8f);
            EntityViews.Add(entityId, view.transform);
            return view.transform;
        }

        private void ConfigureEntityView(Transform view, EntitySnapshot snapshot, float deltaTime)
        {
            Color resolvedColor = ResolveColor(snapshot);
            Vector3 resolvedPosition = ToWorldPosition(new Vector2Int(snapshot.X, snapshot.Y), -0.1f);
            Vector3 resolvedScale = Vector3.one * (cellSize * 0.8f);
            Color displayColor = resolvedColor;
            if (animationLayerEnabled && activeAnimations.TryGetValue(snapshot.EntityId, out ActiveEntityAnimation animation))
            {
                if (animation.Advance(deltaTime, resolvedPosition, resolvedScale, resolvedColor, out Vector3 animatedPosition, out Vector3 animatedScale, out Color animatedColor))
                {
                    view.localPosition = animatedPosition;
                    view.localRotation = animation.Rotation;
                    view.localScale = animatedScale;
                    displayColor = animatedColor;
                }
                else
                {
                    activeAnimations.Remove(snapshot.EntityId);
                    view.localPosition = resolvedPosition;
                    view.localRotation = Quaternion.identity;
                    view.localScale = resolvedScale;
                }
            }
            else
            {
                view.localPosition = resolvedPosition;
                view.localRotation = Quaternion.identity;
                view.localScale = resolvedScale;
            }

            SpriteRenderer spriteRenderer = view.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = displayColor;
            }

            LineRenderer lineRenderer = view.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                ConfigurePortLine(lineRenderer, snapshot);
            }
        }

        private void ConfigurePortLine(LineRenderer lineRenderer, EntitySnapshot snapshot)
        {
            bool showPort = PortDebugVisualizationUtility.HasPorts(snapshot);
            lineRenderer.enabled = showPort;
            if (!showPort)
            {
                return;
            }

            DirectionMask worldPorts = PortDebugVisualizationUtility.GetWorldPorts(snapshot);
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.widthMultiplier = 0.12f;
            lineRenderer.positionCount = 2;
            lineRenderer.sharedMaterial = portLineMaterial;
            lineRenderer.startColor = portLineColor;
            lineRenderer.endColor = portLineColor;
            lineRenderer.sortingOrder = 12;
            if (worldPorts.Contains(Direction.Up) || worldPorts.Contains(Direction.Down))
            {
                lineRenderer.SetPosition(0, new Vector3(0f, -0.45f, -0.02f));
                lineRenderer.SetPosition(1, new Vector3(0f, 0.45f, -0.02f));
                return;
            }

            lineRenderer.SetPosition(0, new Vector3(-0.45f, 0f, -0.02f));
            lineRenderer.SetPosition(1, new Vector3(0.45f, 0f, -0.02f));
        }

        private Color ResolveColor(EntitySnapshot snapshot)
        {
            if (snapshot.PlayerControlled)
            {
                return playerColor;
            }

            if (snapshot.AutoMove || snapshot.Bouncable)
            {
                return ballColor;
            }

            if (snapshot.ConfigId == DefaultWorldConfig.ConveyorConfigId)
            {
                return conveyorColor;
            }

            if (snapshot.ConfigId == DefaultWorldConfig.PortConnectorBlockerConfigId)
            {
                return portConnectorColor;
            }

            if (snapshot.Blocking)
            {
                return blockerColor;
            }

            return playerColor;
        }

        private void RemoveMissingViews(HashSet<long> seen)
        {
            removedEntityIds.Clear();
            foreach (KeyValuePair<long, Transform> pair in EntityViews)
            {
                if (!seen.Contains(pair.Key))
                {
                    removedEntityIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < removedEntityIds.Count; i++)
            {
                long entityId = removedEntityIds[i];
                if (EntityViews.TryGetValue(entityId, out Transform view) && view != null)
                {
                    DestroyImmediateOrDeferred(view.gameObject);
                }

                EntityViews.Remove(entityId);
                activeAnimations.Remove(entityId);
            }
        }

        private void DrainAnimationEvents()
        {
            if (!animationLayerEnabled || runner == null || runner.Context == null)
            {
                activeAnimations.Clear();
                latestDrainedEvents.Clear();
                ClearRotateAnimationGroups();
                return;
            }

            latestDrainedEvents.Clear();
            if (runner.Context.AnimationLayer == null)
            {
                rotateAnimationGroups.Clear();
                return;
            }

            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                latestDrainedEvents[animationEvent.EntityId] = animationEvent;
            }

            foreach (ClientAnimationEvent animationEvent in latestDrainedEvents.Values)
            {
                if (!animationEvent.IsRotatePivot)
                {
                    continue;
                }

                string groupKey = RotateGroupKey(animationEvent);
                if (!rotateAnimationGroups.ContainsKey(groupKey))
                {
                    rotateAnimationGroups[groupKey] = new SharedRotateAnimationGroup(animationEvent.RotateDirection);
                }
            }

            foreach (KeyValuePair<long, ClientAnimationEvent> pair in latestDrainedEvents)
            {
                ClientAnimationEvent animationEvent = pair.Value;
                Transform view = GetOrCreateEntityView(animationEvent.EntityId);
                if (animationEvent.MotionKind == ClientAnimationMotionKind.Remove)
                {
                    if (EntityViews.TryGetValue(animationEvent.EntityId, out Transform removedView) && removedView != null)
                    {
                        DestroyImmediateOrDeferred(removedView.gameObject);
                    }

                    EntityViews.Remove(animationEvent.EntityId);
                    activeAnimations.Remove(animationEvent.EntityId);
                    continue;
                }

                Vector3 startPosition = activeAnimations.ContainsKey(animationEvent.EntityId)
                    ? view.localPosition
                    : ToWorldPosition(animationEvent.FromCoord, -0.1f);
                SharedRotateAnimationGroup rotateGroup = animationEvent.IsRotatePivot && rotateAnimationGroups.TryGetValue(RotateGroupKey(animationEvent), out SharedRotateAnimationGroup foundGroup) ? foundGroup : null;
                string rotateGroupKey = rotateGroup == null ? string.Empty : RotateGroupKey(animationEvent);
                ActiveEntityAnimation animation = ActiveEntityAnimation.Create(animationEvent, startPosition, ToWorldPosition(animationEvent.ToCoord, -0.1f), cellSize, rotateGroup, rotateGroupKey);
                view.localPosition = animation.StartPosition;
                activeAnimations[animationEvent.EntityId] = animation;
            }
        }

        private void CleanupUnusedRotateAnimationGroups()
        {
            removedPortConnectionKeys.Clear();
            foreach (KeyValuePair<string, SharedRotateAnimationGroup> pair in rotateAnimationGroups)
            {
                bool used = false;
                foreach (KeyValuePair<long, ActiveEntityAnimation> animationPair in activeAnimations)
                {
                    if (animationPair.Value.RotateGroupKey == pair.Key)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    removedPortConnectionKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < removedPortConnectionKeys.Count; i++)
            {
                rotateAnimationGroups.Remove(removedPortConnectionKeys[i]);
            }
        }

        private void ClearRotateAnimationGroups()
        {
            rotateAnimationGroups.Clear();
        }

        private void PrepareRotateAnimationGroups(float deltaTime)
        {
            foreach (KeyValuePair<string, SharedRotateAnimationGroup> pair in rotateAnimationGroups)
            {
                pair.Value.ResetFrame();
            }

            foreach (KeyValuePair<long, ActiveEntityAnimation> pair in activeAnimations)
            {
                ActiveEntityAnimation animation = pair.Value;
                if (string.IsNullOrEmpty(animation.RotateGroupKey) || !rotateAnimationGroups.TryGetValue(animation.RotateGroupKey, out SharedRotateAnimationGroup group))
                {
                    continue;
                }

                group.Include(animation.PreviewElapsed(deltaTime), animation.DurationSeconds, animation.Easing);
            }

            foreach (KeyValuePair<string, SharedRotateAnimationGroup> pair in rotateAnimationGroups)
            {
                pair.Value.CommitFrame();
            }
        }

        private static string RotateGroupKey(ClientAnimationEvent animationEvent)
        {
            return animationEvent.ServerTick + "|" + animationEvent.MotionKind + "|" + animationEvent.PivotEntityId + "|" + animationEvent.PivotCoord.x + "," + animationEvent.PivotCoord.y + "|" + animationEvent.RotateDirection + "|" + animationEvent.Bounce;
        }

        private void ConfigurePortConnections(IReadOnlyList<EntitySnapshot> snapshots)
        {
            IReadOnlyList<PortDebugConnection> connections = PortDebugVisualizationUtility.FindConnections(snapshots);
            seenPortConnectionKeys.Clear();
            foreach (PortDebugConnection connection in connections)
            {
                seenPortConnectionKeys.Add(connection.Key);
                LineRenderer line = GetOrCreatePortConnectionLine(connection.Key);
                line.enabled = true;
                line.useWorldSpace = false;
                line.loop = false;
                line.widthMultiplier = 0.06f;
                line.positionCount = 2;
                line.sharedMaterial = portLineMaterial;
                line.startColor = portLineColor;
                line.endColor = portLineColor;
                line.sortingOrder = 13;
                line.SetPosition(0, ResolveConnectionPosition(connection.FromEntityId, connection.FromCoord));
                line.SetPosition(1, ResolveConnectionPosition(connection.ToEntityId, connection.ToCoord));
            }

            removedPortConnectionKeys.Clear();
            foreach (KeyValuePair<string, LineRenderer> pair in portConnectionLines)
            {
                if (!seenPortConnectionKeys.Contains(pair.Key))
                {
                    removedPortConnectionKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < removedPortConnectionKeys.Count; i++)
            {
                string key = removedPortConnectionKeys[i];
                if (portConnectionLines.TryGetValue(key, out LineRenderer line) && line != null)
                {
                    DestroyImmediateOrDeferred(line.gameObject);
                }

                portConnectionLines.Remove(key);
            }
        }

        private LineRenderer GetOrCreatePortConnectionLine(string key)
        {
            if (portConnectionLines.TryGetValue(key, out LineRenderer found) && found != null)
            {
                return found;
            }

            GameObject lineObject = new GameObject("PortConnection_" + key);
            lineObject.transform.SetParent(portConnectionRoot, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = portLineMaterial;
            portConnectionLines[key] = line;
            return line;
        }

        private Vector3 ResolveConnectionPosition(long entityId, Vector2Int fallbackCoord)
        {
            if (EntityViews.TryGetValue(entityId, out Transform view) && view != null)
            {
                Vector3 position = view.localPosition;
                position.z = -0.04f;
                return position;
            }

            return ToWorldPosition(fallbackCoord, -0.04f);
        }

        private void DrawGrid()
        {
            GameObject grid = new GameObject("Grid");
            grid.transform.SetParent(transform, false);

            int min = -gridRadius;
            int max = gridRadius + 1;
            for (int x = min; x <= max; x++)
            {
                CreateGridLine(grid.transform, new Vector3(x * cellSize, min * cellSize, 0f), new Vector3(x * cellSize, max * cellSize, 0f));
            }

            for (int y = min; y <= max; y++)
            {
                CreateGridLine(grid.transform, new Vector3(min * cellSize, y * cellSize, 0f), new Vector3(max * cellSize, y * cellSize, 0f));
            }
        }

        private void CreateGridLine(Transform parent, Vector3 start, Vector3 end)
        {
            GameObject line = new GameObject("Line");
            line.transform.SetParent(parent, false);
            LineRenderer lineRenderer = line.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.widthMultiplier = 0.025f;
            lineRenderer.positionCount = 2;
            lineRenderer.sharedMaterial = gridMaterial;
            lineRenderer.startColor = gridColor;
            lineRenderer.endColor = gridColor;
            lineRenderer.sortingOrder = 1;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }

        private Vector3 ToWorldPosition(Vector2Int coord, float z)
        {
            return new Vector3((coord.x + 0.5f) * cellSize, (coord.y + 0.5f) * cellSize, z);
        }

        private static Sprite CreateSquareSprite(out Texture2D texture)
        {
            texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private static Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Sprites/Default"));
            material.color = color;
            return material;
        }

        private static void DestroyImmediateOrDeferred(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }

        private static void DestroyAsset(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }

        private sealed class ActiveEntityAnimation
        {
            private readonly ClientAnimationEvent animationEvent;
            private readonly Vector3 startScale;
            private readonly float cellSize;
            private readonly SharedRotateAnimationGroup rotateGroup;
            private float elapsed;

            private ActiveEntityAnimation(ClientAnimationEvent animationEvent, Vector3 startPosition, Vector3 endPosition, Vector3 startScale, float cellSize, SharedRotateAnimationGroup rotateGroup, string rotateGroupKey)
            {
                this.animationEvent = animationEvent;
                StartPosition = startPosition;
                EndPosition = endPosition;
                this.startScale = startScale;
                this.cellSize = cellSize;
                this.rotateGroup = rotateGroup;
                RotateGroupKey = rotateGroupKey;
                elapsed = 0f;
            }

            public Vector3 StartPosition { get; }
            public Quaternion Rotation { get; private set; } = Quaternion.identity;
            public string RotateGroupKey { get; }
            public float DurationSeconds => animationEvent.Style.DurationSeconds;
            public ClientAnimationEasing Easing => animationEvent.Style.Easing;
            private Vector3 EndPosition { get; }

            public static ActiveEntityAnimation Create(ClientAnimationEvent animationEvent, Vector3 startPosition, Vector3 endPosition, float cellSize)
                => Create(animationEvent, startPosition, endPosition, cellSize, null, string.Empty);

            public static ActiveEntityAnimation Create(ClientAnimationEvent animationEvent, Vector3 startPosition, Vector3 endPosition, float cellSize, SharedRotateAnimationGroup rotateGroup, string rotateGroupKey)
            {
                if (animationEvent.IsImpulse)
                {
                    endPosition = startPosition + DirectionOffset(animationEvent.ImpulseDirection, cellSize * 0.32f);
                }

                return new ActiveEntityAnimation(animationEvent, startPosition, animationEvent.Style.Instant ? endPosition : endPosition, Vector3.one * (cellSize * 0.8f), cellSize, rotateGroup, rotateGroupKey);
            }

            public bool Advance(float deltaTime, Vector3 finalPosition, Vector3 finalScale, Color baseColor, out Vector3 position, out Vector3 scale, out Color color)
            {
                if (animationEvent.Style.Instant || animationEvent.Style.DurationSeconds <= 0f)
                {
                    Rotation = Quaternion.identity;
                    position = finalPosition;
                    scale = finalScale;
                    color = Color.Lerp(baseColor, animationEvent.Style.FlashColor, 0.35f);
                    return false;
                }

                elapsed += Mathf.Max(0f, deltaTime);
                float normalized = Mathf.Clamp01(elapsed / animationEvent.Style.DurationSeconds);
                float eased = Ease(normalized, animationEvent.Style.Easing);
                if (animationEvent.IsRotatePivot)
                {
                    float rotationProgress = rotateGroup == null ? eased : rotateGroup.FrameProgress;
                    position = EvaluateRotatePivot(animationEvent, rotationProgress, finalPosition, out Quaternion rotation);
                    Rotation = rotation;
                }
                else
                {
                    position = animationEvent.IsImpulse ? Vector3.Lerp(StartPosition, EndPosition, Mathf.Sin(eased * Mathf.PI)) : Vector3.Lerp(StartPosition, EndPosition, eased);
                    Rotation = Quaternion.identity;
                }

                float pulse = Mathf.Sin(normalized * Mathf.PI);
                float feedback = Mathf.Lerp(1f, animationEvent.Style.ScaleFeedback, pulse);
                scale = startScale * feedback;
                color = Color.Lerp(baseColor, animationEvent.Style.FlashColor, pulse * 0.45f);
                if (normalized >= 1f)
                {
                    Rotation = Quaternion.identity;
                    position = finalPosition;
                    scale = finalScale;
                    color = baseColor;
                    return false;
                }

                return true;
            }

            public float PreviewElapsed(float deltaTime)
            {
                if (animationEvent.Style.Instant || animationEvent.Style.DurationSeconds <= 0f)
                {
                    return animationEvent.Style.DurationSeconds;
                }

                return elapsed + Mathf.Max(0f, deltaTime);
            }

            private Vector3 EvaluateRotatePivot(ClientAnimationEvent animationEvent, float eased, Vector3 finalPosition, out Quaternion rotation)
            {
                if (animationEvent.MotionKind == ClientAnimationMotionKind.RotatePivotBounce)
                {
                    float outAndBack = Mathf.Sin(eased * Mathf.PI);
                    return RotateAroundPivot(animationEvent, outAndBack, out rotation);
                }

                Vector3 rotated = RotateAroundPivot(animationEvent, eased, out rotation);
                if (animationEvent.FromCoord == animationEvent.ToCoord)
                {
                    return finalPosition;
                }

                return rotated;
            }

            private Vector3 RotateAroundPivot(ClientAnimationEvent animationEvent, float normalized, out Quaternion rotation)
            {
                Vector3 pivot = ToWorldPosition(animationEvent.PivotCoord, -0.1f, cellSize);
                Vector3 offset = StartPosition - pivot;
                float sign = animationEvent.RotateDirection == RotatePivotDirection.Clockwise ? -1f : 1f;
                float angle = sign * normalized * 90f;
                float radians = angle * Mathf.Deg2Rad;
                float sin = Mathf.Sin(radians);
                float cos = Mathf.Cos(radians);
                Vector3 rotatedOffset = new Vector3(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos, offset.z);
                Vector3 result = pivot + rotatedOffset;
                result.z = -0.1f;
                rotation = Quaternion.Euler(0f, 0f, angle);
                return result;
            }

            private static float Ease(float value, ClientAnimationEasing easing)
            {
                return easing switch
                {
                    ClientAnimationEasing.EaseOut => 1f - Mathf.Pow(1f - value, 3f),
                    ClientAnimationEasing.EaseInOut => value < 0.5f ? 4f * value * value * value : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f,
                    _ => value
                };
            }

            private static Vector3 DirectionOffset(Direction direction, float distance)
            {
                return direction switch
                {
                    Direction.Left => new Vector3(-distance, 0f, 0f),
                    Direction.Right => new Vector3(distance, 0f, 0f),
                    Direction.Up => new Vector3(0f, distance, 0f),
                    Direction.Down => new Vector3(0f, -distance, 0f),
                    _ => Vector3.zero
                };
            }

            private static Vector3 ToWorldPosition(Vector2Int coord, float z, float cellSize)
            {
                return new Vector3((coord.x + 0.5f) * cellSize, (coord.y + 0.5f) * cellSize, z);
            }
        }

        private sealed class SharedRotateAnimationGroup
        {
            private readonly RotatePivotDirection rotateDirection;
            public SharedRotateAnimationGroup(RotatePivotDirection rotateDirection)
            {
                this.rotateDirection = rotateDirection;
            }

            public float FrameProgress { get; private set; }

            public void ResetFrame()
            {
                FrameProgress = 0f;
            }

            public void Include(float entityElapsed, float duration, ClientAnimationEasing easing)
            {
                FrameProgress = Mathf.Max(FrameProgress, Evaluate(entityElapsed, duration, easing));
            }

            public void CommitFrame()
            {
                FrameProgress = rotateDirection == RotatePivotDirection.Clockwise || rotateDirection == RotatePivotDirection.CounterClockwise ? FrameProgress : 0f;
            }

            private float Evaluate(float entityElapsed, float duration, ClientAnimationEasing easing)
            {
                float eased;
                if (duration <= 0f)
                {
                    eased = 1f;
                }
                else
                {
                    eased = Ease(Mathf.Clamp01(entityElapsed / duration), easing);
                }

                return eased;
            }

            private static float Ease(float value, ClientAnimationEasing easing)
            {
                return easing switch
                {
                    ClientAnimationEasing.EaseOut => 1f - Mathf.Pow(1f - value, 3f),
                    ClientAnimationEasing.EaseInOut => value < 0.5f ? 4f * value * value * value : 1f - Mathf.Pow(-2f * value + 2f, 3f) * 0.5f,
                    _ => value
                };
            }
        }
    }
}
