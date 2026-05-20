namespace DG.GameCore
{
    public static class BehaviorDefinitionCatalog
    {
        public static BehaviorDefinitionRegistry CreateDefault()
        {
            var registry = new BehaviorDefinitionRegistry();
            registry.Register(new BehaviorDefinition(
                new BehaviorId("move_entity"),
                new RunnerId("move_runner"),
                ActionPrimitive.Move,
                BehaviorClaimChannel.Movement,
                BehaviorClaimMode.Exclusive,
                1));
            registry.Register(new BehaviorDefinition(
                new BehaviorId("spawn_entity"),
                new RunnerId("spawn_runner"),
                ActionPrimitive.Spawn,
                BehaviorClaimChannel.Interaction,
                BehaviorClaimMode.Exclusive,
                1));
            registry.Register(new BehaviorDefinition(
                new BehaviorId("remove_entity"),
                new RunnerId("remove_runner"),
                ActionPrimitive.Remove,
                BehaviorClaimChannel.Interaction,
                BehaviorClaimMode.Exclusive,
                1));
            registry.Register(new BehaviorDefinition(
                new BehaviorId("apply_runtime_effect"),
                new RunnerId("apply_effect_runner"),
                ActionPrimitive.ApplyRuntimeEffect,
                BehaviorClaimChannel.Status,
                BehaviorClaimMode.Shared,
                1));
            registry.Register(new BehaviorDefinition(
                new BehaviorId("remove_runtime_effect"),
                new RunnerId("remove_effect_runner"),
                ActionPrimitive.RemoveRuntimeEffect,
                BehaviorClaimChannel.Status,
                BehaviorClaimMode.Shared,
                1));
            registry.Register(new BehaviorDefinition(
                new BehaviorId("set_tag"),
                new RunnerId("set_tag_runner"),
                ActionPrimitive.SetTag,
                BehaviorClaimChannel.Status,
                BehaviorClaimMode.Shared,
                1));
            return registry;
        }
    }
}
