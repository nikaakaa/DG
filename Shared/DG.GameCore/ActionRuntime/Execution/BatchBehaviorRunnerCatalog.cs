namespace DG.GameCore
{
    public static class BatchBehaviorRunnerCatalog
    {
        public static BatchBehaviorRunnerRegistry CreateDefault(ActionSpecRegistry actionSpecs)
        {
            var registry = new BatchBehaviorRunnerRegistry();
            registry.Register(new RotatePivotRunner(actionSpecs));
            return registry;
        }
    }
}
