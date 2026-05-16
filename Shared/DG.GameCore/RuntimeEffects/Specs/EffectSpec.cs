using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class EffectSpec
{
    public EffectSpec(EffectSpecId specId, EffectKind kind, EffectTargetBinding targetBinding, EffectDurationPolicy durationPolicy, EffectStackPolicy stackPolicy, EffectRemovePolicy removePolicy, long durationTicks, int autoMoveIntervalTicks, DirectionMask portMask, bool canMove, bool canBePushed, WorldTag tag, string cueId = "", string statPayloadId = "")
    {
        SpecId = specId;
        Kind = kind;
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

    public EffectSpecId SpecId { get; }
    public EffectKind Kind { get; }
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

    public RuntimeEffectKind RuntimeKind => Kind switch
    {
        EffectKind.Blocking => RuntimeEffectKind.TemporaryBlocking,
        EffectKind.AutoMove => RuntimeEffectKind.TemporaryAutoMove,
        EffectKind.Pushable => RuntimeEffectKind.TemporaryPushable,
        EffectKind.PortConnector => RuntimeEffectKind.TemporaryPort,
        EffectKind.MovementPermission => RuntimeEffectKind.TemporaryImmobile,
        EffectKind.Tag => RuntimeEffectKind.TemporaryTag,
        _ => throw new InvalidOperationException("Unknown effect kind: " + Kind)
    };

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

        if (!Enum.IsDefined(typeof(EffectKind), Kind) ||
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

        if (Kind == EffectKind.PortConnector && PortMask == DirectionMask.None)
        {
            throw new InvalidOperationException("Port effect requires port mask: " + SpecId);
        }

        if (Kind == EffectKind.Tag && Tag == WorldTag.None)
        {
            throw new InvalidOperationException("Tag effect requires tag: " + SpecId);
        }
    }
}

}
