using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct EffectContribution
{
    public EffectContribution(long entityId, ComponentSourceKey source, EffectKind kind, ComponentSourceContribution? component, WorldTag addTag, WorldTag removeTag)
    {
        EntityId = entityId;
        Source = source;
        Kind = kind;
        Component = component;
        AddTag = addTag;
        RemoveTag = removeTag;
    }

    public long EntityId { get; }
    public ComponentSourceKey Source { get; }
    public EffectKind Kind { get; }
    public ComponentSourceContribution? Component { get; }
    public WorldTag AddTag { get; }
    public WorldTag RemoveTag { get; }
}

}
