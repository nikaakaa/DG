using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

public sealed class SandboxEntityPaletteEntry
{
    public SandboxEntityPaletteEntry(EntityArchetype archetype)
    {
        Archetype = archetype ?? throw new ArgumentNullException(nameof(archetype));
        ComponentSummary = string.Join(",", archetype.Components.Select(component => component.ToString()));
        TagSummary = string.Join(",", archetype.Tags);
    }

    public EntityArchetype Archetype { get; }
    public int ConfigId => Archetype.ConfigId;
    public int ArchetypeId => Archetype.ArchetypeId;
    public int EntityTarget => Archetype.EntityTarget;
    public IReadOnlyList<ComponentKind> Components => Archetype.Components;
    public IReadOnlyList<string> Tags => Archetype.Tags;
    public int DefaultAutoMoveIntervalTicks => Archetype.DefaultAutoMoveIntervalTicks;
    public string ComponentSummary { get; }
    public string TagSummary { get; }
}

public sealed class SandboxEntityPalette
{
    private readonly IReadOnlyList<SandboxEntityPaletteEntry> entries;

    private SandboxEntityPalette(IReadOnlyList<SandboxEntityPaletteEntry> entries)
    {
        this.entries = entries;
    }

    public IReadOnlyList<SandboxEntityPaletteEntry> Entries => entries;

    public static SandboxEntityPalette FromProvider(IGameConfigProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        return new SandboxEntityPalette(provider.GetEntityArchetypes()
            .Select(archetype => new SandboxEntityPaletteEntry(archetype))
            .OrderBy(entry => entry.ConfigId)
            .ToArray());
    }

    public IReadOnlyList<SandboxEntityPaletteEntry> Search(string text, ComponentKind? componentKind = null)
    {
        string query = text ?? string.Empty;
        return entries
            .Where(entry => Matches(entry, query, componentKind))
            .ToArray();
    }

    private static bool Matches(SandboxEntityPaletteEntry entry, string query, ComponentKind? componentKind)
    {
        if (componentKind.HasValue && !entry.Components.Contains(componentKind.Value))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(entry.ConfigId.ToString(), query) ||
            Contains(entry.ArchetypeId.ToString(), query) ||
            entry.Tags.Any(tag => Contains(tag, query)) ||
            entry.Components.Any(component => Contains(component.ToString(), query));
    }

    private static bool Contains(string text, string query)
    {
        return text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

public static class SandboxScenarioParser
{
    public static bool TryParse(string json, IGameConfigProvider provider, out SandboxScenarioDocument document, out IReadOnlyList<string> errors)
    {
        document = default!;
        var errorList = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            errors = new[] { "json is empty" };
            return false;
        }

        try
        {
            JToken token = JToken.Parse(json);
            CollectForbiddenEntityDefinitions(token, errorList);
            document = token.ToObject<SandboxScenarioDocument>() ?? new SandboxScenarioDocument();
        }
        catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidCastException)
        {
            errors = new[] { ex.Message };
            return false;
        }

        errorList.AddRange(SandboxScenarioValidator.Validate(document, provider));
        errors = errorList;
        return errorList.Count == 0;
    }

    private static void CollectForbiddenEntityDefinitions(JToken token, List<string> errors)
    {
        if (token is JObject obj)
        {
            foreach (JProperty property in obj.Properties())
            {
                string name = property.Name;
                if (string.Equals(name, "components", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "componentKinds", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "archetype", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "archetypeId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "tags", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("scenario must not define entity archetype data: " + name);
                }

                CollectForbiddenEntityDefinitions(property.Value, errors);
            }

            return;
        }

        if (token is JArray array)
        {
            foreach (JToken item in array)
            {
                CollectForbiddenEntityDefinitions(item, errors);
            }
        }
    }
}

public static class SandboxScenarioStorage
{
    public static string ToJson(SandboxScenarioDocument document)
    {
        return JsonConvert.SerializeObject(document, Formatting.Indented);
    }

    public static void Save(string path, SandboxScenarioDocument document)
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

