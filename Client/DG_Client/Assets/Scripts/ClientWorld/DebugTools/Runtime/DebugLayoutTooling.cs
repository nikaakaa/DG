using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.GameCore;
using UnityEngine;

namespace DG.Map
{
    public enum DebugLayoutToolState
    {
        SingleCellTool = 0,
        Selecting = 1,
        SelectionPreview = 2,
        DraggingSelection = 3,
        StructureGhostPlacement = 4,
        SubmittingBatch = 5
    }

    public readonly struct DebugBatchMoveRequest
    {
        public DebugBatchMoveRequest(long entityId, Vector2Int targetCoord)
        {
            EntityId = entityId;
            TargetCoord = targetCoord;
        }

        public long EntityId { get; }
        public Vector2Int TargetCoord { get; }
    }

    public readonly struct PortDebugConnection
    {
        public PortDebugConnection(long fromEntityId, long toEntityId, Direction direction, Vector2Int fromCoord, Vector2Int toCoord)
        {
            FromEntityId = fromEntityId;
            ToEntityId = toEntityId;
            Direction = direction;
            FromCoord = fromCoord;
            ToCoord = toCoord;
        }

        public long FromEntityId { get; }
        public long ToEntityId { get; }
        public Direction Direction { get; }
        public Vector2Int FromCoord { get; }
        public Vector2Int ToCoord { get; }
        public string Key => FromEntityId < ToEntityId ? FromEntityId + ":" + ToEntityId : ToEntityId + ":" + FromEntityId;
    }

    public readonly struct DebugStructureGhostCell
    {
        public DebugStructureGhostCell(Vector2Int coord, DirectionMask worldPorts, bool hasInternalConnection, DirectionMask boundaryPorts)
        {
            Coord = coord;
            WorldPorts = worldPorts;
            HasInternalConnection = hasInternalConnection;
            BoundaryPorts = boundaryPorts;
        }

        public Vector2Int Coord { get; }
        public DirectionMask WorldPorts { get; }
        public bool HasInternalConnection { get; }
        public DirectionMask BoundaryPorts { get; }
    }

    public sealed class DebugLayoutSelectionSet
    {
        private readonly Dictionary<long, EntitySnapshot> snapshots = new();

        public int Count => snapshots.Count;
        public IReadOnlyCollection<long> EntityIds => snapshots.Keys;

        public bool Add(EntitySnapshot snapshot)
        {
            if (snapshot.EntityId == 0)
            {
                return false;
            }

            bool exists = snapshots.ContainsKey(snapshot.EntityId);
            snapshots[snapshot.EntityId] = snapshot;
            return !exists;
        }

        public bool Remove(long entityId)
        {
            return snapshots.Remove(entityId);
        }

        public void Clear()
        {
            snapshots.Clear();
        }

        public bool Contains(long entityId)
        {
            return snapshots.ContainsKey(entityId);
        }

        public IReadOnlyList<EntitySnapshot> Snapshots()
        {
            return snapshots.Values.OrderBy(snapshot => snapshot.EntityId).ToArray();
        }

        public Vector2Int Anchor()
        {
            if (snapshots.Count == 0)
            {
                return Vector2Int.zero;
            }

            return new Vector2Int(snapshots.Values.Min(snapshot => snapshot.X), snapshots.Values.Min(snapshot => snapshot.Y));
        }

        public int RemoveMissing(ClientMapWorld world)
        {
            if (world == null)
            {
                int count = snapshots.Count;
                snapshots.Clear();
                return count;
            }

            var removed = new List<long>();
            foreach (long entityId in snapshots.Keys)
            {
                if (!world.TryGetSnapshot(entityId, out EntitySnapshot snapshot))
                {
                    removed.Add(entityId);
                    continue;
                }

                snapshots[entityId] = snapshot;
            }

            for (int i = 0; i < removed.Count; i++)
            {
                snapshots.Remove(removed[i]);
            }

            return removed.Count;
        }

        public DebugStructureBlockDocument ToStructureBlock(string name)
        {
            return DebugStructureBlockStorage.FromSnapshots(name, Snapshots());
        }

        public DebugStructureBlockDocument ToStructureBlock(string name, ClientMapWorld world)
        {
            RuntimeEffectStore runtimeEffects = world?.CoreWorld.RuntimeEffects;
            long tick = world?.CoreWorld.ServerTick ?? 0;
            return DebugStructureBlockStorage.FromSnapshots(name, Snapshots(), runtimeEffects, tick);
        }

