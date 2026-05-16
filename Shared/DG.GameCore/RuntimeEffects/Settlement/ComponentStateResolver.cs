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
    MovementPermission = 5,
    RotatePivot = 6
}

public static class ComponentResultIds
{
    public static readonly ComponentResultId Blocking = new("blocking");
    public static readonly ComponentResultId AutoMove = new("auto_move");
    public static readonly ComponentResultId Pushable = new("pushable");
    public static readonly ComponentResultId PortConnector = new("port_connector");
    public static readonly ComponentResultId MovementPermission = new("movement_permission");
    public static readonly ComponentResultId RotatePivot = new("rotate_pivot");
}

public readonly struct ComponentResultId : IEquatable<ComponentResultId>
{
    public ComponentResultId(string value)
        : this(RuntimeKeyUtility.StableRuntimeKey(value), value)
    {
    }

    public ComponentResultId(ComponentResultKind kind)
        : this(KindName(kind))
    {
    }

    private static string KindName(ComponentResultKind kind)
    {
        return kind switch
        {
            ComponentResultKind.Blocking => "blocking",
            ComponentResultKind.AutoMove => "auto_move",
            ComponentResultKind.Pushable => "pushable",
            ComponentResultKind.PortConnector => "port_connector",
            ComponentResultKind.MovementPermission => "movement_permission",
            ComponentResultKind.RotatePivot => "rotate_pivot",
            _ => kind.ToString()
        };
    }

    public ComponentResultId(int runtimeKey, string debugName)
    {
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }

    public string Value { get; }
    public int RuntimeKey { get; }
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(ComponentResultId other)
    {
        return RuntimeKey == other.RuntimeKey;
    }

    public override bool Equals(object obj)
    {
        return obj is ComponentResultId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return RuntimeKey;
    }

    public override string ToString()
    {
        return Value;
    }
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

    public static ComponentSourceKey Commit(long sourceActionId, long entityId)
    {
        return new ComponentSourceKey("commit", sourceActionId == 0 ? entityId : sourceActionId);
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
        : this(entityId, new ComponentResultId(kind), kind, source, autoMoveIntervalTicks, portMask, canMove, canBePushed)
    {
    }

    public ComponentSourceContribution(long entityId, ComponentResultId resultId, ComponentResultKind legacyKind, ComponentSourceKey source, int autoMoveIntervalTicks, DirectionMask portMask, bool canMove, bool canBePushed)
    {
        EntityId = entityId;
        ResultId = resultId.IsValid ? resultId : new ComponentResultId(legacyKind);
        Kind = legacyKind;
        Source = source;
        AutoMoveIntervalTicks = Math.Max(1, autoMoveIntervalTicks);
        PortMask = portMask;
        CanMove = canMove;
        CanBePushed = canBePushed;
    }

    public long EntityId { get; }
    public ComponentResultId ResultId { get; }
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

    public static ComponentSourceContribution RotatePivot(long entityId, ComponentSourceKey source)
    {
        return new ComponentSourceContribution(entityId, ComponentResultKind.RotatePivot, source, 1, DirectionMask.None, true, true);
    }
}

public sealed class ComponentStateResolver
{
    private readonly Dictionary<long, List<ComponentSourceContribution>> staticSources = new();
    private readonly Dictionary<long, List<ComponentSourceContribution>> runtimeSources = new();
    private readonly Dictionary<long, List<EffectContribution>> staticTagSources = new();
    private readonly Dictionary<long, List<EffectContribution>> runtimeTagSources = new();
    private readonly HashSet<long> trackedEntityIds = new();
    private readonly ComponentResultResolverRegistry resultResolvers;

    public ComponentStateResolver() : this(ComponentResultResolverRegistry.Default)
    {
    }

    public ComponentStateResolver(ComponentResultResolverRegistry resultResolvers)
    {
        this.resultResolvers = resultResolvers ?? throw new ArgumentNullException(nameof(resultResolvers));
    }

    public void AddStaticSource(ComponentSourceContribution contribution)
    {
        if (!staticSources.TryGetValue(contribution.EntityId, out List<ComponentSourceContribution> contributions))
        {
            contributions = new List<ComponentSourceContribution>();
            staticSources.Add(contribution.EntityId, contributions);
        }

        contributions.RemoveAll(item => item.ResultId.Equals(contribution.ResultId) && item.Source.Equals(contribution.Source));
        contributions.Add(contribution);
        trackedEntityIds.Add(contribution.EntityId);
    }

    public void AddRuntimeSource(ComponentSourceContribution contribution)
    {
        if (!runtimeSources.TryGetValue(contribution.EntityId, out List<ComponentSourceContribution> contributions))
        {
            contributions = new List<ComponentSourceContribution>();
            runtimeSources.Add(contribution.EntityId, contributions);
        }

        contributions.RemoveAll(item => item.ResultId.Equals(contribution.ResultId) && item.Source.Equals(contribution.Source));
        contributions.Add(contribution);
        trackedEntityIds.Add(contribution.EntityId);
    }

    public bool RemoveRuntimeSource(long entityId, ComponentSourceKey source)
    {
        if (!runtimeSources.TryGetValue(entityId, out List<ComponentSourceContribution> contributions))
        {
            return false;
        }

        int removed = contributions.RemoveAll(item => item.Source.Equals(source));
        if (contributions.Count == 0)
        {
            runtimeSources.Remove(entityId);
        }

        trackedEntityIds.Add(entityId);
        return removed > 0;
    }

    public void AddStaticTagSource(long entityId, ComponentSourceKey source, WorldTag tag)
    {
        if (tag == WorldTag.None)
        {
            return;
        }

        if (!staticTagSources.TryGetValue(entityId, out List<EffectContribution> contributions))
        {
            contributions = new List<EffectContribution>();
            staticTagSources.Add(entityId, contributions);
        }

        contributions.RemoveAll(item => item.Source.Equals(source));
        contributions.Add(new EffectContribution(entityId, source, EffectKind.Tag, null, tag, WorldTag.None));
        trackedEntityIds.Add(entityId);
    }

    public void AddRuntimeTagSource(long entityId, ComponentSourceKey source, WorldTag tag)
    {
        if (tag == WorldTag.None)
        {
            return;
        }

        if (!runtimeTagSources.TryGetValue(entityId, out List<EffectContribution> contributions))
        {
            contributions = new List<EffectContribution>();
            runtimeTagSources.Add(entityId, contributions);
        }

        contributions.RemoveAll(item => item.Source.Equals(source) && item.AddTag == tag);
        contributions.Add(new EffectContribution(entityId, source, EffectKind.Tag, null, tag, WorldTag.None));
        trackedEntityIds.Add(entityId);
    }

    public bool RemoveRuntimeTagSource(long entityId, ComponentSourceKey source, WorldTag tag)
    {
        if (!runtimeTagSources.TryGetValue(entityId, out List<EffectContribution> contributions))
        {
            return false;
        }

        int removed = contributions.RemoveAll(item => item.Source.Equals(source) && (tag == WorldTag.None || (item.AddTag & tag) != WorldTag.None));
        if (contributions.Count == 0)
        {
            runtimeTagSources.Remove(entityId);
        }

        trackedEntityIds.Add(entityId);
        return removed > 0;
    }

    public bool RemoveRuntimeSources(long entityId)
    {
        bool removed = runtimeSources.Remove(entityId);
        removed |= runtimeTagSources.Remove(entityId);
        trackedEntityIds.Add(entityId);
        return removed;
    }

    public void CaptureStaticSources(GameWorld world, GameEntity entity)
    {
        var source = ComponentSourceKey.Static(entity.EntityId);
        var contributions = new List<ComponentSourceContribution>();
        var tagContributions = new List<EffectContribution>();
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

        if (world.HasComponent<RotatePivotComponent>(entity))
        {
            contributions.Add(ComponentSourceContribution.RotatePivot(entity.EntityId, source));
        }

        if (world.TryGetComponent(entity, out TagSetComponent tags))
        {
            tagContributions.Add(new EffectContribution(entity.EntityId, source, EffectKind.Tag, null, tags.Tags, WorldTag.None));
        }

        if (contributions.Count == 0)
        {
            staticSources.Remove(entity.EntityId);
        }
        else
        {
            staticSources[entity.EntityId] = contributions;
        }

        if (tagContributions.Count == 0)
        {
            staticTagSources.Remove(entity.EntityId);
        }
        else
        {
            staticTagSources[entity.EntityId] = tagContributions;
        }
        trackedEntityIds.Add(entity.EntityId);
    }

    public void RemoveEntity(long entityId)
    {
        staticSources.Remove(entityId);
        runtimeSources.Remove(entityId);
        staticTagSources.Remove(entityId);
        runtimeTagSources.Remove(entityId);
        trackedEntityIds.Remove(entityId);
    }

    public void Resolve(GameWorld world, RuntimeEffectStore runtimeEffects, long tick)
    {
        IReadOnlyList<RuntimeEffectInstance> activeEffects = runtimeEffects.ActiveAt(tick);
        IReadOnlyList<ComponentSourceContribution> runtime = BuildRuntimeContributions(activeEffects);
        IReadOnlyList<EffectContribution> runtimeTags = BuildRuntimeTagContributions(activeEffects);
        HashSet<long> entityIds = new HashSet<long>(trackedEntityIds);
        entityIds.UnionWith(staticSources.Keys);
        entityIds.UnionWith(runtimeSources.Keys);
        entityIds.UnionWith(staticTagSources.Keys);
        entityIds.UnionWith(runtimeTagSources.Keys);
        for (int i = 0; i < runtime.Count; i++)
        {
            entityIds.Add(runtime[i].EntityId);
        }
        for (int i = 0; i < runtimeTags.Count; i++)
        {
            entityIds.Add(runtimeTags[i].EntityId);
        }

        var nextTracked = new HashSet<long>(staticSources.Keys);
        nextTracked.UnionWith(runtimeSources.Keys);
        nextTracked.UnionWith(staticTagSources.Keys);
        nextTracked.UnionWith(runtimeTagSources.Keys);
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
            if (runtimeSources.TryGetValue(entityId, out List<ComponentSourceContribution> runtimeItems))
            {
                contributions.AddRange(runtimeItems);
            }

            contributions.AddRange(runtime.Where(item => item.EntityId == entityId));
            ApplyFinalResults(world, entity, contributions);

            var tagContributions = new List<EffectContribution>();
            if (staticTagSources.TryGetValue(entityId, out List<EffectContribution> staticTags))
            {
                tagContributions.AddRange(staticTags);
            }
            if (runtimeTagSources.TryGetValue(entityId, out List<EffectContribution> runtimeTagItems))
            {
                tagContributions.AddRange(runtimeTagItems);
            }

            tagContributions.AddRange(runtimeTags.Where(item => item.EntityId == entityId));
            ApplyFinalTags(world, entity, tagContributions);
            if (contributions.Count > 0 || tagContributions.Count > 0)
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
            if (spec.PayloadId.Equals(new EffectPayloadId(RuntimeEffectKind.TemporaryBlocking)))
            {
                result.Add(ComponentSourceContribution.Blocking(spec.TargetEntityId, source));
            }
            else if (spec.PayloadId.Equals(new EffectPayloadId(RuntimeEffectKind.TemporaryAutoMove)))
            {
                result.Add(ComponentSourceContribution.AutoMove(spec.TargetEntityId, source, spec.AutoMoveIntervalTicks));
            }
            else if (spec.PayloadId.Equals(new EffectPayloadId(RuntimeEffectKind.TemporaryPushable)))
            {
                result.Add(ComponentSourceContribution.Pushable(spec.TargetEntityId, source));
            }
            else if (spec.PayloadId.Equals(new EffectPayloadId(RuntimeEffectKind.TemporaryPort)))
            {
                result.Add(ComponentSourceContribution.PortConnector(spec.TargetEntityId, source, spec.PortMask));
            }
            else if (spec.PayloadId.Equals(new EffectPayloadId(RuntimeEffectKind.TemporaryImmobile)))
            {
                result.Add(ComponentSourceContribution.MovementPermission(spec.TargetEntityId, source, spec.CanMove, spec.CanBePushed));
            }
            else if (spec.PayloadId.Equals(new EffectPayloadId(RuntimeEffectKind.TemporaryRotatePivot)) ||
                spec.PayloadId.Equals(new EffectPayloadId("RotatePivot")) ||
                spec.PayloadId.Equals(new EffectPayloadId("rotate_pivot")))
            {
                result.Add(ComponentSourceContribution.RotatePivot(spec.TargetEntityId, source));
            }
        }

        return result;
    }

    private static IReadOnlyList<EffectContribution> BuildRuntimeTagContributions(IReadOnlyList<RuntimeEffectInstance> effects)
    {
        var result = new List<EffectContribution>();
        for (int i = 0; i < effects.Count; i++)
        {
            RuntimeEffectInstance effect = effects[i];
            RuntimeEffectSpec spec = effect.Spec;
            if (spec.PayloadId.Equals(new EffectPayloadId(RuntimeEffectKind.TemporaryTag)) && spec.Tag != WorldTag.None)
            {
                result.Add(new EffectContribution(spec.TargetEntityId, ComponentSourceKey.Runtime(effect.Id), EffectKind.Tag, null, spec.Tag, WorldTag.None));
            }
        }

        return result;
    }

    private void ApplyFinalResults(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> contributions)
    {
        resultResolvers.Resolve(world, entity, contributions);
    }

    private static void ApplyFinalTags(GameWorld world, GameEntity entity, IReadOnlyList<EffectContribution> contributions)
    {
        WorldTag desired = WorldTag.None;
        for (int i = 0; i < contributions.Count; i++)
        {
            desired |= contributions[i].AddTag;
            desired &= ~contributions[i].RemoveTag;
        }

        bool current = world.TryGetComponent(entity, out TagSetComponent existing);
        if (desired == WorldTag.None)
        {
            if (current)
            {
                world.RemoveComponent<TagSetComponent>(entity);
                world.MarkDirty(entity.EntityId);
            }

            return;
        }

        if (current && existing.Tags == desired)
        {
            return;
        }

        world.SetComponent(entity, new TagSetComponent(desired));
        world.MarkDirty(entity.EntityId);
    }

    internal static void ApplyBlocking(GameWorld world, GameEntity entity, bool desired)
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

    internal static void ApplyPushable(GameWorld world, GameEntity entity, bool desired)
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

    internal static void ApplyAutoMove(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources)
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

    internal static void ApplyPortConnector(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources)
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

    internal static void ApplyMovementPermission(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources)
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

    internal static void ApplyRotatePivot(GameWorld world, GameEntity entity, bool desired)
    {
        bool current = world.HasComponent<RotatePivotComponent>(entity);
        if (current == desired)
        {
            return;
        }

        if (desired)
        {
            world.SetComponent(entity, new RotatePivotComponent());
        }
        else
        {
            world.RemoveComponent<RotatePivotComponent>(entity);
        }

        world.MarkDirty(entity.EntityId);
    }
}

