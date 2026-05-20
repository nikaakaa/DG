using System;
using System.Collections.Generic;

namespace DG.GameCore
{
public sealed class BehaviorScheduledOutput
{
    public BehaviorScheduledOutput(long releaseTick, BehaviorStepOutput output)
    {
        ReleaseTick = releaseTick;
        Output = output ?? BehaviorStepOutput.Empty;
    }

    public long ReleaseTick { get; }
    public BehaviorStepOutput Output { get; }
}

public sealed class BehaviorInstanceRelease
{
    public BehaviorInstanceRelease(ActionBehaviorInstance instance, BehaviorStep step)
    {
        Instance = instance;
        Step = step;
    }

    public ActionBehaviorInstance Instance { get; }
    public BehaviorStep Step { get; }
    public BehaviorStepOutput Output => Step.Output;
    public bool Completion => Step.Result == BehaviorStepResult.Completed;
}

public sealed class BehaviorInstanceRunner
{
    private sealed class RunningBehavior
    {
        public RunningBehavior(ActionBehaviorInstance instance, BehaviorRunner runner)
        {
            Instance = instance;
            Runner = runner;
        }

        public ActionBehaviorInstance Instance { get; }
        public BehaviorRunner Runner { get; }
    }

    private readonly List<RunningBehavior> running = new();
    private readonly RunningBehaviorInstanceStore store = new();
    private readonly IBehaviorStateMachine stateMachine = new ActionBehaviorStateMachine();

    public int RunningInstanceCount => running.Count;
    public int RunningReservationCount => store.Count;

    public BehaviorStep Start(ActionBehaviorInstance instance, GameWorld world)
    {
        var runningBehavior = new RunningBehavior(instance, new BehaviorRunner(stateMachine));
        running.Add(runningBehavior);
        store.Add(instance);
        return runningBehavior.Runner.Step(instance, new BehaviorRunnerContext(world, instance.StartTick, BehaviorRunnerPhase.Enter));
    }

    public BehaviorStep RunTerminal(ActionBehaviorInstance instance, GameWorld world)
    {
        var runner = new BehaviorRunner(stateMachine);
        return runner.Step(instance, new BehaviorRunnerContext(world, instance.StartTick, BehaviorRunnerPhase.Enter));
    }

    public IReadOnlyList<BehaviorInstanceRelease> StepDue(GameWorld world, long serverTick)
    {
        var releases = new List<BehaviorInstanceRelease>();
        for (int index = running.Count - 1; index >= 0; index--)
        {
            RunningBehavior behavior = running[index];
            BehaviorStep tick = behavior.Runner.Step(behavior.Instance, new BehaviorRunnerContext(world, serverTick, BehaviorRunnerPhase.Tick));
            if (tick.Output != BehaviorStepOutput.Empty)
            {
                releases.Add(new BehaviorInstanceRelease(behavior.Instance, tick));
            }

            if (behavior.Instance.EndTick > serverTick)
            {
                continue;
            }

            running.RemoveAt(index);
            store.Release(behavior.Instance.InstanceId);
            BehaviorStep completion = behavior.Runner.Step(behavior.Instance, new BehaviorRunnerContext(world, serverTick, BehaviorRunnerPhase.Exit));
            releases.Add(new BehaviorInstanceRelease(behavior.Instance, completion));
        }

        return releases;
    }

    public bool TryGetRunningSubject(long entityId, long serverTick, out ActionBehaviorInstance instance)
    {
        return TryGetRunningSubject(entityId, BehaviorClaimChannel.Movement, serverTick, out instance);
    }

    public bool TryGetRunningSubject(long entityId, BehaviorClaimChannel channel, long serverTick, out ActionBehaviorInstance instance)
    {
        if (store.TryGetBySubject(entityId, channel, out instance) && instance.EndTick >= serverTick)
        {
            return true;
        }

        instance = null!;
        return false;
    }

    public bool TryGetRunningSubject(long entityId, long serverTick, long excludingSourceActionId, out ActionBehaviorInstance instance)
    {
        if (TryGetRunningSubject(entityId, serverTick, out instance) &&
            instance.SourceActionId != excludingSourceActionId)
        {
            return true;
        }

        instance = null!;
        return false;
    }

    public bool TryGetReservedCell(GridCoord cell, long serverTick, out ActionBehaviorInstance instance)
    {
        return TryGetReservedCell(cell, BehaviorClaimChannel.Movement, serverTick, out instance);
    }

    public bool TryGetReservedCell(GridCoord cell, BehaviorClaimChannel channel, long serverTick, out ActionBehaviorInstance instance)
    {
        if (store.TryGetByCell(cell, channel, out instance) && instance.EndTick >= serverTick)
        {
            return true;
        }

        instance = null!;
        return false;
    }

    public bool TryGetReservedCell(GridCoord cell, long serverTick, long excludingSourceActionId, out ActionBehaviorInstance instance)
    {
        if (TryGetReservedCell(cell, serverTick, out instance) &&
            instance.SourceActionId != excludingSourceActionId)
        {
            return true;
        }

        instance = null!;
        return false;
    }

    public bool TryGetReservedResource(ResourceKey resource, long serverTick, out ActionBehaviorInstance instance)
    {
        return TryGetReservedResource(resource, BehaviorClaimChannel.Movement, serverTick, out instance);
    }

    public bool TryGetReservedResource(ResourceKey resource, BehaviorClaimChannel channel, long serverTick, out ActionBehaviorInstance instance)
    {
        if (store.TryGetByResource(resource, channel, out instance) && instance.EndTick >= serverTick)
        {
            return true;
        }

        instance = null!;
        return false;
    }
}
}
