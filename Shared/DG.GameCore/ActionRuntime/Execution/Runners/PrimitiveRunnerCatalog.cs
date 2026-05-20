namespace DG.GameCore
{
    public static class PrimitiveRunnerCatalog
    {
        public static PrimitiveRunnerRegistry CreateDefault()
        {
            var registry = new PrimitiveRunnerRegistry();
            var move = new MoveRunner();
            var spawn = new SpawnRunner();
            var remove = new RemoveRunner();
            var applyEffect = new ApplyEffectRunner();
            var removeEffect = new RemoveEffectRunner();
            var setTag = new SetTagRunner();
            registry.Register(new RunnerId("move_runner"), context => move.Process(context));
            registry.Register(new RunnerId("spawn_runner"), context => spawn.Process(context));
            registry.Register(new RunnerId("remove_runner"), context => remove.Process(context));
            registry.Register(new RunnerId("apply_effect_runner"), context => applyEffect.Process(context));
            registry.Register(new RunnerId("remove_effect_runner"), context => removeEffect.Process(context));
            registry.Register(new RunnerId("set_tag_runner"), context => setTag.Process(context));
            return registry;
        }
    }
}
