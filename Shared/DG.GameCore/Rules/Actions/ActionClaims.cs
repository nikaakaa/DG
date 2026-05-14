using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public readonly struct ActionClaim
{
    public ActionClaim(long actionId, long bodyId, long entityId, ActionClaimKind kind, GridCoord fromCoord, GridCoord toCoord, string conflictGroup, ActionClaimMode mode, WorldActionPriority priority)
    {
        ActionId = actionId;
        BodyId = bodyId;
        EntityId = entityId;
        Kind = kind;
        FromCoord = fromCoord;
        ToCoord = toCoord;
        ConflictGroup = conflictGroup ?? string.Empty;
        Mode = mode;
        Priority = priority;
    }

    public long ActionId { get; }
    public long BodyId { get; }
    public long EntityId { get; }
    public ActionClaimKind Kind { get; }
    public GridCoord FromCoord { get; }
    public GridCoord ToCoord { get; }
    public string ConflictGroup { get; }
    public ActionClaimMode Mode { get; }
    public WorldActionPriority Priority { get; }
}


public sealed class AcceptedAction
{
    public AcceptedAction(ActionRequest request, ActionSpec spec, BehaviorBody body, Direction direction, GridCoord? targetCoord, IReadOnlyList<ActionClaim> claims, long serverTick)
    {
        Request = request;
        Spec = spec;
        Body = body;
        Direction = direction;
        TargetCoord = targetCoord;
        Claims = claims;
        ServerTick = serverTick;
    }

    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public BehaviorBody Body { get; }
    public Direction Direction { get; }
    public GridCoord? TargetCoord { get; }
    public IReadOnlyList<ActionClaim> Claims { get; }
    public long ServerTick { get; }

}


public sealed class RejectedAction
{
    public RejectedAction(ActionRequest request, ActionSpec spec, MoveResult result, string reason)
    {
        Request = request;
        Spec = spec;
        Result = result;
        Reason = reason ?? string.Empty;
    }

    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public MoveResult Result { get; }
    public string Reason { get; }
    public ActionResultBranch Branch => ActionResultBranch.Failed;
}


public sealed class DerivedAction
{
    public DerivedAction(ActionRequest request, ActionSpec spec, string reason)
        : this(request, spec, ActionResultBranch.Handoff, reason)
    {
    }

    public DerivedAction(ActionRequest request, ActionSpec spec, ActionResultBranch branch, string reason)
    {
        Request = request;
        Spec = spec;
        Branch = branch;
        Reason = reason ?? string.Empty;
    }

    public ActionRequest Request { get; }
    public ActionSpec Spec { get; }
    public ActionResultBranch Branch { get; }
    public string Reason { get; }
}


public sealed class ActionArbitrationResult
{
    private readonly List<AcceptedAction> acceptedActions = new();
    private readonly List<RejectedAction> rejectedActions = new();
    private readonly List<DerivedAction> derivedActions = new();
    private readonly List<DeferredAction> deferredActions = new();
    private readonly List<CommitProposal> commitProposals = new();
    private readonly Dictionary<long, MoveResult> actionResults = new();
    private readonly List<string> reasons = new();
    private readonly List<ActionUnitTransition> transitions = new();

    public IReadOnlyList<AcceptedAction> AcceptedActions => acceptedActions;
    public IReadOnlyList<RejectedAction> RejectedActions => rejectedActions;
    public IReadOnlyList<DerivedAction> DerivedActions => derivedActions;
    public IReadOnlyList<DeferredAction> DeferredActions => deferredActions;
    public IReadOnlyList<CommitProposal> CommitProposals => commitProposals;
    public IReadOnlyDictionary<long, MoveResult> ActionResults => actionResults;
    public IReadOnlyList<string> Reasons => reasons;
    public IReadOnlyList<ActionUnitTransition> Transitions => transitions;

    public void Accept(AcceptedAction action, MoveResult? result)
    {
        acceptedActions.Add(action);
        transitions.Add(new ActionUnitTransition(action.Request.ActionId, ActionUnitLifecycleState.CandidateBuilt, ActionUnitLifecycleState.Accepted, string.Empty));
        if (result.HasValue && action.Request.Source.SourceStateId == 0)
        {
            actionResults[action.Request.ActionId] = result.Value;
        }
    }

    public void Reject(RejectedAction action)
    {
        rejectedActions.Add(action);
        transitions.Add(new ActionUnitTransition(action.Request.ActionId, ActionUnitLifecycleState.CandidateBuilt, ActionUnitLifecycleState.Rejected, action.Reason));
        if (action.Request.Source.SourceStateId == 0)
        {
            actionResults[action.Request.ActionId] = action.Result;
        }

        AddReason(action.Reason);
    }

    public void Derive(DerivedAction action, MoveResult? result)
    {
        derivedActions.Add(action);
        transitions.Add(new ActionUnitTransition(action.Request.ActionId, ActionUnitLifecycleState.CandidateBuilt, ActionUnitLifecycleState.DeferredOutputEmitted, action.Reason));
        if (result.HasValue && action.Request.Source.SourceStateId == 0)
        {
            actionResults[action.Request.ActionId] = result.Value;
        }

        AddReason(action.Reason);
    }

    public void AddProposal(CommitProposal proposal)
    {
        commitProposals.Add(proposal);
    }

    public void AddDeferred(DeferredAction action)
    {
        deferredActions.Add(action);
    }

    public void RecordTransition(ActionUnitTransition transition)
    {
        transitions.Add(transition);
    }

    public void AddReason(string reason)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            reasons.Add(reason);
        }
    }
}

}
