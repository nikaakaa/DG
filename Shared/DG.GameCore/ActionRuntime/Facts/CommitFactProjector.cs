using System.Collections.Generic;

namespace DG.GameCore
{
    public static class MovePresentationResolver
    {
        public static PresentationFactType ResolveForMove(ActionRequest request, ActionSpec spec, ActionPresentationRegistry presentationRegistry)
        {
            if (presentationRegistry != null && presentationRegistry.TryGet(spec.SpecId, out ActionPresentationConfig config))
            {
                return config.FactType;
            }

            if (request.Source.Kind == ActionSourceKind.Handoff || request.DeferredCausalitySamples.Count != 0)
            {
                return PresentationFactType.EntityPushed;
            }

            return spec.DefaultSource == ActionSourceKind.Mechanism || spec.DefaultSource == ActionSourceKind.Handoff
                ? PresentationFactType.EntityPushed
                : PresentationFactType.EntityMoved;
        }

        public static ActionFactType ToActionFactType(PresentationFactType presentationType)
        {
            return presentationType switch
            {
                PresentationFactType.EntityMoved => ActionFactType.EntityMoved,
                PresentationFactType.EntityPushed => ActionFactType.EntityPushed,
                PresentationFactType.EntitySpawned => ActionFactType.EntitySpawned,
                PresentationFactType.EntityRemoved => ActionFactType.EntityRemoved,
                PresentationFactType.BodyMoved => ActionFactType.BodyMoved,
                _ => ActionFactType.Unknown
            };
        }
    }

    public static class CommitFactProjector
    {
        public static bool TryProject(CommitProposal proposal, long serverTick, out ActionFact fact)
        {
            fact = default;
            if (proposal.SourceStateId != 0)
            {
                return false;
            }

            PresentationFactType presentationType = proposal.PresentationHint;
            if (presentationType == PresentationFactType.Unknown)
            {
                return false;
            }

            ActionFactType factType = MovePresentationResolver.ToActionFactType(presentationType);
            if (factType == ActionFactType.Unknown)
            {
                return false;
            }

            fact = ActionFact.WithProjection(
                factType,
                serverTick,
                presentationType,
                PresentationFactResultKind.Success,
                proposal.SourceActionId,
                0,
                proposal.EntityId,
                new[] { proposal.EntityId },
                proposal.From,
                proposal.To,
                proposal.Direction != Direction.None ? proposal.Direction : ResolveDirection(proposal.From, proposal.To),
                serverTick,
                0,
                serverTick,
                0d,
                0,
                0,
                default,
                RotatePivotDirection.None,
                System.Array.Empty<PresentationFactMember>(),
                System.Array.Empty<PresentationFactImpact>());
            return true;
        }

        private static Direction ResolveDirection(GridCoord from, GridCoord to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dx < 0) return Direction.Left;
            if (dx > 0) return Direction.Right;
            if (dy < 0) return Direction.Down;
            if (dy > 0) return Direction.Up;
            return Direction.None;
        }
    }
}
