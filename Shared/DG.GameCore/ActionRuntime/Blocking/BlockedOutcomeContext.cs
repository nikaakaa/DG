using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public readonly struct BlockedOutcomeContext
{
    public BlockedOutcomeContext(GameWorld world, ActionRequest request, ActionContext actionContext, ActionSpec spec, BehaviorBody body, GridCoord current, Direction direction, IReadOnlyList<ActionTargetData> targetData, IReadOnlyList<ExternalPushContact> contacts, long serverTick)
    {
        World = world;
        Request = request;
        ActionContext = actionContext;
        Spec = spec;
        Body = body;
        Current = current;
        Direction = direction;
        TargetData = targetData ?? Array.Empty<ActionTargetData>();
        Contacts = contacts ?? Array.Empty<ExternalPushContact>();
        ServerTick = serverTick;
    }

    public GameWorld World { get; }
    public ActionRequest Request { get; }
    public ActionContext ActionContext { get; }
    public ActionSpec Spec { get; }
    public BehaviorBody Body { get; }
    public GridCoord Current { get; }
    public Direction Direction { get; }
    public IReadOnlyList<ActionTargetData> TargetData { get; }
    public IReadOnlyList<ExternalPushContact> Contacts { get; }
    public long ServerTick { get; }
}
}
