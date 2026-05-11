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
        private readonly Dictionary<string, LineRenderer> portConnectionLines = new();
        private Transform entityRoot;
        private Transform portConnectionRoot;
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
            DrawGrid();
        }

        private void LateUpdate()
        {
            if (runner == null || runner.Context == null)
            {
                return;
            }

            IReadOnlyList<EntitySnapshot> snapshots = runner.Context.ClientMapWorld.CreateSnapshot();
            runner.Context.AnimationLayer.Enabled = animationLayerEnabled;
            DrainAnimationEvents();
            var seen = new HashSet<long>();
            for (int i = 0; i < snapshots.Count; i++)
            {
                EntitySnapshot snapshot = snapshots[i];
                seen.Add(snapshot.EntityId);
                Transform view = GetOrCreateEntityView(snapshot.EntityId);
                ConfigureEntityView(view, snapshot, Time.deltaTime);
            }

            RemoveMissingViews(seen);
            ConfigurePortConnections(snapshots);
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
            spriteRenderer.sprite = CreateSquareSprite();
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
                    view.localScale = animatedScale;
                    displayColor = animatedColor;
                }
                else
                {
                    activeAnimations.Remove(snapshot.EntityId);
                    view.localPosition = resolvedPosition;
                    view.localScale = resolvedScale;
                }
            }
            else
            {
                view.localPosition = resolvedPosition;
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
            lineRenderer.material = CreateMaterial(portLineColor);
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
            var removed = new List<long>();
            foreach (KeyValuePair<long, Transform> pair in EntityViews)
            {
                if (!seen.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }

            for (int i = 0; i < removed.Count; i++)
            {
                long entityId = removed[i];
                if (EntityViews.TryGetValue(entityId, out Transform view) && view != null)
                {
                    Destroy(view.gameObject);
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
                return;
            }

            latestDrainedEvents.Clear();
            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                latestDrainedEvents[animationEvent.EntityId] = animationEvent;
            }

            foreach (KeyValuePair<long, ClientAnimationEvent> pair in latestDrainedEvents)
            {
                ClientAnimationEvent animationEvent = pair.Value;
                Transform view = GetOrCreateEntityView(animationEvent.EntityId);
                if (animationEvent.MotionKind == ClientAnimationMotionKind.Remove)
                {
                    if (EntityViews.TryGetValue(animationEvent.EntityId, out Transform removedView) && removedView != null)
                    {
                        Destroy(removedView.gameObject);
                    }

                    EntityViews.Remove(animationEvent.EntityId);
                    activeAnimations.Remove(animationEvent.EntityId);
                    continue;
                }

                Vector3 startPosition = activeAnimations.ContainsKey(animationEvent.EntityId)
                    ? view.localPosition
                    : ToWorldPosition(animationEvent.FromCoord, -0.1f);
                ActiveEntityAnimation animation = ActiveEntityAnimation.Create(animationEvent, startPosition, ToWorldPosition(animationEvent.ToCoord, -0.1f), cellSize);
                view.localPosition = animation.StartPosition;
                activeAnimations[animationEvent.EntityId] = animation;
            }
        }

        private void ConfigurePortConnections(IReadOnlyList<EntitySnapshot> snapshots)
        {
            IReadOnlyList<PortDebugConnection> connections = PortDebugVisualizationUtility.FindConnections(snapshots);
            var seen = new HashSet<string>();
            foreach (PortDebugConnection connection in connections)
            {
                seen.Add(connection.Key);
                LineRenderer line = GetOrCreatePortConnectionLine(connection.Key);
                line.enabled = true;
                line.useWorldSpace = false;
                line.loop = false;
                line.widthMultiplier = 0.06f;
                line.positionCount = 2;
                line.material = CreateMaterial(portLineColor);
                line.startColor = portLineColor;
                line.endColor = portLineColor;
                line.sortingOrder = 13;
                line.SetPosition(0, ToWorldPosition(connection.FromCoord, -0.04f));
                line.SetPosition(1, ToWorldPosition(connection.ToCoord, -0.04f));
            }

            foreach (KeyValuePair<string, LineRenderer> pair in portConnectionLines)
            {
                if (!seen.Contains(pair.Key) && pair.Value != null)
                {
                    pair.Value.enabled = false;
                }
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
            portConnectionLines[key] = line;
            return line;
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
            lineRenderer.material = CreateMaterial(gridColor);
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

        private static Sprite CreateSquareSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
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

        private sealed class ActiveEntityAnimation
        {
            private readonly ClientAnimationEvent animationEvent;
            private readonly Vector3 startScale;
            private float elapsed;

            private ActiveEntityAnimation(ClientAnimationEvent animationEvent, Vector3 startPosition, Vector3 endPosition, Vector3 startScale)
            {
                this.animationEvent = animationEvent;
                StartPosition = startPosition;
                EndPosition = endPosition;
                this.startScale = startScale;
                elapsed = 0f;
            }

            public Vector3 StartPosition { get; }
            private Vector3 EndPosition { get; }

            public static ActiveEntityAnimation Create(ClientAnimationEvent animationEvent, Vector3 startPosition, Vector3 endPosition, float cellSize)
            {
                if (animationEvent.IsImpulse)
                {
                    endPosition = startPosition + DirectionOffset(animationEvent.ImpulseDirection, cellSize * 0.32f);
                }

                return new ActiveEntityAnimation(animationEvent, startPosition, animationEvent.Style.Instant ? endPosition : endPosition, Vector3.one * (cellSize * 0.8f));
            }

            public bool Advance(float deltaTime, Vector3 finalPosition, Vector3 finalScale, Color baseColor, out Vector3 position, out Vector3 scale, out Color color)
            {
                if (animationEvent.Style.Instant || animationEvent.Style.DurationSeconds <= 0f)
                {
                    position = finalPosition;
                    scale = finalScale;
                    color = Color.Lerp(baseColor, animationEvent.Style.FlashColor, 0.35f);
                    return false;
                }

                elapsed += Mathf.Max(0f, deltaTime);
                float normalized = Mathf.Clamp01(elapsed / animationEvent.Style.DurationSeconds);
                float eased = Ease(normalized, animationEvent.Style.Easing);
                position = animationEvent.IsImpulse ? Vector3.Lerp(StartPosition, EndPosition, Mathf.Sin(eased * Mathf.PI)) : Vector3.Lerp(StartPosition, EndPosition, eased);
                float pulse = Mathf.Sin(normalized * Mathf.PI);
                float feedback = Mathf.Lerp(1f, animationEvent.Style.ScaleFeedback, pulse);
                scale = startScale * feedback;
                color = Color.Lerp(baseColor, animationEvent.Style.FlashColor, pulse * 0.45f);
                if (normalized >= 1f)
                {
                    position = finalPosition;
                    scale = finalScale;
                    color = baseColor;
                    return false;
                }

                return true;
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
        }
    }
}
