using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class EffectSpec
{
    public EffectSpec(EffectSpecId specId, EffectPayloadId payloadId, EffectTargetBinding targetBinding, EffectDurationPolicy durationPolicy, EffectStackPolicy stackPolicy, EffectRemovePolicy removePolicy, long durationTicks, int autoMoveIntervalTicks, DirectionMask portMask, bool canMove, bool canBePushed, WorldTag tag, string cueId = "", string statPayloadId = "")
    {
        SpecId = specId;
        PayloadId = payloadId;
        TargetBinding = targetBinding;
        DurationPolicy = durationPolicy;
        StackPolicy = stackPolicy;
        RemovePolicy = removePolicy;
        DurationTicks = Math.Max(0, durationTicks);
        AutoMoveIntervalTicks = Math.Max(1, autoMoveIntervalTicks);
        PortMask = portMask;
        CanMove = canMove;
        CanBePushed = canBePushed;
        Tag = tag;
        CueId = cueId ?? string.Empty;
        StatPayloadId = statPayloadId ?? string.Empty;
        Validate();
    }

    public EffectSpec(EffectSpecId specId, EffectKind kind, EffectTargetBinding targetBinding, EffectDurationPolicy durationPolicy, EffectStackPolicy stackPolicy, EffectRemovePolicy removePolicy, long durationTicks, int autoMoveIntervalTicks, DirectionMask portMask, bool canMove, bool canBePushed, WorldTag tag, string cueId = "", string statPayloadId = "")
        : this(specId, new EffectPayloadId(kind), targetBinding, durationPolicy, stackPolicy, removePolicy, durationTicks, autoMoveIntervalTicks, portMask, canMove, canBePushed, tag, cueId, statPayloadId)
    {
    }

    public EffectSpecId SpecId { get; }
    public EffectPayloadId PayloadId { get; }
    public EffectKind Kind => EffectPayloadCompatibility.ToEffectKind(PayloadId);
    public EffectTargetBinding TargetBinding { get; }
    public EffectDurationPolicy DurationPolicy { get; }
    public EffectStackPolicy StackPolicy { get; }
    public EffectRemovePolicy RemovePolicy { get; }
    public long DurationTicks { get; }
    public int AutoMoveIntervalTicks { get; }
    public DirectionMask PortMask { get; }
    public bool CanMove { get; }
    public bool CanBePushed { get; }
    public WorldTag Tag { get; }
    public string CueId { get; }
    public string StatPayloadId { get; }

    public RuntimeEffectKind RuntimeKind => EffectPayloadCompatibility.ToRuntimeKind(PayloadId);

    public long ResolveExpireTick(long startTick)
    {
        return DurationPolicy == EffectDurationPolicy.TimedTicks ? startTick + DurationTicks : 0;
    }

    private void Validate()
    {
        if (!SpecId.IsValid)
        {
            throw new InvalidOperationException("Effect spec id is empty.");
        }

        if (!PayloadId.IsValid ||
            !Enum.IsDefined(typeof(EffectTargetBinding), TargetBinding) ||
            !Enum.IsDefined(typeof(EffectDurationPolicy), DurationPolicy) ||
            !Enum.IsDefined(typeof(EffectStackPolicy), StackPolicy) ||
            !Enum.IsDefined(typeof(EffectRemovePolicy), RemovePolicy))
        {
            throw new InvalidOperationException("Effect spec enum is invalid: " + SpecId);
        }

        if (DurationPolicy == EffectDurationPolicy.TimedTicks && DurationTicks <= 0)
        {
            throw new InvalidOperationException("Timed effect duration is invalid: " + SpecId);
        }

        if (PayloadId.Equals(new EffectPayloadId("PortConnector")) && PortMask == DirectionMask.None)
        {
            throw new InvalidOperationException("Port effect requires port mask: " + SpecId);
        }

        if (PayloadId.Equals(new EffectPayloadId("Tag")) && Tag == WorldTag.None)
        {
            throw new InvalidOperationException("Tag effect requires tag: " + SpecId);
        }
    }
}

public static class EffectPayloadCompatibility
{
    public static EffectKind ToEffectKind(EffectPayloadId id)
    {
        foreach (EffectKind kind in Enum.GetValues(typeof(EffectKind)))
        {
            if (new EffectPayloadId(kind).Equals(id))
            {
                return kind;
            }
        }

        return 0;
    }

    public static RuntimeEffectKind ToRuntimeKind(EffectPayloadId id)
    {
        if (id.Equals(new EffectPayloadId("Blocking")))
        {
            return RuntimeEffectKind.TemporaryBlocking;
        }

        if (id.Equals(new EffectPayloadId("AutoMove")))
        {
            return RuntimeEffectKind.TemporaryAutoMove;
        }

        if (id.Equals(new EffectPayloadId("Pushable")))
        {
            return RuntimeEffectKind.TemporaryPushable;
        }

        if (id.Equals(new EffectPayloadId("PortConnector")))
        {
            return RuntimeEffectKind.TemporaryPort;
        }

        if (id.Equals(new EffectPayloadId("MovementPermission")))
        {
            return RuntimeEffectKind.TemporaryImmobile;
        }

        if (id.Equals(new EffectPayloadId("Tag")))
        {
            return RuntimeEffectKind.TemporaryTag;
        }

        if (id.Equals(new EffectPayloadId("RotatePivot")) ||
            id.Equals(new EffectPayloadId("rotate_pivot")))
        {
            return RuntimeEffectKind.TemporaryRotatePivot;
        }

        throw new InvalidOperationException("Unknown effect payload id: " + id);
    }
}

}