        public DebugStructureBlockDocument ToStructureBlock(string name, ClientMapWorld world, IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects)
        {
            RuntimeEffectStore worldRuntimeEffects = world?.CoreWorld.RuntimeEffects;
            long tick = world?.CoreWorld.ServerTick ?? 0;
            IReadOnlyList<DebugStructureRuntimeEffectRecord> merged = MergeRuntimeEffects(worldRuntimeEffects, tick, runtimeEffects);
            return DebugStructureBlockStorage.FromSnapshots(name, Snapshots(), merged);
        }

        public DebugStructureBlockDocument ToStructureBlock(string name, IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects)
        {
            return DebugStructureBlockStorage.FromSnapshots(name, Snapshots(), runtimeEffects);
        }

        private static IReadOnlyList<DebugStructureRuntimeEffectRecord> MergeRuntimeEffects(RuntimeEffectStore worldRuntimeEffects, long tick, IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects)
        {
            var merged = new List<DebugStructureRuntimeEffectRecord>();
            var seen = new HashSet<string>();
            if (worldRuntimeEffects != null)
            {
                foreach (RuntimeEffectInstance effect in worldRuntimeEffects.ActiveAt(tick))
                {
                    var record = new DebugStructureRuntimeEffectRecord(effect.TargetEntityId, effect.Kind, effect.Spec.AutoMoveIntervalTicks, effect.Spec.PortMask);
                    merged.Add(record);
                    seen.Add(RuntimeEffectRecordKey(record));
                }
            }

            if (runtimeEffects != null)
            {
                for (int i = 0; i < runtimeEffects.Count; i++)
                {
                    DebugStructureRuntimeEffectRecord record = runtimeEffects[i];
                    if (seen.Add(RuntimeEffectRecordKey(record)))
                    {
                        merged.Add(record);
                    }
                }
            }

            return merged;
        }

        private static string RuntimeEffectRecordKey(DebugStructureRuntimeEffectRecord record)
        {
            return record.EntityId + ":" + (int)record.Kind + ":" + (int)record.PortLocalPorts;
        }

        public IReadOnlyList<DebugBatchMoveRequest> CreateMoveRequests(Vector2Int targetAnchor, ClientMapWorld world, out IReadOnlyList<string> skippedReasons)
        {
            var requests = new List<DebugBatchMoveRequest>();
            var skipped = new List<string>();
            Vector2Int anchor = Anchor();
            foreach (EntitySnapshot snapshot in Snapshots())
            {
                EntitySnapshot source = snapshot;
                if (world != null)
                {
                    if (!world.TryGetSnapshot(snapshot.EntityId, out EntitySnapshot current))
                    {
                        skipped.Add("entity missing: " + snapshot.EntityId);
                        continue;
                    }

                    source = current;
                }

                if (source.EntityId == 0)
                {
                    skipped.Add("entity missing: " + snapshot.EntityId);
                    continue;
                }

                Vector2Int offset = new Vector2Int(snapshot.X - anchor.x, snapshot.Y - anchor.y);
                requests.Add(new DebugBatchMoveRequest(source.EntityId, targetAnchor + offset));
            }

            skippedReasons = skipped;
            return requests;
        }
    }

    public static class DebugLayoutPaths
    {
        public const string DefaultFileName = "last.dgdebuglayout.json";

        public static string DefaultDirectory()
        {
            return Path.Combine(Application.dataPath, "DebugLayouts");
        }

        public static string DefaultFilePath()
        {
            return Path.Combine(DefaultDirectory(), DefaultFileName);
        }

        public static string NamedFilePath(string name)
        {
            return Path.Combine(DefaultDirectory(), name + DebugStructureBlockStorage.FileExtension);
        }
    }

    public static class DebugLayoutTooling
    {
        public static bool TrySnapshotAt(ClientMapWorld world, Vector2Int coord, out EntitySnapshot snapshot)
        {
            snapshot = default;
            if (world == null)
            {
                return false;
            }

            foreach (EntitySnapshot candidate in world.CreateSnapshot())
            {
                if (candidate.X == coord.x && candidate.Y == coord.y)
                {
                    snapshot = candidate;
                    return true;
                }
            }

            return false;
        }

