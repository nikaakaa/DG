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
        private readonly Dictionary<long, Transform> EntityViews = new();
        private Transform entityRoot;

        private void Awake()
        {
            if (runner == null)
            {
                runner = FindObjectOfType<ClientWorldRunner>();
            }

            entityRoot = new GameObject("Entities").transform;
            entityRoot.SetParent(transform, false);
            DrawGrid();
        }

        private void LateUpdate()
        {
            if (runner == null || runner.Context == null)
            {
                return;
            }

            IReadOnlyList<EntitySnapshot> snapshots = runner.Context.ClientMapWorld.CreateSnapshot();
            var seen = new HashSet<long>();
            for (int i = 0; i < snapshots.Count; i++)
            {
                EntitySnapshot snapshot = snapshots[i];
                seen.Add(snapshot.EntityId);
                Transform view = GetOrCreateEntityView(snapshot.EntityId);
                ConfigureEntityView(view, snapshot);
            }

            RemoveMissingViews(seen);
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

        private void ConfigureEntityView(Transform view, EntitySnapshot snapshot)
        {
            view.localPosition = ToWorldPosition(new Vector2Int(snapshot.X, snapshot.Y), -0.1f);
            view.localScale = Vector3.one * (cellSize * 0.8f);
            SpriteRenderer spriteRenderer = view.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = ResolveColor(snapshot);
            }

            LineRenderer lineRenderer = view.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                ConfigurePortLine(lineRenderer, snapshot);
            }
        }

        private void ConfigurePortLine(LineRenderer lineRenderer, EntitySnapshot snapshot)
        {
            bool showPort = snapshot.ConfigId == DefaultWorldConfig.PortConnectorBlockerConfigId;
            lineRenderer.enabled = showPort;
            if (!showPort)
            {
                return;
            }

            DirectionMask worldPorts = (DirectionMask.Left | DirectionMask.Right).RotateBy(snapshot.Direction);
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
            }
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
    }
}
