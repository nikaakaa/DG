using DG.GameCore;

namespace Fantasy;

public sealed class PresentationFactComposer
{
    private readonly ActionPresentationRegistry actionPresentationRegistry;

    public PresentationFactComposer()
        : this(ActionPresentationRegistry.Default)
    {
    }

    public PresentationFactComposer(ActionPresentationRegistry actionPresentationRegistry)
    {
        this.actionPresentationRegistry = actionPresentationRegistry ?? new ActionPresentationRegistry(Array.Empty<ActionPresentationConfig>());
    }

    public IReadOnlyList<PresentationFact> Compose(long serverTick, IReadOnlyList<WorldAction> actions, BehaviorRuntimeTickResult result)
    {
        var facts = new List<PresentationFact>();
        var recorded = new HashSet<string>();
        AddActionFacts(result, facts, recorded);
        AssignFactIds(serverTick, facts);
        return facts;
    }

    private static void AddActionFacts(BehaviorRuntimeTickResult result, List<PresentationFact> facts, HashSet<string> recorded)
    {
        for (int i = 0; i < result.ActionFacts.Count; i++)
        {
            ActionFact actionFact = result.ActionFacts[i];
            if (!actionFact.HasPresentationProjection)
            {
                continue;
            }

            PresentationFact fact = ActionFactProjection.ToPresentationFact(actionFact);
            string key = fact.FactType + "|" + fact.SourceEntityId + "|" + fact.SourceActionId + "|" + fact.ResultKind;
            if (!recorded.Add(key))
            {
                continue;
            }

            facts.Add(fact);
        }
    }

    private static void AssignFactIds(long serverTick, List<PresentationFact> facts)
    {
        for (int i = 0; i < facts.Count; i++)
        {
            PresentationFact item = facts[i];
            facts[i] = new PresentationFact(i + 1, serverTick, item.FactType, item.ResultKind, item.SourceActionId, item.ClientInputId, item.SourceEntityId, item.SubjectEntityIds, item.From, item.To, item.Direction, item.StartTick, item.ContactTick, item.EndTick, item.ContactProgress, item.EffectiveCostTicks, item.PivotEntityId, item.PivotCoord, item.RotateDirection, item.Members, item.Impacts);
        }
    }
}
