using Fantasy.Async;
using Fantasy.Network;
using DG.GameCore;

namespace Fantasy;

public sealed class AuthoritativeWorldTickRunner
{
    private readonly GameWorld World;
    private readonly AutoMoveSystem AutoMoveSystem;
    private readonly PushOnEnterSystem PushOnEnterSystem;
    private readonly AuthoritativeWorldSyncSystem SyncSystem;
    private readonly int tickIntervalMs;
    private FCancellationToken? cancellationToken;
    private bool running;

    public AuthoritativeWorldTickRunner(GameWorld world, AutoMoveSystem autoMoveSystem, PushOnEnterSystem pushOnEnterSystem, AuthoritativeWorldSyncSystem syncSystem, int tickIntervalMs)
    {
        World = world;
        AutoMoveSystem = autoMoveSystem;
        PushOnEnterSystem = pushOnEnterSystem;
        SyncSystem = syncSystem;
        this.tickIntervalMs = tickIntervalMs;
    }

    public bool Start(Scene scene, Func<IReadOnlyList<Session>> observerProvider)
    {
        if (running)
        {
            return false;
        }

        running = true;
        cancellationToken = FCancellationToken.ToKen;
        Run(scene, observerProvider, cancellationToken).Coroutine();
        return true;
    }

    public void Stop()
    {
        cancellationToken?.Cancel();
        cancellationToken?.Dispose();
        cancellationToken = null;
        running = false;
    }

    private async FTask Run(Scene scene, Func<IReadOnlyList<Session>> observerProvider, FCancellationToken token)
    {
        while (!token.IsCancel && running)
        {
            bool completed = await FTask.Wait(scene, tickIntervalMs, token);
            if (!completed)
            {
                break;
            }

            IReadOnlyList<DG.GameCore.MoveResult> results = AutoMoveSystem.Tick(World);
            IReadOnlyList<DG.GameCore.MoveResult> pushResults = PushOnEnterSystem.Tick(World);
            if (results.Count == 0 && pushResults.Count == 0)
            {
                continue;
            }

            IReadOnlyList<Session> observers = observerProvider();
            SyncSystem.BroadcastDelta(observers);
        }
    }
}
