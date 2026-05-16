using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class TargetSelectorRegistry
{
    private readonly Dictionary<TargetSelectorId, ITargetSelector> selectors = new();

    public TargetSelectorRegistry()
    {
    }

    public TargetSelectorRegistry(IEnumerable<ITargetSelector> selectors)
    {
        if (selectors == null)
        {
            return;
        }

        foreach (ITargetSelector selector in selectors)
        {
            Register(selector);
        }
    }

    public static TargetSelectorRegistry CreateDefault()
    {
        var registry = new TargetSelectorRegistry();
        registry.Register(new NoneTargetSelector());
        registry.Register(new SelfTargetSelector());
        registry.Register(new DirectionCellTargetSelector());
        registry.Register(new TargetCoordSelector());
        registry.Register(new FrontEntitiesTargetSelector());
        return registry;
    }

    public void Register(ITargetSelector selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        if (!selector.SelectorId.IsValid)
        {
            throw new InvalidOperationException("Target selector id is empty.");
        }

        if (selectors.ContainsKey(selector.SelectorId))
        {
            throw new InvalidOperationException("Duplicate target selector id: " + selector.SelectorId);
        }

        selectors.Add(selector.SelectorId, selector);
    }

    public ITargetSelector Get(TargetSelectorId selectorId)
    {
        if (!selectors.TryGetValue(selectorId, out ITargetSelector selector))
        {
            throw new ArgumentOutOfRangeException(nameof(selectorId), selectorId, "Unknown target selector");
        }

        return selector;
    }
}
}
