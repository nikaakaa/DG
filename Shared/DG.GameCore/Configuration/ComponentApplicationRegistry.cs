using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public delegate void ComponentApplication(GameWorld world, IGameConfigProvider provider, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn);

public sealed class ComponentApplicationRegistry
{
    private readonly Dictionary<ComponentId, ComponentApplication> applicators;

    public ComponentApplicationRegistry(IReadOnlyDictionary<ComponentKind, ComponentApplication> applicators)
        : this((applicators ?? throw new ArgumentNullException(nameof(applicators))).ToDictionary(pair => new ComponentId(pair.Key), pair => pair.Value))
    {
    }

    public ComponentApplicationRegistry(IReadOnlyDictionary<ComponentId, ComponentApplication> applicators)
    {
        this.applicators = new Dictionary<ComponentId, ComponentApplication>(applicators ?? throw new ArgumentNullException(nameof(applicators)));
    }

    public static ComponentApplicationRegistry Default { get; } = CreateDefault();

    public IReadOnlyCollection<ComponentKind> RegisteredKinds => applicators.Keys.Select(ComponentKindCompatibility.ToKind).Where(kind => kind != 0).OrderBy(kind => (int)kind).ToArray();
    public IReadOnlyCollection<ComponentId> RegisteredIds => applicators.Keys.OrderBy(id => id.RuntimeKey).ToArray();

    public bool IsRegistered(ComponentKind kind)
    {
        return IsRegistered(new ComponentId(kind));
    }

    public bool IsRegistered(ComponentId id)
    {
        return applicators.ContainsKey(id);
    }

    public void Apply(GameWorld world, IGameConfigProvider provider, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn, ComponentKind kind)
    {
        if (!IsRegistered(kind))
        {
            throw new InvalidOperationException("Unsupported component kind: " + kind);
        }

        Apply(world, provider, entity, archetype, spawn, new ComponentId(kind));
    }

    public void Apply(GameWorld world, IGameConfigProvider provider, GameEntity entity, EntityArchetype archetype, EntitySpawnSpec spawn, ComponentId id)
    {
        if (!applicators.TryGetValue(id, out ComponentApplication applicator))
        {
            throw new InvalidOperationException("Unsupported component id: " + id);
        }

        applicator(world, provider, entity, archetype, spawn);
    }

    private static ComponentApplicationRegistry CreateDefault()
    {
        return new ComponentApplicationRegistry(new Dictionary<ComponentId, ComponentApplication>
        {
            [new ComponentId(ComponentKind.Position)] = (world, _, entity, _, spawn) => world.SetComponent(entity, new PositionComponent(spawn.Position)),
            [new ComponentId(ComponentKind.Direction)] = (world, _, entity, _, spawn) => world.SetComponent(entity, new DirectionComponent(spawn.Direction)),
            [new ComponentId(ComponentKind.Collider)] = (world, _, entity, _, _) => world.SetComponent(entity, new ColliderComponent()),
            [new ComponentId(ComponentKind.Blocking)] = (world, _, entity, _, _) => world.AddStaticComponentSource(ComponentSourceContribution.Blocking(entity.EntityId, ComponentSourceKey.Static(entity.EntityId))),
            [new ComponentId(ComponentKind.Bouncable)] = (world, _, entity, _, _) => world.SetComponent(entity, new BouncableComponent()),
            [new ComponentId(ComponentKind.AutoMove)] = (world, _, entity, archetype, spawn) => world.AddStaticComponentSource(ComponentSourceContribution.AutoMove(entity.EntityId, ComponentSourceKey.Static(entity.EntityId), spawn.AutoMoveIntervalTicks > 0 ? spawn.AutoMoveIntervalTicks : archetype.DefaultAutoMoveIntervalTicks)),
            [new ComponentId(ComponentKind.PlayerControl)] = (world, _, entity, _, spawn) => world.SetComponent(entity, new PlayerControlComponent(spawn.PlayerId != 0 ? spawn.PlayerId : spawn.EntityId)),
            [new ComponentId(ComponentKind.PushOnEnter)] = (world, provider, entity, archetype, _) =>
            {
                if (!provider.TryGetPushOnEnter(archetype.ConfigId, out PushOnEnterConfig config))
                {
                    throw new InvalidOperationException("PushOnEnter config missing: " + archetype.ConfigId);
                }

                world.SetComponent(entity, new PushOnEnterComponent(config.OutputSpecId, config.OutputCostTicks));
            },
            [new ComponentId(ComponentKind.Pushable)] = (world, _, entity, _, _) => world.AddStaticComponentSource(ComponentSourceContribution.Pushable(entity.EntityId, ComponentSourceKey.Static(entity.EntityId))),
            [new ComponentId(ComponentKind.PortConnector)] = (world, provider, entity, archetype, _) =>
            {
                DirectionMask ports = provider.TryGetPortConnector(archetype.ConfigId, out PortConnectorConfig config) ? config.LocalPorts : DirectionMask.None;
                if (ports != DirectionMask.None)
                {
                    world.AddStaticComponentSource(ComponentSourceContribution.PortConnector(entity.EntityId, ComponentSourceKey.Static(entity.EntityId), ports));
                }
            },
            [new ComponentId(ComponentKind.RotatePivot)] = (world, _, entity, _, _) => world.AddStaticComponentSource(ComponentSourceContribution.RotatePivot(entity.EntityId, ComponentSourceKey.Static(entity.EntityId)))
        });
    }
}
}
