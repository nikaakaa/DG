namespace DG.GameCore
{
[ActionStrategy("move", ActionPrimitive.Move)]
public sealed class MoveActionStrategy : IActionStrategy
{
    public ActionPrimitive Primitive => ActionPrimitive.Move;
    public ActionStrategyId StrategyId => "move";

    public void Process(ActionStrategyContext context)
    {
        context.MoveRequests.Add(context.Request);
    }
}
}
