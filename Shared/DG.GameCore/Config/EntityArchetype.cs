using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class EntityArchetype
{
    public EntityArchetype(int configId, int archetypeId, int entityTarget, IReadOnlyList<ComponentKind> components, IReadOnlyList<string> tags, int defaultAutoMoveIntervalTicks)
    {
        ConfigId = configId;
        ArchetypeId = archetypeId;
        EntityTarget = entityTarget;
        Components = components ?? Array.Empty<ComponentKind>();
        Tags = tags ?? Array.Empty<string>();
        DefaultAutoMoveIntervalTicks = Math.Max(1, defaultAutoMoveIntervalTicks);
    }

    public int ConfigId { get; }
    public int ArchetypeId { get; }
    public int EntityTarget { get; }
    public IReadOnlyList<ComponentKind> Components { get; }
    public IReadOnlyList<string> Tags { get; }
    public int DefaultAutoMoveIntervalTicks { get; }
}
}
