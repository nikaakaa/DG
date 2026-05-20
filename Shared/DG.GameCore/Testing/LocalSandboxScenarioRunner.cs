using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DG.GameCore
{
public sealed class LocalSandboxScenarioRunner
{
    private readonly IGameConfigProvider provider;
    private readonly GameWorld world;
    private readonly WorldActionQueue actionQueue = new();
    private readonly BehaviorRuntime ruleSystem = new();
    private readonly Dictionary<string, long> aliases = new(StringComparer.OrdinalIgnoreCase);
    private long nextEntityId = 700000000;
    private SandboxOperationResult lastResult = new(true, string.Empty, 0);

    public LocalSandboxScenarioRunner(IGameConfigProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        world = new GameWorld(provider);
    }

    public GameWorld World => world;
    public SandboxOperationResult LastResult => lastResult;

    public SandboxScenarioRunResult Run(SandboxScenarioDocument document)
    {
        IReadOnlyList<string> validationErrors = SandboxScenarioValidator.Validate(document, provider);
        if (validationErrors.Count > 0)
        {
            return new SandboxScenarioRunResult(false, validationErrors[0], -1, validationErrors, lastResult);
        }

        foreach (SandboxScenarioEntityRef entityRef in document.Entities ?? Enumerable.Empty<SandboxScenarioEntityRef>())
        {
            if (!string.IsNullOrWhiteSpace(entityRef.Alias))
            {
                aliases[entityRef.Alias] = entityRef.EntityId;
            }
        }

        IReadOnlyList<SandboxScenarioStep> steps = document.Steps != null ? document.Steps : Array.Empty<SandboxScenarioStep>();
        for (int i = 0; i < steps.Count; i++)
        {
            if (!ExecuteStep(steps[i], i, out string reason))
            {
                return new SandboxScenarioRunResult(false, reason, i, new[] { reason }, lastResult);
            }
        }

        IReadOnlyList<SandboxScenarioExpectation> expectations = document.Expectations != null ? document.Expectations : Array.Empty<SandboxScenarioExpectation>();
        for (int i = 0; i < expectations.Count; i++)
        {
            if (!CheckExpectation(expectations[i], steps.Count + i, out string reason))
            {
                return new SandboxScenarioRunResult(false, reason, steps.Count + i, new[] { reason }, lastResult);
            }
        }

        return new SandboxScenarioRunResult(true, string.Empty, -1, Array.Empty<string>(), lastResult);
    }

    private bool ExecuteStep(SandboxScenarioStep step, int index, out string reason)
    {
        string kind = Normalize(step.Kind);
        if (kind == "spawn")
        {
            long entityId = step.EntityId > 0 ? step.EntityId : AllocateEntityId();
            aliases[step.Alias] = entityId;
            SandboxScenarioValidator.TryParseDirection(step.Direction, true, out Direction direction);
            actionQueue.EnqueueDebugSpawn(entityId, step.ConfigId, new GridCoord(step.X, step.Y), direction, step.PlayerId, step.AutoMoveIntervalTicks <= 0 ? 1 : step.AutoMoveIntervalTicks);
            ExecuteTick();
            reason = lastResult.Success ? string.Empty : lastResult.Reason;
            return lastResult.Success;
        }

        if (kind == "move" || kind == "drag")
        {
            long entityId = ResolveEntityId(step.Alias, step.EntityId);
            actionQueue.EnqueueDebugMove(entityId, new GridCoord(step.X, step.Y));
            ExecuteTick();
            reason = lastResult.Success ? string.Empty : lastResult.Reason;
            return lastResult.Success;
        }

        if (kind == "playermove")
        {
            long entityId = ResolveEntityId(step.Alias, step.EntityId);
            actionQueue.EnqueuePlayerMove(entityId, new GridCoord(step.X, step.Y), world.ServerTick);
            ExecuteTick();
            reason = string.Empty;
            return true;
        }

        if (kind == "delete")
        {
            long entityId = ResolveEntityId(step.Alias, step.EntityId);
            actionQueue.EnqueueDebugRemove(entityId);
            ExecuteTick();
            reason = lastResult.Success ? string.Empty : lastResult.Reason;
            return lastResult.Success;
        }

        if (kind == "settag")
        {
            long entityId = ResolveEntityId(step.Alias, step.EntityId);
            SandboxScenarioValidator.TryParseTag(step.Tag, out WorldTag tag);
            if (!world.TryGetEntity(entityId, out GameEntity entity))
            {
                reason = "step " + index + ": entity not found";
                lastResult = new SandboxOperationResult(false, reason, entityId);
                return false;
            }

            if (step.Enabled)
            {
                world.AddTag(entity, tag);
            }
            else
            {
                world.RemoveTag(entity, tag);
            }

            world.MarkDirty(entityId);
            world.FlushDelta();
            reason = string.Empty;
            lastResult = new SandboxOperationResult(true, string.Empty, entityId);
            return true;
        }

        if (kind == "tick" || kind == "wait")
        {
            int count = Math.Max(step.Ticks, step.Frames);
            for (int i = 0; i < count; i++)
            {
                ExecuteTick();
            }

            reason = string.Empty;
            return true;
        }

        if (kind == "expectposition" || kind == "expecttag" || kind == "expectlastresult")
        {
            return CheckExpectation(new SandboxScenarioExpectation
            {
                Kind = step.Kind,
                Alias = step.Alias,
                EntityId = step.EntityId,
                X = step.X,
                Y = step.Y,
                Tag = step.Tag
            }, index, out reason);
        }

        reason = "step " + index + ": unknown step kind";
        return false;
    }

    private bool CheckExpectation(SandboxScenarioExpectation expectation, int index, out string reason)
    {
        string kind = Normalize(expectation.Kind);
        if (kind == "expectposition")
        {
            long entityId = ResolveEntityId(expectation.Alias, expectation.EntityId);
            if (!world.TryGetEntity(entityId, out GameEntity entity) ||
                !world.TryGetComponent(entity, out PositionComponent position))
            {
                reason = "step " + index + ": entity position missing";
                return false;
            }

            GridCoord expected = new(expectation.X, expectation.Y);
            if (position.Coord != expected)
            {
                reason = "step " + index + ": position expected " + expected.X + "," + expected.Y + " actual " + position.Coord.X + "," + position.Coord.Y;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        if (kind == "expecttag")
        {
            long entityId = ResolveEntityId(expectation.Alias, expectation.EntityId);
            SandboxScenarioValidator.TryParseTag(expectation.Tag, out WorldTag tag);
            if (!world.TryGetEntity(entityId, out GameEntity entity))
            {
                reason = "step " + index + ": entity not found";
                return false;
            }

            if (!world.HasTag(entity, tag))
            {
                reason = "step " + index + ": tag missing " + tag;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        if (kind == "expectlastresult")
        {
            if (expectation.Success.HasValue && lastResult.Success != expectation.Success.Value)
            {
                reason = "step " + index + ": last result success mismatch";
                return false;
            }

            if (!string.IsNullOrEmpty(expectation.Reason) &&
                !string.Equals(lastResult.Reason, expectation.Reason, StringComparison.OrdinalIgnoreCase))
            {
                reason = "step " + index + ": last result reason mismatch";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        reason = "step " + index + ": unknown expectation kind";
        return false;
    }

    private void ExecuteTick()
    {
        long serverTick = world.NextTick();
        EnqueueAutoMoveActions(serverTick);
        ExplicitOutputPolicies.EnqueuePushOnEnterActions(world, actionQueue, serverTick, ruleSystem.HasRunningMovementClaim);
        IReadOnlyList<WorldAction> actions = actionQueue.DrainReady(serverTick);
        IReadOnlyList<WorldAction> lastInputActions = actions;
        BehaviorRuntimeTickResult result = ruleSystem.Tick(world, actions, serverTick);
        foreach (DeferredAction deferredAction in result.DeferredActions)
        {
            actionQueue.EnqueueDeferred(deferredAction);
        }

        for (int i = 0; i < 8 && ruleSystem.RunningBehaviorCount != 0 && result.ActionResults.Count == 0; i++)
        {
            serverTick = world.NextTick();
            actions = actionQueue.DrainReady(serverTick);
            result = ruleSystem.Tick(world, actions, serverTick);
            foreach (DeferredAction deferredAction in result.DeferredActions)
            {
                actionQueue.EnqueueDeferred(deferredAction);
            }
        }

        lastResult = ResolveLastResult(lastInputActions.Count == 0 ? actions : lastInputActions, result);
        world.FlushDelta();
    }

    private SandboxOperationResult ResolveLastResult(IReadOnlyList<WorldAction> actions, BehaviorRuntimeTickResult result)
    {
        if (actions.Count == 0)
        {
            string reason = result.Reasons.Count > 0 ? result.Reasons[0] : string.Empty;
            return new SandboxOperationResult(result.Reasons.Count == 0, reason, lastResult.EntityId);
        }

        WorldAction action = actions[actions.Count - 1];
        if (result.ActionResults.TryGetValue(action.ActionId, out MoveResult moveResult))
        {
            return new SandboxOperationResult(moveResult.Success, moveResult.Reason, moveResult.EntityId);
        }

        return new SandboxOperationResult(false, "action not resolved", action.EntityId);
    }

    private void EnqueueAutoMoveActions(long serverTick)
    {
        IReadOnlyList<AutoMoveQueryResult> entities = world.QueryAutoMove(EntityIterationOrder.EntityId);
        for (int i = 0; i < entities.Count; i++)
        {
            AutoMoveQueryResult entity = entities[i];

            if (serverTick - entity.AutoMove.LastMoveTick >= entity.AutoMove.IntervalTicks)
            {
                actionQueue.EnqueueAutoMove(entity.EntityId, serverTick - 1, 1);
            }
        }
    }

    private long ResolveEntityId(string alias, long entityId)
    {
        if (entityId > 0)
        {
            return entityId;
        }

        return aliases.TryGetValue(alias, out long resolved) ? resolved : 0;
    }

    private long AllocateEntityId()
    {
        while (world.TryGetEntity(nextEntityId, out _))
        {
            nextEntityId++;
        }

        return nextEntityId++;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();
    }
}
}
