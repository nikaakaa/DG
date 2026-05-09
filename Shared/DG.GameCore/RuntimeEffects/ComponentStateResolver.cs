using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum ComponentResultKind
{
    Blocking = 1,
    AutoMove = 2,
    Pushable = 3,
    PortConnector = 4,
    MovementPermission = 5
}

public readonly struct ComponentSourceKey : IEquatable<ComponentSourceKey>
{
    public ComponentSourceKey(string kind, long id)
    {
        Kind = kind ?? string.Empty;
        Id = id;
    }

    public string Kind { get; }
    public long Id { get; }

    public static ComponentSourceKey Static(long entityId)
    {
        return new ComponentSourceKey("static", entityId);
    }

    public static ComponentSourceKey Runtime(RuntimeEffectId id)
    {
        return new ComponentSourceKey("runtime", id.Value);
    }

    public bool Equals(ComponentSourceKey other)
    {
        return Kind == other.Kind && Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is ComponentSourceKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Kind != null ? Kind.GetHashCode() : 0) * 397) ^ Id.GetHashCode();
        }
    }
}

public readonly struct ComponentSourceContribution
{
    public ComponentSourceContribution(long entityId, ComponentResultKind kind, ComponentSourceKey source, int autoMoveIntervalTicks, DirectionMask portMask, bool canMove, bool canBePushed)
    {
        EntityId = entityId;
        Kind = kind;
        Source = source;
        AutoMoveIntervalTicks = Math.Max(1, autoMoveIntervalTicks);
        PortMask = portMask;
        CanMove = canMove;
        CanBePushed = canBePushed;
    }

    public long EntityId { get; }
    public ComponentResultKind Kind { get; }
    public ComponentSourceKey Source { get; }
    public int AutoMoveIntervalTicks { get; }
    public DirectionMask PortMask { get; }
    public bool CanMove { get; }
    public bool CanBePushed { get; }

    public static ComponentSourceContribution Blocking(long entityId, ComponentSourceKey source)
    {
        return new ComponentSourceContribution(entityId, ComponentResultKind.Blocking, source, 1, DirectionMask.None, true, true);
    }

    public static ComponentSourceContribution AutoMove(long entityId, ComponentSourceKey source, int intervalTicks)
    {
        return new ComponentSourceContribution(entityId, ComponentResultKind.AutoMove, source, intervalTicks, DirectionMask.None, true, true);
    }

    public static ComponentSourceContribution Pushable(long entityId, ComponentSourceKey source)
    {
        return new ComponentSourceContribution(entityId, ComponentResultKind.Pushable, source, 1, DirectionMask.None, true, true);
    }

    public static ComponentSourceContribution PortConnector(long entityId, ComponentSourceKey source, DirectionMask portMask)
    {
        return new ComponentSourceContribution(entityId, ComponentResultKind.PortConnector, source, 1, portMask, true, true);
    }

    public static ComponentSourceContribution MovementPermission(long entityId, ComponentSourceKey source, bool canMove, bool canBePushed)
    {
        return new ComponentSourceContribution(entityId, ComponentResultKind.MovementPermission, source, 1, DirectionMask.None, canMove, canBePushed);
    }
}

public sealed class ComponentStateResolver
{
    private readonly Dictionary<long, List<ComponentSourceContribution>> staticSources = new();
    private readonly HashSet<long> trackedEntityIds = new();

    public void AddStaticSource(ComponentSourceContribution contribution)
    {
        if (!staticSources.TryGetValue(contribution.EntityId, out List<ComponentSourceContribution> contributions))
        {
            contributions = new List<ComponentSourceContribution>();
            staticSources.Add(contribution.EntityId, contributions);
        }

        contributions.RemoveAll(item => item.Kind == contribution.Kind && item.Source.Equals(contribution.Source));
        contributions.Add(contribution);
        trackedEntityIds.Add(contribution.EntityId);
    }

