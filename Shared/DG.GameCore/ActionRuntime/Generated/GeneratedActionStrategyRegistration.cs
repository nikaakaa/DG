namespace DG.GameCore
{
public static partial class GeneratedActionStrategyRegistration
{
    public static ActionStrategyRegistry CreateDefault()
    {
        var registry = new ActionStrategyRegistry();
        registry.Register(new DG.GameCore.MoveActionStrategy());
        registry.Register(new DG.GameCore.ApplyRuntimeEffectActionStrategy());
        registry.Register(new DG.GameCore.RemoveActionStrategy());
        registry.Register(new DG.GameCore.SpawnActionStrategy());
        return registry;
    }
}
}