        public static int SelectRectangle(DebugLayoutSelectionSet selection, ClientMapWorld world, Vector2Int a, Vector2Int b, bool append)
        {
            if (selection == null || world == null)
            {
                return 0;
            }

            if (!append)
            {
                selection.Clear();
            }

            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minY = Mathf.Min(a.y, b.y);
            int maxY = Mathf.Max(a.y, b.y);
            int added = 0;
            foreach (EntitySnapshot snapshot in world.CreateSnapshot())
            {
                if (snapshot.X < minX || snapshot.X > maxX || snapshot.Y < minY || snapshot.Y > maxY)
                {
                    continue;
                }

                if (selection.Add(snapshot))
                {
                    added++;
                }
            }

            return added;
        }

        public static IReadOnlyList<DebugStructureSpawnRequest> CreateCopyRequests(DebugLayoutSelectionSet selection, Vector2Int targetAnchor)
        {
            return CreateCopyRequests(selection, (ClientMapWorld)null, targetAnchor);
        }

        public static IReadOnlyList<DebugStructureSpawnRequest> CreateCopyRequests(DebugLayoutSelectionSet selection, ClientMapWorld world, Vector2Int targetAnchor)
        {
            if (selection == null)
            {
                return Array.Empty<DebugStructureSpawnRequest>();
            }

            DebugStructureBlockDocument document = selection.ToStructureBlock("selection", world);
            return CreateCopyRequests(document, targetAnchor);
        }

        public static IReadOnlyList<DebugStructureSpawnRequest> CreateCopyRequests(DebugLayoutSelectionSet selection, ClientMapWorld world, IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects, Vector2Int targetAnchor)
        {
            if (selection == null)
            {
                return Array.Empty<DebugStructureSpawnRequest>();
            }

            return CreateCopyRequests(selection.ToStructureBlock("selection", world, runtimeEffects), targetAnchor);
        }

        public static IReadOnlyList<DebugStructureSpawnRequest> CreateCopyRequests(DebugLayoutSelectionSet selection, IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects, Vector2Int targetAnchor)
        {
            if (selection == null)
            {
                return Array.Empty<DebugStructureSpawnRequest>();
            }

            return CreateCopyRequests(selection.ToStructureBlock("selection", runtimeEffects), targetAnchor);
        }

        private static IReadOnlyList<DebugStructureSpawnRequest> CreateCopyRequests(DebugStructureBlockDocument document, Vector2Int targetAnchor)
        {
            if (document == null)
            {
                return Array.Empty<DebugStructureSpawnRequest>();
            }

            return DebugStructureBlockStorage.CreateSpawnRequests(document, targetAnchor.x, targetAnchor.y);
        }

        public static bool TrySaveSelection(DebugLayoutSelectionSet selection, string name, string path, out string reason)
        {
            return TrySaveSelection(selection, (ClientMapWorld)null, name, path, out reason);
        }

        public static bool TrySaveSelection(DebugLayoutSelectionSet selection, ClientMapWorld world, string name, string path, out string reason)
        {
            if (selection == null || selection.Count == 0)
            {
                reason = "selection is empty";
                return false;
            }

            DebugStructureBlockDocument document = selection.ToStructureBlock(name, world);
            DebugStructureBlockStorage.Save(path, document);
            reason = string.Empty;
            return true;
        }

        public static bool TrySaveSelection(DebugLayoutSelectionSet selection, ClientMapWorld world, IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects, string name, string path, out string reason)
        {
            if (selection == null || selection.Count == 0)
            {
                reason = "selection is empty";
                return false;
            }

            DebugStructureBlockDocument document = selection.ToStructureBlock(name, world, runtimeEffects);
            DebugStructureBlockStorage.Save(path, document);
            reason = string.Empty;
            return true;
        }

        public static bool TrySaveSelection(DebugLayoutSelectionSet selection, IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects, string name, string path, out string reason)
        {
            if (selection == null || selection.Count == 0)
            {
                reason = "selection is empty";
                return false;
            }

            DebugStructureBlockDocument document = selection.ToStructureBlock(name, runtimeEffects);
            DebugStructureBlockStorage.Save(path, document);
            reason = string.Empty;
            return true;
        }

