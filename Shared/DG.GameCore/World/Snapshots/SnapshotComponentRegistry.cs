using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class EntitySnapshotBuilder
{
    public long EntityId { get; set; }
    public int ConfigId { get; set; }
    public int ArchetypeId { get; set; }
    public int EntityTarget { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Direction Direction { get; set; }
    public bool HasCollider { get; set; }
    public bool Blocking { get; set; }
    public bool Bouncable { get; set; }
    public bool AutoMove { get; set; }
    public int AutoMoveIntervalTicks { get; set; }
    public bool PlayerControlled { get; set; }
    public bool Pushable { get; set; }
    public DirectionMask PortLocalPorts { get; set; }
    public bool HasMovementPermission { get; set; }
    public bool CanMove { get; set; } = true;
    public bool CanBePushed { get; set; } = true;
    public long ServerTick { get; set; }

    public EntitySnapshot ToSnapshot()
    {
        return new EntitySnapshot(EntityId, ConfigId, ArchetypeId, EntityTarget, X, Y, Direction, HasCollider, Blocking, Bouncable, AutoMove, AutoMoveIntervalTicks, PlayerControlled, Pushable, PortLocalPorts, HasMovementPermission, CanMove, CanBePushed, ServerTick);
    }
}

public interface IEntitySnapshotProjector
{
    ComponentId ComponentId { get; }
    void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder);
}

public interface IEntitySnapshotApplier
{
    ComponentId ComponentId { get; }
    void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot);
}

public sealed class SnapshotComponentRegistry
{
    private readonly List<IEntitySnapshotProjector> projectors = new();
    private readonly List<IEntitySnapshotApplier> appliers = new();

    public static SnapshotComponentRegistry Default { get; } = CreateDefault();

    public void Register(IEntitySnapshotProjector projector, IEntitySnapshotApplier applier)
    {
        if (projector == null)
        {
            throw new ArgumentNullException(nameof(projector));
        }

        if (applier == null)
        {
            throw new ArgumentNullException(nameof(applier));
        }

        if (!projector.ComponentId.Equals(applier.ComponentId))
        {
            throw new InvalidOperationException("Snapshot projector/applier id mismatch: " + projector.ComponentId + " / " + applier.ComponentId);
        }

        if (projectors.Any(item => item.ComponentId.Equals(projector.ComponentId)) || appliers.Any(item => item.ComponentId.Equals(applier.ComponentId)))
        {
            throw new InvalidOperationException("Duplicate snapshot component id: " + projector.ComponentId);
        }

        projectors.Add(projector);
        appliers.Add(applier);
    }

    public EntitySnapshot Project(GameWorld world, GameEntity entity, long serverTick)
    {
        var builder = new EntitySnapshotBuilder
        {
            EntityId = entity.EntityId,
            ConfigId = entity.ConfigId,
            ArchetypeId = entity.ArchetypeId,
            EntityTarget = entity.EntityTarget,
            ServerTick = serverTick
        };

        foreach (IEntitySnapshotProjector projector in projectors.OrderBy(item => item.ComponentId.RuntimeKey))
        {
            projector.Project(world, entity, builder);
        }

        return builder.ToSnapshot();
    }

    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot)
    {
        foreach (IEntitySnapshotApplier applier in appliers.OrderBy(item => item.ComponentId.RuntimeKey))
        {
            applier.Apply(world, entity, snapshot);
        }
    }

    private static SnapshotComponentRegistry CreateDefault()
    {
        var registry = new SnapshotComponentRegistry();
        registry.Register(new PositionSnapshotProjection(), new PositionSnapshotProjection());
        registry.Register(new DirectionSnapshotProjection(), new DirectionSnapshotProjection());
        registry.Register(new ColliderSnapshotProjection(), new ColliderSnapshotProjection());
        registry.Register(new BlockingSnapshotProjection(), new BlockingSnapshotProjection());
        registry.Register(new BouncableSnapshotProjection(), new BouncableSnapshotProjection());
        registry.Register(new AutoMoveSnapshotProjection(), new AutoMoveSnapshotProjection());
        registry.Register(new PlayerControlSnapshotProjection(), new PlayerControlSnapshotProjection());
        registry.Register(new PushOnEnterSnapshotProjection(), new PushOnEnterSnapshotProjection());
        registry.Register(new PushableSnapshotProjection(), new PushableSnapshotProjection());
        registry.Register(new PortConnectorSnapshotProjection(), new PortConnectorSnapshotProjection());
        registry.Register(new MovementPermissionSnapshotProjection(), new MovementPermissionSnapshotProjection());
        return registry;
    }
}

