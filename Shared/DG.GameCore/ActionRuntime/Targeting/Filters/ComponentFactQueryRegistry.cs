using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public delegate bool ComponentFactQuery(GameWorld world, GameEntity entity);

public sealed class ComponentFactQueryRegistry
{
    private readonly Dictionary<ComponentId, ComponentFactQuery> queries = new();

    public static ComponentFactQueryRegistry Default { get; } = CreateDefault();

    public void Register(ComponentId id, ComponentFactQuery query)
    {
        if (!id.IsValid)
        {
            throw new InvalidOperationException("Component query id is empty.");
        }

        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (queries.ContainsKey(id))
        {
            throw new InvalidOperationException("Duplicate component query id: " + id);
        }

        queries.Add(id, query);
    }

    public bool Has(GameWorld world, GameEntity entity, ComponentId id)
    {
        if (!queries.TryGetValue(id, out ComponentFactQuery query))
        {
            throw new InvalidOperationException("No component query registered for id: " + id);
        }

        return query(world, entity);
    }

    public bool Has(GameWorld world, GameEntity entity, ComponentKind kind)
    {
        return Has(world, entity, new ComponentId(kind));
    }

    private static ComponentFactQueryRegistry CreateDefault()
    {
        var registry = new ComponentFactQueryRegistry();
        registry.Register(new ComponentId(ComponentKind.Position), (world, entity) => world.HasComponent<PositionComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.Direction), (world, entity) => world.HasComponent<DirectionComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.Collider), (world, entity) => world.HasComponent<ColliderComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.Blocking), (world, entity) => world.HasComponent<BlockingComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.Bouncable), (world, entity) => world.HasComponent<BouncableComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.AutoMove), (world, entity) => world.HasComponent<AutoMoveComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.PlayerControl), (world, entity) => world.HasComponent<PlayerControlComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.PushOnEnter), (world, entity) => world.HasComponent<PushOnEnterComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.Pushable), (world, entity) => world.HasComponent<PushableComponent>(entity));
        registry.Register(new ComponentId(ComponentKind.PortConnector), (world, entity) => world.HasComponent<PortConnectorComponent>(entity));
        return registry;
    }
}
}