        public static bool TryLoadStructureBlock(string path, IGameConfigProvider provider, out DebugStructureBlockDocument document, out string reason)
        {
            if (!DebugStructureBlockStorage.TryLoad(path, provider, out document, out IReadOnlyList<string> errors))
            {
                reason = string.Join("|", errors);
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    public static class PortDebugVisualizationUtility
    {
        public static DirectionMask GetWorldPorts(EntitySnapshot snapshot)
        {
            return snapshot.PortLocalPorts.RotateBy(snapshot.Direction);
        }

        public static bool HasPorts(EntitySnapshot snapshot)
        {
            return snapshot.PortLocalPorts != DirectionMask.None;
        }

        public static IReadOnlyList<Direction> Directions(DirectionMask mask)
        {
            var result = new List<Direction>(4);
            AddIfContains(result, mask, Direction.Left);
            AddIfContains(result, mask, Direction.Right);
            AddIfContains(result, mask, Direction.Up);
            AddIfContains(result, mask, Direction.Down);
            return result;
        }

        public static IReadOnlyList<PortDebugConnection> FindConnections(IReadOnlyList<EntitySnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return Array.Empty<PortDebugConnection>();
            }

            var byCoord = new Dictionary<Vector2Int, EntitySnapshot>();
            foreach (EntitySnapshot snapshot in snapshots)
            {
                byCoord[new Vector2Int(snapshot.X, snapshot.Y)] = snapshot;
            }

            var result = new List<PortDebugConnection>();
            foreach (EntitySnapshot snapshot in snapshots)
            {
                DirectionMask ports = GetWorldPorts(snapshot);
                foreach (Direction direction in Directions(ports))
                {
                    Vector2Int fromCoord = new Vector2Int(snapshot.X, snapshot.Y);
                    Vector2Int toCoord = fromCoord + ToVector(direction);
                    if (!byCoord.TryGetValue(toCoord, out EntitySnapshot target))
                    {
                        continue;
                    }

                    if (snapshot.EntityId > target.EntityId)
                    {
                        continue;
                    }

                    DirectionMask targetPorts = GetWorldPorts(target);
                    if (!targetPorts.Contains(direction.Opposite()))
                    {
                        continue;
                    }

                    result.Add(new PortDebugConnection(snapshot.EntityId, target.EntityId, direction, fromCoord, toCoord));
                }
            }

            return result;
        }

        public static IReadOnlyList<DebugStructureGhostCell> BuildGhostCells(DebugStructureBlockDocument document, Vector2Int anchor)
        {
            if (document == null || document.Entries == null || document.Entries.Count == 0)
            {
                return Array.Empty<DebugStructureGhostCell>();
            }

            var portsByCoord = new Dictionary<Vector2Int, DirectionMask>();
            foreach (DebugStructureBlockEntry entry in document.Entries)
            {
                Vector2Int coord = new Vector2Int(anchor.x + entry.OffsetX, anchor.y + entry.OffsetY);
                Direction direction = ParseDirection(entry.Direction);
                DirectionMask worldPorts = ((DirectionMask)entry.PortLocalPorts).RotateBy(direction);
                portsByCoord[coord] = worldPorts;
            }

            var result = new List<DebugStructureGhostCell>();
            foreach (KeyValuePair<Vector2Int, DirectionMask> pair in portsByCoord)
            {
                bool hasInternalConnection = false;
                DirectionMask boundaryPorts = DirectionMask.None;
                foreach (Direction direction in Directions(pair.Value))
                {
                    Vector2Int targetCoord = pair.Key + ToVector(direction);
                    if (portsByCoord.TryGetValue(targetCoord, out DirectionMask targetPorts) && targetPorts.Contains(direction.Opposite()))
                    {
                        hasInternalConnection = true;
                    }
                    else
                    {
                        boundaryPorts |= direction.ToMask();
                    }
                }

                result.Add(new DebugStructureGhostCell(pair.Key, pair.Value, hasInternalConnection, boundaryPorts));
            }

            result.Sort((left, right) =>
            {
                int y = left.Coord.y.CompareTo(right.Coord.y);
                return y != 0 ? y : left.Coord.x.CompareTo(right.Coord.x);
            });
            return result;
        }

        public static Vector2Int ToVector(Direction direction)
        {
            return direction switch
            {
                Direction.Left => Vector2Int.left,
                Direction.Right => Vector2Int.right,
                Direction.Up => Vector2Int.up,
                Direction.Down => Vector2Int.down,
                _ => Vector2Int.zero
            };
        }

        private static void AddIfContains(List<Direction> result, DirectionMask mask, Direction direction)
        {
            if (mask.Contains(direction))
            {
                result.Add(direction);
            }
        }

        private static Direction ParseDirection(string value)
        {
            return Enum.TryParse(value, out Direction direction) ? direction : Direction.None;
        }
    }
}
