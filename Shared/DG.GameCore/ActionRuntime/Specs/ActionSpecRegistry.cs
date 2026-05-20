using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public sealed class ActionSpec
{
    public static ActionSpec FromLegacyPrimitive(
        ActionSpecId specId,
        ActionPrimitive primitive,
        ActionSourceKind defaultSource,
        WorldActionPriority defaultPriority,
        WorldTag sourceTag,
        WorldTag abilityTag,
        WorldTag requiredTags,
        WorldTag blockedTags,
        ActionTargetRule targetRule,
        BlockedResultPolicyId blockedResultPolicyId,
        ActionConflictPolicy conflictPolicy,
        ActionInterruptPolicy interruptPolicy,
        ActionMergePolicy mergePolicy,
        ActionPlanRule planRule,
        ActionCommitRule commitRules,
        ActionSubjectKind subjectKind = ActionSubjectKind.HitEntity,
        ActionHandoffSpec handoff = default,
        int defaultCostTicks = 1,
        TargetingSpec? targeting = null,
        EffectSpecId effectSpecId = default)
    {
        return new ActionSpec(specId, primitive, defaultSource, defaultPriority, sourceTag, abilityTag, requiredTags, blockedTags, targetRule, blockedResultPolicyId, conflictPolicy, interruptPolicy, mergePolicy, planRule, commitRules, subjectKind, handoff, defaultCostTicks, targeting, effectSpecId);
    }

    public ActionSpec(
        ActionSpecId specId,
        ActionPrimitive primitive,
        ActionSourceKind defaultSource,
        WorldActionPriority defaultPriority,
        WorldTag sourceTag,
        WorldTag abilityTag,
        WorldTag requiredTags,
        WorldTag blockedTags,
        ActionTargetRule targetRule,
        BlockedResultPolicyId blockedResultPolicyId,
        ActionConflictPolicy conflictPolicy,
        ActionInterruptPolicy interruptPolicy,
        ActionMergePolicy mergePolicy,
        ActionPlanRule planRule,
        ActionCommitRule commitRules,
        ActionSubjectKind subjectKind = ActionSubjectKind.HitEntity,
        ActionHandoffSpec handoff = default,
        int defaultCostTicks = 1,
        TargetingSpec? targeting = null,
        EffectSpecId effectSpecId = default)
    {
        SpecId = specId;
        Primitive = primitive;
        DefaultSource = defaultSource;
        DefaultPriority = defaultPriority;
        SourceTag = sourceTag;
        AbilityTag = abilityTag;
        RequiredTags = requiredTags;
        BlockedTags = blockedTags;
        TargetRule = targetRule;
        BlockedResultPolicyId = blockedResultPolicyId;
        ConflictPolicy = conflictPolicy;
        InterruptPolicy = interruptPolicy;
        MergePolicy = mergePolicy;
        PlanRule = planRule;
        CommitRules = commitRules;
        SubjectKind = subjectKind;
        if (defaultCostTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultCostTicks), defaultCostTicks, "Action spec default cost ticks must be at least 1.");
        }

        Handoff = handoff.Policy == ActionHandoffPolicy.None && !handoff.SpecId.IsValid ? ActionHandoffSpec.None : handoff;
        DefaultCostTicks = defaultCostTicks;
        Targeting = targeting ?? TargetingSpec.FromLegacyRule(targetRule);
        EffectSpecId = effectSpecId;
    }

    public ActionSpecId SpecId { get; }
    public ActionPrimitive Primitive { get; }
    public ActionSourceKind DefaultSource { get; }
    public WorldActionPriority DefaultPriority { get; }
    public WorldTag SourceTag { get; }
    public WorldTag AbilityTag { get; }
    public WorldTag RequiredTags { get; }
    public WorldTag BlockedTags { get; }
    public ActionTargetRule TargetRule { get; }
    public BlockedResultPolicyId BlockedResultPolicyId { get; }
    public ActionConflictPolicy ConflictPolicy { get; }
    public ActionInterruptPolicy InterruptPolicy { get; }
    public ActionMergePolicy MergePolicy { get; }
    public ActionPlanRule PlanRule { get; }
    public ActionCommitRule CommitRules { get; }
    public ActionSubjectKind SubjectKind { get; }
    public ActionHandoffSpec Handoff { get; }
    public int DefaultCostTicks { get; }
    public TargetingSpec Targeting { get; }
    public EffectSpecId EffectSpecId { get; }
    public bool AllowsConnectedBodySubject => SubjectKind == ActionSubjectKind.ConnectedBodyIfAny;
}


public sealed class ActionSpecRegistry
{
    private readonly Dictionary<ActionSpecId, ActionSpec> specs;
    private readonly Dictionary<BlockedResultPolicyId, BlockedResultPolicy> blockedResultPolicies;
    private readonly Dictionary<TargetingSpecId, TargetingSpec> targetingSpecs;
    private readonly Dictionary<TargetFilterSpecId, TargetFilterSpec> targetFilters;
    private readonly Dictionary<string, ActionSpecId> specAliases;
    private readonly Dictionary<string, BlockedResultPolicyId> blockedResultPolicyAliases;

