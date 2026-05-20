using DG.GameCore;
using System.Collections.Generic;

namespace Fantasy;

public sealed class DebugWorldEditService
{
    private readonly GameWorld World;
    private readonly AuthoritativeInputQueue InputQueue;
    private readonly IGameConfigProvider ConfigProvider;
    private readonly long FirstDebugEntityId;
    private long nextDebugEntityId;

    public DebugWorldEditService(GameWorld world, AuthoritativeInputQueue inputQueue, IGameConfigProvider? configProvider = null, long firstDebugEntityId = 800000000)
    {
        World = world;
        InputQueue = inputQueue;
        ConfigProvider = configProvider ?? FallbackGameConfigProvider.Instance;
        FirstDebugEntityId = firstDebugEntityId;
        nextDebugEntityId = firstDebugEntityId;
    }

    public bool Enabled { get; set; } = true;

    public long ResolveSpawnEntityId(long requestedEntityId)
    {
        return requestedEntityId > 0 ? requestedEntityId : AllocateEntityId();
    }

    public AuthoritativeDebugActionInput EnqueueSpawn(long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks)
        => EnqueueSpawn(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks, false);

    public AuthoritativeDebugActionInput EnqueueSpawn(long entityId, int configId, GridCoord coord, Direction direction, long playerId, int autoMoveIntervalTicks, bool rotatePivot)
    {
        if (!Enabled)
        {
            return CreateRejectedInput(entityId, "debug edit disabled");
        }

        return InputQueue.EnqueueDebugSpawn(entityId, configId, coord, direction, playerId, autoMoveIntervalTicks, rotatePivot);
    }

    public AuthoritativeDebugActionInput EnqueueMove(long entityId, GridCoord coord)
    {
        if (!Enabled)
        {
            return CreateRejectedInput(entityId, "debug edit disabled");
        }

        return InputQueue.EnqueueDebugMove(entityId, coord);
    }

    public AuthoritativeDebugActionInput EnqueueRemove(long entityId)
    {
        if (!Enabled)
        {
            return CreateRejectedInput(entityId, "debug edit disabled");
        }

        return InputQueue.EnqueueDebugRemove(entityId);
    }

    public AuthoritativeDebugActionInput EnqueueSetTag(long entityId, WorldTag tag, bool enabled)
    {
        if (!Enabled)
        {
            return CreateRejectedInput(entityId, "debug edit disabled");
        }

        return InputQueue.EnqueueDebugSetTag(entityId, tag, enabled);
    }

    public AuthoritativeDebugActionInput EnqueueApplyRuntimeEffect(long entityId, RuntimeEffectKind kind, DirectionMask portMask, long expireTick)
    {
        if (!Enabled)
        {
            return CreateRejectedInput(entityId, "debug edit disabled");
        }

        if (!TryResolveDebugEffectSpec(kind, portMask, out EffectSpec spec, out string reason))
        {
            return CreateRejectedInput(entityId, reason);
        }

        return InputQueue.EnqueueDebugApplyEffect(entityId, spec.SpecId, DebugStackKey(kind, entityId), expireTick);
    }

    public AuthoritativeDebugActionInput EnqueueRemoveRuntimeEffect(long entityId, RuntimeEffectKind kind, RuntimeEffectId requestedEffectId)
    {
        if (!Enabled)
        {
            return CreateRejectedInput(entityId, "debug edit disabled");
        }

        RuntimeEffectId effectId = requestedEffectId.IsValid
            ? FindRuntimeEffect(entityId, kind, requestedEffectId)
            : FindRuntimeEffect(entityId, kind);
        if (!effectId.IsValid)
        {
            return CreateRejectedInput(entityId, "runtime effect not found");
        }

        return InputQueue.EnqueueDebugRemoveEffect(entityId, effectId);
    }

    public RuntimeEffectId FindLatestRuntimeEffect(long entityId, RuntimeEffectKind kind)
    {
        return FindRuntimeEffect(entityId, kind);
    }

    private static AuthoritativeDebugActionInput CreateRejectedInput(long entityId, string reason)
    {
        var placeholder = new WorldAction(0, WorldActionPriority.Debug, default, entityId, null, Direction.None, 0, 0, 0, 1);
        var input = new AuthoritativeDebugActionInput(placeholder);
        input.Complete(new MoveResult(false, entityId, default, Direction.None, MoveErrorCode.Blocked, reason, false, default, 0));
        return input;
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
