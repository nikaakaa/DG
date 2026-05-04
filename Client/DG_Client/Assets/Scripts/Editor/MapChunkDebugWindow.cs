using DG.GameCore;
using DG.Map;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using CoreMapCoordinate = DG.GameCore.MapCoordinate;
using UnityMapCoordinate = DG.Map.MapCoordinate;

namespace DG.EditorTools
{
    public sealed class MapChunkDebugWindow : EditorWindow
    {
        private static MapChunkDebugWindow Instance;

        private Vector2Int inspectWorldCoord;
        private Vector2Int drawCenterWorldCoord;
        private int chunkRadius = 1;
        private bool drawCells = true;
        private bool drawLabels = true;
        private string entityIndexCheckResult = "";

        [MenuItem("DG/Map/Chunk Debug")]
        private static void Open()
        {
            Instance = GetWindow<MapChunkDebugWindow>("Chunk Debug");
            Instance.Show();
            SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            Instance = this;
            SceneView.duringSceneGui += DrawScene;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneView.duringSceneGui -= DrawScene;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            inspectWorldCoord = EditorGUILayout.Vector2IntField("Inspect World Coord", inspectWorldCoord);
            drawCenterWorldCoord = EditorGUILayout.Vector2IntField("Draw Center World Coord", drawCenterWorldCoord);
            chunkRadius = EditorGUILayout.IntSlider("Chunk Radius", chunkRadius, 0, 4);
            drawCells = EditorGUILayout.Toggle("Draw Cells", drawCells);
            drawLabels = EditorGUILayout.Toggle("Draw Labels", drawLabels);

            Vector2Int chunkCoord = UnityMapCoordinate.ToChunkCoord(inspectWorldCoord);
            Vector2Int localCoord = UnityMapCoordinate.ToLocalCoord(inspectWorldCoord);
            Vector2Int restoredWorldCoord = UnityMapCoordinate.ToWorldCoord(chunkCoord, localCoord);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chunk Coord", chunkCoord.ToString());
            EditorGUILayout.LabelField("Local Coord", localCoord.ToString());
            EditorGUILayout.LabelField("Restored World Coord", restoredWorldCoord.ToString());

            if (GUILayout.Button("Frame Draw Center"))
            {
                Vector3 center = ToScenePosition(drawCenterWorldCoord) + new Vector3(0.5f, 0.5f, 0f);
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    sceneView.Frame(new Bounds(center, Vector3.one * UnityMapCoordinate.ChunkSize), false);
                }
            }

            if (GUILayout.Button("Run Entity Index Check"))
            {
                entityIndexCheckResult = RunEntityIndexCheck();
            }

            if (!string.IsNullOrEmpty(entityIndexCheckResult))
            {
                EditorGUILayout.Space();
                EditorGUILayout.TextArea(entityIndexCheckResult);
            }

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
        }

        private static void DrawScene(SceneView sceneView)
        {
            if (Instance == null)
            {
                return;
            }

            Instance.DrawChunks();
        }

        private void DrawChunks()
        {
            Vector2Int centerChunk = UnityMapCoordinate.ToChunkCoord(drawCenterWorldCoord);

            for (int x = -chunkRadius; x <= chunkRadius; x++)
            {
                for (int y = -chunkRadius; y <= chunkRadius; y++)
                {
                    Vector2Int chunkCoord = new Vector2Int(centerChunk.x + x, centerChunk.y + y);
                    DrawChunk(chunkCoord);
                }
            }

            DrawInspectedCell();
        }

        private void DrawChunk(Vector2Int chunkCoord)
        {
            Vector2Int origin = UnityMapCoordinate.ToWorldCoord(chunkCoord, Vector2Int.zero);
            Vector3 a = ToScenePosition(origin);
            Vector3 b = ToScenePosition(origin + new Vector2Int(UnityMapCoordinate.ChunkSize, 0));
            Vector3 c = ToScenePosition(origin + new Vector2Int(UnityMapCoordinate.ChunkSize, UnityMapCoordinate.ChunkSize));
            Vector3 d = ToScenePosition(origin + new Vector2Int(0, UnityMapCoordinate.ChunkSize));

            Handles.color = new Color(0.1f, 0.8f, 1f, 1f);
            Handles.DrawAAPolyLine(4f, a, b, c, d, a);

            if (drawCells)
            {
                Handles.color = new Color(0.35f, 0.35f, 0.35f, 0.35f);
                for (int i = 1; i < UnityMapCoordinate.ChunkSize; i++)
                {
                    Vector3 verticalA = ToScenePosition(origin + new Vector2Int(i, 0));
                    Vector3 verticalB = ToScenePosition(origin + new Vector2Int(i, UnityMapCoordinate.ChunkSize));
                    Vector3 horizontalA = ToScenePosition(origin + new Vector2Int(0, i));
                    Vector3 horizontalB = ToScenePosition(origin + new Vector2Int(UnityMapCoordinate.ChunkSize, i));
                    Handles.DrawLine(verticalA, verticalB);
                    Handles.DrawLine(horizontalA, horizontalB);
                }
            }

            if (drawLabels)
            {
                Handles.color = Color.white;
                Handles.Label(a + new Vector3(0.25f, 0.25f, 0f), $"Chunk {chunkCoord}");
            }
        }

