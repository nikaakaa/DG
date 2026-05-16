using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct RuntimeEffectSpec
{
    public RuntimeEffectSpec(RuntimeEffectKind kind, long targetEntityId, long sourceEntityId, long startTick, long expireTick, int autoMoveIntervalTicks, DirectionMask portMask, bool canMove, bool canBePushed)
        : this(default, kind, targetEntityId, sourceEntityId, startTick, expireTick, string.Empty, 0, default, 0, autoMoveIntervalTicks, portMask, canMove, canBePushed, WorldTag.None, EffectStackPolicy.AllowMultiple, string.Empty)
    {
    }

    public RuntimeEffectSpec(EffectSpecId effectSpecId, RuntimeEffectKind kind, long targetEntityId, long sourceEntityId, long startTick, long expireTick, string stackKey, long causalityId, ActionSpecId sourceActionSpecId, long sourceActionId, int autoMoveIntervalTicks, DirectionMask portMask, bool canMove, bool canBePushed, WorldTag tag, EffectStackPolicy stackPolicy, string cueId)
    {
        EffectSpecId = effectSpecId;
        Kind = kind;
        TargetEntityId = targetEntityId;
        SourceEntityId = sourceEntityId;
        StartTick = startTick;
        ExpireTick = expireTick;
        StackKey = stackKey ?? string.Empty;
        CausalityId = causalityId;
        SourceActionSpecId = sourceActionSpecId;
        SourceActionId = sourceActionId;
        AutoMoveIntervalTicks = Math.Max(1, autoMoveIntervalTicks);
        PortMask = portMask;
        CanMove = canMove;
        CanBePushed = canBePushed;
        Tag = tag;
        StackPolicy = stackPolicy == 0 ? EffectStackPolicy.AllowMultiple : stackPolicy;
        CueId = cueId ?? string.Empty;
    }

    public EffectSpecId EffectSpecId { get; }
    public RuntimeEffectKind Kind { get; }
    public long TargetEntityId { get; }
    public long SourceEntityId { get; }
    public long StartTick { get; }
    public long ExpireTick { get; }
    public string StackKey { get; }
    public long CausalityId { get; }
    public ActionSpecId SourceActionSpecId { get; }
    public long SourceActionId { get; }
    public int AutoMoveIntervalTicks { get; }
    public DirectionMask PortMask { get; }
    public bool CanMove { get; }
    public bool CanBePushed { get; }
    public WorldTag Tag { get; }
    public EffectStackPolicy StackPolicy { get; }
    public string CueId { get; }

    public static RuntimeEffectSpec Blocking(long targetEntityId, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryBlocking, targetEntityId, sourceEntityId, startTick, expireTick, 1, DirectionMask.None, true, true);
    }

    public static RuntimeEffectSpec AutoMove(long targetEntityId, int intervalTicks, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryAutoMove, targetEntityId, sourceEntityId, startTick, expireTick, intervalTicks, DirectionMask.None, true, true);
    }

    public static RuntimeEffectSpec Pushable(long targetEntityId, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryPushable, targetEntityId, sourceEntityId, startTick, expireTick, 1, DirectionMask.None, true, true);
    }

    public static RuntimeEffectSpec Port(long targetEntityId, DirectionMask portMask, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryPort, targetEntityId, sourceEntityId, startTick, expireTick, 1, portMask, true, true);
    }

    public static RuntimeEffectSpec Immobile(long targetEntityId, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(RuntimeEffectKind.TemporaryImmobile, targetEntityId, sourceEntityId, startTick, expireTick, 1, DirectionMask.None, false, false);
    }

    public static RuntimeEffectSpec TagEffect(long targetEntityId, WorldTag tag, long startTick = 0, long expireTick = 0, long sourceEntityId = 0)
    {
        return new RuntimeEffectSpec(default, RuntimeEffectKind.TemporaryTag, targetEntityId, sourceEntityId, startTick, expireTick, string.Empty, 0, default, 0, 1, DirectionMask.None, true, true, tag, EffectStackPolicy.AllowMultiple, string.Empty);
    }
}

}
