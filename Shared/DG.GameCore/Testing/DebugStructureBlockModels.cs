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

public readonly struct DebugStructureRuntimeEffectRecord
{
    public DebugStructureRuntimeEffectRecord(long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portLocalPorts)
    {
        EntityId = entityId;
        Kind = kind;
        AutoMoveIntervalTicks = Math.Max(1, autoMoveIntervalTicks);
        PortLocalPorts = portLocalPorts;
    }

    public long EntityId { get; }
    public RuntimeEffectKind Kind { get; }
    public int AutoMoveIntervalTicks { get; }
    public DirectionMask PortLocalPorts { get; }
}

}
