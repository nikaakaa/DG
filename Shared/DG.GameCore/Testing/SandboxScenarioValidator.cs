using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DG.GameCore
{
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

}