    public static bool TryLoad(string path, IGameConfigProvider provider, out SandboxScenarioDocument document, out IReadOnlyList<string> errors)
    {
        document = default!;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            errors = new[] { "scenario file missing" };
            return false;
        }

        return SandboxScenarioParser.TryParse(File.ReadAllText(path), provider, out document, out errors);
    }
}

public static class SandboxScenarioValidator
{
    public static IReadOnlyList<string> Validate(SandboxScenarioDocument document, IGameConfigProvider provider)
    {
        var errors = new List<string>();
        if (provider == null)
        {
            errors.Add("config provider is missing");
            return errors;
        }

        if (document == null)
        {
            errors.Add("scenario is missing");
            return errors;
        }

        if (document.SchemaVersion <= 0)
        {
            errors.Add("schemaVersion must be positive");
        }

        var aliases = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (SandboxScenarioEntityRef entityRef in document.Entities ?? Enumerable.Empty<SandboxScenarioEntityRef>())
        {
            RegisterAlias(entityRef.Alias, entityRef.EntityId, aliases, errors);
        }

        IReadOnlyList<SandboxScenarioStep> steps = document.Steps != null ? document.Steps : Array.Empty<SandboxScenarioStep>();
        for (int i = 0; i < steps.Count; i++)
        {
            ValidateStep(steps[i], i, provider, aliases, errors);
        }

        IReadOnlyList<SandboxScenarioExpectation> expectations = document.Expectations != null ? document.Expectations : Array.Empty<SandboxScenarioExpectation>();
        for (int i = 0; i < expectations.Count; i++)
        {
            ValidateExpectation(expectations[i], steps.Count + i, aliases, errors);
        }

        return errors;
    }

    private static void ValidateStep(SandboxScenarioStep step, int index, IGameConfigProvider provider, Dictionary<string, long> aliases, List<string> errors)
    {
        string kind = Normalize(step.Kind);
        if (kind == "spawn")
        {
            if (string.IsNullOrWhiteSpace(step.Alias))
            {
                errors.Add(Error(index, "spawn alias is missing"));
            }

            if (step.ConfigId <= 0 || !provider.TryGetArchetype(step.ConfigId, out _))
            {
                errors.Add(Error(index, "unknown configId: " + step.ConfigId));
            }

            if (!TryParseDirection(step.Direction, true, out _))
            {
                errors.Add(Error(index, "unknown direction: " + step.Direction));
            }

            RegisterAlias(step.Alias, step.EntityId, aliases, errors, index);
            return;
        }

        if (kind == "move" || kind == "drag" || kind == "playermove")
        {
            ValidateEntityReference(step.Alias, step.EntityId, aliases, errors, index);
            return;
        }

        if (kind == "delete")
        {
            ValidateEntityReference(step.Alias, step.EntityId, aliases, errors, index);
            return;
        }

        if (kind == "settag")
        {
            ValidateEntityReference(step.Alias, step.EntityId, aliases, errors, index);
            if (!TryParseTag(step.Tag, out _))
            {
                errors.Add(Error(index, "unknown tag: " + step.Tag));
            }

            return;
        }

        if (kind == "tick" || kind == "wait")
        {
            if (step.Ticks <= 0 && step.Frames <= 0)
            {
                errors.Add(Error(index, "tick count is missing"));
            }

            return;
        }

        if (kind == "expectposition" || kind == "expecttag" || kind == "expectlastresult")
        {
            ValidateExpectation(new SandboxScenarioExpectation
            {
                Kind = step.Kind,
                Alias = step.Alias,
                EntityId = step.EntityId,
                X = step.X,
                Y = step.Y,
                Tag = step.Tag
            }, index, aliases, errors);
            return;
        }

        errors.Add(Error(index, "unknown step kind: " + step.Kind));
    }

    private static void ValidateExpectation(SandboxScenarioExpectation expectation, int index, Dictionary<string, long> aliases, List<string> errors)
    {
        string kind = Normalize(expectation.Kind);
        if (kind == "expectposition")
        {
            ValidateEntityReference(expectation.Alias, expectation.EntityId, aliases, errors, index);
            return;
        }

        if (kind == "expecttag")
        {
            ValidateEntityReference(expectation.Alias, expectation.EntityId, aliases, errors, index);
            if (!TryParseTag(expectation.Tag, out _))
            {
                errors.Add(Error(index, "unknown tag: " + expectation.Tag));
            }

            return;
        }

        if (kind == "expectlastresult")
        {
            return;
        }

        errors.Add(Error(index, "unknown expectation kind: " + expectation.Kind));
    }

