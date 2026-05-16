using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class EntityArchetype
{
    public EntityArchetype(int configId, int archetypeId, int entityTarget, IReadOnlyList<ComponentKind> components, IReadOnlyList<string> tags, int defaultAutoMoveIntervalTicks)
        : this(configId, archetypeId, entityTarget, components == null ? Array.Empty<ComponentId>() : components.Select(kind => new ComponentId(kind)).ToArray(), tags, defaultAutoMoveIntervalTicks)
    {
        LegacyComponents = components ?? Array.Empty<ComponentKind>();
    }

    public EntityArchetype(int configId, int archetypeId, int entityTarget, IReadOnlyList<ComponentId> components, IReadOnlyList<string> tags, int defaultAutoMoveIntervalTicks)
    {
        ConfigId = configId;
        ArchetypeId = archetypeId;
        EntityTarget = entityTarget;
        ComponentIds = components ?? Array.Empty<ComponentId>();
        LegacyComponents = ComponentIds.Select(ComponentKindCompatibility.ToKind).Where(kind => kind != 0).ToArray();
        Tags = tags ?? Array.Empty<string>();
        DefaultAutoMoveIntervalTicks = Math.Max(1, defaultAutoMoveIntervalTicks);
    }

    public int ConfigId { get; }
    public int ArchetypeId { get; }
    public int EntityTarget { get; }
    public IReadOnlyList<ComponentId> ComponentIds { get; }
    public IReadOnlyList<ComponentKind> Components => LegacyComponents;
    private IReadOnlyList<ComponentKind> LegacyComponents { get; }
    public IReadOnlyList<string> Tags { get; }
    public int DefaultAutoMoveIntervalTicks { get; }
}

public static class ComponentKindCompatibility
{
    public static ComponentId ToId(ComponentKind kind)
    {
        return new ComponentId(kind);
    }

    public static ComponentKind ToKind(ComponentId id)
    {
        foreach (ComponentKind kind in Enum.GetValues(typeof(ComponentKind)))
        {
            if (new ComponentId(kind).Equals(id))
            {
                return kind;
            }
        }

        return 0;
    }
}
}
