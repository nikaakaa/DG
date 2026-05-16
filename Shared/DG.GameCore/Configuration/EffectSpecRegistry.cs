using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class EffectSpecRegistry
{
    private readonly Dictionary<EffectSpecId, EffectSpec> specs = new();

    public EffectSpecRegistry(IEnumerable<EffectSpec> specs)
    {
        if (specs == null)
        {
            throw new ArgumentNullException(nameof(specs));
        }

        foreach (EffectSpec spec in specs)
        {
            if (this.specs.ContainsKey(spec.SpecId))
            {
                throw new InvalidOperationException("Duplicate effect spec id: " + spec.SpecId);
            }

            this.specs.Add(spec.SpecId, spec);
        }
    }

    public IReadOnlyCollection<EffectSpec> Specs => specs.Values.OrderBy(spec => spec.SpecId.RuntimeKey).ToArray();

    public bool TryGet(EffectSpecId id, out EffectSpec spec)
    {
        return specs.TryGetValue(id, out spec);
    }

    public EffectSpec Get(EffectSpecId id)
    {
        if (!TryGet(id, out EffectSpec spec))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown effect spec");
        }

        return spec;
    }

    public static EffectSpecRegistry FromProvider(IGameConfigProvider provider, IEnumerable<EffectSpecId> ids)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        var specs = new List<EffectSpec>();
        foreach (EffectSpecId id in ids ?? Array.Empty<EffectSpecId>())
        {
            if (!provider.TryGetEffectSpec(id, out EffectSpec spec))
            {
                throw new InvalidOperationException("Unknown effect spec: " + id);
            }

            specs.Add(spec);
        }

        return new EffectSpecRegistry(specs);
    }

    public static EffectSpecRegistry CreateFirstSliceDefaults()
    {
        return new EffectSpecRegistry(new[]
        {
            new EffectSpec("temporary_blocking", EffectKind.Blocking, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.RefreshDuration, EffectRemovePolicy.ExplicitOrExpire, 5, 1, DirectionMask.None, true, true, WorldTag.None, "temporary_blocking"),
            new EffectSpec("temporary_auto_move", EffectKind.AutoMove, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.RefreshDuration, EffectRemovePolicy.ExplicitOrExpire, 5, 1, DirectionMask.None, true, true, WorldTag.None, "temporary_auto_move"),
            new EffectSpec("temporary_pushable", EffectKind.Pushable, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOrExpire, 5, 1, DirectionMask.None, true, true, WorldTag.None, "temporary_pushable"),
            new EffectSpec("temporary_port", EffectKind.PortConnector, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.ReplaceByStackKey, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.Left | DirectionMask.Right, true, true, WorldTag.None, "temporary_port"),
            new EffectSpec("temporary_port_left", EffectKind.PortConnector, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.ReplaceByStackKey, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.Left, true, true, WorldTag.None, "temporary_port_left"),
            new EffectSpec("temporary_port_right", EffectKind.PortConnector, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.ReplaceByStackKey, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.Right, true, true, WorldTag.None, "temporary_port_right"),
            new EffectSpec("temporary_port_up", EffectKind.PortConnector, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.ReplaceByStackKey, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.Up, true, true, WorldTag.None, "temporary_port_up"),
            new EffectSpec("temporary_port_down", EffectKind.PortConnector, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.ReplaceByStackKey, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.Down, true, true, WorldTag.None, "temporary_port_down"),
            new EffectSpec("temporary_immobile", EffectKind.MovementPermission, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.RefreshDuration, EffectRemovePolicy.ExplicitOrExpire, 5, 1, DirectionMask.None, false, false, WorldTag.None, "temporary_immobile"),
            new EffectSpec("temporary_tag_super_armor", EffectKind.Tag, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOrExpire, 5, 1, DirectionMask.None, true, true, WorldTag.StateSuperArmor, "temporary_tag")
        });
    }
}
}
