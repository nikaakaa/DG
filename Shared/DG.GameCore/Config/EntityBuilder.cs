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
        for (int i = 0; i < archetype.Components.Count; i++)
        {
            ApplyComponent(world, provider, entity, archetype, spawn, archetype.Components[i]);
        }
    }

    private static void ApplyComponent(GameWorld world, IGameConfigProvider provider, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn, ComponentKind kind)
    {
        Registry.Apply(world, provider, entity, archetype, spawn, kind);
    }
}
}
