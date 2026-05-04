using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public static class EntityBuilder
{
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

        ApplyComponents(world, entity, archetype, spawn);
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

    private static void ApplyComponents(GameWorld world, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn)
    {
        for (int i = 0; i < archetype.Components.Count; i++)
        {
            ApplyComponent(world, entity, archetype, spawn, archetype.Components[i]);
        }
    }

    private static void ApplyComponent(GameWorld world, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn, ComponentKind kind)
    {
        switch (kind)
        {
            case ComponentKind.Position:
                world.SetComponent(entity, new PositionComponent(spawn.Position));
                break;
            case ComponentKind.Direction:
                world.SetComponent(entity, new DirectionComponent(spawn.Direction));
                break;
            case ComponentKind.Collider:
                world.SetComponent(entity, new ColliderComponent());
                break;
            case ComponentKind.Blocking:
                world.SetComponent(entity, new BlockingComponent());
                break;
            case ComponentKind.Bouncable:
                world.SetComponent(entity, new BouncableComponent());
                break;
            case ComponentKind.AutoMove:
                world.SetComponent(entity, new AutoMoveComponent(spawn.AutoMoveIntervalTicks > 0 ? spawn.AutoMoveIntervalTicks : archetype.DefaultAutoMoveIntervalTicks));
                break;
            case ComponentKind.PlayerControl:
                world.SetComponent(entity, new PlayerControlComponent(spawn.PlayerId != 0 ? spawn.PlayerId : spawn.EntityId));
                break;
            case ComponentKind.PushOnEnter:
                world.SetComponent(entity, new PushOnEnterComponent());
                break;
            default:
                throw new InvalidOperationException("Unsupported component kind: " + kind);
        }
    }
}
}
