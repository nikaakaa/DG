using DG.GameCore;
using System.Collections.Generic;

namespace Fantasy;

public sealed class DebugWorldEditService
{
    private readonly GameWorld World;
    private readonly IGameConfigProvider ConfigProvider;
    private readonly long FirstDebugEntityId;
    private long nextDebugEntityId;

    public DebugWorldEditService(GameWorld world, IGameConfigProvider? configProvider = null, long firstDebugEntityId = 800000000)
    {
        World = world;
        ConfigProvider = configProvider ?? FallbackGameConfigProvider.Instance;
        FirstDebugEntityId = firstDebugEntityId;
        nextDebugEntityId = firstDebugEntityId;
    }

    public bool Enabled { get; set; } = true;

    public long ResolveSpawnEntityId(long requestedEntityId)
    {
        return requestedEntityId > 0 ? requestedEntityId : AllocateEntityId();
    }

    public bool TrySpawn(long requestedEntityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, out long entityId, out string reason)
        => TrySpawn(requestedEntityId, configId, coord, direction, playerId, autoMoveIntervalTicks, false, out entityId, out reason);

    public bool TrySpawn(long requestedEntityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, bool rotatePivot, out long entityId, out string reason)
    {
        entityId = ResolveSpawnEntityId(requestedEntityId);
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (configId <= 0)
        {
            reason = "invalid config id";
            return false;
        }

        if (World.TryGetEntity(entityId, out _))
        {
            reason = "entity already exists";
            return false;
        }

        var spawn = new EntitySpawnSpec(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks <= 0 ? 1 : autoMoveIntervalTicks, rotatePivot);
        if (!World.AddEntity(spawn))
        {
            reason = "spawn failed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryMove(long entityId, GridCoord coord, out GridCoord finalCoord, out string reason)
    {
        finalCoord = coord;
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (!World.TryGetEntity(entityId, out GameEntity entity))
        {
            reason = "entity not found";
            return false;
        }

        World.MoveEntity(entity, coord);
        reason = string.Empty;
        return true;
    }

    public bool TryRemove(long entityId, out string reason)
    {
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (!World.RemoveEntity(entityId))
        {
            reason = "entity not found";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TrySetTag(long entityId, WorldTag tag, bool enabled, out string reason)
    {
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (tag == WorldTag.None)
        {
            reason = "invalid tag";
            return false;
        }

        if (!World.TryGetEntity(entityId, out GameEntity entity))
        {
            reason = "entity not found";
            return false;
        }

        if (enabled)
        {
            World.AddTag(entity, tag);
        }
        else
        {
            World.RemoveTag(entity, tag);
        }

        World.MarkDirty(entityId);
        reason = string.Empty;
        return true;
    }

    public bool TryApplyRuntimeEffect(long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portMask, long expireTick, out RuntimeEffectId effectId, out string reason)
    {
        effectId = default;
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (!World.TryGetEntity(entityId, out _))
        {
            reason = "entity not found";
            return false;
        }

        if (!TryResolveDebugEffectSpec(kind, portMask, out EffectSpec effectSpec, out reason))
        {
            return false;
        }

        ActionContext context = CreateDebugActionContext(entityId);
        ActionTargetData target = ActionTargetData.Self(entityId, default, Direction.None);
        EffectApplication application = new EffectApplication(context, effectSpec, target, World.ServerTick, DebugStackKey(kind, entityId), expireTick);
        var resolver = new CommitResolver();
        IReadOnlyList<CommitProposalResult> results = resolver.Resolve(World, new[]
        {
            CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 0, application, World.ServerTick)
        });
        if (results.Count == 0 || !results[0].Accepted)
        {
            reason = results.Count == 0 ? "runtime effect commit failed" : results[0].Reason;
            return false;
        }

        RuntimeEffectInstance instance = World.RuntimeEffects.ActiveAt(World.ServerTick)
            .Where(effect => effect.TargetEntityId == entityId && effect.Kind == kind)
            .OrderByDescending(effect => effect.Id.Value)
            .First();
        effectId = instance.Id;
        reason = string.Empty;
        return true;
    }

    public bool TryRemoveRuntimeEffect(long entityId, RuntimeEffectKind kind, RuntimeEffectId requestedEffectId, out RuntimeEffectId removedEffectId, out string reason)
    {
        removedEffectId = default;
        if (!Enabled)
        {
            reason = "debug edit disabled";
            return false;
        }

        if (!World.TryGetEntity(entityId, out _))
        {
            reason = "entity not found";
            return false;
        }

        RuntimeEffectId effectId = requestedEffectId.IsValid ? FindRuntimeEffect(entityId, kind, requestedEffectId) : FindRuntimeEffect(entityId, kind);
        if (!effectId.IsValid)
        {
            reason = "runtime effect not found";
            return false;
        }

        var resolver = new CommitResolver();
        IReadOnlyList<CommitProposalResult> results = resolver.Resolve(World, new[]
        {
            CommitProposal.RemoveRuntimeEffect(WorldActionPriority.Debug, 0, entityId, effectId, World.ServerTick)
        });
        if (results.Count == 0 || !results[0].Accepted)
        {
            reason = results.Count == 0 ? "runtime effect not found" : results[0].Reason;
            return false;
        }

        removedEffectId = effectId;
        reason = string.Empty;
        return true;
    }

    private bool TryResolveDebugEffectSpec(RuntimeEffectKind kind, DirectionMask portMask, out EffectSpec spec, out string reason)
    {
        string specIdValue = kind == RuntimeEffectKind.TemporaryPort ? PortEffectSpecId(portMask) : kind switch
        {
            RuntimeEffectKind.TemporaryBlocking => "temporary_blocking",
            RuntimeEffectKind.TemporaryAutoMove => "temporary_auto_move",
            RuntimeEffectKind.TemporaryPushable => "temporary_pushable",
            RuntimeEffectKind.TemporaryImmobile => "temporary_immobile",
            RuntimeEffectKind.TemporaryTag => "temporary_tag_super_armor",
            _ => string.Empty
        };
        EffectSpecId specId = specIdValue;

        if (!specId.IsValid)
        {
            spec = null!;
            reason = "invalid runtime effect kind";
            return false;
        }

        if (kind == RuntimeEffectKind.TemporaryPort && portMask == DirectionMask.None)
        {
            spec = null!;
            reason = "invalid port mask";
            return false;
        }

        if (!ConfigProvider.TryGetEffectSpec(specId, out spec))
        {
            reason = "unknown effect spec";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private ActionContext CreateDebugActionContext(long entityId)
    {
        return new ActionContext(0, 0, new ActionSpecId("debug_runtime_effect"), WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, entityId, 0, WorldTag.SourceDebug), entityId, entityId, entityId, entityId, new ActionTarget(entityId, null, Direction.None), Direction.None, World.ServerTick, World.ServerTick, 1, 0, 0);
    }

    private static string DebugStackKey(RuntimeEffectKind kind, long entityId)
    {
        return "debug:" + entityId + ":" + (int)kind;
    }

    private static string PortEffectSpecId(DirectionMask portMask)
    {
        if ((portMask & DirectionMask.Left) != 0)
        {
            return "temporary_port_left";
        }

        if ((portMask & DirectionMask.Right) != 0)
        {
            return "temporary_port_right";
        }

        if ((portMask & DirectionMask.Up) != 0)
        {
            return "temporary_port_up";
        }

        if ((portMask & DirectionMask.Down) != 0)
        {
            return "temporary_port_down";
        }

        return string.Empty;
    }

    private RuntimeEffectId FindRuntimeEffect(long entityId, RuntimeEffectKind kind)
    {
        IReadOnlyList<RuntimeEffectInstance> active = World.RuntimeEffects.ActiveAt(World.ServerTick);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance effect = active[i];
            if (effect.TargetEntityId == entityId && effect.Kind == kind)
            {
                return effect.Id;
            }
        }

        return default;
    }

    private RuntimeEffectId FindRuntimeEffect(long entityId, RuntimeEffectKind kind, RuntimeEffectId requestedEffectId)
    {
        IReadOnlyList<RuntimeEffectInstance> active = World.RuntimeEffects.ActiveAt(World.ServerTick);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance effect = active[i];
            if (effect.Id.Equals(requestedEffectId) && effect.TargetEntityId == entityId && effect.Kind == kind)
            {
                return effect.Id;
            }
        }

        return default;
    }

    private long AllocateEntityId()
    {
        while (World.TryGetEntity(nextDebugEntityId, out _))
        {
            nextDebugEntityId++;
        }

        if (nextDebugEntityId < FirstDebugEntityId)
        {
            nextDebugEntityId = FirstDebugEntityId;
        }

        return nextDebugEntityId++;
    }
}
