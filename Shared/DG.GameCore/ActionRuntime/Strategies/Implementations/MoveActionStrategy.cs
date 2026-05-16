namespace DG.GameCore
{
[ActionStrategy("move")]
public sealed class MoveActionStrategy : IActionStrategy
{
    public ActionStrategyId StrategyId => "move";

    public void Process(ActionStrategyContext context)
    {
        context.MoveRequests.Add(context.Request);
    }
}
}
