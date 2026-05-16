using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class GameEntity
{
    private readonly HashSet<string> tags = new();

    public GameEntity(long entityId, int configId, int archetypeId, int entityTarget)
    {
        EntityId = entityId;
        ConfigId = configId;
        ArchetypeId = archetypeId;
        EntityTarget = entityTarget;
    }

    public long EntityId { get; }
    public int ConfigId { get; }
    public int ArchetypeId { get; }
    public int EntityTarget { get; }
    public IReadOnlyCollection<string> Tags => tags;

    public void AddTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            tags.Add(tag);
        }
    }

    public bool HasTag(string tag)
    {
        return tags.Contains(tag);
    }
}
}