    public static bool TryParseDirection(string value, bool allowEmpty, out Direction direction)
    {
        direction = Direction.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return allowEmpty;
        }

        return Enum.TryParse(value, true, out direction);
    }

    public static bool TryParseTag(string value, out WorldTag tag)
    {
        tag = WorldTag.None;
        return !string.IsNullOrWhiteSpace(value) &&
            Enum.TryParse(value, true, out tag) &&
            tag != WorldTag.None;
    }

    private static void ValidateEntityReference(string alias, long entityId, Dictionary<string, long> aliases, List<string> errors, int index)
    {
        if (entityId > 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(alias))
        {
            errors.Add(Error(index, "entity reference is missing"));
            return;
        }

        if (!aliases.ContainsKey(alias))
        {
            errors.Add(Error(index, "alias is not defined: " + alias));
        }
    }

    private static void RegisterAlias(string alias, long entityId, Dictionary<string, long> aliases, List<string> errors, int index = -1)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        if (aliases.ContainsKey(alias))
        {
            errors.Add(index >= 0 ? Error(index, "duplicate alias: " + alias) : "duplicate alias: " + alias);
            return;
        }

        aliases.Add(alias, entityId);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();
    }

    private static string Error(int index, string message)
    {
        return "step " + index + ": " + message;
    }
}

public sealed class LocalSandboxScenarioRunner
{
    private readonly IGameConfigProvider provider;
    private readonly GameWorld world;
    private readonly WorldActionQueue actionQueue = new();
    private readonly PendingRuleStateStore pendingStates = new();
    private readonly StateDrivenRuleExecutionSystem ruleSystem = new();
    private readonly Dictionary<string, long> aliases = new(StringComparer.OrdinalIgnoreCase);
    private long nextEntityId = 700000000;
    private SandboxOperationResult lastResult = new(true, string.Empty, 0);

    public LocalSandboxScenarioRunner(IGameConfigProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        world = new GameWorld(provider);
    }

    public GameWorld World => world;
    public SandboxOperationResult LastResult => lastResult;

    public SandboxScenarioRunResult Run(SandboxScenarioDocument document)
    {
        IReadOnlyList<string> validationErrors = SandboxScenarioValidator.Validate(document, provider);
        if (validationErrors.Count > 0)
        {
            return new SandboxScenarioRunResult(false, validationErrors[0], -1, validationErrors, lastResult);
        }

        foreach (SandboxScenarioEntityRef entityRef in document.Entities ?? Enumerable.Empty<SandboxScenarioEntityRef>())
        {
            if (!string.IsNullOrWhiteSpace(entityRef.Alias))
            {
                aliases[entityRef.Alias] = entityRef.EntityId;
            }
        }

        IReadOnlyList<SandboxScenarioStep> steps = document.Steps != null ? document.Steps : Array.Empty<SandboxScenarioStep>();
        for (int i = 0; i < steps.Count; i++)
        {
            if (!ExecuteStep(steps[i], i, out string reason))
            {
                return new SandboxScenarioRunResult(false, reason, i, new[] { reason }, lastResult);
            }
        }

        IReadOnlyList<SandboxScenarioExpectation> expectations = document.Expectations != null ? document.Expectations : Array.Empty<SandboxScenarioExpectation>();
        for (int i = 0; i < expectations.Count; i++)
        {
            if (!CheckExpectation(expectations[i], steps.Count + i, out string reason))
            {
                return new SandboxScenarioRunResult(false, reason, steps.Count + i, new[] { reason }, lastResult);
            }
        }

        return new SandboxScenarioRunResult(true, string.Empty, -1, Array.Empty<string>(), lastResult);
    }

