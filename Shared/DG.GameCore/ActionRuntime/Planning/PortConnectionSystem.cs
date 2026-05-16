using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public static class PortConnectionSystem
{
    public static DirectionMask GetWorldPorts(GameWorld world, GameEntity entity)
    {
        if (!world.TryGetComponent(entity, out PortConnectorComponent connector))
        {
            return DirectionMask.None;
        }

        Direction orientation = world.TryGetComponent(entity, out DirectionComponent direction) ? direction.Direction : Direction.Right;
        return connector.LocalPorts.RotateBy(orientation);
    }

    public static bool HasWorldPort(GameWorld world, GameEntity entity, Direction direction)
    {
        return GetWorldPorts(world, entity).Contains(direction);
    }

    public static bool AreConnected(GameWorld world, GameEntity from, GameEntity to, Direction directionFromTo)
    {
        return HasWorldPort(world, from, directionFromTo) &&
            HasWorldPort(world, to, directionFromTo.Opposite());
    }

    public static IReadOnlyList<GameEntity> CollectConnectedGroup(GameWorld world, GameEntity root)
    {
        if (!world.HasComponent<PortConnectorComponent>(root))
        {
            return new[] { root };
        }

        var result = new List<GameEntity>();
        var visited = new HashSet<long>();
        var queue = new Queue<GameEntity>();
        queue.Enqueue(root);
        visited.Add(root.EntityId);

        while (queue.Count > 0)
        {
            GameEntity current = queue.Dequeue();
            result.Add(current);
            DirectionMask ports = GetWorldPorts(world, current);
            TryEnqueueNeighbor(world, current, Direction.Left, ports, visited, queue);
            TryEnqueueNeighbor(world, current, Direction.Right, ports, visited, queue);
            TryEnqueueNeighbor(world, current, Direction.Up, ports, visited, queue);
            TryEnqueueNeighbor(world, current, Direction.Down, ports, visited, queue);
        }

        return result.OrderBy(entity => entity.EntityId).ToArray();
    }

    private static void TryEnqueueNeighbor(GameWorld world, GameEntity current, Direction direction, DirectionMask ports, HashSet<long> visited, Queue<GameEntity> queue)
    {
        if (!ports.Contains(direction) ||
            !world.TryGetComponent(current, out PositionComponent position))
        {
            return;
        }

        IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(position.Coord.Add(direction));
        for (int i = 0; i < targets.Count; i++)
        {
            GameEntity target = targets[i];
            if (visited.Contains(target.EntityId) ||
                !world.HasComponent<PortConnectorComponent>(target) ||
                !AreConnected(world, current, target, direction))
            {
                continue;
            }

            visited.Add(target.EntityId);
            queue.Enqueue(target);
        }
    }
}
}