        private void DrawInspectedCell()
        {
            Vector3 a = ToScenePosition(inspectWorldCoord);
            Vector3 b = ToScenePosition(inspectWorldCoord + new Vector2Int(1, 0));
            Vector3 c = ToScenePosition(inspectWorldCoord + new Vector2Int(1, 1));
            Vector3 d = ToScenePosition(inspectWorldCoord + new Vector2Int(0, 1));

            Handles.color = new Color(1f, 0.85f, 0.1f, 1f);
            Handles.DrawAAPolyLine(5f, a, b, c, d, a);

            if (drawLabels)
            {
                Vector2Int chunkCoord = UnityMapCoordinate.ToChunkCoord(inspectWorldCoord);
                Vector2Int localCoord = UnityMapCoordinate.ToLocalCoord(inspectWorldCoord);
                Handles.Label(a + new Vector3(0.1f, 0.55f, 0f), $"{inspectWorldCoord} / {chunkCoord}:{localCoord}");
            }
        }

        private static Vector3 ToScenePosition(Vector2Int coord)
        {
            return new Vector3(coord.x, coord.y, 0f);
        }

        private static string RunEntityIndexCheck()
        {
            GameWorld world = new GameWorld();
            StringBuilder builder = new StringBuilder();

            AppendCheck(builder, "Add player (0,0)", world.AddEntity(DefaultWorldConfig.PlayerSpawn(1, 1, new GridCoord(0, 0))));
            AppendCheck(builder, "Add ball (2,0)", world.AddEntity(DefaultWorldConfig.BallSpawn(2, new GridCoord(2, 0), Direction.Right, 1)));
            AppendCheck(builder, "Add blocker (40,0)", world.AddEntity(DefaultWorldConfig.BlockerSpawn(3, new GridCoord(40, 0))));
            AppendCellCheck(builder, world, new Vector2Int(0, 0), DefaultWorldConfig.PlayerTarget, 1);
            AppendCellCheck(builder, world, new Vector2Int(2, 0), DefaultWorldConfig.BallTarget, 2);
            AppendCellCheck(builder, world, new Vector2Int(40, 0), DefaultWorldConfig.BlockerTarget, 3);

            GameEntity player = world.GetEntitiesAt(new GridCoord(0, 0), DefaultWorldConfig.PlayerTarget)[0];
            world.MoveEntity(player, new GridCoord(31, 0));
            AppendCellCheck(builder, world, new Vector2Int(31, 0), DefaultWorldConfig.PlayerTarget, 1);

            world.MoveEntity(player, new GridCoord(32, 0));
            AppendCellCheck(builder, world, new Vector2Int(32, 0), DefaultWorldConfig.PlayerTarget, 1);

            world.MoveEntity(player, new GridCoord(-1, 0));
            AppendCellCheck(builder, world, new Vector2Int(-1, 0), DefaultWorldConfig.PlayerTarget, 1);

            SpatialVisibilityDelta delta = world.DiffVisibility(new GridCoord(32, 0), new GridCoord(-1, 0), 16, DefaultWorldConfig.BallTarget, 1);
            AppendCheck(builder, "Ball visibility entered", Contains(delta.Entered, 2));
            AppendCheck(builder, "Ball visibility left empty", delta.Left.Count == 0);

            if (world.TryGetChunkEntities(CoreMapCoordinate.ToChunkCoord(new GridCoord(40, 0)), DefaultWorldConfig.BlockerTarget, out IReadOnlyList<GameEntity> objects))
            {
                AppendCheck(builder, "Blocker chunk target", Contains(objects, 3));
            }
            else
            {
                AppendCheck(builder, "Blocker chunk target", false);
            }

            builder.Append("Dirty Cells: ");
            builder.AppendLine(world.ChangedCells.Count.ToString());
            builder.Append("Dirty Chunks: ");
            builder.AppendLine(world.ChangedChunks.Count.ToString());

            return builder.ToString();
        }

        private static void AppendCheck(StringBuilder builder, string label, bool passed)
        {
            builder.Append(label);
            builder.Append(": ");
            builder.AppendLine(passed ? "Pass" : "Fail");
        }

        private static void AppendCellCheck(StringBuilder builder, GameWorld world, Vector2Int coord, int target, long entityId)
        {
            bool passed = Contains(world.GetEntitiesAt(UnityMapCoordinate.ToGridCoord(coord), target), entityId);

            Vector2Int chunkCoord = UnityMapCoordinate.ToChunkCoord(coord);
            Vector2Int localCoord = UnityMapCoordinate.ToLocalCoord(coord);
            builder.Append(coord);
            builder.Append(" -> ");
            builder.Append(chunkCoord);
            builder.Append(":");
            builder.Append(localCoord);
            builder.Append(" ");
            builder.AppendLine(passed ? "Pass" : "Fail");
        }

        private static bool Contains(IReadOnlyList<GameEntity> entities, long entityId)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].EntityId == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<long> entities, long entityId)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] == entityId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
