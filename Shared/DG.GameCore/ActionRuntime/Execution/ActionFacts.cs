using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public enum ActionFactType
{
    Unknown = 0,
    EntityMoved = 1,
    EntityPushed = 2,
    EntitySpawned = 3,
    EntityRemoved = 4,
    RotateStarted = 5,
    RotateContacted = 6,
    RotateCompleted = 7,
    BodyMoved = 8,
    BehaviorRejected = 9,
    BehaviorCancelled = 10,
    BehaviorFailed = 11
}

public readonly struct ActionFact
{
    private ActionFact(ActionFactType factType, long serverTick, long sourceActionId, PresentationFactType projectionFactType, PresentationFactResultKind resultKind, long clientInputId, long sourceEntityId, IReadOnlyList<long> subjectEntityIds, GridCoord from, GridCoord to, Direction direction, long startTick, long contactTick, long endTick, double contactProgress, int effectiveCostTicks, long pivotEntityId, GridCoord pivotCoord, RotatePivotDirection rotateDirection, IReadOnlyList<PresentationFactMember> members, IReadOnlyList<PresentationFactImpact> impacts)
    {
        FactType = factType;
        ServerTick = serverTick;
        SourceActionId = sourceActionId;
        ProjectionFactType = projectionFactType;
        ResultKind = resultKind;
        ClientInputId = clientInputId;
        SourceEntityId = sourceEntityId;
        SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? Array.Empty<long>() : new List<long>(subjectEntityIds).ToArray();
        From = from;
        To = to;
        Direction = direction;
        StartTick = startTick;
        ContactTick = contactTick;
        EndTick = endTick;
        ContactProgress = contactProgress;
        EffectiveCostTicks = effectiveCostTicks;
        PivotEntityId = pivotEntityId;
        PivotCoord = pivotCoord;
        RotateDirection = rotateDirection;
        Members = members == null || members.Count == 0 ? Array.Empty<PresentationFactMember>() : new List<PresentationFactMember>(members).ToArray();
        Impacts = impacts == null || impacts.Count == 0 ? Array.Empty<PresentationFactImpact>() : new List<PresentationFactImpact>(impacts).ToArray();
    }

    public ActionFactType FactType { get; }
    public long ServerTick { get; }
    public long SourceActionId { get; }
    public PresentationFactType ProjectionFactType { get; }
    public bool HasPresentationProjection => ProjectionFactType != PresentationFactType.Unknown;
    public PresentationFactResultKind ResultKind { get; }
    public long ClientInputId { get; }
    public long SourceEntityId { get; }
    public IReadOnlyList<long> SubjectEntityIds { get; }
    public GridCoord From { get; }
    public GridCoord To { get; }
    public Direction Direction { get; }
    public long StartTick { get; }
    public long ContactTick { get; }
    public long EndTick { get; }
    public double ContactProgress { get; }
    public int EffectiveCostTicks { get; }
    public long PivotEntityId { get; }
    public GridCoord PivotCoord { get; }
    public RotatePivotDirection RotateDirection { get; }
    public IReadOnlyList<PresentationFactMember> Members { get; }
    public IReadOnlyList<PresentationFactImpact> Impacts { get; }

    public static ActionFact Internal(ActionFactType factType)
    {
        return Internal(factType, 0, Array.Empty<long>());
    }

    public static ActionFact Internal(ActionFactType factType, long sourceActionId)
    {
        return Internal(factType, sourceActionId, Array.Empty<long>());
    }

    public static ActionFact Internal(ActionFactType factType, long sourceActionId, IReadOnlyList<long> subjectEntityIds)
    {
        IReadOnlyList<long> subjects = subjectEntityIds ?? Array.Empty<long>();
        long sourceEntityId = subjects.Count == 0 ? 0 : subjects[0];
        return new ActionFact(factType, 0, sourceActionId, PresentationFactType.Unknown, PresentationFactResultKind.None, 0, sourceEntityId, subjects, default, default, Direction.None, 0, 0, 0, 0d, 0, 0, default, RotatePivotDirection.None, Array.Empty<PresentationFactMember>(), Array.Empty<PresentationFactImpact>());
    }

    public static ActionFact BehaviorRejected(long serverTick, long sourceActionId, IReadOnlyList<long> subjectEntityIds)
    {
        IReadOnlyList<long> subjects = subjectEntityIds ?? Array.Empty<long>();
        long sourceEntityId = subjects.Count == 0 ? 0 : subjects[0];
        return new ActionFact(ActionFactType.BehaviorRejected, serverTick, sourceActionId, PresentationFactType.Unknown, PresentationFactResultKind.None, 0, sourceEntityId, subjects, default, default, Direction.None, serverTick, 0, serverTick, 0d, 0, 0, default, RotatePivotDirection.None, Array.Empty<PresentationFactMember>(), Array.Empty<PresentationFactImpact>());
    }

    public static ActionFact BehaviorCancelled(long serverTick, long sourceActionId, IReadOnlyList<long> subjectEntityIds)
    {
        IReadOnlyList<long> subjects = subjectEntityIds ?? Array.Empty<long>();
        long sourceEntityId = subjects.Count == 0 ? 0 : subjects[0];
        return new ActionFact(ActionFactType.BehaviorCancelled, serverTick, sourceActionId, PresentationFactType.Unknown, PresentationFactResultKind.None, 0, sourceEntityId, subjects, default, default, Direction.None, serverTick, 0, serverTick, 0d, 0, 0, default, RotatePivotDirection.None, Array.Empty<PresentationFactMember>(), Array.Empty<PresentationFactImpact>());
    }

    public static ActionFact BehaviorFailed(long serverTick, long sourceActionId, IReadOnlyList<long> subjectEntityIds)
    {
        IReadOnlyList<long> subjects = subjectEntityIds ?? Array.Empty<long>();
        long sourceEntityId = subjects.Count == 0 ? 0 : subjects[0];
        return new ActionFact(ActionFactType.BehaviorFailed, serverTick, sourceActionId, PresentationFactType.Unknown, PresentationFactResultKind.None, 0, sourceEntityId, subjects, default, default, Direction.None, serverTick, 0, serverTick, 0d, 0, 0, default, RotatePivotDirection.None, Array.Empty<PresentationFactMember>(), Array.Empty<PresentationFactImpact>());
    }

    public static ActionFact WithProjection(ActionFactType factType, long serverTick, PresentationFactType projectionFactType, PresentationFactResultKind resultKind, long sourceActionId, long clientInputId, long sourceEntityId, IReadOnlyList<long> subjectEntityIds, GridCoord from, GridCoord to, Direction direction, long startTick, long contactTick, long endTick, double contactProgress, int effectiveCostTicks, long pivotEntityId, GridCoord pivotCoord, RotatePivotDirection rotateDirection, IReadOnlyList<PresentationFactMember> members, IReadOnlyList<PresentationFactImpact> impacts)
    {
        return new ActionFact(factType, serverTick, sourceActionId, projectionFactType, resultKind, clientInputId, sourceEntityId, subjectEntityIds, from, to, direction, startTick, contactTick, endTick, contactProgress, effectiveCostTicks, pivotEntityId, pivotCoord, rotateDirection, members, impacts);
    }
}

public static class ActionFactProjection
{
    public static PresentationFact ToPresentationFact(ActionFact fact)
    {
        if (!fact.HasPresentationProjection)
        {
            return default;
        }

        return new PresentationFact(0, fact.ServerTick, fact.ProjectionFactType, fact.ResultKind, fact.SourceActionId, fact.ClientInputId, fact.SourceEntityId, fact.SubjectEntityIds, fact.From, fact.To, fact.Direction, fact.StartTick, fact.ContactTick, fact.EndTick, fact.ContactProgress, fact.EffectiveCostTicks, fact.PivotEntityId, fact.PivotCoord, fact.RotateDirection, fact.Members, fact.Impacts);
    }

    public static IReadOnlyList<PresentationFact> ToPresentationFacts(IReadOnlyList<ActionFact> facts)
    {
        if (facts == null || facts.Count == 0)
        {
            return Array.Empty<PresentationFact>();
        }

        var result = new List<PresentationFact>(facts.Count);
        for (int i = 0; i < facts.Count; i++)
        {
            if (facts[i].HasPresentationProjection)
            {
                result.Add(ToPresentationFact(facts[i]));
            }
        }

        return result;
    }

}
}
