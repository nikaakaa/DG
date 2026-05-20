using System;
using System.Collections.Generic;
using DG.GameCore;
using Fantasy;
using UnityEngine;

namespace DG.Map
{
    public readonly struct ClientPresentationFact
    {
        public ClientPresentationFact(long factId, long serverTick, PresentationFactType factType, PresentationFactResultKind resultKind, long sourceActionId, long clientInputId, long sourceEntityId, IReadOnlyList<long> subjectEntityIds, Vector2Int fromCoord, Vector2Int toCoord, Direction direction, long startTick, long contactTick, long endTick, double contactProgress, int effectiveCostTicks, long pivotEntityId, Vector2Int pivotCoord, RotatePivotDirection rotateDirection, IReadOnlyList<ClientPresentationFactMember> members, IReadOnlyList<ClientPresentationFactImpact> impacts)
        {
            FactId = factId;
            ServerTick = serverTick;
            FactType = factType;
            ResultKind = resultKind;
            SourceActionId = sourceActionId;
            ClientInputId = clientInputId;
            SourceEntityId = sourceEntityId;
            SubjectEntityIds = subjectEntityIds == null || subjectEntityIds.Count == 0 ? Array.Empty<long>() : new List<long>(subjectEntityIds).ToArray();
            FromCoord = fromCoord;
            ToCoord = toCoord;
            Direction = direction;
            StartTick = startTick;
            ContactTick = contactTick;
            EndTick = endTick;
            ContactProgress = contactProgress;
            EffectiveCostTicks = effectiveCostTicks;
            PivotEntityId = pivotEntityId;
            PivotCoord = pivotCoord;
            RotateDirection = rotateDirection;
            Members = members == null || members.Count == 0 ? Array.Empty<ClientPresentationFactMember>() : new List<ClientPresentationFactMember>(members).ToArray();
            Impacts = impacts == null || impacts.Count == 0 ? Array.Empty<ClientPresentationFactImpact>() : new List<ClientPresentationFactImpact>(impacts).ToArray();
        }

        public long FactId { get; }
        public long ServerTick { get; }
        public PresentationFactType FactType { get; }
        public PresentationFactResultKind ResultKind { get; }
        public long SourceActionId { get; }
        public long ClientInputId { get; }
        public long SourceEntityId { get; }
        public IReadOnlyList<long> SubjectEntityIds { get; }
        public Vector2Int FromCoord { get; }
        public Vector2Int ToCoord { get; }
        public Direction Direction { get; }
        public long StartTick { get; }
        public long ContactTick { get; }
        public long EndTick { get; }
        public double ContactProgress { get; }
        public int EffectiveCostTicks { get; }
        public long PivotEntityId { get; }
        public Vector2Int PivotCoord { get; }
        public RotatePivotDirection RotateDirection { get; }
        public IReadOnlyList<ClientPresentationFactMember> Members { get; }
        public IReadOnlyList<ClientPresentationFactImpact> Impacts { get; }
        public long PrimarySubjectEntityId => SubjectEntityIds.Count == 0 ? SourceEntityId : SubjectEntityIds[0];
    }

    public readonly struct ClientPresentationFactMember
    {
        public ClientPresentationFactMember(long entityId, Vector2Int fromCoord, Vector2Int toCoord, Direction fromDirection, Direction toDirection, DirectionMask fromPortLocalPorts, DirectionMask toPortLocalPorts)
        {
            EntityId = entityId;
            FromCoord = fromCoord;
            ToCoord = toCoord;
            FromDirection = fromDirection;
            ToDirection = toDirection;
            FromPortLocalPorts = fromPortLocalPorts;
            ToPortLocalPorts = toPortLocalPorts;
        }

        public long EntityId { get; }
        public Vector2Int FromCoord { get; }
        public Vector2Int ToCoord { get; }
        public Direction FromDirection { get; }
        public Direction ToDirection { get; }
        public DirectionMask FromPortLocalPorts { get; }
        public DirectionMask ToPortLocalPorts { get; }
    }

    public readonly struct ClientPresentationFactImpact
    {
        public ClientPresentationFactImpact(long blockerEntityId, long impactMemberId, Vector2Int impactFromCoord, Vector2Int impactToCoord, Direction pushDirection)
        {
            BlockerEntityId = blockerEntityId;
            ImpactMemberId = impactMemberId;
            ImpactFromCoord = impactFromCoord;
            ImpactToCoord = impactToCoord;
            PushDirection = pushDirection;
        }

        public long BlockerEntityId { get; }
        public long ImpactMemberId { get; }
        public Vector2Int ImpactFromCoord { get; }
        public Vector2Int ImpactToCoord { get; }
        public Direction PushDirection { get; }
    }

    public static class ClientPresentationFactTranslator
    {
        public static IReadOnlyList<ClientPresentationFact> Translate(IReadOnlyList<G2C_PresentationFact> facts)
        {
            if (facts == null || facts.Count == 0)
            {
                return Array.Empty<ClientPresentationFact>();
            }

            var result = new List<ClientPresentationFact>(facts.Count);
            for (int i = 0; i < facts.Count; i++)
            {
                G2C_PresentationFact fact = facts[i];
                var members = new ClientPresentationFactMember[fact.Members.Count];
                for (int memberIndex = 0; memberIndex < fact.Members.Count; memberIndex++)
                {
                    G2C_PresentationFactMember member = fact.Members[memberIndex];
                    members[memberIndex] = new ClientPresentationFactMember(
                        member.EntityId,
                        new Vector2Int(member.FromX, member.FromY),
                        new Vector2Int(member.ToX, member.ToY),
                        (Direction)member.FromDirection,
                        (Direction)member.ToDirection,
                        (DirectionMask)member.FromPortLocalPorts,
                        (DirectionMask)member.ToPortLocalPorts);
                }

                var impacts = new ClientPresentationFactImpact[fact.Impacts.Count];
                for (int impactIndex = 0; impactIndex < fact.Impacts.Count; impactIndex++)
                {
                    G2C_PresentationFactImpact impact = fact.Impacts[impactIndex];
                    impacts[impactIndex] = new ClientPresentationFactImpact(
                        impact.BlockerEntityId,
                        impact.ImpactMemberId,
                        new Vector2Int(impact.ImpactFromX, impact.ImpactFromY),
                        new Vector2Int(impact.ImpactToX, impact.ImpactToY),
                        (Direction)impact.PushDirection);
                }

                result.Add(new ClientPresentationFact(
                    fact.FactId,
                    fact.ServerTick,
                    (PresentationFactType)fact.FactType,
                    (PresentationFactResultKind)fact.ResultKind,
                    fact.SourceActionId,
                    fact.ClientInputId,
                    fact.SourceEntityId,
                    fact.SubjectEntityIds,
                    new Vector2Int(fact.FromX, fact.FromY),
                    new Vector2Int(fact.ToX, fact.ToY),
                    (Direction)fact.Direction,
                    fact.StartTick,
                    fact.ContactTick,
                    fact.EndTick,
                    fact.ContactProgress,
                    fact.EffectiveCostTicks,
                    fact.PivotEntityId,
                    new Vector2Int(fact.PivotX, fact.PivotY),
                    (RotatePivotDirection)fact.RotateDirection,
                    members,
                    impacts));
            }

            return result;
        }
    }
}
