using System;

namespace DG.GameCore
{
public interface IActionStrategy
{
    ActionPrimitive Primitive { get; }
    ActionStrategyId StrategyId { get; }
    void Process(ActionStrategyContext context);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ActionStrategyAttribute : Attribute
{
    public ActionStrategyAttribute(ActionPrimitive primitive)
    {
        Primitive = primitive;
        StrategyKey = new ActionStrategyId(primitive).Value;
    }

    public ActionStrategyAttribute(string strategyKey, ActionPrimitive primitive)
    {
        StrategyKey = string.IsNullOrWhiteSpace(strategyKey) ? throw new ArgumentException("Strategy key is empty.", nameof(strategyKey)) : strategyKey;
        Primitive = primitive;
    }

    public string StrategyKey { get; }
    public ActionPrimitive Primitive { get; }
}
}