public interface IComponentResultResolver
{
    ComponentResultId ResultId { get; }
    void Apply(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources);
}

public sealed class ComponentResultResolverRegistry
{
    private readonly Dictionary<ComponentResultId, IComponentResultResolver> resolvers = new();

    public static ComponentResultResolverRegistry Default { get; } = CreateDefault();

    public void Register(IComponentResultResolver resolver)
    {
        if (resolver == null)
        {
            throw new ArgumentNullException(nameof(resolver));
        }

        if (!resolver.ResultId.IsValid)
        {
            throw new InvalidOperationException("Component result id is empty.");
        }

        if (resolvers.ContainsKey(resolver.ResultId))
        {
            throw new InvalidOperationException("Duplicate component result resolver: " + resolver.ResultId);
        }

        resolvers.Add(resolver.ResultId, resolver);
    }

    public void Resolve(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> contributions)
    {
        foreach (KeyValuePair<ComponentResultId, IComponentResultResolver> pair in resolvers.OrderBy(item => item.Key.RuntimeKey))
        {
            pair.Value.Apply(world, entity, contributions.Where(item => item.ResultId.Equals(pair.Key)).ToArray());
        }
    }

    private static ComponentResultResolverRegistry CreateDefault()
    {
        var registry = new ComponentResultResolverRegistry();
        registry.Register(new BlockingResultResolver());
        registry.Register(new AutoMoveResultResolver());
        registry.Register(new PushableResultResolver());
        registry.Register(new PortConnectorResultResolver());
        registry.Register(new MovementPermissionResultResolver());
        registry.Register(new RotatePivotResultResolver());
        return registry;
    }
}

