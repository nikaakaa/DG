using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DG.GameCore
{
public static class DebugStructureBlockStorage
{
    public const string FileExtension = ".dgdebuglayout.json";
    public const string DefaultRelativeDirectory = "Assets/DebugLayouts";

    public static DebugStructureBlockDocument FromSnapshots(string name, IReadOnlyList<EntitySnapshot> snapshots)
    {
        return FromSnapshots(name, snapshots, null, 0);
    }

    public static DebugStructureBlockDocument FromSnapshots(string name, IReadOnlyList<EntitySnapshot> snapshots, RuntimeEffectStore? runtimeEffects, long tick)
    {
        return FromSnapshots(name, snapshots, RuntimeEffectRecords(runtimeEffects, tick));
    }

    public static DebugStructureBlockDocument FromSnapshots(string name, IReadOnlyList<EntitySnapshot> snapshots, IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects)
    {
        if (snapshots == null || snapshots.Count == 0)
        {
            return new DebugStructureBlockDocument { Name = name ?? string.Empty };
        }

        int anchorX = snapshots.Min(snapshot => snapshot.X);
        int anchorY = snapshots.Min(snapshot => snapshot.Y);
        var document = new DebugStructureBlockDocument
        {
            Name = name ?? string.Empty,
            AnchorX = anchorX,
            AnchorY = anchorY
        };

        Dictionary<long, DebugRuntimeEffectSummary> runtimeByEntity = BuildRuntimeEffectSummary(runtimeEffects);
        foreach (EntitySnapshot snapshot in snapshots.OrderBy(snapshot => snapshot.EntityId))
        {
            runtimeByEntity.TryGetValue(snapshot.EntityId, out DebugRuntimeEffectSummary runtime);
            document.Entries.Add(new DebugStructureBlockEntry
            {
                Alias = "entity_" + snapshot.EntityId,
                ConfigId = snapshot.ConfigId,
                OffsetX = snapshot.X - anchorX,
                OffsetY = snapshot.Y - anchorY,
                Direction = snapshot.Direction.ToString(),
                PlayerId = 0,
                AutoMoveIntervalTicks = runtime.AutoMoveIntervalTicks > 0 ? runtime.AutoMoveIntervalTicks : snapshot.AutoMoveIntervalTicks <= 0 ? 1 : snapshot.AutoMoveIntervalTicks,
                PortLocalPorts = (int)snapshot.PortLocalPorts,
                RotatePivot = snapshot.RotatePivot,
                RuntimeBlocking = runtime.Blocking,
                RuntimeAutoMove = runtime.AutoMove,
                RuntimePushable = runtime.Pushable,
                RuntimePortLocalPorts = (int)runtime.PortLocalPorts,
                RuntimeImmobile = runtime.Immobile
            });
        }

        return document;
    }

    private static IReadOnlyList<DebugStructureRuntimeEffectRecord> RuntimeEffectRecords(RuntimeEffectStore? runtimeEffects, long tick)
    {
        if (runtimeEffects == null)
        {
            return Array.Empty<DebugStructureRuntimeEffectRecord>();
        }

        return runtimeEffects.ActiveAt(tick)
            .Select(effect => new DebugStructureRuntimeEffectRecord(
                effect.TargetEntityId,
                effect.Kind,
                effect.Spec.AutoMoveIntervalTicks,
                effect.Spec.PortMask))
            .ToArray();
    }

    private static Dictionary<long, DebugRuntimeEffectSummary> BuildRuntimeEffectSummary(IReadOnlyList<DebugStructureRuntimeEffectRecord> runtimeEffects)
    {
        var result = new Dictionary<long, DebugRuntimeEffectSummary>();
        if (runtimeEffects == null)
        {
            return result;
        }

        foreach (DebugStructureRuntimeEffectRecord effect in runtimeEffects)
        {
            if (!result.TryGetValue(effect.EntityId, out DebugRuntimeEffectSummary summary))
            {
                summary = default;
            }

            switch (effect.Kind)
            {
                case RuntimeEffectKind.TemporaryBlocking:
                    summary.Blocking = true;
                    break;
                case RuntimeEffectKind.TemporaryAutoMove:
                    summary.AutoMove = true;
                    summary.AutoMoveIntervalTicks = Math.Max(summary.AutoMoveIntervalTicks, effect.AutoMoveIntervalTicks);
                    break;
                case RuntimeEffectKind.TemporaryPushable:
                    summary.Pushable = true;
                    break;
                case RuntimeEffectKind.TemporaryPort:
                    summary.PortLocalPorts |= effect.PortLocalPorts;
                    break;
                case RuntimeEffectKind.TemporaryImmobile:
                    summary.Immobile = true;
                    break;
            }

            result[effect.EntityId] = summary;
        }

        return result;
    }

    public static string ToJson(DebugStructureBlockDocument document)
    {
        return JsonConvert.SerializeObject(document, Formatting.Indented);
    }

    public static void Save(string path, DebugStructureBlockDocument document)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path is empty", nameof(path));
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToJson(document));
    }

    public static bool TryLoad(string path, IGameConfigProvider provider, out DebugStructureBlockDocument document, out IReadOnlyList<string> errors)
    {
        document = default!;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            errors = new[] { "structure block file missing" };
            return false;
        }

        return TryParse(File.ReadAllText(path), provider, out document, out errors);
    }

    public static bool TryParse(string json, IGameConfigProvider provider, out DebugStructureBlockDocument document, out IReadOnlyList<string> errors)
    {
        document = default!;
        if (string.IsNullOrWhiteSpace(json))
        {
            errors = new[] { "json is empty" };
            return false;
        }

        try
        {
            document = JsonConvert.DeserializeObject<DebugStructureBlockDocument>(json) ?? new DebugStructureBlockDocument();
        }
        catch (JsonException ex)
        {
            errors = new[] { ex.Message };
            return false;
        }

        errors = Validate(document, provider);
        return errors.Count == 0;
    }

    public static IReadOnlyList<string> Validate(DebugStructureBlockDocument document, IGameConfigProvider provider)
    {
        var errors = new List<string>();
        if (provider == null)
        {
            errors.Add("config provider is missing");
            return errors;
        }

        if (document == null)
        {
            errors.Add("structure block is missing");
            return errors;
        }

        if (document.SchemaVersion <= 0)
        {
            errors.Add("schemaVersion must be positive");
        }

        IReadOnlyList<DebugStructureBlockEntry> entries = document.Entries != null ? document.Entries : Array.Empty<DebugStructureBlockEntry>();
        if (entries.Count == 0)
        {
            errors.Add("structure block is empty");
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            DebugStructureBlockEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.Alias))
            {
                errors.Add(Error(i, "alias is missing"));
            }
            else if (!aliases.Add(entry.Alias))
            {
                errors.Add(Error(i, "duplicate alias: " + entry.Alias));
            }

            if (entry.ConfigId <= 0 || !provider.TryGetArchetype(entry.ConfigId, out _))
            {
                errors.Add(Error(i, "unknown configId: " + entry.ConfigId));
            }

            if (!SandboxScenarioValidator.TryParseDirection(entry.Direction, true, out _))
            {
                errors.Add(Error(i, "unknown direction: " + entry.Direction));
            }

            DirectionMask ports = (DirectionMask)entry.PortLocalPorts;
            if ((ports & ~DirectionMask.All) != DirectionMask.None)
            {
                errors.Add(Error(i, "unknown port mask: " + entry.PortLocalPorts));
            }

            DirectionMask runtimePorts = (DirectionMask)entry.RuntimePortLocalPorts;
            if ((runtimePorts & ~DirectionMask.All) != DirectionMask.None)
            {
                errors.Add(Error(i, "unknown runtime port mask: " + entry.RuntimePortLocalPorts));
            }
        }

        return errors;
    }

    public static IReadOnlyList<DebugStructureSpawnRequest> CreateSpawnRequests(DebugStructureBlockDocument document, int anchorX, int anchorY)
    {
        IReadOnlyList<DebugStructureBlockEntry> entries = document?.Entries != null ? document.Entries : Array.Empty<DebugStructureBlockEntry>();
        var requests = new List<DebugStructureSpawnRequest>(entries.Count);
        foreach (DebugStructureBlockEntry entry in entries)
        {
            SandboxScenarioValidator.TryParseDirection(entry.Direction, true, out Direction direction);
            requests.Add(new DebugStructureSpawnRequest(
                entry.Alias,
                entry.ConfigId,
                anchorX + entry.OffsetX,
                anchorY + entry.OffsetY,
                direction,
                entry.PlayerId,
                entry.AutoMoveIntervalTicks <= 0 ? 1 : entry.AutoMoveIntervalTicks,
                (DirectionMask)entry.PortLocalPorts,
                entry.RotatePivot,
                entry.RuntimeBlocking,
                entry.RuntimeAutoMove,
                entry.RuntimePushable,
                (DirectionMask)entry.RuntimePortLocalPorts,
                entry.RuntimeImmobile));
        }

        return requests;
    }

    public static SandboxScenarioDocument ToScenario(DebugStructureBlockDocument document, int anchorX, int anchorY)
    {
        var scenario = new SandboxScenarioDocument
        {
            SchemaVersion = 1,
            Name = document?.Name ?? string.Empty
        };

        if (document == null)
        {
            return scenario;
        }

        foreach (DebugStructureSpawnRequest request in CreateSpawnRequests(document, anchorX, anchorY))
        {
            scenario.Steps.Add(new SandboxScenarioStep
            {
                Kind = "spawn",
                Alias = request.Alias,
                ConfigId = request.ConfigId,
                X = request.X,
                Y = request.Y,
                Direction = request.Direction.ToString(),
                PlayerId = request.PlayerId,
                AutoMoveIntervalTicks = request.AutoMoveIntervalTicks
            });
        }

        return scenario;
    }

    private struct DebugRuntimeEffectSummary
    {
        public bool Blocking;
        public bool AutoMove;
        public int AutoMoveIntervalTicks;
        public bool Pushable;
        public DirectionMask PortLocalPorts;
        public bool Immobile;
    }

    private static string Error(int index, string message)
    {
        return "entry " + index + ": " + message;
    }
}
}
