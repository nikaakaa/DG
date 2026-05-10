using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public delegate void ComponentApplication(GameWorld world, IGameConfigProvider provider, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn);

public sealed class ComponentApplicationRegistry
{
    private readonly Dictionary<ComponentKind, ComponentApplication> applicators;

    public ComponentApplicationRegistry(IReadOnlyDictionary<ComponentKind, ComponentApplication> applicators)
    {
        this.applicators = new Dictionary<ComponentKind, ComponentApplication>(applicators ?? throw new ArgumentNullException(nameof(applicators)));
    }

    public static ComponentApplicationRegistry Default { get; } = CreateDefault();

    public IReadOnlyCollection<ComponentKind> RegisteredKinds => applicators.Keys.OrderBy(kind => (int)kind).ToArray();

    public bool IsRegistered(ComponentKind kind)
    {
        return applicators.ContainsKey(kind);
    }

    public void Apply(GameWorld world, IGameConfigProvider provider, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn, ComponentKind kind)
    {
        if (!applicators.TryGetValue(kind, out ComponentApplication applicator))
        {
            throw new InvalidOperationException("Unsupported component kind: " + kind);
        }

        applicator(world, provider, entity, archetype, spawn);
    }

    private static ComponentApplicationRegistry CreateDefault()
    {
        return new ComponentApplicationRegistry(new Dictionary<ComponentKind, ComponentApplication>
        {
            [ComponentKind.Position] = (world, _, entity, _, spawn) => world.SetComponent(entity, new PositionComponent(spawn.Position)),
            [ComponentKind.Direction] = (world, _, entity, _, spawn) => world.SetComponent(entity, new DirectionComponent(spawn.Direction)),
            [ComponentKind.Collider] = (world, _, entity, _, _) => world.SetComponent(entity, new ColliderComponent()),
            [ComponentKind.Blocking] = (world, _, entity, _, _) => world.AddStaticComponentSource(ComponentSourceContribution.Blocking(entity.EntityId, ComponentSourceKey.Static(entity.EntityId))),
            [ComponentKind.Bouncable] = (world, _, entity, _, _) => world.SetComponent(entity, new BouncableComponent()),
            [ComponentKind.AutoMove] = (world, _, entity, archetype, spawn) => world.AddStaticComponentSource(ComponentSourceContribution.AutoMove(entity.EntityId, ComponentSourceKey.Static(entity.EntityId), spawn.AutoMoveIntervalTicks > 0 ? spawn.AutoMoveIntervalTicks : archetype.DefaultAutoMoveIntervalTicks)),
            [ComponentKind.PlayerControl] = (world, _, entity, _, spawn) => world.SetComponent(entity, new PlayerControlComponent(spawn.PlayerId != 0 ? spawn.PlayerId : spawn.EntityId)),
            [ComponentKind.PushOnEnter] = (world, provider, entity, archetype, _) =>
            {
                if (!provider.TryGetPushOnEnter(archetype.ConfigId, out PushOnEnterConfig config))
                {
                    throw new InvalidOperationException("PushOnEnter config missing: " + archetype.ConfigId);
                }

                world.SetComponent(entity, new PushOnEnterComponent(config.OutputSpecId, config.OutputCostTicks));
            },
            [ComponentKind.Pushable] = (world, _, entity, _, _) => world.AddStaticComponentSource(ComponentSourceContribution.Pushable(entity.EntityId, ComponentSourceKey.Static(entity.EntityId))),
            [ComponentKind.PortConnector] = (world, provider, entity, archetype, _) =>
            {
                DirectionMask ports = provider.TryGetPortConnector(archetype.ConfigId, out PortConnectorConfig config) ? config.LocalPorts : DirectionMask.None;
                if (ports != DirectionMask.None)
                {
                    world.AddStaticComponentSource(ComponentSourceContribution.PortConnector(entity.EntityId, ComponentSourceKey.Static(entity.EntityId), ports));
                }
            }
        });
    }
}
}