public sealed class PositionSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.Position);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder)
    {
        if (world.TryGetComponent(entity, out PositionComponent position))
        {
            builder.X = position.Coord.X;
            builder.Y = position.Coord.Y;
        }
    }

    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => world.SetComponent(entity, new PositionComponent(new GridCoord(snapshot.X, snapshot.Y)));
}

public sealed class DirectionSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.Direction);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.Direction = world.TryGetComponent(entity, out DirectionComponent direction) ? direction.Direction : Direction.None;
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => world.SetComponent(entity, new DirectionComponent(snapshot.Direction));
}

public sealed class ColliderSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.Collider);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.HasCollider = world.HasComponent<ColliderComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ApplyPresence(world, entity, snapshot.HasCollider, new ColliderComponent());
    internal static void ApplyPresence<TComponent>(GameWorld world, GameEntity entity, bool desired, TComponent component) where TComponent : struct
    {
        if (desired)
        {
            world.SetComponent(entity, component);
            return;
        }

        world.RemoveComponent<TComponent>(entity);
    }
}

public sealed class BlockingSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.Blocking);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.Blocking = world.HasComponent<BlockingComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.Blocking, new BlockingComponent());
}

public sealed class BouncableSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.Bouncable);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.Bouncable = world.HasComponent<BouncableComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.Bouncable, new BouncableComponent());
}

public sealed class AutoMoveSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.AutoMove);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder)
    {
        builder.AutoMove = world.TryGetComponent(entity, out AutoMoveComponent autoMove);
        builder.AutoMoveIntervalTicks = builder.AutoMove ? autoMove.IntervalTicks : 0;
    }

    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot)
    {
        if (!snapshot.AutoMove)
        {
            world.RemoveComponent<AutoMoveComponent>(entity);
            return;
        }

        long lastMoveTick = world.TryGetComponent(entity, out AutoMoveComponent existing) ? existing.LastMoveTick : 0;
        var component = new AutoMoveComponent(snapshot.AutoMoveIntervalTicks > 0 ? snapshot.AutoMoveIntervalTicks : 1) { LastMoveTick = lastMoveTick };
        world.SetComponent(entity, component);
    }
}

public sealed class PlayerControlSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.PlayerControl);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.PlayerControlled = world.HasComponent<PlayerControlComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.PlayerControlled, new PlayerControlComponent(entity.EntityId));
}

public sealed class PushableSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.Pushable);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.Pushable = world.HasComponent<PushableComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.Pushable, new PushableComponent());
}

public sealed class PushOnEnterSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.PushOnEnter);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder)
    {
    }

    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot)
    {
    }
}

public sealed class PortConnectorSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new(ComponentKind.PortConnector);
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.PortLocalPorts = world.TryGetComponent(entity, out PortConnectorComponent portConnector) ? portConnector.LocalPorts : DirectionMask.None;
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot)
    {
        if (snapshot.PortLocalPorts == DirectionMask.None)
        {
            world.RemoveComponent<PortConnectorComponent>(entity);
            return;
        }

        world.SetComponent(entity, new PortConnectorComponent(snapshot.PortLocalPorts));
    }
}

public sealed class MovementPermissionSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public ComponentId ComponentId => new("MovementPermission");
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder)
    {
        builder.HasMovementPermission = world.TryGetComponent(entity, out MovementPermissionComponent permission);
        builder.CanMove = !builder.HasMovementPermission || permission.CanMove;
        builder.CanBePushed = !builder.HasMovementPermission || permission.CanBePushed;
    }

    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot)
    {
        if (!snapshot.HasMovementPermission)
        {
            world.RemoveComponent<MovementPermissionComponent>(entity);
            return;
        }

        world.SetComponent(entity, new MovementPermissionComponent(snapshot.CanMove, snapshot.CanBePushed));
    }
}
}
