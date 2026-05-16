using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public static class EntityBuilder
{
    private static readonly ComponentApplicationRegistry Registry = ComponentApplicationRegistry.Default;

    public static bool AddEntity(GameWorld world, IGameConfigProvider provider, EntitySpawnSpec spawn)
    {
        if (world == null || provider == null || !provider.TryGetArchetype(spawn.ConfigId, out EntityArchetype archetype))
        {
            return false;
        }

        var entity = new GameEntity(spawn.EntityId, archetype.ConfigId, archetype.ArchetypeId, archetype.EntityTarget);
        for (int i = 0; i < archetype.Tags.Count; i++)
        {
            entity.AddTag(archetype.Tags[i]);
        }

        if (!world.AddEntity(entity))
        {
            return false;
        }

        ApplyComponents(world, provider, entity, archetype, spawn);
        ApplySpawnOverrides(world, entity, spawn);
        ApplyTags(world, entity, archetype);
        world.ResolveComponentResults();
        return true;
    }

    public static bool AddOrUpdateEntity(GameWorld world, IGameConfigProvider provider, EntitySpawnSpec spawn)
    {
        if (world == null)
        {
            return false;
        }

        if (world.TryGetEntity(spawn.EntityId, out _))
        {
            world.RemoveEntity(spawn.EntityId);
        }

        return AddEntity(world, provider, spawn);
    }

    private static void ApplyComponents(GameWorld world, IGameConfigProvider provider, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn)
    {
        for (int i = 0; i < archetype.ComponentIds.Count; i++)
        {
            ApplyComponent(world, provider, entity, archetype, spawn, archetype.ComponentIds[i]);
        }
    }

    private static void ApplyTags(GameWorld world, GameEntity entity, EntityArchetype archetype)
    {
        WorldTag tags = WorldTag.None;
        for (int i = 0; i < archetype.Tags.Count; i++)
        {
            if (Enum.TryParse(archetype.Tags[i], out WorldTag tag))
            {
                tags |= tag;
            }
        }

        if (tags != WorldTag.None)
        {
            world.AddStaticTagSource(entity.EntityId, ComponentSourceKey.Static(entity.EntityId), tags);
        }
    }

    private static void ApplySpawnOverrides(GameWorld world, GameEntity entity, EntitySpawnSpec spawn)
    {
        if (spawn.RotatePivot)
        {
            world.AddStaticComponentSource(ComponentSourceContribution.RotatePivot(entity.EntityId, ComponentSourceKey.Static(entity.EntityId)));
        }
    }

    private static void ApplyComponent(GameWorld world, IGameConfigProvider provider, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn, ComponentId id)
    {
        Registry.Apply(world, provider, entity, archetype, spawn, id);
    }
}
}
