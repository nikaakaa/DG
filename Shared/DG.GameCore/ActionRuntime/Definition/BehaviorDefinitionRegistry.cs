using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class BehaviorDefinitionRegistry
{
    private readonly Dictionary<BehaviorId, BehaviorDefinition> definitions = new();
    private readonly Dictionary<ActionPrimitive, BehaviorId> byPrimitive = new();

    public static BehaviorDefinitionRegistry Default { get; } = BehaviorDefinitionCatalog.CreateDefault();

    public void Register(BehaviorDefinition definition)
    {
        if (!definition.IsValid)
        {
            throw new InvalidOperationException("Behavior definition is invalid.");
        }

        if (definitions.ContainsKey(definition.BehaviorId))
        {
            throw new InvalidOperationException("Duplicate behavior id: " + definition.BehaviorId);
        }

        if (byPrimitive.ContainsKey(definition.Primitive))
        {
            throw new InvalidOperationException("Duplicate primitive binding for '" + definition.Primitive + "' on behavior '" + definition.BehaviorId + "'.");
        }

        definitions.Add(definition.BehaviorId, definition);
        byPrimitive.Add(definition.Primitive, definition.BehaviorId);
    }

    public BehaviorDefinition Get(BehaviorId id)
    {
        if (!definitions.TryGetValue(id, out BehaviorDefinition definition))
        {
            throw new InvalidOperationException("No behavior definition registered for id: " + id);
        }

        return definition;
    }

    public bool TryGet(BehaviorId id, out BehaviorDefinition definition)
    {
        return definitions.TryGetValue(id, out definition);
    }

    public bool TryGetByPrimitive(ActionPrimitive primitive, out BehaviorDefinition definition)
    {
        if (byPrimitive.TryGetValue(primitive, out BehaviorId behaviorId))
        {
            return definitions.TryGetValue(behaviorId, out definition);
        }

        definition = default;
        return false;
    }

    public bool Contains(BehaviorId id) => definitions.ContainsKey(id);

    public IReadOnlyCollection<BehaviorDefinition> Definitions => definitions.Values;
}
}
