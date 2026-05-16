using System;

namespace DG.GameCore
{
public interface IActionStrategy
{
    ActionPrimitive Primitive => 0;
    ActionStrategyId StrategyId { get; }
    void Process(ActionStrategyContext context);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ActionStrategyAttribute : Attribute
{
    public ActionStrategyAttribute(string strategyKey)
    {
        StrategyKey = string.IsNullOrWhiteSpace(strategyKey) ? throw new ArgumentException("Strategy key is empty.", nameof(strategyKey)) : strategyKey;
    }

    public string StrategyKey { get; }
}
}
