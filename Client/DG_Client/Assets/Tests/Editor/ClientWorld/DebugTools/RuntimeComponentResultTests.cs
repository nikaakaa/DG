using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DG.GameCore;
using DG.Map;
using NUnit.Framework;
using UnityEngine;

namespace DG.EditorTests
{
    public sealed class RuntimeComponentResultTests
    {
        [Test]
        public void RuntimeRemove_KeepsStaticBlockingResult()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(1, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(1, out GameEntity entity));

            RuntimeEffectInstance effect = AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryBlocking, "blocking", 1), 1, 0, "blocking");

            Assert.IsTrue(world.HasComponent<BlockingComponent>(entity));
            Assert.IsTrue(RemoveEffect(world, 1, effect.Id));
            Assert.IsTrue(world.HasComponent<BlockingComponent>(entity));
        }

        [Test]
        public void RuntimeMultiSourceRemove_KeepsOtherRuntimeThenRemovesLast()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.ConveyorSpawn(2, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(2, out GameEntity entity));

            RuntimeEffectInstance first = AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryAutoMove, "auto_a", 2), 2, 0, "auto_a");
            RuntimeEffectInstance second = AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryAutoMove, "auto_b", 3), 2, 0, "auto_b");

            Assert.IsTrue(world.HasComponent<AutoMoveComponent>(entity));
            Assert.IsTrue(RemoveEffect(world, 2, first.Id));
            Assert.IsTrue(world.HasComponent<AutoMoveComponent>(entity));
            Assert.IsTrue(RemoveEffect(world, 2, second.Id));
            Assert.IsFalse(world.HasComponent<AutoMoveComponent>(entity));
        }

        [Test]
        public void ComponentResultResolverRegistry_AddsResultWithoutStateResolverBranch()
        {
            var registry = new ComponentResultResolverRegistry();
            registry.Register(new TestComponentResultResolver());
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(44, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(44, out GameEntity entity));
            var contribution = new ComponentSourceContribution(44, new ComponentResultId("test_result"), 0, ComponentSourceKey.Commit(1, 44), 1, DirectionMask.None, true, true);

            registry.Resolve(world, entity, new[] { contribution });

            Assert.IsTrue(world.HasComponent<BouncableComponent>(entity));
        }

        [Test]
        public void RuntimeEffectStore_DoesNotWriteGameWorldComponents()
        {
            var world = new GameWorld();
            var store = new RuntimeEffectStore();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.ConveyorSpawn(3, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(3, out GameEntity entity));

            store.Add(RuntimeEffectSpec.Blocking(3));

            Assert.AreEqual(1, store.Count);
            Assert.IsFalse(world.HasComponent<BlockingComponent>(entity));
        }

        [Test]
        public void EffectLifecycleSystem_ReportsActiveRemovedAndExpired()
        {
            var store = new RuntimeEffectStore();
            var lifecycle = new EffectLifecycleSystem();
            RuntimeEffectInstance expiring = store.Add(RuntimeEffectSpec.Blocking(4, 0, 2));
            RuntimeEffectInstance removable = store.Add(RuntimeEffectSpec.Pushable(4));

            RuntimeEffectLifecycleResult active = lifecycle.Active(store, 1);
            Assert.AreEqual(2, active.Active.Count);

            RuntimeEffectLifecycleResult expired = lifecycle.Expire(store, 2);
            Assert.AreEqual(expiring.Id, expired.Expired.Single().Id);
            Assert.AreEqual(removable.Id, expired.Active.Single().Id);

            RuntimeEffectLifecycleResult removed = lifecycle.Remove(store, removable.Id, 2);
            Assert.AreEqual(removable.Id, removed.Removed.Single().Id);
            Assert.AreEqual(0, removed.Active.Count);
        }

        [Test]
        public void RuntimePort_MergesWithStaticAndRemovalKeepsStaticPort()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PortConnectorBlockerSpawn(5, new GridCoord(0, 0), Direction.Right)));
            Assert.IsTrue(world.TryGetEntity(5, out GameEntity entity));
            Assert.IsTrue(world.TryGetComponent(entity, out PortConnectorComponent staticPort));
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right, staticPort.LocalPorts);

            RuntimeEffectInstance runtimePort = AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryPort, "port", 1, DirectionMask.Up), 5, 0, "port");

            Assert.IsTrue(world.TryGetComponent(entity, out PortConnectorComponent mergedPort));
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right | DirectionMask.Up, mergedPort.LocalPorts);
            Assert.IsTrue(RemoveEffect(world, 5, runtimePort.Id));
            Assert.IsTrue(world.TryGetComponent(entity, out PortConnectorComponent finalPort));
            Assert.AreEqual(DirectionMask.Left | DirectionMask.Right, finalPort.LocalPorts);
        }

        [Test]
        public void RuntimeMovementPermission_RejectsMoveThroughRules()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(6, 6, new GridCoord(0, 0))));
            AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryImmobile, "immobile", 1), 6, 0, "immobile");
            var action = new WorldAction(1, WorldActionPriority.Player, "player_move", 6, new GridCoord(1, 0), Direction.None, 0, 0, 0, 1);

            BehaviorRuntimeTickResult result = new BehaviorRuntime().Tick(world, new[] { action }, 1);

            Assert.IsFalse(result.ActionResults[1].Success);
            Assert.AreEqual(MoveErrorCode.Blocked, result.ActionResults[1].ErrorCode);
            Assert.AreEqual("blocked by movement permission", result.ActionResults[1].Reason);
        }

        [Test]
        public void StaticMovementPermissionSource_AppliesThroughResolver()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(9, 9, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(9, out GameEntity entity));

            world.AddStaticComponentSource(ComponentSourceContribution.MovementPermission(9, ComponentSourceKey.Static(9), false, true));
            world.ResolveComponentResults();

            Assert.IsTrue(world.TryGetComponent(entity, out MovementPermissionComponent permission));
            Assert.IsFalse(permission.CanMove);
            Assert.IsTrue(permission.CanBePushed);
        }

        [Test]
        public void RuntimeTagCommitRemove_KeepsStaticTagSource()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(10, 10, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(10, out GameEntity entity));
            world.AddStaticTagSource(10, ComponentSourceKey.Static(10), WorldTag.SourceDebug);
            world.ResolveComponentResults();
            Assert.IsTrue(world.HasTag(entity, WorldTag.SourceDebug));

            var resolver = new CommitResolver();
            resolver.Resolve(world, new[] { CommitProposal.AddTag(WorldActionPriority.Debug, 900, 10, WorldTag.StateSuperArmor, 0) });
            Assert.IsTrue(world.HasTag(entity, WorldTag.SourceDebug));
            Assert.IsTrue(world.HasTag(entity, WorldTag.StateSuperArmor));

            resolver.Resolve(world, new[] { CommitProposal.RemoveTag(WorldActionPriority.Debug, 900, 10, WorldTag.StateSuperArmor, 0) });
            Assert.IsTrue(world.HasTag(entity, WorldTag.SourceDebug));
            Assert.IsFalse(world.HasTag(entity, WorldTag.StateSuperArmor));
        }

        [Test]
        public void CommitHandlerRegistry_AddsHandlerWithoutCommitResolverBranch()
        {
            var registry = new CommitHandlerRegistry();
            registry.Register(new TestCommitHandler());
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(45, new GridCoord(0, 0))));

            IReadOnlyList<CommitProposalResult> results = new CommitResolver(registry).Resolve(world, new[]
            {
                CommitProposal.AddTag(WorldActionPriority.Debug, 77, 45, WorldTag.StateSuperArmor, 0)
            });

            Assert.IsTrue(results.Single().Accepted);
            Assert.AreEqual("test-handler", results.Single().Reason);
        }

        [Test]
        public void ClientMapWorld_AppliesServerFinalComponentResultsOnly()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());
            var snapshot = new EntitySnapshot(
                7,
                DefaultWorldConfig.ConveyorConfigId,
                DefaultWorldConfig.ConveyorArchetypeId,
                DefaultWorldConfig.BlockerTarget,
                0,
                0,
                Direction.Right,
                true,
                true,
                false,
                true,
                4,
                false,
                true,
                DirectionMask.Down,
                true,
                false,
                false,
                10);

            Assert.IsTrue(world.ApplySnapshot(snapshot));
            Assert.IsTrue(world.TryGetCoreEntity(7, out GameEntity entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<BlockingComponent>(entity));
            Assert.IsTrue(world.CoreWorld.HasComponent<PushableComponent>(entity));
            Assert.IsTrue(world.CoreWorld.TryGetComponent(entity, out PortConnectorComponent port));
            Assert.AreEqual(DirectionMask.Down, port.LocalPorts);
            Assert.IsTrue(world.CoreWorld.TryGetComponent(entity, out AutoMoveComponent autoMove));
            Assert.AreEqual(4, autoMove.IntervalTicks);
            Assert.IsTrue(world.CoreWorld.TryGetComponent(entity, out MovementPermissionComponent permission));
            Assert.IsFalse(permission.CanMove);
            Assert.IsFalse(permission.CanBePushed);
            Assert.AreEqual(0, world.CoreWorld.RuntimeEffects.Count);
        }

        [Test]
        public void ClientMapWorld_FinalSnapshotCanRemoveStaticFirstSliceResults()
        {
            var world = new ClientMapWorld(ClientGameConfigProviderFactory.Create());
            var snapshot = new EntitySnapshot(
                8,
                DefaultWorldConfig.PortConnectorBlockerConfigId,
                DefaultWorldConfig.PortConnectorBlockerArchetypeId,
                DefaultWorldConfig.BlockerTarget,
                0,
                0,
                Direction.Right,
                true,
                false,
                false,
                false,
                0,
                false,
                false,
                DirectionMask.None,
                false,
                true,
                true,
                11);

            Assert.IsTrue(world.ApplySnapshot(snapshot));
            Assert.IsTrue(world.TryGetCoreEntity(8, out GameEntity entity));
            Assert.IsFalse(world.CoreWorld.HasComponent<BlockingComponent>(entity));
            Assert.IsFalse(world.CoreWorld.HasComponent<PushableComponent>(entity));
            Assert.IsFalse(world.CoreWorld.HasComponent<PortConnectorComponent>(entity));
        }

        [Test]
        public void NoAbilityKindMappingExistsInFirstSlice()
        {
            string root = RepositoryRoot();
            string[] files = Directory.GetFiles(Path.Combine(root, "Shared", "DG.GameCore"), "*.cs", SearchOption.AllDirectories);
            string text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

            Assert.IsFalse(Regex.IsMatch(text, @"\bAbilityKind\b"));
            Assert.IsFalse(Regex.IsMatch(text, @"\bAbilityKind\b[\s\S]*\bComponentKind\b|\bComponentKind\b[\s\S]*\bAbilityKind\b"));
        }

        [Test]
        public void EffectSpecRegistry_LoadsFirstSliceSpecsFromLuban()
        {
            var provider = LubanGameConfigProvider.FromDirectory(GameConfigDirectory());

            Assert.IsTrue(provider.TryGetEffectSpec("temporary_pushable", out EffectSpec pushable));
            Assert.AreEqual(EffectKind.Pushable, pushable.Kind);
            Assert.AreEqual(EffectDurationPolicy.InfiniteUntilRemove, pushable.DurationPolicy);
            Assert.AreEqual(EffectRemovePolicy.ExplicitOnly, pushable.RemovePolicy);
            Assert.AreEqual(EffectStackPolicy.RefreshDuration, pushable.StackPolicy);
            Assert.IsTrue(provider.TryGetEffectSpec("temporary_tag_super_armor", out EffectSpec tag));
            Assert.AreEqual(WorldTag.StateSuperArmor, tag.Tag);
            Assert.IsTrue(provider.TryGetEffectSpec("temporary_rotate_pivot", out EffectSpec pivot));
            Assert.AreEqual(EffectKind.RotatePivot, pivot.Kind);
        }

        [Test]
        public void EffectApplication_CarriesActionTargetAndPayload()
        {
            var provider = LubanGameConfigProvider.FromDirectory(GameConfigDirectory());
            Assert.IsTrue(provider.TryGetEffectSpec("temporary_pushable", out EffectSpec spec));
            ActionSpec actionSpec = new ActionSpec("grant_pushable", ActionPrimitive.ApplyRuntimeEffect, ActionSourceKind.Debug, WorldActionPriority.Debug, WorldTag.SourceDebug, WorldTag.None, WorldTag.None, WorldTag.None, ActionTargetRule.Self, "reject", ActionConflictPolicy.None, ActionInterruptPolicy.None, ActionMergePolicy.None, ActionPlanRule.None, ActionCommitRule.None, effectSpecId: "temporary_pushable");
            var request = new ActionRequest(10, actionSpec.SpecId, actionSpec.DefaultPriority, new ActionSourceContext(ActionSourceKind.Debug, 1, 0, WorldTag.SourceDebug), 1, new ActionTarget(1, null, Direction.None), default, 3, 3, 0);
            Assert.IsTrue(request.TryCreateContext(actionSpec, out ActionContext context, out _));

            var application = new EffectApplication(context, spec, ActionTargetData.Self(2, new GridCoord(1, 0), Direction.None), 7, "source-target");

            Assert.AreEqual(new EffectSpecId("temporary_pushable"), application.Spec.SpecId);
            Assert.AreEqual(2, application.TargetEntityId);
            Assert.AreEqual(7, application.StartTick);
            Assert.AreEqual(0, application.ExpireTick);
            Assert.AreEqual("source-target", application.StackKey);
            Assert.AreEqual(RuntimeEffectKind.TemporaryPushable, application.ToRuntimeSpec().Kind);
        }

        [Test]
        public void AddRuntimeEffectCommit_ResolvesFinalComponent()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(20, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(20, out GameEntity entity));
            var provider = LubanGameConfigProvider.FromDirectory(GameConfigDirectory());
            Assert.IsTrue(provider.TryGetEffectSpec("temporary_pushable", out EffectSpec spec));
            var context = new ActionContext(1, 1, "grant_pushable", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, 20, 0, WorldTag.SourceDebug), 20, 20, 20, 20, new ActionTarget(20, null, Direction.None), Direction.None, 0, 0, 1, 0, 1);
            var application = new EffectApplication(context, spec, ActionTargetData.Self(20, new GridCoord(0, 0), Direction.None), 0, "pushable");

            IReadOnlyList<CommitProposalResult> results = new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 1, application, 0) });

            Assert.IsTrue(results.Single().Accepted);
            Assert.IsTrue(world.HasComponent<PushableComponent>(entity));
            Assert.AreEqual(1, world.RuntimeEffects.Count);
        }

        [Test]
        public void EffectApplication_ExpireOverrideKeepsDebugPushableUntilManualRemove()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(121, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(121, out GameEntity entity));
            var provider = LubanGameConfigProvider.FromDirectory(GameConfigDirectory());
            Assert.IsTrue(provider.TryGetEffectSpec("temporary_pushable", out EffectSpec spec));
            var context = new ActionContext(121, 121, "debug_pushable", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, 121, 0, WorldTag.SourceDebug), 121, 121, 121, 121, new ActionTarget(121, null, Direction.None), Direction.None, 0, 0, 1, 0, 121);
            var application = new EffectApplication(context, spec, ActionTargetData.Self(121, default, Direction.None), 0, "debug:121:3", 0);

            new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 121, application, 0) });
            world.ExpireRuntimeEffects(100);

            Assert.IsTrue(world.HasComponent<PushableComponent>(entity));
            Assert.AreEqual(1, world.RuntimeEffects.ActiveAt(100).Count);
        }

        [Test]
        public void RuntimeTagEffect_AddsAndRemovesThroughResolver()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(21, 21, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(21, out GameEntity entity));
            var provider = LubanGameConfigProvider.FromDirectory(GameConfigDirectory());
            Assert.IsTrue(provider.TryGetEffectSpec("temporary_tag_super_armor", out EffectSpec spec));
            var context = new ActionContext(2, 2, "grant_tag", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, 21, 0, WorldTag.SourceDebug), 21, 21, 21, 21, new ActionTarget(21, null, Direction.None), Direction.None, 0, 0, 1, 0, 2);
            var application = new EffectApplication(context, spec, ActionTargetData.Self(21, new GridCoord(0, 0), Direction.None), 0, "tag");

            new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 2, application, 0) });
            Assert.IsTrue(world.HasTag(entity, WorldTag.StateSuperArmor));
            RuntimeEffectId effectId = world.RuntimeEffects.ActiveAt(0).Single().Id;

            new CommitResolver().Resolve(world, new[] { CommitProposal.RemoveRuntimeEffect(WorldActionPriority.Debug, 3, 21, effectId, 1) });
            Assert.IsFalse(world.HasTag(entity, WorldTag.StateSuperArmor));
        }

        [Test]
        public void StackPolicy_RefreshKeepsSingleSourceAndExtendsExpiry()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(22, new GridCoord(0, 0))));
            var spec = new EffectSpec("refresh_blocking", EffectKind.Blocking, EffectTargetBinding.TargetEntity, EffectDurationPolicy.TimedTicks, EffectStackPolicy.RefreshDuration, EffectRemovePolicy.ExplicitOrExpire, 5, 1, DirectionMask.None, true, true, WorldTag.None);
            var context = new ActionContext(4, 4, "refresh", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, 22, 0, WorldTag.SourceDebug), 22, 22, 22, 22, new ActionTarget(22, null, Direction.None), Direction.None, 0, 0, 1, 0, 4);
            var first = new EffectApplication(context, spec, ActionTargetData.Self(22, new GridCoord(0, 0), Direction.None), 0, "same");
            var second = new EffectApplication(context, spec, ActionTargetData.Self(22, new GridCoord(0, 0), Direction.None), 3, "same");

            var resolver = new CommitResolver();
            resolver.Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 4, first, 0) });
            resolver.Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 5, second, 3) });

            Assert.AreEqual(1, world.RuntimeEffects.Count);
            Assert.AreEqual(8, world.RuntimeEffects.ActiveAt(3).Single().Spec.ExpireTick);
        }

        [Test]
        public void ClearRuntimeSources_RemovesEffectsAndKeepsStaticFinalResult()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(23, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(23, out GameEntity entity));
            AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryAutoMove, "reset_auto", 2), 23, 0, "auto");
            AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryImmobile, "reset_immobile", 1), 23, 0, "immobile");
            new CommitResolver().Resolve(world, new[] { CommitProposal.AddTag(WorldActionPriority.Debug, 901, 23, WorldTag.StateSuperArmor, 0) });
            Assert.IsTrue(world.HasComponent<AutoMoveComponent>(entity));
            Assert.IsTrue(world.HasComponent<MovementPermissionComponent>(entity));
            Assert.IsTrue(world.HasTag(entity, WorldTag.StateSuperArmor));

            IReadOnlyList<CommitProposalResult> results = new CommitResolver().Resolve(world, new[] { CommitProposal.ClearRuntimeSources(WorldActionPriority.Debug, 902, 23, 0) });

            Assert.IsTrue(results.Single().Accepted);
            Assert.AreEqual(0, world.RuntimeEffects.Count);
            Assert.IsTrue(world.HasComponent<PushableComponent>(entity));
            Assert.IsFalse(world.HasComponent<AutoMoveComponent>(entity));
            Assert.IsFalse(world.HasComponent<MovementPermissionComponent>(entity));
            Assert.IsFalse(world.HasTag(entity, WorldTag.StateSuperArmor));
        }

        [Test]
        public void RuntimeRotatePivotSource_MergesWithStaticAndRuntimeSources()
        {
            var world = new GameWorld();
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(240, new GridCoord(0, 0))));
            Assert.IsTrue(world.TryGetEntity(240, out GameEntity runtimeOnly));
            RuntimeEffectInstance first = AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryRotatePivot, "pivot_a", 1), 240, 0, "pivot_a");
            RuntimeEffectInstance second = AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryRotatePivot, "pivot_b", 1), 240, 0, "pivot_b");

            Assert.IsTrue(world.HasComponent<RotatePivotComponent>(runtimeOnly));
            Assert.IsTrue(RemoveEffect(world, 240, first.Id));
            Assert.IsTrue(world.HasComponent<RotatePivotComponent>(runtimeOnly));
            Assert.IsTrue(RemoveEffect(world, 240, second.Id));
            Assert.IsFalse(world.HasComponent<RotatePivotComponent>(runtimeOnly));

            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.BlockerSpawn(241, new GridCoord(1, 0))));
            Assert.IsTrue(world.TryGetEntity(241, out GameEntity staticEntity));
            world.SetComponent(staticEntity, new RotatePivotComponent());
            world.CaptureStaticComponentSources(staticEntity);
            RuntimeEffectInstance runtime = AddEffect(world, EffectSpecFor(RuntimeEffectKind.TemporaryRotatePivot, "pivot_static", 1), 241, 0, "pivot_static");

            Assert.IsTrue(RemoveEffect(world, 241, runtime.Id));
            Assert.IsTrue(world.HasComponent<RotatePivotComponent>(staticEntity));
        }

        [Test]
        public void ApplyRuntimeEffectStrategy_GeneratesApplicationsForMultipleTargets()
        {
            var provider = LubanGameConfigProvider.FromDirectory(GameConfigDirectory());
            var actionSpec = new ActionSpec("multi_pushable", ActionPrimitive.ApplyRuntimeEffect, ActionSourceKind.Debug, WorldActionPriority.Debug, WorldTag.SourceDebug, WorldTag.None, WorldTag.None, WorldTag.None, ActionTargetRule.FrontEntities, "reject", ActionConflictPolicy.None, ActionInterruptPolicy.None, ActionMergePolicy.None, ActionPlanRule.None, ActionCommitRule.None, targeting: new TargetingSpec("front_two", "front_entities", TargetDirectionSource.Request, TargetFilterSpec.None.FilterId, TargetOrderingPolicy.HitOrderThenCoordThenEntity, 3, 2), effectSpecId: "temporary_pushable");
            var registry = new ActionSpecRegistry(new[] { actionSpec }, new[] { BlockedResultPolicyFactory.RejectPolicy("reject") });
            var primitiveRunners = new PrimitiveRunnerRegistry();
            var applyEffect = new ApplyEffectRunner();
            primitiveRunners.Register(new RunnerId("apply_effect_runner"), context => applyEffect.Process(context));
            var world = new GameWorld(provider);
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PlayerSpawn(30, 30, new GridCoord(0, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(31, new GridCoord(1, 0))));
            Assert.IsTrue(world.AddEntity(DefaultWorldConfig.PushableBlockerSpawn(32, new GridCoord(2, 0))));
            var queue = new WorldActionQueue(registry);
            WorldAction action = queue.EnqueueConfiguredMove("multi_pushable", 30, Direction.Right, 0, 1);

            var system = new BehaviorRuntime(registry, primitiveRunners, provider);
            BehaviorRuntimeTickResult result = system.Tick(world, queue.DrainReady(1), 1);
            for (int i = 0; i < 8 && system.RunningBehaviorCount != 0 && result.ActionResults.Count == 0; i++)
            {
                world.NextTick();
                result = system.Tick(world, queue.DrainReady(world.ServerTick), world.ServerTick);
            }

            Assert.IsTrue(result.ActionResults[action.ActionId].Success);
            Assert.AreEqual(2, world.RuntimeEffects.Count);
            Assert.IsTrue(world.RuntimeEffects.ActiveAt(result.ProposalResults.Max(item => item.Proposal.ServerTick)).Any(effect => effect.TargetEntityId == 31));
            Assert.IsTrue(world.RuntimeEffects.ActiveAt(result.ProposalResults.Max(item => item.Proposal.ServerTick)).Any(effect => effect.TargetEntityId == 32));
        }

        [Test]
        public void RulesDoNotQueryRuntimeEffectOrAbilityKinds()
        {
            string rulesPath = Path.Combine(RepositoryRoot(), "Shared", "DG.GameCore", "ActionRuntime");
            string[] files = Directory.GetFiles(rulesPath, "*.cs", SearchOption.AllDirectories);
            string text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

            Assert.IsFalse(text.Contains("RuntimeEffectStore"));
            Assert.IsFalse(text.Contains("RuntimeEffectKind"));
            Assert.IsFalse(Regex.IsMatch(text, @"\bAbilityKind\b"));
            Assert.IsFalse(Regex.IsMatch(text, @"\bEffectKind\b"));
        }

        [Test]
        public void ServerAuthoritativeSubmitter_DoesNotContainLocalRuntimeEffectMutation()
        {
            string path = Path.Combine(RepositoryRoot(), "Client", "DG_Client", "Assets", "Scripts", "ClientWorld", "Networking", "Runtime", "ClientMoveNetworkSubmitter.cs");
            string text = File.ReadAllText(path);

            Assert.IsFalse(Regex.IsMatch(text, @"\.(AddRuntimeEffect|RemoveRuntimeEffect)\s*\("));
            Assert.IsFalse(text.Contains("TryCreateRuntimeEffectSpec"));
            Assert.IsFalse(text.Contains("RuntimeEffectSpec."));
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }

        private static string GameConfigDirectory()
        {
            return Path.Combine(Application.streamingAssetsPath, "GameConfig");
        }

        private static RuntimeEffectInstance AddEffect(GameWorld world, EffectSpec spec, long entityId, long startTick, string stackKey)
        {
            var context = new ActionContext(99, 99, "test_effect", WorldActionPriority.Debug, new ActionSourceContext(ActionSourceKind.Debug, entityId, 0, WorldTag.SourceDebug), entityId, entityId, entityId, entityId, new ActionTarget(entityId, null, Direction.None), Direction.None, startTick, startTick, 1, 0, 99);
            var application = new EffectApplication(context, spec, ActionTargetData.Self(entityId, default, Direction.None), startTick, stackKey);
            IReadOnlyList<CommitProposalResult> results = new CommitResolver().Resolve(world, new[] { CommitProposal.AddRuntimeEffect(WorldActionPriority.Debug, 99, application, startTick) });
            Assert.IsTrue(results.Single().Accepted);
            return world.RuntimeEffects.ActiveAt(startTick).OrderByDescending(effect => effect.Id.Value).First(effect => effect.TargetEntityId == entityId && effect.Spec.EffectSpecId.Equals(spec.SpecId));
        }

        private static bool RemoveEffect(GameWorld world, long entityId, RuntimeEffectId effectId)
        {
            IReadOnlyList<CommitProposalResult> results = new CommitResolver().Resolve(world, new[] { CommitProposal.RemoveRuntimeEffect(WorldActionPriority.Debug, 100, entityId, effectId, world.ServerTick) });
            return results.Single().Accepted;
        }

        private static EffectSpec EffectSpecFor(RuntimeEffectKind kind, string id, int autoMoveIntervalTicks, DirectionMask portMask = DirectionMask.None)
        {
            return kind switch
            {
                RuntimeEffectKind.TemporaryBlocking => new EffectSpec(id, EffectKind.Blocking, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.None, true, true, WorldTag.None),
                RuntimeEffectKind.TemporaryAutoMove => new EffectSpec(id, EffectKind.AutoMove, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, autoMoveIntervalTicks, DirectionMask.None, true, true, WorldTag.None),
                RuntimeEffectKind.TemporaryPort => new EffectSpec(id, EffectKind.PortConnector, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, portMask, true, true, WorldTag.None),
                RuntimeEffectKind.TemporaryImmobile => new EffectSpec(id, EffectKind.MovementPermission, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.None, false, false, WorldTag.None),
                RuntimeEffectKind.TemporaryRotatePivot => new EffectSpec(id, EffectKind.RotatePivot, EffectTargetBinding.TargetEntity, EffectDurationPolicy.InfiniteUntilRemove, EffectStackPolicy.AllowMultiple, EffectRemovePolicy.ExplicitOnly, 0, 1, DirectionMask.None, true, true, WorldTag.None),
                _ => throw new InvalidOperationException("unsupported test effect kind")
            };
        }

        private sealed class TestComponentResultResolver : IComponentResultResolver
        {
            public ComponentResultId ResultId => new("test_result");

            public void Apply(GameWorld world, GameEntity entity, IReadOnlyList<ComponentSourceContribution> sources)
            {
                if (sources.Count > 0)
                {
                    world.SetComponent(entity, new BouncableComponent());
                }
            }
        }

        private sealed class TestCommitHandler : ICommitProposalHandler
        {
            public CommitProposalId ProposalId => new(CommitProposalKind.AddTag);
            public CommitProposalKind Kind => CommitProposalKind.AddTag;

            public CommitProposalResult Apply(GameWorld world, CommitProposal proposal, CommitResolveContext context)
            {
                return new CommitProposalResult(proposal, true, "test-handler");
            }
        }
    }
}
