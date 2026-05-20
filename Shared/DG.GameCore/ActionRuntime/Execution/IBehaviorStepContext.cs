namespace DG.GameCore
{
    public interface IBehaviorStepContext
    {
        GameWorld World { get; }
        long ServerTick { get; }
        BehaviorRunnerPhase Phase { get; }
    }
}
