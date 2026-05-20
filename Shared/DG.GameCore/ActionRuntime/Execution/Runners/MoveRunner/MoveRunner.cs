namespace DG.GameCore
{
    public sealed class MoveRunner
    {
        public const string RunnerId = "move_runner";

        public void Process(PrimitiveRunnerContext context)
        {
            context.MoveRequests.Add(context.Request);
        }
    }
}
