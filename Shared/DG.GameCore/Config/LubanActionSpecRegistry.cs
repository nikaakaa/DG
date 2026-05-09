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

        return new ActionSpecRegistry(tables.TbActionSpec.DataList.Select(Convert));
    }

    private static ActionSpec Convert(cfg.gamecore.ActionSpec row)
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

        return new ActionSpec(
            specId,
            ConvertEnum<cfg.gamecore.ActionPrimitive, ActionPrimitive>(row.Primitive, specId, nameof(row.Primitive)),
            ConvertEnum<cfg.gamecore.ActionSourceKind, ActionSourceKind>(row.Source, specId, nameof(row.Source)),
            ConvertEnum<cfg.gamecore.ActionPriority, WorldActionPriority>(row.Priority, specId, nameof(row.Priority)),
            ParseTags(row.SourceTag),
            ParseTags(row.AbilityTag),
            ParseTags(row.RequiredTags),
            ParseTags(row.BlockedTags),
            ConvertEnum<cfg.gamecore.ActionTargetRule, ActionTargetRule>(row.TargetRule, specId, nameof(row.TargetRule)),
            ConvertEnum<cfg.gamecore.ActionBlockedPolicy, ActionBlockedPolicy>(row.BlockedPolicy, specId, nameof(row.BlockedPolicy)),
            ConvertEnum<cfg.gamecore.ActionConflictPolicy, ActionConflictPolicy>(row.ConflictPolicy, specId, nameof(row.ConflictPolicy)),
            ConvertEnum<cfg.gamecore.ActionInterruptPolicy, ActionInterruptPolicy>(row.InterruptPolicy, specId, nameof(row.InterruptPolicy)),
            ConvertEnum<cfg.gamecore.ActionMergePolicy, ActionMergePolicy>(row.MergePolicy, specId, nameof(row.MergePolicy)),
            ConvertEnum<cfg.gamecore.ActionPlanRule, ActionPlanRule>(row.PlanRule, specId, nameof(row.PlanRule)),
            ConvertCommitRules(row.CommitRules),
            ConvertEnum<cfg.gamecore.ActionSubjectPolicy, ActionSubjectKind>(row.SubjectPolicy, specId, nameof(row.SubjectPolicy)),
            new ActionHandoffSpec(handoffPolicy, row.HandoffSpecId, handoffSubject),
            row.DefaultCostTicks);
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
