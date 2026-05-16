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
    public bool RotatePivot { get; set; }
    public bool HasMovementPermission { get; set; }
    public bool CanMove { get; set; } = true;
    public bool CanBePushed { get; set; } = true;
    public long ServerTick { get; set; }

    public EntitySnapshot ToSnapshot()
    {
        return new EntitySnapshot(EntityId, ConfigId, ArchetypeId, EntityTarget, X, Y, Direction, HasCollider, Blocking, Bouncable, AutoMove, AutoMoveIntervalTicks, PlayerControlled, Pushable, PortLocalPorts, RotatePivot, HasMovementPermission, CanMove, CanBePushed, ServerTick);
    }
}

public interface IEntitySnapshotProjector
{
    ComponentId ComponentId => new(PayloadId.Value);
    SnapshotPayloadId PayloadId { get; }
    void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder);
}

public interface IEntitySnapshotApplier
{
    ComponentId ComponentId => new(PayloadId.Value);
    SnapshotPayloadId PayloadId { get; }
    void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot);
}

public readonly struct SnapshotPayloadId : IEquatable<SnapshotPayloadId>
{
    public SnapshotPayloadId(string value)
        : this(RuntimeKeyUtility.StableRuntimeKey(value), value)
    {
    }

    public SnapshotPayloadId(ComponentId componentId)
        : this(componentId.RuntimeKey, componentId.Value)
    {
    }

    public SnapshotPayloadId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(SnapshotPayloadId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is SnapshotPayloadId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }

    public static implicit operator SnapshotPayloadId(string value)
    {
        return new SnapshotPayloadId(value);
    }
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

        if (!projector.PayloadId.Equals(applier.PayloadId))
        {
            throw new InvalidOperationException("Snapshot projector/applier id mismatch: " + projector.PayloadId + " / " + applier.PayloadId);
        }

        if (projectors.Any(item => item.PayloadId.Equals(projector.PayloadId)) || appliers.Any(item => item.PayloadId.Equals(applier.PayloadId)))
        {
            throw new InvalidOperationException("Duplicate snapshot payload id: " + projector.PayloadId);
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

        foreach (IEntitySnapshotProjector projector in projectors.OrderBy(item => item.PayloadId.RuntimeKey))
        {
            projector.Project(world, entity, builder);
        }

        return builder.ToSnapshot();
    }

    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot)
    {
        foreach (IEntitySnapshotApplier applier in appliers.OrderBy(item => item.PayloadId.RuntimeKey))
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
        registry.Register(new RotatePivotSnapshotProjection(), new RotatePivotSnapshotProjection());
        registry.Register(new MovementPermissionSnapshotProjection(), new MovementPermissionSnapshotProjection());
        return registry;
    }
}

public sealed class PositionSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "position";
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
    public SnapshotPayloadId PayloadId => "direction";
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.Direction = world.TryGetComponent(entity, out DirectionComponent direction) ? direction.Direction : Direction.None;
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => world.SetComponent(entity, new DirectionComponent(snapshot.Direction));
}

public sealed class ColliderSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "collider";
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
    public SnapshotPayloadId PayloadId => "blocking";
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.Blocking = world.HasComponent<BlockingComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.Blocking, new BlockingComponent());
}

public sealed class BouncableSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "bouncable";
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.Bouncable = world.HasComponent<BouncableComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.Bouncable, new BouncableComponent());
}

public sealed class AutoMoveSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "auto_move";
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
    public SnapshotPayloadId PayloadId => "player_control";
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.PlayerControlled = world.HasComponent<PlayerControlComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.PlayerControlled, new PlayerControlComponent(entity.EntityId));
}

public sealed class PushableSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "pushable";
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.Pushable = world.HasComponent<PushableComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.Pushable, new PushableComponent());
}

public sealed class PushOnEnterSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "push_on_enter";
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder)
    {
    }

    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot)
    {
    }
}

public sealed class PortConnectorSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "port_connector";
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

public sealed class RotatePivotSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "rotate_pivot";
    public void Project(GameWorld world, GameEntity entity, EntitySnapshotBuilder builder) => builder.RotatePivot = world.HasComponent<RotatePivotComponent>(entity);
    public void Apply(GameWorld world, GameEntity entity, EntitySnapshot snapshot) => ColliderSnapshotProjection.ApplyPresence(world, entity, snapshot.RotatePivot, new RotatePivotComponent());
}

public sealed class MovementPermissionSnapshotProjection : IEntitySnapshotProjector, IEntitySnapshotApplier
{
    public SnapshotPayloadId PayloadId => "movement_permission";
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
