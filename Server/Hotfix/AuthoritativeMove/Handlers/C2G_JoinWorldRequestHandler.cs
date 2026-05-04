using System;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace Fantasy;

public sealed class C2G_JoinWorldRequestHandler : MessageRPC<C2G_JoinWorldRequest, G2C_JoinWorldResponse>
{
    protected override async FTask Run(Session session, C2G_JoinWorldRequest request, G2C_JoinWorldResponse response, Action reply)
    {
        if (!AuthoritativeMoveWorldProvider.Players.TryJoin(session, out PlayerEntitySnapshot player, out bool created, out string reason))
        {
            response.Success = false;
            response.EntityId = 0;
            response.CurrentX = 0;
            response.CurrentY = 0;
            response.Reason = reason;
            Log.Info("[C2G_JoinWorldRequestHandler] rejected reason:{0}", reason);
            reply();
            await FTask.CompletedTask;
            return;
        }

        AuthoritativeMoveWorldProvider.Observers.RegisterObserver(session, player.EntityId);
        AuthoritativeMoveWorldProvider.Observers.RefreshOwner(player.EntityId, session);
        bool tickStarted = AuthoritativeMoveWorldProvider.TickRunner.Start(
            session.Scene,
            () => AuthoritativeMoveWorldProvider.Players.EnumerateAvailable(observer => !observer.IsDisposed));

        response.Success = true;
        response.EntityId = player.EntityId;
        response.CurrentX = player.Coord.X;
        response.CurrentY = player.Coord.Y;
        response.Reason = string.Empty;

        Log.Info(
            "[C2G_JoinWorldRequestHandler] joined entity:{0} coord:({1},{2}) created:{3} players:{4} tickStarted:{5}",
            response.EntityId,
            response.CurrentX,
            response.CurrentY,
            created,
            AuthoritativeMoveWorldProvider.Players.OnlineCount,
            tickStarted);

        reply();

        IReadOnlyList<Session> observers = AuthoritativeMoveWorldProvider.Players.EnumerateAvailable(observer => !observer.IsDisposed);
        AuthoritativeMoveWorldProvider.SyncSystem.BroadcastSnapshot(observers);

        await FTask.CompletedTask;
    }
}
