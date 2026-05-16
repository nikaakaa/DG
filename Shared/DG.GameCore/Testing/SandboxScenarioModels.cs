using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DG.GameCore
{
public sealed class SandboxScenarioDocument
{
    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonProperty("version")]
    public int Version
    {
        get => SchemaVersion;
        set => SchemaVersion = value;
    }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("entities")]
    public List<SandboxScenarioEntityRef> Entities { get; set; } = new();

    [JsonProperty("steps")]
    public List<SandboxScenarioStep> Steps { get; set; } = new();

    [JsonProperty("expectations")]
    public List<SandboxScenarioExpectation> Expectations { get; set; } = new();
}

public sealed class SandboxScenarioEntityRef
{
    [JsonProperty("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonProperty("entityId")]
    public long EntityId { get; set; }
}

public sealed class SandboxScenarioStep
{
    [JsonProperty("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonProperty("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonProperty("entityId")]
    public long EntityId { get; set; }

    [JsonProperty("configId")]
    public int ConfigId { get; set; }

    [JsonProperty("x")]
    public int X { get; set; }

    [JsonProperty("y")]
    public int Y { get; set; }

    [JsonProperty("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonProperty("playerId")]
    public long PlayerId { get; set; }

    [JsonProperty("autoMoveIntervalTicks")]
    public int AutoMoveIntervalTicks { get; set; }

    [JsonProperty("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("ticks")]
    public int Ticks { get; set; }

    [JsonProperty("frames")]
    public int Frames { get; set; }
}

public sealed class SandboxScenarioExpectation
{
    [JsonProperty("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonProperty("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonProperty("entityId")]
    public long EntityId { get; set; }

    [JsonProperty("x")]
    public int X { get; set; }

    [JsonProperty("y")]
    public int Y { get; set; }

    [JsonProperty("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonProperty("success")]
    public bool? Success { get; set; }

    [JsonProperty("reason")]
    public string Reason { get; set; } = string.Empty;
}

public readonly struct SandboxOperationResult
{
    public SandboxOperationResult(bool success, string reason, long entityId)
    {
        Success = success;
        Reason = reason ?? string.Empty;
        EntityId = entityId;
    }

    public bool Success { get; }
    public string Reason { get; }
    public long EntityId { get; }
}

public sealed class SandboxScenarioRunResult
{
    public SandboxScenarioRunResult(bool success, string reason, int failedStepIndex, IReadOnlyList<string> errors, SandboxOperationResult lastResult)
    {
        Success = success;
        Reason = reason ?? string.Empty;
        FailedStepIndex = failedStepIndex;
        Errors = errors ?? Array.Empty<string>();
        LastResult = lastResult;
    }

    public bool Success { get; }
    public string Reason { get; }
    public int FailedStepIndex { get; }
    public IReadOnlyList<string> Errors { get; }
    public SandboxOperationResult LastResult { get; }
}

}
