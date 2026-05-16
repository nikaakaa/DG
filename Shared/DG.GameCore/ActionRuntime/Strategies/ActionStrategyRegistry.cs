using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class ActionStrategyRegistry
{
    private readonly Dictionary<ActionStrategyId, IActionStrategy> strategies = new();

    public static ActionStrategyRegistry Default => GeneratedActionStrategyRegistration.CreateDefault();

    public void Register(IActionStrategy strategy)
    {
        if (strategy == null)
        {
            throw new ArgumentNullException(nameof(strategy));
        }

        if (!strategy.StrategyId.IsValid)
        {
            throw new InvalidOperationException("Action strategy id is empty.");
        }

        if (strategies.ContainsKey(strategy.StrategyId))
        {
            throw new InvalidOperationException("Duplicate action strategy id: " + strategy.StrategyId);
        }

        strategies.Add(strategy.StrategyId, strategy);
    }

    public IActionStrategy Get(ActionSpec spec)
    {
        if (!strategies.TryGetValue(spec.StrategyId, out IActionStrategy strategy))
        {
            throw new InvalidOperationException("No action strategy registered for id: " + spec.StrategyId);
        }

        return strategy;
    }
}
}
