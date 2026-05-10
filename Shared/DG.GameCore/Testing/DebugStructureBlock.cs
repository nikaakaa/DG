using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DG.GameCore
{
public sealed class DebugStructureBlockDocument
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("anchorX")]
    public int AnchorX { get; set; }

    [JsonProperty("anchorY")]
    public int AnchorY { get; set; }

    [JsonProperty("entries")]
    public List<DebugStructureBlockEntry> Entries { get; set; } = new();
}

public sealed class DebugStructureBlockEntry
{
    [JsonProperty("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonProperty("configId")]
    public int ConfigId { get; set; }

    [JsonProperty("offsetX")]
    public int OffsetX { get; set; }

    [JsonProperty("offsetY")]
    public int OffsetY { get; set; }

    [JsonProperty("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonProperty("playerId")]
    public long PlayerId { get; set; }

    [JsonProperty("autoMoveIntervalTicks")]
    public int AutoMoveIntervalTicks { get; set; }

    [JsonProperty("portLocalPorts")]
    public int PortLocalPorts { get; set; }

    [JsonProperty("runtimeBlocking")]
    public bool RuntimeBlocking { get; set; }

    [JsonProperty("runtimeAutoMove")]
    public bool RuntimeAutoMove { get; set; }

    [JsonProperty("runtimePushable")]
    public bool RuntimePushable { get; set; }

    [JsonProperty("runtimePortLocalPorts")]
    public int RuntimePortLocalPorts { get; set; }

    [JsonProperty("runtimeImmobile")]
    public bool RuntimeImmobile { get; set; }
}

public readonly struct DebugStructureSpawnRequest
{
    public DebugStructureSpawnRequest(string alias, int configId, int x, int y, Direction direction, long playerId, int autoMoveIntervalTicks, DirectionMask portLocalPorts, bool runtimeBlocking, bool runtimeAutoMove, bool runtimePushable, DirectionMask runtimePortLocalPorts, bool runtimeImmobile)
    {
        Alias = alias ?? string.Empty;
        ConfigId = configId;
        X = x;
        Y = y;
        Direction = direction;
        PlayerId = playerId;
        AutoMoveIntervalTicks = Math.Max(1, autoMoveIntervalTicks);
        PortLocalPorts = portLocalPorts;
        RuntimeBlocking = runtimeBlocking;
        RuntimeAutoMove = runtimeAutoMove;
        RuntimePushable = runtimePushable;
        RuntimePortLocalPorts = runtimePortLocalPorts;
        RuntimeImmobile = runtimeImmobile;
    }

    public string Alias { get; }
    public int ConfigId { get; }
    public int X { get; }
    public int Y { get; }
    public Direction Direction { get; }
    public long PlayerId { get; }
    public int AutoMoveIntervalTicks { get; }
    public DirectionMask PortLocalPorts { get; }
    public bool RuntimeBlocking { get; }
    public bool RuntimeAutoMove { get; }
    public bool RuntimePushable { get; }
    public DirectionMask RuntimePortLocalPorts { get; }
    public bool RuntimeImmobile { get; }
    public bool HasRuntimeEffects => RuntimeBlocking || RuntimeAutoMove || RuntimePushable || RuntimePortLocalPorts != DirectionMask.None || RuntimeImmobile;
}

public static class DebugStructureBlockStorage
{
    public const string FileExtension = ".dgdebuglayout.json";
    public const string DefaultRelativeDirectory = "Assets/DebugLayouts";

    public static DebugStructureBlockDocument FromSnapshots(string name, IReadOnlyList<EntitySnapshot> snapshots)
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

        foreach (EntitySnapshot snapshot in snapshots.OrderBy(snapshot => snapshot.EntityId))
        {
            document.Entries.Add(new DebugStructureBlockEntry
            {
                Alias = "entity_" + snapshot.EntityId,
                ConfigId = snapshot.ConfigId,
                OffsetX = snapshot.X - anchorX,
                OffsetY = snapshot.Y - anchorY,
                Direction = snapshot.Direction.ToString(),
                PlayerId = 0,
                AutoMoveIntervalTicks = snapshot.AutoMoveIntervalTicks <= 0 ? 1 : snapshot.AutoMoveIntervalTicks,
                PortLocalPorts = (int)snapshot.PortLocalPorts,
                RuntimeBlocking = snapshot.Blocking,
                RuntimeAutoMove = snapshot.AutoMove,
                RuntimePushable = snapshot.Pushable,
                RuntimePortLocalPorts = (int)snapshot.PortLocalPorts,
                RuntimeImmobile = snapshot.HasMovementPermission && (!snapshot.CanMove || !snapshot.CanBePushed)
            });
        }

        return document;
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

    private static string Error(int index, string message)
    {
        return "entry " + index + ": " + message;
    }
}
}