public sealed class BlockingResultResolver : IComponentResultResolver
{
    public ComponentResultId ResultId => ComponentResultIds.Blocking;
    public void Apply(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources) => ComponentStateResolver.ApplyBlocking(world, entity, sources.Count > 0);
}

public sealed class PushableResultResolver : IComponentResultResolver
{
    public ComponentResultId ResultId => ComponentResultIds.Pushable;
    public void Apply(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources) => ComponentStateResolver.ApplyPushable(world, entity, sources.Count > 0);
}

public sealed class AutoMoveResultResolver : IComponentResultResolver
{
    public ComponentResultId ResultId => ComponentResultIds.AutoMove;
    public void Apply(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources) => ComponentStateResolver.ApplyAutoMove(world, entity, sources);
}

public sealed class PortConnectorResultResolver : IComponentResultResolver
{
    public ComponentResultId ResultId => ComponentResultIds.PortConnector;
    public void Apply(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources) => ComponentStateResolver.ApplyPortConnector(world, entity, sources);
}

public sealed class MovementPermissionResultResolver : IComponentResultResolver
{
    public ComponentResultId ResultId => ComponentResultIds.MovementPermission;
    public void Apply(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources) => ComponentStateResolver.ApplyMovementPermission(world, entity, sources);
}

public sealed class RotatePivotResultResolver : IComponentResultResolver
{
    public ComponentResultId ResultId => ComponentResultIds.RotatePivot;
    public void Apply(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources) => ComponentStateResolver.ApplyRotatePivot(world, entity, sources.Count > 0);
}
}