    public void CaptureStaticSources(GameWorld world, GameEntity entity)
    {
        var source = ComponentSourceKey.Static(entity.EntityId);
        var contributions = new List<ComponentSourceContribution>();
        if (world.HasComponent<BlockingComponent>(entity))
        {
            contributions.Add(ComponentSourceContribution.Blocking(entity.EntityId, source));
        }

        if (world.TryGetComponent(entity, out AutoMoveComponent autoMove))
        {
            contributions.Add(ComponentSourceContribution.AutoMove(entity.EntityId, source, autoMove.IntervalTicks));
        }

        if (world.HasComponent<PushableComponent>(entity))
        {
            contributions.Add(ComponentSourceContribution.Pushable(entity.EntityId, source));
        }

        if (world.TryGetComponent(entity, out PortConnectorComponent portConnector))
        {
            contributions.Add(ComponentSourceContribution.PortConnector(entity.EntityId, source, portConnector.LocalPorts));
        }

        if (world.TryGetComponent(entity, out MovementPermissionComponent permission))
        {
            contributions.Add(ComponentSourceContribution.MovementPermission(entity.EntityId, source, permission.CanMove, permission.CanBePushed));
        }

        if (contributions.Count == 0)
        {
            staticSources.Remove(entity.EntityId);
            return;
        }

        staticSources[entity.EntityId] = contributions;
        trackedEntityIds.Add(entity.EntityId);
    }

    public void RemoveEntity(long entityId)
    {
        staticSources.Remove(entityId);
        trackedEntityIds.Remove(entityId);
    }

    public void Resolve(GameWorld world, RuntimeEffectStore runtimeEffects, long tick)
    {
        IReadOnlyList<ComponentSourceContribution> runtime = BuildRuntimeContributions(runtimeEffects.ActiveAt(tick));
        HashSet<long> entityIds = new HashSet<long>(trackedEntityIds);
        entityIds.UnionWith(staticSources.Keys);
        for (int i = 0; i < runtime.Count; i++)
        {
            entityIds.Add(runtime[i].EntityId);
        }

        var nextTracked = new HashSet<long>(staticSources.Keys);
        foreach (long entityId in entityIds.OrderBy(id => id))
        {
            if (!world.TryGetEntity(entityId, out GameEntity entity))
            {
                staticSources.Remove(entityId);
                nextTracked.Remove(entityId);
                continue;
            }

            var contributions = new List<ComponentSourceContribution>();
            if (staticSources.TryGetValue(entityId, out List<ComponentSourceContribution> staticItems))
            {
                contributions.AddRange(staticItems);
            }

            contributions.AddRange(runtime.Where(item => item.EntityId == entityId));
            ApplyFinalResults(world, entity, contributions);
            if (contributions.Count > 0)
            {
                nextTracked.Add(entityId);
            }
        }

        trackedEntityIds.Clear();
        trackedEntityIds.UnionWith(nextTracked);
    }

    private static IReadOnlyList<ComponentSourceContribution> BuildRuntimeContributions(IReadOnlyList<RuntimeEffectInstance> effects)
    {
        var result = new List<ComponentSourceContribution>();
        for (int i = 0; i < effects.Count; i++)
        {
            RuntimeEffectInstance effect = effects[i];
            ComponentSourceKey source = ComponentSourceKey.Runtime(effect.Id);
            RuntimeEffectSpec spec = effect.Spec;
            if (spec.Kind == RuntimeEffectKind.TemporaryBlocking)
            {
                result.Add(ComponentSourceContribution.Blocking(spec.TargetEntityId, source));
            }
            else if (spec.Kind == RuntimeEffectKind.TemporaryAutoMove)
            {
                result.Add(ComponentSourceContribution.AutoMove(spec.TargetEntityId, source, spec.AutoMoveIntervalTicks));
            }
            else if (spec.Kind == RuntimeEffectKind.TemporaryPushable)
            {
                result.Add(ComponentSourceContribution.Pushable(spec.TargetEntityId, source));
            }
            else if (spec.Kind == RuntimeEffectKind.TemporaryPort)
            {
                result.Add(ComponentSourceContribution.PortConnector(spec.TargetEntityId, source, spec.PortMask));
            }
            else if (spec.Kind == RuntimeEffectKind.TemporaryImmobile)
            {
                result.Add(ComponentSourceContribution.MovementPermission(spec.TargetEntityId, source, spec.CanMove, spec.CanBePushed));
            }
        }

        return result;
    }

