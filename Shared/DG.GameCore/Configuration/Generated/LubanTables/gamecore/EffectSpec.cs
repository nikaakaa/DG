using Luban;
using Newtonsoft.Json.Linq;

namespace cfg.gamecore
{
public sealed partial class EffectSpec : Luban.BeanBase
{
    public EffectSpec(JToken _buf)
    {
        JObject _obj = _buf as JObject;
        EffectId = (string)_obj.GetValue("effect_id");
        EffectPayloadId = (string)_obj.GetValue("effect_payload_id");
        TargetBinding = (gamecore.EffectTargetBinding)(int)_obj.GetValue("target_binding");
        DurationPolicy = (gamecore.EffectDurationPolicy)(int)_obj.GetValue("duration_policy");
        StackPolicy = (gamecore.EffectStackPolicy)(int)_obj.GetValue("stack_policy");
        RemovePolicy = (gamecore.EffectRemovePolicy)(int)_obj.GetValue("remove_policy");
        DurationTicks = (long)_obj.GetValue("duration_ticks");
        AutoMoveIntervalTicks = (int)_obj.GetValue("auto_move_interval_ticks");
        PortMask = (int)_obj.GetValue("port_mask");
        CanMove = (bool)_obj.GetValue("can_move");
        CanBePushed = (bool)_obj.GetValue("can_be_pushed");
        Tag = (string)_obj.GetValue("tag");
        CueId = (string)_obj.GetValue("cue_id");
        StatPayloadId = (string)_obj.GetValue("stat_payload_id");
    }

    public static EffectSpec DeserializeEffectSpec(JToken _buf)
    {
        return new gamecore.EffectSpec(_buf);
    }

    public readonly string EffectId;
    public readonly string EffectPayloadId;
    public readonly gamecore.EffectTargetBinding TargetBinding;
    public readonly gamecore.EffectDurationPolicy DurationPolicy;
    public readonly gamecore.EffectStackPolicy StackPolicy;
    public readonly gamecore.EffectRemovePolicy RemovePolicy;
    public readonly long DurationTicks;
    public readonly int AutoMoveIntervalTicks;
    public readonly int PortMask;
    public readonly bool CanMove;
    public readonly bool CanBePushed;
    public readonly string Tag;
    public readonly string CueId;
    public readonly string StatPayloadId;

    public const int __ID__ = 812420501;
    public override int GetTypeId() => __ID__;

    public void ResolveRef(Tables tables)
    {
    }
}
}
