using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class FallbackGameConfigProvider : IGameConfigProvider
{
    public static FallbackGameConfigProvider Instance { get; } = new FallbackGameConfigProvider();

    private readonly Dictionary<int, EntityArchetype> archetypes = new Dictionary<int, EntityArchetype>
    {
        [DefaultWorldConfig.PlayerConfigId] = new EntityArchetype(
            DefaultWorldConfig.PlayerConfigId,
            DefaultWorldConfig.PlayerArchetypeId,
            DefaultWorldConfig.PlayerTarget,
            new[] { ComponentKind.Position, ComponentKind.Collider, ComponentKind.Blocking, ComponentKind.PlayerControl },
            new[] { "Entity.Player" },
            1),
        [DefaultWorldConfig.BallConfigId] = new EntityArchetype(
            DefaultWorldConfig.BallConfigId,
            DefaultWorldConfig.BallArchetypeId,
            DefaultWorldConfig.BallTarget,
            new[] { ComponentKind.Position, ComponentKind.Direction, ComponentKind.Collider, ComponentKind.Blocking, ComponentKind.Bouncable, ComponentKind.AutoMove },
            new[] { "Entity.Ball", "Movement.Bouncable" },
            1),
        [DefaultWorldConfig.BlockerConfigId] = new EntityArchetype(
            DefaultWorldConfig.BlockerConfigId,
            DefaultWorldConfig.BlockerArchetypeId,
            DefaultWorldConfig.BlockerTarget,
            new[] { ComponentKind.Position, ComponentKind.Collider, ComponentKind.Blocking },
            new[] { "Entity.Blocker" },
            1),
        [DefaultWorldConfig.PushableBlockerConfigId] = new EntityArchetype(
            DefaultWorldConfig.PushableBlockerConfigId,
            DefaultWorldConfig.PushableBlockerArchetypeId,
            DefaultWorldConfig.BlockerTarget,
            new[] { ComponentKind.Position, ComponentKind.Collider, ComponentKind.Blocking, ComponentKind.Pushable },
            new[] { "Entity.PushableBlocker" },
            1),
        [DefaultWorldConfig.PortConnectorBlockerConfigId] = new EntityArchetype(
            DefaultWorldConfig.PortConnectorBlockerConfigId,
            DefaultWorldConfig.PortConnectorBlockerArchetypeId,
            DefaultWorldConfig.BlockerTarget,
            new[] { ComponentKind.Position, ComponentKind.Direction, ComponentKind.Collider, ComponentKind.Blocking, ComponentKind.Pushable, ComponentKind.PortConnector },
            new[] { "Entity.PortConnectorBlocker" },
            1),
        [DefaultWorldConfig.ConveyorConfigId] = new EntityArchetype(
            DefaultWorldConfig.ConveyorConfigId,
            DefaultWorldConfig.ConveyorArchetypeId,
            DefaultWorldConfig.BlockerTarget,
            new[] { ComponentKind.Position, ComponentKind.Direction, ComponentKind.Collider, ComponentKind.PushOnEnter },
            new[] { "Tile.Conveyor" },
            1)
    };

    private readonly EntitySpawnSpec[] demoSpawns =
    {
        DefaultWorldConfig.BallSpawn(DefaultWorldConfig.BallEntityId, new GridCoord(-2, 0), Direction.Right, 1),
        DefaultWorldConfig.PushableBlockerSpawn(DefaultWorldConfig.FirstBlockerEntityId - 1, new GridCoord(0, 1)),
        DefaultWorldConfig.PortConnectorBlockerSpawn(DefaultWorldConfig.PortConnectorEntityId, new GridCoord(0, -1), Direction.Right),
        DefaultWorldConfig.PortConnectorBlockerSpawn(DefaultWorldConfig.PortConnectorEntityId + 1, new GridCoord(1, -1), Direction.Right),
        DefaultWorldConfig.BlockerSpawn(DefaultWorldConfig.FirstBlockerEntityId, new GridCoord(4, 0)),
        DefaultWorldConfig.BlockerSpawn(DefaultWorldConfig.FirstBlockerEntityId + 1, new GridCoord(-4, 0)),
        DefaultWorldConfig.ConveyorSpawn(DefaultWorldConfig.ConveyorEntityId, new GridCoord(1, 0), Direction.Right),
        DefaultWorldConfig.ConveyorSpawn(DefaultWorldConfig.BlockedConveyorEntityId, new GridCoord(3, 0), Direction.Right)
    };

    private readonly PlayerSpawnRule defaultSpawnRule = new PlayerSpawnRule(DefaultWorldConfig.DefaultPlayerSpawnRuleId, DefaultWorldConfig.PlayerConfigId, new GridCoord(0, 0), new GridCoord(0, 1), 1024);
    private readonly Dictionary<int, PortConnectorConfig> portConnectors = new Dictionary<int, PortConnectorConfig>
    {
        [DefaultWorldConfig.PortConnectorBlockerConfigId] = new PortConnectorConfig(DefaultWorldConfig.PortConnectorBlockerConfigId, DirectionMask.Left | DirectionMask.Right)
    };

    public bool TryGetArchetype(int configId, out EntityArchetype archetype)
    {
        return archetypes.TryGetValue(configId, out archetype);
    }

    public IReadOnlyList<EntityArchetype> GetEntityArchetypes()
    {
        return archetypes.Values
            .OrderBy(archetype => archetype.ConfigId)
            .ToArray();
    }

    public IReadOnlyList<EntitySpawnSpec> GetWorldSpawns(string worldId)
    {
        return worldId == DefaultWorldConfig.DemoWorldId ? demoSpawns : Array.Empty<EntitySpawnSpec>();
    }

    public bool TryGetPlayerSpawnRule(string ruleId, out PlayerSpawnRule rule)
    {
        if (ruleId == DefaultWorldConfig.DefaultPlayerSpawnRuleId)
        {
            rule = defaultSpawnRule;
            return true;
        }

        rule = default;
        return false;
    }

    public bool TryGetPortConnector(int configId, out PortConnectorConfig config)
    {
        return portConnectors.TryGetValue(configId, out config);
    }

    public bool TryGetPushOnEnter(int configId, out PushOnEnterConfig config)
    {
        config = default;
        return false;
    }
}
}
