using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public static class LubanActionSpecRegistry
{
    public static ActionSpecRegistry FromDirectory(string dataDirectory)
    {
        return FromTables(LubanConfigLoader.LoadTables(dataDirectory));
    }

    public static ActionSpecRegistry FromTables(cfg.Tables tables)
    {
        if (tables == null)
        {
            throw new ArgumentNullException(nameof(tables));
        }

        IReadOnlyDictionary<TargetFilterSpecId, TargetFilterSpec> filters = ConvertTargetFilters(tables);
        IReadOnlyDictionary<TargetingSpecId, TargetingSpec> targetingSpecs = ConvertTargetingSpecs(tables, filters);
        IReadOnlyList<ActionSpec> specs = tables.TbActionSpec.DataList.Select(row => Convert(row, targetingSpecs)).ToArray();
        return new ActionSpecRegistry(specs, ConvertBlockedResultPolicies(tables, specs));
    }

    private static ActionSpec Convert(cfg.gamecore.ActionSpec row, IReadOnlyDictionary<TargetingSpecId, TargetingSpec> targetingSpecs)
    {
        if (row == null)
        {
            throw new ArgumentNullException(nameof(row));
        }

        ActionSpecId specId = row.SpecId;
        if (!specId.IsValid)
        {
            throw new InvalidOperationException("Action spec id is empty.");
        }

        ActionHandoffPolicy handoffPolicy = ConvertEnum<cfg.gamecore.ActionHandoffPolicy, ActionHandoffPolicy>(row.HandoffPolicy, specId, nameof(row.HandoffPolicy));
        ActionSubjectKind handoffSubject = ConvertEnum<cfg.gamecore.ActionSubjectPolicy, ActionSubjectKind>(row.HandoffSubjectPolicy, specId, nameof(row.HandoffSubjectPolicy));

        ActionTargetRule legacyRule = ConvertEnum<cfg.gamecore.ActionTargetRule, ActionTargetRule>(row.TargetRule, specId, nameof(row.TargetRule));
        TargetingSpecId targetingId = string.IsNullOrWhiteSpace(row.TargetingId)
            ? TargetingSpec.FromLegacyRule(legacyRule).SpecId
            : new TargetingSpecId(row.TargetingId);
        if (!targetingSpecs.TryGetValue(targetingId, out TargetingSpec targeting))
        {
            throw new InvalidOperationException("Unknown targeting spec on " + specId + ": " + targetingId);
        }

        if (legacyRule != ActionTargetRule.None && targeting.LegacyRule != ActionTargetRule.None && targeting.LegacyRule != legacyRule)
        {
            throw new InvalidOperationException("Targeting spec legacy rule mismatch on " + specId + ": " + targeting.SpecId);
        }

        return new ActionSpec(
            specId,
            ConvertEnum<cfg.gamecore.ActionPrimitive, ActionPrimitive>(row.Primitive, specId, nameof(row.Primitive)),
            ConvertEnum<cfg.gamecore.ActionSourceKind, ActionSourceKind>(row.Source, specId, nameof(row.Source)),
            ConvertEnum<cfg.gamecore.ActionPriority, WorldActionPriority>(row.Priority, specId, nameof(row.Priority)),
            ParseTags(row.SourceTag),
            ParseTags(row.AbilityTag),
            ParseTags(row.RequiredTags),
            ParseTags(row.BlockedTags),
            legacyRule,
            row.BlockedResultPolicyId,
            ConvertEnum<cfg.gamecore.ActionConflictPolicy, ActionConflictPolicy>(row.ConflictPolicy, specId, nameof(row.ConflictPolicy)),
            ConvertEnum<cfg.gamecore.ActionInterruptPolicy, ActionInterruptPolicy>(row.InterruptPolicy, specId, nameof(row.InterruptPolicy)),
            ConvertEnum<cfg.gamecore.ActionMergePolicy, ActionMergePolicy>(row.MergePolicy, specId, nameof(row.MergePolicy)),
            ConvertEnum<cfg.gamecore.ActionPlanRule, ActionPlanRule>(row.PlanRule, specId, nameof(row.PlanRule)),
            ConvertCommitRules(row.CommitRules),
            ConvertEnum<cfg.gamecore.ActionSubjectPolicy, ActionSubjectKind>(row.SubjectPolicy, specId, nameof(row.SubjectPolicy)),
            new ActionHandoffSpec(handoffPolicy, row.HandoffSpecId, handoffSubject),
            row.DefaultCostTicks,
            targeting);
    }

    private static IReadOnlyDictionary<TargetFilterSpecId, TargetFilterSpec> ConvertTargetFilters(cfg.Tables tables)
    {
        var filters = new Dictionary<TargetFilterSpecId, TargetFilterSpec>();
        foreach (cfg.gamecore.TargetFilterSpec row in tables.TbTargetFilterSpec.DataList)
        {
            var filter = new TargetFilterSpec(row.FilterId, row.Conditions.Select(ConvertTargetFilterCondition).ToArray());
            if (filters.ContainsKey(filter.FilterId))
            {
                throw new InvalidOperationException("Duplicate target filter id: " + filter.FilterId);
            }

            filters.Add(filter.FilterId, filter);
        }

        if (!filters.ContainsKey(TargetFilterSpec.None.FilterId))
        {
            filters.Add(TargetFilterSpec.None.FilterId, TargetFilterSpec.None);
        }

        return filters;
    }

    private static IReadOnlyDictionary<TargetingSpecId, TargetingSpec> ConvertTargetingSpecs(cfg.Tables tables, IReadOnlyDictionary<TargetFilterSpecId, TargetFilterSpec> filters)
    {
        var specs = new Dictionary<TargetingSpecId, TargetingSpec>();
        foreach (cfg.gamecore.TargetingSpec row in tables.TbTargetingSpec.DataList)
        {
            TargetFilterSpecId filterId = row.FilterId;
            if (!filters.ContainsKey(filterId))
            {
                throw new InvalidOperationException("Unknown target filter id on " + row.TargetingId + ": " + filterId);
            }

            var spec = new TargetingSpec(
                row.TargetingId,
                row.SelectorId,
                ConvertEnum<cfg.gamecore.TargetDirectionSource, TargetDirectionSource>(row.DirectionSource, row.TargetingId, nameof(row.DirectionSource)),
                filterId,
                ConvertEnum<cfg.gamecore.TargetOrderingPolicy, TargetOrderingPolicy>(row.OrderingPolicy, row.TargetingId, nameof(row.OrderingPolicy)),
                row.Range,
                row.MaxTargets);
            TargetSelectorRegistry.CreateDefault().Get(spec.SelectorId);

            if (specs.ContainsKey(spec.SpecId))
            {
                throw new InvalidOperationException("Duplicate targeting spec id: " + spec.SpecId);
            }

            specs.Add(spec.SpecId, spec);
        }

        return specs;
    }

    private static TargetFilterCondition ConvertTargetFilterCondition(cfg.gamecore.TargetFilterCondition row)
    {
        return new TargetFilterCondition(
            ConvertEnum<cfg.gamecore.TargetFilterConditionKind, TargetFilterConditionKind>(row.Kind, row.ComponentKind, nameof(row.Kind)),
            ConvertEnum<cfg.gamecore.TargetFilterSubject, TargetFilterSubject>(row.Subject, row.ComponentKind, nameof(row.Subject)),
            ParseComponentKind(row.ComponentKind),
            ParseTags(row.Tag));
    }

    private static IReadOnlyList<BlockedResultPolicy> ConvertBlockedResultPolicies(cfg.Tables tables, IReadOnlyList<ActionSpec> specs)
    {
        var specMap = specs.ToDictionary(spec => spec.SpecId);
        var policies = new List<BlockedResultPolicy>();
        foreach (cfg.gamecore.BlockedResultPolicy row in tables.TbBlockedResultPolicy.DataList)
        {
            BlockedResultPolicyId policyId = row.PolicyId;
            List<BlockedResultBranch> branches = tables.TbBlockedResultBranch.DataList
                .Where(branch => branch.PolicyId == row.PolicyId)
                .Select(branch => ConvertBranch(branch, specMap))
                .ToList();

            if (branches.Count == 0)
            {
                throw new InvalidOperationException("Blocked result policy has no branches: " + policyId);
            }

            policies.Add(new BlockedResultPolicy(policyId, branches));
        }

        return policies;
    }

    private static BlockedResultBranch ConvertBranch(cfg.gamecore.BlockedResultBranch row, IReadOnlyDictionary<ActionSpecId, ActionSpec> specs)
    {
        ActionSpecId resultSpecId = row.ResultSpecId;
        bool hasOwnerSpec = TryFindOwnerSpec(specs, row.PolicyId, resultSpecId, out ActionSpec ownerSpec);
        if (row.ResultKind == cfg.gamecore.BlockedResultKind.DeriveAction && !hasOwnerSpec)
        {
            throw new InvalidOperationException("Blocked result branch cannot resolve result spec id for policy " + row.PolicyId + ".");
        }

        return new BlockedResultBranch(
            row.Order,
            row.Conditions.Select(ConvertCondition).ToArray(),
            ConvertEnum<cfg.gamecore.BlockedResultKind, BlockedResultKind>(row.ResultKind, row.PolicyId, nameof(row.ResultKind)),
            resultSpecId.IsValid ? resultSpecId : hasOwnerSpec ? ownerSpec.Handoff.SpecId : default,
            ConvertEnum<cfg.gamecore.ActionSubjectPolicy, ActionSubjectKind>(row.SubjectPolicy, row.PolicyId, nameof(row.SubjectPolicy)),
            row.Reason,
            ParseMoveErrorCode(row.ErrorCode, row.PolicyId),
            ConvertCommitRules(row.CommitRules));
    }

    private static bool TryFindOwnerSpec(IReadOnlyDictionary<ActionSpecId, ActionSpec> specs, string policyId, ActionSpecId resultSpecId, out ActionSpec spec)
    {
        if (resultSpecId.IsValid)
        {
            return specs.TryGetValue(resultSpecId, out spec);
        }

        var resolvedPolicyId = new BlockedResultPolicyId(policyId);
        foreach (ActionSpec item in specs.Values)
        {
            if (item.BlockedResultPolicyId.Equals(resolvedPolicyId) && item.Handoff.IsEnabled)
            {
                spec = item;
                return true;
            }
        }

        spec = null!;
        return false;
    }

    private static ActionCondition ConvertCondition(cfg.gamecore.ActionCondition row)
    {
        return new ActionCondition(
            ConvertEnum<cfg.gamecore.ActionConditionKind, ActionConditionKind>(row.Kind, row.Tag, nameof(row.Kind)),
            ConvertEnum<cfg.gamecore.ActionConditionSubject, ActionConditionSubject>(row.Subject, row.Tag, nameof(row.Subject)),
            ParseTags(row.Tag),
            ParseComponentKind(row.ComponentKind),
            ParseBodyKind(row.BodyKind));
    }

    private static ComponentKind ParseComponentKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (!Enum.TryParse(value, out ComponentKind result))
        {
            throw new InvalidOperationException("Unknown action component kind: " + value);
        }

        return result;
    }

    private static BehaviorBodyKind ParseBodyKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (!Enum.TryParse(value, out BehaviorBodyKind result))
        {
            throw new InvalidOperationException("Unknown action body kind: " + value);
        }

        return result;
    }

    private static MoveErrorCode ParseMoveErrorCode(string value, string owner)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MoveErrorCode.None;
        }

        if (!Enum.TryParse(value, out MoveErrorCode result))
        {
            throw new InvalidOperationException("Unknown move error code on " + owner + ": " + value);
        }

        return result;
    }

    private static ActionCommitRule ConvertCommitRules(IReadOnlyList<cfg.gamecore.ActionCommitRule> rows)
    {
        ActionCommitRule result = ActionCommitRule.None;
        for (int i = 0; i < rows.Count; i++)
        {
            result |= ConvertEnum<cfg.gamecore.ActionCommitRule, ActionCommitRule>(rows[i], string.Empty, nameof(rows));
        }

        return result;
    }

    private static TTarget ConvertEnum<TSource, TTarget>(TSource value, ActionSpecId specId, string fieldName)
        where TSource : Enum
        where TTarget : struct, Enum
    {
        int rawValue = System.Convert.ToInt32(value);
        var target = (TTarget)Enum.ToObject(typeof(TTarget), rawValue);
        if (!Enum.IsDefined(typeof(TTarget), target))
        {
            string owner = specId.IsValid ? specId.ToString() : "<commit-rule>";
            throw new InvalidOperationException("Unknown action enum " + fieldName + " on " + owner + ": " + rawValue);
        }

        return target;
    }

    private static TTarget ConvertEnum<TSource, TTarget>(TSource value, string owner, string fieldName)
        where TSource : Enum
        where TTarget : struct, Enum
    {
        int rawValue = System.Convert.ToInt32(value);
        var target = (TTarget)Enum.ToObject(typeof(TTarget), rawValue);
        if (!Enum.IsDefined(typeof(TTarget), target))
        {
            throw new InvalidOperationException("Unknown action enum " + fieldName + " on " + owner + ": " + rawValue);
        }

        return target;
    }

    private static WorldTag ParseTags(IReadOnlyList<string> values)
    {
        WorldTag result = WorldTag.None;
        for (int i = 0; i < values.Count; i++)
        {
            result |= ParseTags(values[i]);
        }

        return result;
    }

    private static WorldTag ParseTags(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return WorldTag.None;
        }

        WorldTag result = WorldTag.None;
        string[] parts = value.Split(new[] { ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (!Enum.TryParse(parts[i], out WorldTag tag))
            {
                throw new InvalidOperationException("Unknown action tag: " + parts[i]);
            }

            result |= tag;
        }

        return result;
    }
}
}
