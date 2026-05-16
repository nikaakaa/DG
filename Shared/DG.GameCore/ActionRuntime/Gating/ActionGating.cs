using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct ActionGateResult
{
    public ActionGateResult(bool accepted, MoveErrorCode errorCode, string reason)
    {
        Accepted = accepted;
        ErrorCode = errorCode;
        Reason = reason ?? string.Empty;
    }

    public bool Accepted { get; }
    public MoveErrorCode ErrorCode { get; }
    public string Reason { get; }

    public static ActionGateResult Pass => new(true, MoveErrorCode.None, string.Empty);

    public static ActionGateResult Fail(MoveErrorCode errorCode, string reason)
    {
        return new ActionGateResult(false, errorCode, reason);
    }
}

public sealed class ActionTagGate
{
    public ActionGateResult Evaluate(GameWorld world, ActionSpec spec, BehaviorBody body)
    {
        WorldTag bodyTags = AggregateTags(world, body);
        if (spec.RequiredTags != WorldTag.None && (bodyTags & spec.RequiredTags) != spec.RequiredTags)
        {
            return ActionGateResult.Fail(MoveErrorCode.Blocked, "missing required tag");
        }

        if (spec.BlockedTags != WorldTag.None && (bodyTags & spec.BlockedTags) != WorldTag.None)
        {
            return ActionGateResult.Fail(MoveErrorCode.Blocked, "blocked by tag");
        }

        return ActionGateResult.Pass;
    }

    private static WorldTag AggregateTags(GameWorld world, BehaviorBody body)
    {
        WorldTag tags = WorldTag.None;
        for (int i = 0; i < body.Entities.Count; i++)
        {
            if (world.TryGetComponent(body.Entities[i], out TagSetComponent component))
            {
                tags |= component.Tags;
            }
        }

        return tags;
    }
}

}
