using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class MovementResolveSystem
{
    public MoveResult Resolve(GameWorld world, MoveCommand command)
    {
        if (!world.TryGetEntity(command.EntityId, out GameEntity entity))
        {
            return new MoveResult(false, command.EntityId, default, Direction.None, MoveErrorCode.UnknownEntity, "unknown entity", false, default, command.ClientTick);
        }

        if (!world.TryGetComponent(entity, out PositionComponent position))
        {
            return new MoveResult(false, command.EntityId, default, Direction.None, MoveErrorCode.MissingPosition, "missing position", false, default, command.ClientTick);
        }

        GridCoord current = position.Coord;
        GridCoord target;
        bool hasDirection = world.TryGetComponent(entity, out DirectionComponent directionComponent);
        Direction finalDirection = hasDirection ? directionComponent.Direction : Direction.None;

        if (command.Target.HasValue)
        {
            target = command.Target.Value;
            if (current.ManhattanDistance(target) > 1)
            {
                return new MoveResult(false, entity.EntityId, current, finalDirection, MoveErrorCode.TooFar, "target too far", false, default, command.ClientTick);
            }
        }
        else
        {
            Direction direction = command.Direction != Direction.None ? command.Direction : finalDirection;
            if (direction == Direction.None)
            {
                return new MoveResult(false, entity.EntityId, current, finalDirection, MoveErrorCode.InvalidDirection, "invalid direction", false, default, command.ClientTick);
            }

            finalDirection = direction;
            target = current.Add(direction);
        }

        IReadOnlyList<GameEntity> targetEntities = world.GetEntitiesAt(target);
        for (int i = 0; i < targetEntities.Count; i++)
        {
            GameEntity targetEntity = targetEntities[i];
            if (targetEntity.EntityId == entity.EntityId || !world.HasComponent<BlockingComponent>(targetEntity))
            {
                continue;
            }

            bool targetPlayerControlled = world.HasComponent<PlayerControlComponent>(targetEntity);
            MoveErrorCode errorCode = targetPlayerControlled ? MoveErrorCode.Occupied : MoveErrorCode.Blocked;
            string reason = targetPlayerControlled ? "occupied by player" : "blocked cell";
            var collision = new CollisionInfo(targetEntity.EntityId, true, targetPlayerControlled);
            if (world.HasComponent<BouncableComponent>(entity) && hasDirection)
            {
                finalDirection = directionComponent.Direction.Opposite();
                world.SetDirection(entity, finalDirection);
                return new MoveResult(false, entity.EntityId, current, finalDirection, errorCode, reason, true, collision, command.ClientTick);
            }

            return new MoveResult(false, entity.EntityId, current, finalDirection, errorCode, reason, false, collision, command.ClientTick);
        }

        if (hasDirection && command.Direction != Direction.None && directionComponent.Direction != command.Direction)
        {
            world.SetDirection(entity, command.Direction);
        }

        world.MoveEntity(entity, target);
        return new MoveResult(true, entity.EntityId, target, finalDirection, MoveErrorCode.None, string.Empty, false, default, command.ClientTick);
    }
}

public sealed class AutoMoveSystem
{
    private readonly MovementResolveSystem movementResolveSystem;

    public AutoMoveSystem(MovementResolveSystem movementResolveSystem)
    {
        this.movementResolveSystem = movementResolveSystem;
    }

    public IReadOnlyList<MoveResult> Tick(GameWorld world)
    {
        long tick = world.NextTick();
        var results = new List<MoveResult>();
        IReadOnlyList<GameEntity> entities = world.EnumerateEntities();
        for (int i = 0; i < entities.Count; i++)
        {
            GameEntity entity = entities[i];
            if (!world.TryGetComponent(entity, out PositionComponent _) ||
                !world.TryGetComponent(entity, out DirectionComponent directionComponent) ||
                !world.TryGetComponent(entity, out AutoMoveComponent autoMove))
            {
                continue;
            }

            if (tick - autoMove.LastMoveTick < autoMove.IntervalTicks)
            {
                continue;
            }

            autoMove.LastMoveTick = tick;
            world.SetAutoMove(entity, autoMove);
            MoveCommand command = MoveCommand.ToDirection(entity.EntityId, directionComponent.Direction, MoveCommandSource.AutoTick, tick, 0);
            results.Add(movementResolveSystem.Resolve(world, command));
        }

        return results;
    }
}

public sealed class PushOnEnterSystem
{
    private readonly MovementResolveSystem movementResolveSystem;

    public PushOnEnterSystem(MovementResolveSystem movementResolveSystem)
    {
        this.movementResolveSystem = movementResolveSystem;
    }

    public IReadOnlyList<MoveResult> Tick(GameWorld world)
    {
        var results = new List<MoveResult>();
        var moved = new HashSet<long>();
        IReadOnlyList<GameEntity> triggers = world.EnumerateEntities();
        for (int i = 0; i < triggers.Count; i++)
        {
            GameEntity trigger = triggers[i];
            if (!world.TryGetComponent(trigger, out PositionComponent triggerPosition) ||
                !world.TryGetComponent(trigger, out DirectionComponent triggerDirection) ||
                !world.HasComponent<PushOnEnterComponent>(trigger))
            {
                continue;
            }

            IReadOnlyList<GameEntity> targets = world.GetEntitiesAt(triggerPosition.Coord);
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                GameEntity target = targets[targetIndex];
                if (target.EntityId == trigger.EntityId ||
                    moved.Contains(target.EntityId) ||
                    !world.TryGetComponent(target, out PositionComponent _))
                {
                    continue;
                }

                moved.Add(target.EntityId);
                MoveCommand command = MoveCommand.ToDirection(target.EntityId, triggerDirection.Direction, MoveCommandSource.PushOnEnter, world.ServerTick, 0);
                results.Add(movementResolveSystem.Resolve(world, command));
            }
        }

        return results;
    }
}
}

