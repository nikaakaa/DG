using System;

namespace DG.GameCore
{
public sealed class BehaviorResolver
{
    private readonly BehaviorDefinitionRegistry definitions;

    public BehaviorResolver(BehaviorDefinitionRegistry definitions)
    {
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
    }

    public static BehaviorResolver Default { get; } = new BehaviorResolver(BehaviorDefinitionRegistry.Default);

    public BehaviorDefinition Resolve(ActionSpec spec, GameWorld world, ActionRequest request)
    {
        if (spec == null)
        {
            throw new ArgumentNullException(nameof(spec));
        }

        if (!definitions.TryGetByPrimitive(spec.Primitive, out BehaviorDefinition definition))
        {
            throw new InvalidOperationException("Behavior resolver has no registered behavior for primitive '" + spec.Primitive + "' on action spec '" + spec.SpecId + "'.");
        }

        return definition;
    }
}
}