    private static void ApplyFinalResults(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> contributions)
    {
        ApplyBlocking(world, entity, contributions.Any(item => item.Kind == ComponentResultKind.Blocking));
        ApplyPushable(world, entity, contributions.Any(item => item.Kind == ComponentResultKind.Pushable));
        ApplyAutoMove(world, entity, contributions.Where(item => item.Kind == ComponentResultKind.AutoMove).ToArray());
        ApplyPortConnector(world, entity, contributions.Where(item => item.Kind == ComponentResultKind.PortConnector).ToArray());
        ApplyMovementPermission(world, entity, contributions.Where(item => item.Kind == ComponentResultKind.MovementPermission).ToArray());
    }

    private static void ApplyBlocking(GameWorld world, GameEntity entity, bool desired)
    {
        bool current = world.HasComponent<BlockingComponent>(entity);
        if (current == desired)
        {
            return;
        }

        if (desired)
        {
            world.SetComponent(entity, new BlockingComponent());
        }
        else
        {
            world.RemoveComponent<BlockingComponent>(entity);
        }

        world.MarkDirty(entity.EntityId);
    }

    private static void ApplyPushable(GameWorld world, GameEntity entity, bool desired)
    {
        bool current = world.HasComponent<PushableComponent>(entity);
        if (current == desired)
        {
            return;
        }

        if (desired)
        {
            world.SetComponent(entity, new PushableComponent());
        }
        else
        {
            world.RemoveComponent<PushableComponent>(entity);
        }

        world.MarkDirty(entity.EntityId);
    }

    private static void ApplyAutoMove(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources)
    {
        bool desired = sources.Count > 0;
        bool current = world.TryGetComponent(entity, out AutoMoveComponent existing);
        if (!desired)
        {
            if (current)
            {
                world.RemoveComponent<AutoMoveComponent>(entity);
                world.MarkDirty(entity.EntityId);
            }

            return;
        }

        int interval = sources.OrderBy(item => item.Source.Kind == "static" ? 0 : 1).ThenBy(item => item.Source.Id).First().AutoMoveIntervalTicks;
        long lastMoveTick = current ? existing.LastMoveTick : 0;
        if (current && existing.IntervalTicks == interval)
        {
            return;
        }

        var component = new AutoMoveComponent(interval)
        {
            LastMoveTick = lastMoveTick
        };
        world.SetComponent(entity, component);
        world.MarkDirty(entity.EntityId);
    }

    private static void ApplyPortConnector(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources)
    {
        DirectionMask desired = DirectionMask.None;
        for (int i = 0; i < sources.Count; i++)
        {
            desired |= sources[i].PortMask;
        }

        bool current = world.TryGetComponent(entity, out PortConnectorComponent existing);
        if (desired == DirectionMask.None)
        {
            if (current)
            {
                world.RemoveComponent<PortConnectorComponent>(entity);
                world.MarkDirty(entity.EntityId);
            }

            return;
        }

        if (current && existing.LocalPorts == desired)
        {
            return;
        }

        world.SetComponent(entity, new PortConnectorComponent(desired));
        world.MarkDirty(entity.EntityId);
    }

    private static void ApplyMovementPermission(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources)
    {
        bool desired = sources.Count > 0;
        bool current = world.TryGetComponent(entity, out MovementPermissionComponent existing);
        if (!desired)
        {
            if (current)
            {
                world.RemoveComponent<MovementPermissionComponent>(entity);
                world.MarkDirty(entity.EntityId);
            }

            return;
        }

        bool canMove = true;
        bool canBePushed = true;
        for (int i = 0; i < sources.Count; i++)
        {
            canMove &= sources[i].CanMove;
            canBePushed &= sources[i].CanBePushed;
        }

        if (current && existing.CanMove == canMove && existing.CanBePushed == canBePushed)
        {
            return;
        }

        world.SetComponent(entity, new MovementPermissionComponent(canMove, canBePushed));
        world.MarkDirty(entity.EntityId);
    }
}
}