    public ActionSpecRegistry(IEnumerable<ActionSpec> specs, IEnumerable<BlockedResultPolicy> blockedResultPolicies)
        : this(specs, blockedResultPolicies, EmptySpecAliases, EmptyPolicyAliases)
    {
    }

    private static readonly IReadOnlyDictionary<string, ActionSpecId> EmptySpecAliases = new Dictionary<string, ActionSpecId>();
    private static readonly IReadOnlyDictionary<string, BlockedResultPolicyId> EmptyPolicyAliases = new Dictionary<string, BlockedResultPolicyId>();

    public ActionSpecRegistry(IEnumerable<ActionSpec> specs, IEnumerable<BlockedResultPolicy> blockedResultPolicies, IReadOnlyDictionary<string, ActionSpecId> specAliases, IReadOnlyDictionary<string, BlockedResultPolicyId> blockedResultPolicyAliases)
    {
        this.specs = new Dictionary<ActionSpecId, ActionSpec>();
        this.blockedResultPolicies = new Dictionary<BlockedResultPolicyId, BlockedResultPolicy>();
        targetingSpecs = new Dictionary<TargetingSpecId, TargetingSpec>();
        targetFilters = new Dictionary<TargetFilterSpecId, TargetFilterSpec>();
        this.specAliases = new Dictionary<string, ActionSpecId>(StringComparer.Ordinal);
        this.blockedResultPolicyAliases = new Dictionary<string, BlockedResultPolicyId>(StringComparer.Ordinal);
        AddTargetFilter(TargetFilterSpec.None);
        foreach (ActionSpec spec in specs)
        {
            if (!spec.SpecId.IsValid)
            {
                throw new InvalidOperationException("Action spec id is empty.");
            }

            if (this.specs.ContainsKey(spec.SpecId))
            {
                throw new InvalidOperationException("Duplicate action spec id: " + spec.SpecId);
            }

            this.specs.Add(spec.SpecId, spec);
            AddTargetingSpec(spec.Targeting);
            AddSpecAlias(spec.SpecId.Value, spec.SpecId);
        }

        if (blockedResultPolicies != null)
        {
            foreach (BlockedResultPolicy policy in blockedResultPolicies)
            {
                if (this.blockedResultPolicies.ContainsKey(policy.PolicyId))
                {
                    throw new InvalidOperationException("Duplicate blocked result policy id: " + policy.PolicyId);
                }

                this.blockedResultPolicies.Add(policy.PolicyId, policy);
                AddBlockedResultPolicyAlias(policy.PolicyId.Value, policy.PolicyId);
            }
        }

        AddSpecAliases(specAliases);
        AddBlockedResultPolicyAliases(blockedResultPolicyAliases);

        foreach (ActionSpec spec in this.specs.Values)
        {
            if (!spec.BlockedResultPolicyId.IsValid)
            {
                throw new InvalidOperationException("Action spec missing blocked result policy id: " + spec.SpecId);
            }

            if (!this.blockedResultPolicies.ContainsKey(spec.BlockedResultPolicyId))
            {
                throw new InvalidOperationException("Unknown blocked result policy id on " + spec.SpecId + ": " + spec.BlockedResultPolicyId);
            }

            if (!targetingSpecs.ContainsKey(spec.Targeting.SpecId))
            {
                throw new InvalidOperationException("Unknown targeting spec id on " + spec.SpecId + ": " + spec.Targeting.SpecId);
            }

            if (!targetFilters.ContainsKey(spec.Targeting.FilterId))
            {
                throw new InvalidOperationException("Unknown target filter id on " + spec.SpecId + ": " + spec.Targeting.FilterId);
            }
        }

        foreach (BlockedResultPolicy policy in this.blockedResultPolicies.Values)
        {
            for (int i = 0; i < policy.Branches.Count; i++)
            {
                BlockedResultBranch branch = policy.Branches[i];
                if (branch.ResultKind == BlockedResultKind.DeriveAction && !this.specs.ContainsKey(branch.ResultSpecId))
                {
                    throw new InvalidOperationException("Unknown blocked result spec id on " + policy.PolicyId + ": " + branch.ResultSpecId);
                }
            }
        }
    }

    public static ActionSpecRegistry Default { get; } = LubanActionSpecRegistry.FromDirectory(GameCoreConfigPath.FindGeneratedJsonDirectory());

    public IReadOnlyCollection<ActionSpec> Specs => specs.Values.ToArray();
    public IReadOnlyCollection<BlockedResultPolicy> BlockedResultPolicies => blockedResultPolicies.Values.ToArray();
    public IReadOnlyCollection<TargetingSpec> TargetingSpecs => targetingSpecs.Values.ToArray();
    public IReadOnlyCollection<TargetFilterSpec> TargetFilters => targetFilters.Values.ToArray();

