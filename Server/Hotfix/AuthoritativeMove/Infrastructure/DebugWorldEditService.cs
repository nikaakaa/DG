using DG.GameCore;
using System.Collections.Generic;

namespace Fantasy;

public sealed class DebugWorldEditService
{
    private readonly GameWorld World;
    private readonly long FirstDebugEntityId;
    private long nextDebugEntityId;

    public DebugWorldEditService(GameWorld world, long firstDebugEntityId = 800000000)
    {
        World = world;
        FirstDebugEntityId = firstDebugEntityId;
        nextDebugEntityId = firstDebugEntityId;
    }

    public bool Enabled { get; set; } = true;

    public long ResolveSpawnEntityId(long requestedEntityId)
    {
        return requestedEntityId > 0 ? requestedEntityId : AllocateEntityId();
    }

    public bool TrySpawn(long requestedEntityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, out long entityId, out string reason)
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

        var spawn = new EntitySpawnSpec(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks <= 0 ? 1 : autoMoveIntervalTicks);
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

        if (!TryCreateRuntimeEffectSpec(entityId, kind, autoMoveIntervalTicks, portMask, expireTick, out RuntimeEffectSpec spec, out reason))
        {
            return false;
        }

        RuntimeEffectInstance instance = World.AddRuntimeEffect(spec);
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

        if (!World.RemoveRuntimeEffect(effectId))
        {
            reason = "runtime effect not found";
            return false;
        }

        removedEffectId = effectId;
        reason = string.Empty;
        return true;
    }

    private bool TryCreateRuntimeEffectSpec(long entityId, RuntimeEffectKind kind, int autoMoveIntervalTicks, DirectionMask portMask, long expireTick, out RuntimeEffectSpec spec, out string reason)
    {
        if (kind == RuntimeEffectKind.TemporaryBlocking)
        {
            spec = RuntimeEffectSpec.Blocking(entityId, World.ServerTick, expireTick);
            reason = string.Empty;
            return true;
        }

        if (kind == RuntimeEffectKind.TemporaryAutoMove)
        {
            spec = RuntimeEffectSpec.AutoMove(entityId, autoMoveIntervalTicks <= 0 ? 1 : autoMoveIntervalTicks, World.ServerTick, expireTick);
            reason = string.Empty;
            return true;
        }

        if (kind == RuntimeEffectKind.TemporaryPushable)
        {
            spec = RuntimeEffectSpec.Pushable(entityId, World.ServerTick, expireTick);
            reason = string.Empty;
            return true;
        }

        if (kind == RuntimeEffectKind.TemporaryPort)
        {
            if (portMask == DirectionMask.None)
            {
                reason = "invalid port mask";
                spec = default;
                return false;
            }

            spec = RuntimeEffectSpec.Port(entityId, portMask, World.ServerTick, expireTick);
            reason = string.Empty;
            return true;
        }

        if (kind == RuntimeEffectKind.TemporaryImmobile)
        {
            spec = RuntimeEffectSpec.Immobile(entityId, World.ServerTick, expireTick);
            reason = string.Empty;
            return true;
        }

        reason = "invalid runtime effect kind";
        spec = default;
        return false;
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
