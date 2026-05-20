using System;
using System.IO;
using DG.GameCore;

namespace ExtensibilityVerification;

internal static class DummyRunnerExtensibilityVerification
{
    private const string DummyRunnerId = "dg_dummy_extensibility_runner";
    private const string DummyBehaviorId = "dg_dummy_extensibility_behavior";

    public static bool Run(out string reason)
    {
        if (!VerifyAllowlistFilesCleanOfDummyMarkers(out reason))
        {
            return false;
        }

        if (!VerifyDummyRegistrationGoesThroughPublicApi(out reason))
        {
            return false;
        }

        if (!VerifyAllowlistFilesDoNotMentionConcreteBehaviorModules(out reason))
        {
            return false;
        }

        if (!VerifyAllowlistFilesDoNotReverseInferActionFacts(out reason))
        {
            return false;
        }

        if (!VerifyDebugWorldEditServiceRoutesThroughInputQueue(out reason))
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyAllowlistFilesCleanOfDummyMarkers(out string reason)
    {
        foreach (string path in MainFlowAllowlist())
        {
            string content = File.ReadAllText(path);
            if (content.Contains(DummyRunnerId, StringComparison.Ordinal) ||
                content.Contains(DummyBehaviorId, StringComparison.Ordinal))
            {
                reason = "Main-flow file '" + path + "' references the dummy runner. Adding a new behavior must not require editing main-flow files.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyDummyRegistrationGoesThroughPublicApi(out string reason)
    {
        var behaviors = new BehaviorDefinitionRegistry();
        behaviors.Register(new BehaviorDefinition(
            new BehaviorId(DummyBehaviorId),
            new RunnerId(DummyRunnerId),
            ActionPrimitive.SetComponentResult,
            BehaviorClaimChannel.Status,
            BehaviorClaimMode.Shared,
            1));

        if (!behaviors.TryGetByPrimitive(ActionPrimitive.SetComponentResult, out BehaviorDefinition definition))
        {
            reason = "BehaviorDefinitionRegistry did not surface the dummy behavior via TryGetByPrimitive.";
            return false;
        }

        if (definition.BehaviorId.Value != DummyBehaviorId || definition.RunnerId.Value != DummyRunnerId)
        {
            reason = "BehaviorDefinitionRegistry returned the wrong definition for the dummy primitive.";
            return false;
        }

        var runners = new PrimitiveRunnerRegistry();
        int callCount = 0;
        runners.Register(new RunnerId(DummyRunnerId), _ => callCount++);

        Action<PrimitiveRunnerContext> dispatcher = runners.Get(definition);
        dispatcher.Invoke(default);
        if (callCount != 1)
        {
            reason = "Registered dummy dispatcher was not invoked through PrimitiveRunnerRegistry.Get.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyAllowlistFilesDoNotMentionConcreteBehaviorModules(out string reason)
    {
        string actionBehaviorModels = File.ReadAllText(LocateRepoFile(
            "Shared/DG.GameCore/ActionRuntime/Execution/ActionBehaviorModels.cs"));

        if (actionBehaviorModels.Contains("class RotatePivotRunner", StringComparison.Ordinal) ||
            actionBehaviorModels.Contains("class RotatePivotBehaviorModule", StringComparison.Ordinal))
        {
            reason = "ActionBehaviorModels.cs still defines a concrete rotate-pivot batch runner. Concrete batch behavior runners must live outside the main-flow allowlist file.";
            return false;
        }

        if (actionBehaviorModels.Contains("BatchBehaviorRunnerRegistry.CreateDefault", StringComparison.Ordinal) ||
            (actionBehaviorModels.Contains("BatchBehaviorRunnerRegistry", StringComparison.Ordinal) &&
                actionBehaviorModels.Contains("new RotatePivotRunner", StringComparison.Ordinal)))
        {
            reason = "ActionBehaviorModels.cs hardcodes batch behavior runner registration. CreateDefault must live in BatchBehaviorRunnerCatalog.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyAllowlistFilesDoNotReverseInferActionFacts(out string reason)
    {
        string stateDrivenRules = File.ReadAllText(LocateRepoFile(
            "Shared/DG.GameCore/ActionRuntime/Execution/StateDrivenRules.cs"));

        if (stateDrivenRules.Contains("TryCreateCommitActionFact", StringComparison.Ordinal))
        {
            reason = "StateDrivenRules.cs still references TryCreateCommitActionFact. Fact projection must live in CommitFactProjector.";
            return false;
        }

        if (stateDrivenRules.Contains("ResolvePresentationFactType", StringComparison.Ordinal))
        {
            reason = "StateDrivenRules.cs still references ResolvePresentationFactType. The spec-to-fact mapping must live in MovePresentationResolver.";
            return false;
        }

        if (stateDrivenRules.Contains("spec.DefaultSource == ActionSourceKind.Mechanism", StringComparison.Ordinal) ||
            stateDrivenRules.Contains("spec.DefaultSource == ActionSourceKind.Handoff", StringComparison.Ordinal))
        {
            reason = "StateDrivenRules.cs reverse-infers fact type from ActionSpec.DefaultSource. Runners must declare the PresentationHint on CommitProposal/MovePlan.";
            return false;
        }

        if (stateDrivenRules.Contains("proposal.Kind == CommitProposalKind.MoveEntity", StringComparison.Ordinal) ||
            stateDrivenRules.Contains("proposal.Kind == CommitProposalKind.CreateEntity", StringComparison.Ordinal) ||
            stateDrivenRules.Contains("proposal.Kind == CommitProposalKind.DeleteEntity", StringComparison.Ordinal))
        {
            reason = "StateDrivenRules.cs reverse-infers fact type from CommitProposalKind. Use proposal.PresentationHint instead.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool VerifyDebugWorldEditServiceRoutesThroughInputQueue(out string reason)
    {
        string debugServicePath = LocateRepoFile("Server/Hotfix/AuthoritativeMove/Debugging/DebugWorldEditService.cs");
        string content = File.ReadAllText(debugServicePath);

        string[] forbiddenBypasses =
        {
            "World.AddEntity",
            "World.MoveEntity",
            "World.RemoveEntity(",
            "World.AddTag",
            "World.RemoveTag",
            "World.MarkDirty",
            "applyEffectRunner.Commit",
            "removeEffectRunner.Commit",
            "new ApplyEffectRunner",
            "new RemoveEffectRunner",
            "CommitProposal.AddRuntimeEffect",
            "CommitProposal.RemoveRuntimeEffect",
            "CommitProposal.AddTag",
            "CommitProposal.RemoveTag"
        };

        foreach (string forbidden in forbiddenBypasses)
        {
            if (content.Contains(forbidden, StringComparison.Ordinal))
            {
                reason = "DebugWorldEditService.cs still calls '" + forbidden + "'. Debug edits must route through InputQueue.EnqueueDebug* so BehaviorRuntime owns the mutation.";
                return false;
            }
        }

        string[] requiredEnqueueCalls =
        {
            "InputQueue.EnqueueDebugSpawn",
            "InputQueue.EnqueueDebugMove",
            "InputQueue.EnqueueDebugRemove",
            "InputQueue.EnqueueDebugSetTag",
            "InputQueue.EnqueueDebugApplyEffect",
            "InputQueue.EnqueueDebugRemoveEffect"
        };

        foreach (string required in requiredEnqueueCalls)
        {
            if (!content.Contains(required, StringComparison.Ordinal))
            {
                reason = "DebugWorldEditService.cs is missing '" + required + "'. All debug edits must enqueue through AuthoritativeInputQueue.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static string[] MainFlowAllowlist() => new[]
    {
        LocateRepoFile("Shared/DG.GameCore/ActionRuntime/Execution/StateDrivenRules.cs"),
        LocateRepoFile("Shared/DG.GameCore/ActionRuntime/Execution/Runners/MoveBatchOrchestrator/MoveBatchOrchestrator.cs"),
        LocateRepoFile("Shared/DG.GameCore/ActionRuntime/Execution/ActionBehaviorModels.cs"),
        LocateRepoFile("Shared/DG.GameCore/ActionRuntime/Claims/ActionArbiter.cs"),
        LocateRepoFile("Shared/DG.GameCore/ActionRuntime/Claims/ActionClaims.cs"),
        LocateRepoFile("Shared/DG.GameCore/ActionRuntime/Specs/ActionSpecRegistry.cs"),
    };

    private static string LocateRepoFile(string relativeFromRepoRoot)
    {
        string? current = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && current is not null; i++)
        {
            string candidate = Path.Combine(current, relativeFromRepoRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Path.GetDirectoryName(current);
        }

        throw new FileNotFoundException("Could not locate repo file: " + relativeFromRepoRoot);
    }
}