    public bool TryGet(ActionSpecId id, out ActionSpec spec)
    {
        return specs.TryGetValue(id, out spec);
    }

    public bool TryGet(string alias, out ActionSpec spec)
    {
        if (specAliases.TryGetValue(alias ?? string.Empty, out ActionSpecId id))
        {
            return TryGet(id, out spec);
        }

        spec = null!;
        return false;
    }

    public ActionSpec Get(ActionSpecId id)
    {
        if (!TryGet(id, out ActionSpec spec))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown action spec");
        }

        return spec;
    }

    public ActionSpec Get(string alias)
    {
        if (!TryGet(alias, out ActionSpec spec))
        {
            throw new ArgumentOutOfRangeException(nameof(alias), alias, "Unknown action spec");
        }

        return spec;
    }

    public BlockedResultPolicy GetBlockedResultPolicy(BlockedResultPolicyId id)
    {
        if (!blockedResultPolicies.TryGetValue(id, out BlockedResultPolicy policy))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown blocked result policy");
        }

        return policy;
    }

    public BlockedResultPolicy GetBlockedResultPolicy(string alias)
    {
        if (!blockedResultPolicyAliases.TryGetValue(alias ?? string.Empty, out BlockedResultPolicyId id))
        {
            throw new ArgumentOutOfRangeException(nameof(alias), alias, "Unknown blocked result policy");
        }

        return GetBlockedResultPolicy(id);
    }

    public TargetingSpec GetTargetingSpec(TargetingSpecId id)
    {
        if (!targetingSpecs.TryGetValue(id, out TargetingSpec spec))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown targeting spec");
        }

        return spec;
    }

    private void AddSpecAliases(IReadOnlyDictionary<string, ActionSpecId> aliases)
    {
        if (aliases == null)
        {
            return;
        }

        foreach (KeyValuePair<string, ActionSpecId> pair in aliases)
        {
            AddSpecAlias(pair.Key, ResolveSpecId(pair.Value));
        }
    }

    private void AddBlockedResultPolicyAliases(IReadOnlyDictionary<string, BlockedResultPolicyId> aliases)
    {
        if (aliases == null)
        {
            return;
        }

        foreach (KeyValuePair<string, BlockedResultPolicyId> pair in aliases)
        {
            AddBlockedResultPolicyAlias(pair.Key, ResolvePolicyId(pair.Value));
        }
    }

    private ActionSpecId ResolveSpecId(ActionSpecId id)
    {
        if (!specs.ContainsKey(id))
        {
            throw new InvalidOperationException("Unknown action spec alias target: " + id);
        }

        return id;
    }

    private BlockedResultPolicyId ResolvePolicyId(BlockedResultPolicyId id)
    {
        if (!blockedResultPolicies.ContainsKey(id))
        {
            throw new InvalidOperationException("Unknown blocked result policy alias target: " + id);
        }

        return id;
    }

    private void AddSpecAlias(string alias, ActionSpecId id)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            specAliases[alias] = id;
        }
    }

    private void AddBlockedResultPolicyAlias(string alias, BlockedResultPolicyId id)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            blockedResultPolicyAliases[alias] = id;
        }
    }

    private void AddTargetingSpec(TargetingSpec spec)
    {
        if (spec == null)
        {
            return;
        }

        if (!targetingSpecs.ContainsKey(spec.SpecId))
        {
            targetingSpecs.Add(spec.SpecId, spec);
        }
    }

    private void AddTargetFilter(TargetFilterSpec filter)
    {
        if (filter == null)
        {
            return;
        }

        if (!targetFilters.ContainsKey(filter.FilterId))
        {
            targetFilters.Add(filter.FilterId, filter);
        }
    }
}


public sealed class ActionRequestAdapter
{
    private readonly ActionSpecRegistry registry;

    public ActionRequestAdapter(ActionSpecRegistry registry)
    {
        this.registry = registry;
    }

    public ActionRequest FromWorldAction(WorldAction action)
    {
        ActionSpec spec = registry.Get(action.SpecId);
        ActionSourceKind sourceKind = string.IsNullOrEmpty(action.DeferredEquivalenceKey) ? spec.DefaultSource : ActionSourceKind.Handoff;
        var source = new ActionSourceContext(sourceKind, action.EntityId, 0, spec.SourceTag);
        var target = new ActionTarget(0, action.TargetCoord, action.Direction);
        return new ActionRequest(action.ActionId, spec.SpecId, spec.DefaultPriority, source, action.EntityId, target, ActionRuntimeParams.FromWorldAction(action), action.CreatedTick, action.ReadyTick, action.ClientTick, 0, 0, action.DeferredContributionCount, action.DeferredCausalitySamples, action.SubjectEntityIds, action.PushOriginContexts);
    }
}

}