    private bool ExecuteStep(SandboxScenarioStep step, int index, out string reason)
    {
        string kind = Normalize(step.Kind);
        if (kind == "spawn")
        {
            long entityId = step.EntityId > 0 ? step.EntityId : AllocateEntityId();
            aliases[step.Alias] = entityId;
            SandboxScenarioValidator.TryParseDirection(step.Direction, true, out Direction direction);
            actionQueue.EnqueueDebugSpawn(entityId, step.ConfigId, new GridCoord(step.X, step.Y), direction, step.PlayerId, step.AutoMoveIntervalTicks <= 0 ? 1 : step.AutoMoveIntervalTicks);
            ExecuteTick();
            reason = lastResult.Success ? string.Empty : lastResult.Reason;
            return lastResult.Success;
        }

        if (kind == "move" || kind == "drag")
        {
            long entityId = ResolveEntityId(step.Alias, step.EntityId);
            actionQueue.EnqueueDebugMove(entityId, new GridCoord(step.X, step.Y));
            ExecuteTick();
            reason = lastResult.Success ? string.Empty : lastResult.Reason;
            return lastResult.Success;
        }

        if (kind == "playermove")
        {
            long entityId = ResolveEntityId(step.Alias, step.EntityId);
            actionQueue.EnqueuePlayerMove(entityId, new GridCoord(step.X, step.Y), world.ServerTick);
            ExecuteTick();
            reason = string.Empty;
            return true;
        }

        if (kind == "delete")
        {
            long entityId = ResolveEntityId(step.Alias, step.EntityId);
            actionQueue.EnqueueDebugRemove(entityId);
            ExecuteTick();
            reason = lastResult.Success ? string.Empty : lastResult.Reason;
            return lastResult.Success;
        }

        if (kind == "settag")
        {
            long entityId = ResolveEntityId(step.Alias, step.EntityId);
            SandboxScenarioValidator.TryParseTag(step.Tag, out WorldTag tag);
            if (!world.TryGetEntity(entityId, out GameEntity entity))
            {
                reason = "step " + index + ": entity not found";
                lastResult = new SandboxOperationResult(false, reason, entityId);
                return false;
            }

            if (step.Enabled)
            {
                world.AddTag(entity, tag);
            }
            else
            {
                world.RemoveTag(entity, tag);
            }

            world.MarkDirty(entityId);
            world.FlushDelta();
            reason = string.Empty;
            lastResult = new SandboxOperationResult(true, string.Empty, entityId);
            return true;
        }

        if (kind == "tick" || kind == "wait")
        {
            int count = Math.Max(step.Ticks, step.Frames);
            for (int i = 0; i < count; i++)
            {
                ExecuteTick();
            }

            reason = string.Empty;
            return true;
        }

        if (kind == "expectposition" || kind == "expecttag" || kind == "expectlastresult")
        {
            return CheckExpectation(new SandboxScenarioExpectation
            {
                Kind = step.Kind,
                Alias = step.Alias,
                EntityId = step.EntityId,
                X = step.X,
                Y = step.Y,
                Tag = step.Tag
            }, index, out reason);
        }

        reason = "step " + index + ": unknown step kind";
        return false;
    }

