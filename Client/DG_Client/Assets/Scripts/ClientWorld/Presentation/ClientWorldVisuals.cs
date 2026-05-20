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
        private readonly PresentationPlaybackScheduler playbackScheduler = new();
        private readonly Dictionary<string, LineRenderer> portConnectionLines = new();
        private readonly HashSet<long> seenEntityIds = new();
        private readonly List<long> removedEntityIds = new();
        private readonly HashSet<string> seenPortConnectionKeys = new();
        private readonly List<string> removedPortConnectionKeys = new();
        private Transform entityRoot;
        private Transform rotateGroupRoot;
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
            rotateGroupRoot = new GameObject("RotatePivotGroups").transform;
            rotateGroupRoot.SetParent(transform, false);
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
            playbackScheduler.Clear();
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
            AdvanceRotateGroups(animationDeltaTime);
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
                if (animationLayerEnabled && playbackScheduler.HasEntityOwner(snapshot.EntityId))
                {
                    displayColor = resolvedColor;
                }
                else if (animationLayerEnabled && activeAnimations.TryGetValue(snapshot.EntityId, out ActiveEntityAnimation animation))
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
                DirectionMask portMask = animationLayerEnabled && TryGetRotateGroupPortMask(snapshot.EntityId, out DirectionMask rotatePortMask)
                    ? rotatePortMask
                    : animationLayerEnabled && activeAnimations.TryGetValue(snapshot.EntityId, out ActiveEntityAnimation portAnimation)
                    ? portAnimation.PortLineMask(snapshot)
                    : PortDebugVisualizationUtility.GetWorldPorts(snapshot);
                bool showPort = animationLayerEnabled && TryGetRotateGroupPortMask(snapshot.EntityId, out _)
                    ? portMask != DirectionMask.None
                    : snapshot.PortLocalPorts != DirectionMask.None;
                ConfigurePortLine(lineRenderer, showPort, portMask);
            }
        }

        private bool TryGetRotateGroupPortMask(long entityId, out DirectionMask ports)
        {
            ports = DirectionMask.None;
            if (!playbackScheduler.TryGetGroupTrackByEntity(entityId, out RigidBodyGroupTrack runtime))
            {
                return false;
            }

            return runtime.TryGetPortMask(entityId, out ports);
        }

        private void ConfigurePortLine(LineRenderer lineRenderer, EntitySnapshot snapshot)
            => ConfigurePortLine(lineRenderer, PortDebugVisualizationUtility.HasPorts(snapshot), PortDebugVisualizationUtility.GetWorldPorts(snapshot));

        private void ConfigurePortLine(LineRenderer lineRenderer, bool showPort, DirectionMask ports)
        {
            lineRenderer.enabled = showPort;
            if (!showPort)
            {
                return;
            }

            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.widthMultiplier = 0.12f;
            lineRenderer.positionCount = 2;
            lineRenderer.sharedMaterial = portLineMaterial;
            lineRenderer.startColor = portLineColor;
            lineRenderer.endColor = portLineColor;
            lineRenderer.sortingOrder = 12;
            if (ports.Contains(Direction.Up) || ports.Contains(Direction.Down))
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
                ClearRotateGroupContaining(entityId);
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
                ClearRotateAnimationGroups();
                return;
            }

            while (runner.Context.AnimationLayer.TryDequeueRotateGroup(out RotatePivotGroupPlaybackPlan rotatePlan))
            {
                StartRotateGroup(rotatePlan);
            }

            while (runner.Context.AnimationLayer.TryDequeueTranslateGroup(out TranslateGroupPlaybackPlan translatePlan))
            {
                StartTranslateGroup(translatePlan);
            }

            while (runner.Context.AnimationLayer.TryDequeue(out ClientAnimationEvent animationEvent))
            {
                if (playbackScheduler.HasEntityOwner(animationEvent.EntityId) && animationEvent.MotionKind != ClientAnimationMotionKind.Remove)
                {
                    continue;
                }

                if (latestDrainedEvents.TryGetValue(animationEvent.EntityId, out ClientAnimationEvent existingEvent) &&
                    ShouldKeepExistingAnimationEvent(existingEvent, animationEvent))
                {
                    continue;
                }

                latestDrainedEvents[animationEvent.EntityId] = animationEvent;
            }

            foreach (KeyValuePair<long, ClientAnimationEvent> pair in latestDrainedEvents)
            {
                ClientAnimationEvent animationEvent = pair.Value;
                if (animationEvent.MotionKind == ClientAnimationMotionKind.Remove)
                {
                    ClearRotateGroupContaining(animationEvent.EntityId);
                    if (EntityViews.TryGetValue(animationEvent.EntityId, out Transform removedView) && removedView != null)
                    {
                        DestroyImmediateOrDeferred(removedView.gameObject);
                    }

                    EntityViews.Remove(animationEvent.EntityId);
                    activeAnimations.Remove(animationEvent.EntityId);
                    continue;
                }

                Transform view = GetOrCreateEntityView(animationEvent.EntityId);
                Vector3 startPosition = activeAnimations.ContainsKey(animationEvent.EntityId)
                    ? view.localPosition
                    : ToWorldPosition(animationEvent.FromCoord, -0.1f);
                ActiveEntityAnimation animation = ActiveEntityAnimation.Create(animationEvent, startPosition, ToWorldPosition(animationEvent.ToCoord, -0.1f), cellSize);
                view.localPosition = animation.StartPosition;
                activeAnimations[animationEvent.EntityId] = animation;
            }
        }

        private static bool ShouldKeepExistingAnimationEvent(ClientAnimationEvent existingEvent, ClientAnimationEvent incomingEvent)
        {
            if (incomingEvent.MotionKind == ClientAnimationMotionKind.Remove)
            {
                return false;
            }

            if (existingEvent.MotionKind == ClientAnimationMotionKind.Remove)
            {
                return true;
            }

            return existingEvent.IsRotatePivot && !incomingEvent.IsRotatePivot;
        }

        private void CleanupUnusedRotateAnimationGroups()
        {
            removedPortConnectionKeys.Clear();
            foreach (KeyValuePair<string, RigidBodyGroupTrack> pair in playbackScheduler.GroupTracks)
            {
                if (pair.Value.Completed)
                {
                    removedPortConnectionKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < removedPortConnectionKeys.Count; i++)
            {
                playbackScheduler.RemoveCompletedTrack(removedPortConnectionKeys[i]);
            }
        }

        private void ClearRotateAnimationGroups()
        {
            foreach (KeyValuePair<string, RigidBodyGroupTrack> pair in playbackScheduler.GroupTracks)
            {
                pair.Value.Restore(entityRoot);
                DestroyImmediateOrDeferred(pair.Value.Root.gameObject);
            }

            playbackScheduler.Clear();
        }

        private void ClearRotateGroupContaining(long entityId)
        {
            if (!playbackScheduler.TryGetTrackIdByEntity(entityId, out string groupId))
            {
                return;
            }

            playbackScheduler.InterruptTrack(groupId, entityRoot, DestroyImmediateOrDeferred);
        }

        private void StartRotateGroup(RotatePivotGroupPlaybackPlan plan)
        {
            if (plan.Members.Count == 0 || string.IsNullOrEmpty(plan.GroupId))
            {
                return;
            }

            for (int i = 0; i < plan.Members.Count; i++)
            {
                playbackScheduler.InterruptTrackContaining(plan.Members[i].EntityId, entityRoot, DestroyImmediateOrDeferred);
            }

            GameObject rootObject = new GameObject("RotatePivotGroup_" + plan.GroupId);
            Transform root = rootObject.transform;
            root.SetParent(rotateGroupRoot, false);
            root.localPosition = ToWorldPosition(plan.PivotCoord, -0.1f);
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            var runtime = new RigidBodyGroupTrack(plan, root, cellSize);
            for (int i = 0; i < plan.Members.Count; i++)
            {
                RotatePivotGroupMemberPlaybackPlan member = plan.Members[i];
                Transform view = GetOrCreateEntityView(member.EntityId);
                activeAnimations.Remove(member.EntityId);
                runtime.AddMember(member, view, entityRoot);
            }

            playbackScheduler.StartGroupTrack(runtime);
        }

        private void StartTranslateGroup(TranslateGroupPlaybackPlan plan)
        {
            if (plan.Members.Count == 0 || string.IsNullOrEmpty(plan.GroupId))
            {
                return;
            }

            for (int i = 0; i < plan.Members.Count; i++)
            {
                playbackScheduler.InterruptTrackContaining(plan.Members[i].EntityId, entityRoot, DestroyImmediateOrDeferred);
            }

            GameObject rootObject = new GameObject("TranslateGroup_" + plan.GroupId);
            Transform root = rootObject.transform;
            root.SetParent(rotateGroupRoot, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            var runtime = new RigidBodyGroupTrack(plan, root, cellSize);
            for (int i = 0; i < plan.Members.Count; i++)
            {
                RotatePivotGroupMemberPlaybackPlan member = plan.Members[i];
                Transform view = GetOrCreateEntityView(member.EntityId);
                activeAnimations.Remove(member.EntityId);
                runtime.AddMember(member, view, entityRoot);
            }

            playbackScheduler.StartGroupTrack(runtime);
        }

        private void AdvanceRotateGroups(float deltaTime)
        {
            removedPortConnectionKeys.Clear();
            foreach (KeyValuePair<string, RigidBodyGroupTrack> pair in playbackScheduler.GroupTracks)
            {
                pair.Value.Advance(deltaTime);
                if (!pair.Value.Completed)
                {
                    continue;
                }

                pair.Value.Restore(entityRoot);
                DestroyImmediateOrDeferred(pair.Value.Root.gameObject);
                removedPortConnectionKeys.Add(pair.Key);
            }

            for (int i = 0; i < removedPortConnectionKeys.Count; i++)
            {
                string groupId = removedPortConnectionKeys[i];
                playbackScheduler.RemoveCompletedTrack(groupId);
            }
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
                if (playbackScheduler.HasConnectionOwner(connection.Key))
                {
                    line.SetPosition(0, ResolveConnectionPosition(connection.FromEntityId, connection.FromCoord));
                    line.SetPosition(1, ResolveConnectionPosition(connection.ToEntityId, connection.ToCoord));
                    continue;
                }

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
                Vector3 position = transform.InverseTransformPoint(view.position);
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
            private float elapsed;

            private ActiveEntityAnimation(ClientAnimationEvent animationEvent, Vector3 startPosition, Vector3 endPosition, Vector3 startScale, float cellSize)
            {
                this.animationEvent = animationEvent;
                StartPosition = startPosition;
                EndPosition = endPosition;
                this.startScale = startScale;
                elapsed = 0f;
            }

            public Vector3 StartPosition { get; }
            public Quaternion Rotation { get; private set; } = Quaternion.identity;
            public float DurationSeconds => animationEvent.Style.DurationSeconds;
            private Vector3 EndPosition { get; }

            public static ActiveEntityAnimation Create(ClientAnimationEvent animationEvent, Vector3 startPosition, Vector3 endPosition, float cellSize)
            {
                if (animationEvent.IsImpulse)
                {
                    endPosition = startPosition + DirectionOffset(animationEvent.ImpulseDirection, cellSize * 0.32f);
                }

                return new ActiveEntityAnimation(animationEvent, startPosition, animationEvent.Style.Instant ? endPosition : endPosition, Vector3.one * (cellSize * 0.8f), cellSize);
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
                position = animationEvent.IsImpulse ? Vector3.Lerp(StartPosition, EndPosition, Mathf.Sin(eased * Mathf.PI)) : Vector3.Lerp(StartPosition, EndPosition, eased);
                Rotation = Quaternion.identity;

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

            public DirectionMask PortLineMask(EntitySnapshot snapshot)
            {
                return PortDebugVisualizationUtility.GetWorldPorts(snapshot);
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

        private sealed class PresentationPlaybackScheduler
        {
            private readonly Dictionary<string, RigidBodyGroupTrack> groupTracks = new();
            private readonly Dictionary<long, string> entityOwners = new();
            private readonly Dictionary<string, string> connectionOwners = new();

            public IReadOnlyDictionary<string, RigidBodyGroupTrack> GroupTracks => groupTracks;

            public void StartGroupTrack(RigidBodyGroupTrack track)
            {
                if (track == null || string.IsNullOrEmpty(track.TrackId))
                {
                    return;
                }

                groupTracks[track.TrackId] = track;
                for (int i = 0; i < track.MemberEntityIds.Count; i++)
                {
                    entityOwners[track.MemberEntityIds[i]] = track.TrackId;
                }

                for (int first = 0; first < track.MemberEntityIds.Count; first++)
                {
                    for (int second = first + 1; second < track.MemberEntityIds.Count; second++)
                    {
                        connectionOwners[ConnectionKey(track.MemberEntityIds[first], track.MemberEntityIds[second])] = track.TrackId;
                    }
                }
            }

            public bool HasEntityOwner(long entityId)
            {
                return entityOwners.ContainsKey(entityId);
            }

            public bool HasConnectionOwner(string connectionKey)
            {
                return connectionOwners.ContainsKey(connectionKey);
            }

            public bool TryGetTrackIdByEntity(long entityId, out string trackId)
            {
                return entityOwners.TryGetValue(entityId, out trackId);
            }

            public bool TryGetGroupTrackByEntity(long entityId, out RigidBodyGroupTrack track)
            {
                track = null;
                return entityOwners.TryGetValue(entityId, out string trackId) && groupTracks.TryGetValue(trackId, out track);
            }

            public void InterruptTrackContaining(long entityId, Transform entityRoot, System.Action<Object> destroy)
            {
                if (entityOwners.TryGetValue(entityId, out string trackId))
                {
                    InterruptTrack(trackId, entityRoot, destroy);
                }
            }

            public void InterruptTrack(string trackId, Transform entityRoot, System.Action<Object> destroy)
            {
                if (string.IsNullOrEmpty(trackId) || !groupTracks.TryGetValue(trackId, out RigidBodyGroupTrack track))
                {
                    return;
                }

                track.Interrupt();
                track.Restore(entityRoot);
                if (track.Root != null)
                {
                    destroy?.Invoke(track.Root.gameObject);
                }

                RemoveCompletedTrack(trackId);
            }

            public void RemoveCompletedTrack(string trackId)
            {
                if (string.IsNullOrEmpty(trackId))
                {
                    return;
                }

                groupTracks.Remove(trackId);
                foreach (long entityId in new List<long>(entityOwners.Keys))
                {
                    if (entityOwners[entityId] == trackId)
                    {
                        entityOwners.Remove(entityId);
                    }
                }

                foreach (string connectionKey in new List<string>(connectionOwners.Keys))
                {
                    if (connectionOwners[connectionKey] == trackId)
                    {
                        connectionOwners.Remove(connectionKey);
                    }
                }
            }

            public void Clear()
            {
                groupTracks.Clear();
                entityOwners.Clear();
                connectionOwners.Clear();
            }

            private static string ConnectionKey(long first, long second)
            {
                return first <= second ? first + ":" + second : second + ":" + first;
            }
        }

        private sealed class RigidBodyGroupTrack
        {
            private readonly RotatePivotGroupPlaybackPlan plan;
            private readonly TranslateGroupPlaybackPlan translatePlan;
            private readonly RigidBodyGroupMotionKind motionKind;
            private readonly float cellSize;
            private readonly List<MemberRuntime> members = new();
            private float elapsed;

            public RigidBodyGroupTrack(RotatePivotGroupPlaybackPlan plan, Transform root, float cellSize)
            {
                this.plan = plan;
                motionKind = RigidBodyGroupMotionKind.RotateAroundPivot;
                Root = root;
                this.cellSize = cellSize;
            }

            public RigidBodyGroupTrack(TranslateGroupPlaybackPlan plan, Transform root, float cellSize)
            {
                translatePlan = plan;
                motionKind = RigidBodyGroupMotionKind.Translate;
                Root = root;
                this.cellSize = cellSize;
            }

            public string TrackId => motionKind == RigidBodyGroupMotionKind.Translate ? translatePlan.GroupId : plan.GroupId;
            public Transform Root { get; }
            public bool Completed { get; private set; }
            public bool Interrupted { get; private set; }
            public float DurationSeconds => motionKind == RigidBodyGroupMotionKind.Translate ? translatePlan.Style.DurationSeconds : plan.Style.DurationSeconds;
            public IReadOnlyList<long> MemberEntityIds
            {
                get
                {
                    var result = new long[members.Count];
                    for (int i = 0; i < members.Count; i++)
                    {
                        result[i] = members[i].Plan.EntityId;
                    }

                    return result;
                }
            }

            public void AddMember(RotatePivotGroupMemberPlaybackPlan member, Transform view, Transform originalParent)
            {
                Vector3 worldStart = ToWorldPosition(member.FromCoord, -0.1f, cellSize);
                view.SetParent(originalParent, false);
                view.localPosition = worldStart;
                view.localRotation = Quaternion.identity;
                view.SetParent(Root, true);
                members.Add(new MemberRuntime(member, view, originalParent));
            }

            public void Advance(float deltaTime)
            {
                if (Completed || Interrupted)
                {
                    return;
                }

                if (motionKind == RigidBodyGroupMotionKind.RotateAroundPivot && (plan.Style.Instant || plan.Style.DurationSeconds <= 0f) ||
                    motionKind == RigidBodyGroupMotionKind.Translate && (translatePlan.Style.Instant || translatePlan.Style.DurationSeconds <= 0f))
                {
                    ApplyProgress(1f);
                    Completed = true;
                    return;
                }

                elapsed += Mathf.Max(0f, deltaTime);
                float normalized = Mathf.Clamp01(elapsed / DurationSeconds);
                ClientAnimationEasing easing = motionKind == RigidBodyGroupMotionKind.Translate ? translatePlan.Style.Easing : plan.Style.Easing;
                float eased = Ease(normalized, easing);
                float progress = motionKind == RigidBodyGroupMotionKind.RotateAroundPivot && plan.Bounce ? BounceProgress(eased) : eased;
                ApplyProgress(progress);
                if (normalized >= 1f)
                {
                    Completed = true;
                }
            }

            public void Interrupt()
            {
                Interrupted = true;
                Completed = true;
            }

            public void SetElapsedForTests(float value)
            {
                elapsed = Mathf.Max(0f, value);
            }

            public void Restore(Transform entityRoot)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    MemberRuntime member = members[i];
                    if (member.View == null)
                    {
                        continue;
                    }

                    member.View.SetParent(member.OriginalParent == null ? entityRoot : member.OriginalParent, false);
                    member.View.localPosition = ToWorldPosition(member.Plan.ToCoord, -0.1f, cellSize);
                    member.View.localRotation = Quaternion.identity;
                    member.View.localScale = Vector3.one * (cellSize * 0.8f);
                }
            }

            public bool TryGetPortMask(long entityId, out DirectionMask ports)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    MemberRuntime member = members[i];
                    if (member.Plan.EntityId == entityId)
                    {
                        ports = member.Plan.FromPortLocalPorts.RotateBy(member.Plan.FromDirection);
                        return true;
                    }
                }

                ports = DirectionMask.None;
                return false;
            }

            private void ApplyProgress(float progress)
            {
                if (motionKind == RigidBodyGroupMotionKind.Translate)
                {
                    ApplyTranslate(progress);
                    return;
                }

                float angle = Angle(progress);
                Root.localRotation = Quaternion.Euler(0f, 0f, angle);
                for (int i = 0; i < members.Count; i++)
                {
                    MemberRuntime member = members[i];
                    if (member.View == null)
                    {
                        continue;
                    }

                    float pulse = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
                    float feedback = Mathf.Lerp(1f, plan.Style.ScaleFeedback, pulse);
                    member.View.localScale = Vector3.one * (cellSize * 0.8f * feedback);
                }
            }

            private void ApplyTranslate(float progress)
            {
                Root.localRotation = Quaternion.identity;
                Root.localPosition = Vector3.zero;
                for (int i = 0; i < members.Count; i++)
                {
                    MemberRuntime member = members[i];
                    if (member.View == null)
                    {
                        continue;
                    }

                    Vector3 from = ToWorldPosition(member.Plan.FromCoord, -0.1f, cellSize);
                    Vector3 to = ToWorldPosition(member.Plan.ToCoord, -0.1f, cellSize);
                    member.View.localPosition = member.Plan.FromCoord == member.Plan.ToCoord && translatePlan.Direction != Direction.None
                        ? Vector3.Lerp(from, from + DirectionOffset(translatePlan.Direction, cellSize * 0.32f), Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI))
                        : Vector3.Lerp(from, to, progress);
                    float pulse = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
                    float feedback = Mathf.Lerp(1f, translatePlan.Style.ScaleFeedback, pulse);
                    member.View.localScale = Vector3.one * (cellSize * 0.8f * feedback);
                }
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

            private float Angle(float progress)
            {
                if (plan.RotateDirection != RotatePivotDirection.Clockwise && plan.RotateDirection != RotatePivotDirection.CounterClockwise)
                {
                    return 0f;
                }

                float sign = plan.RotateDirection == RotatePivotDirection.Clockwise ? -1f : 1f;
                return sign * progress * 90f;
            }

            private float BounceProgress(float normalized)
            {
                float contact = ContactProgress();
                if (normalized <= contact)
                {
                    return contact <= 0f ? 1f : Mathf.Clamp01(normalized / contact);
                }

                float returnProgress = Mathf.Clamp01((normalized - contact) / Mathf.Max(0.0001f, 1f - contact));
                return 1f - returnProgress;
            }

            private float ContactProgress()
            {
                if (plan.ContactProgress > 0d && plan.ContactProgress < 1d)
                {
                    return (float)plan.ContactProgress;
                }

                if (plan.ContactTick > plan.StartTick && plan.EndTick > plan.StartTick && plan.ContactTick < plan.EndTick)
                {
                    return Mathf.Clamp01((float)(plan.ContactTick - plan.StartTick) / (plan.EndTick - plan.StartTick));
                }

                return 0.5f;
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

            private static Vector3 ToWorldPosition(Vector2Int coord, float z, float cellSize)
            {
                return new Vector3((coord.x + 0.5f) * cellSize, (coord.y + 0.5f) * cellSize, z);
            }

            private readonly struct MemberRuntime
            {
                public MemberRuntime(RotatePivotGroupMemberPlaybackPlan plan, Transform view, Transform originalParent)
                {
                    Plan = plan;
                    View = view;
                    OriginalParent = originalParent;
                }

                public RotatePivotGroupMemberPlaybackPlan Plan { get; }
                public Transform View { get; }
                public Transform OriginalParent { get; }
            }
        }
    }
}
