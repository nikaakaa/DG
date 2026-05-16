using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum ActionUnitLifecycleState
{
    Queued = 1,
    Ready = 2,
    CandidateBuilt = 3,
    Accepted = 4,
    Rejected = 5,
    Interrupted = 6,
    Planned = 7,
    Committed = 8,
    DeferredOutputEmitted = 9,
    Completed = 10,
    Failed = 11
}

public readonly struct ActionUnitTransition
{
    public ActionUnitTransition(long actionId, ActionUnitLifecycleState from, ActionUnitLifecycleState to, string reason)
    {
        ActionId = actionId;
        From = from;
        To = to;
        Reason = reason ?? string.Empty;
    }

    public long ActionId { get; }
    public ActionUnitLifecycleState From { get; }
    public ActionUnitLifecycleState To { get; }
    public string Reason { get; }
}

public sealed class ActionUnitStateMachine
{
    private readonly Dictionary<long, ActionUnitLifecycleState> states = new();

    public ActionUnitLifecycleState Get(long actionId)
    {
        return states.TryGetValue(actionId, out ActionUnitLifecycleState state) ? state : ActionUnitLifecycleState.Queued;
    }

    public ActionUnitTransition Advance(long actionId, ActionUnitLifecycleState next, string reason = "")
    {
        ActionUnitLifecycleState current = Get(actionId);
        states[actionId] = next;
        return new ActionUnitTransition(actionId, current, next, reason);
    }
}

}