    private bool CheckExpectation(SandboxScenarioExpectation expectation, int index, out string reason)
    {
        string kind = Normalize(expectation.Kind);
        if (kind == "expectposition")
        {
            long entityId = ResolveEntityId(expectation.Alias, expectation.EntityId);
            if (!world.TryGetEntity(entityId, out GameEntity entity) ||
                !world.TryGetComponent(entity, out PositionComponent position))
            {
                reason = "step " + index + ": entity position missing";
                return false;
            }

            GridCoord expected = new(expectation.X, expectation.Y);
            if (position.Coord != expected)
            {
                reason = "step " + index + ": position expected " + expected.X + "," + expected.Y + " actual " + position.Coord.X + "," + position.Coord.Y;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        if (kind == "expecttag")
        {
            long entityId = ResolveEntityId(expectation.Alias, expectation.EntityId);
            SandboxScenarioValidator.TryParseTag(expectation.Tag, out WorldTag tag);
            if (!world.TryGetEntity(entityId, out GameEntity entity))
            {
                reason = "step " + index + ": entity not found";
                return false;
            }

            if (!world.HasTag(entity, tag))
            {
                reason = "step " + index + ": tag missing " + tag;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        if (kind == "expectlastresult")
        {
            if (expectation.Success.HasValue && lastResult.Success != expectation.Success.Value)
            {
                reason = "step " + index + ": last result success mismatch";
                return false;
            }

            if (!string.IsNullOrEmpty(expectation.Reason) &&
                !string.Equals(lastResult.Reason, expectation.Reason, StringComparison.OrdinalIgnoreCase))
            {
                reason = "step " + index + ": last result reason mismatch";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        reason = "step " + index + ": unknown expectation kind";
        return false;
    }

    private void ExecuteTick()
    {
        long serverTick = world.NextTick();
        EnqueueAutoMoveActions(serverTick);
        EnqueueMechanismPushActions(serverTick);
        IReadOnlyList<WorldAction> actions = actionQueue.DrainReady(serverTick);
        StateDrivenRuleExecutionResult result = ruleSystem.Tick(world, actions, pendingStates, serverTick);
        lastResult = ResolveLastResult(actions, result);
        world.FlushDelta();
    }

    private SandboxOperationResult ResolveLastResult(IReadOnlyList<WorldAction> actions, StateDrivenRuleExecutionResult result)
    {
        if (actions.Count == 0)
        {
            string reason = result.Reasons.Count > 0 ? result.Reasons[0] : string.Empty;
            return new SandboxOperationResult(result.Reasons.Count == 0, reason, lastResult.EntityId);
        }

        WorldAction action = actions[actions.Count - 1];
        if (result.ActionResults.TryGetValue(action.ActionId, out MoveResult moveResult))
        {
            return new SandboxOperationResult(moveResult.Success, moveResult.Reason, moveResult.EntityId);
        }

        return new SandboxOperationResult(false, "action not resolved", action.EntityId);
    }

    private void EnqueueAutoMoveActions(long serverTick)
    {
        IReadOnlyList<GameEntity> entities = world.EnumerateEntities();
        for (int i = 0; i < entities.Count; i++)
        {
            GameEntity entity = entities[i];
            if (!world.TryGetComponent(entity, out PositionComponent _) ||
                !world.TryGetComponent(entity, out DirectionComponent _) ||
                !world.TryGetComponent(entity, out AutoMoveComponent autoMove))
            {
                continue;
            }

            if (serverTick - autoMove.LastMoveTick >= autoMove.IntervalTicks)
            {
                actionQueue.EnqueueAutoMove(entity.EntityId, serverTick - 1, 1);
            }
        }
    }

    private void EnqueueMechanismPushActions(long serverTick)
    {
        var moved = new HashSet<long>();
        IReadOnlyList<GameEntity> triggers = world.EnumerateEntities();
        for (int i = 0; i < triggers.Count; i++)
        {
            GameEntity trigger = triggers[i];
            if (!world.TryGetComponent(trigger, out PositionComponent triggerPosition) ||
                !world.TryGetComponent(trigger, out DirectionComponent triggerDirection) ||
                !world.HasComponent<PushOnEnterComponent>(trigger))
            {
                continue;
            }

            IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(triggerPosition.Coord);
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                GameEntity target = targets[targetIndex];
                if (target.EntityId == trigger.EntityId ||
                    moved.Contains(target.EntityId) ||
                    !world.TryGetComponent(target, out PositionComponent _))
                {
                    continue;
                }

                moved.Add(target.EntityId);
                actionQueue.EnqueueMechanismPush(target.EntityId, triggerDirection.Direction, serverTick - 1, 1);
            }
        }
    }

    private long ResolveEntityId(string alias, long entityId)
    {
        if (entityId > 0)
        {
            return entityId;
        }

        return aliases.TryGetValue(alias, out long resolved) ? resolved : 0;
    }

    private long AllocateEntityId()
    {
        while (world.TryGetEntity(nextEntityId, out _))
        {
            nextEntityId++;
        }

        return nextEntityId++;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();
    }
}
}
