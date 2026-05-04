using System;
using System.Collections.Generic;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_DebugMoveEntityRequestHandler : MessageRPC<C2G_DebugMoveEntityRequest, G2C_DebugMoveEntityResponse>
{
    protected override async FTask Run(Session session, C2G_DebugMoveEntityRequest request, G2C_DebugMoveEntityResponse response, Action reply)
    {
        AuthoritativeMoveWorldProvider.NextServerTick();
        bool success = AuthoritativeMoveWorldProvider.DebugEdit.TryMove(
            request.EntityId,
            new GridCoord(request.TargetX, request.TargetY),
            out GridCoord finalCoord,
            out string reason);

        response.Success = success;
        response.EntityId = request.EntityId;
        response.FinalX = finalCoord.X;
        response.FinalY = finalCoord.Y;
        response.Reason = reason;

        Log.Info(
            "[C2G_DebugMoveEntityRequestHandler] success:{0} entity:{1} final:({2},{3}) reason:{4}",
            response.Success,
            response.EntityId,
            response.FinalX,
            response.FinalY,
            response.Reason);

        reply();
        if (success)
        {
            BroadcastDelta();
        }

        await FTask.CompletedTask;
    }

    private static void BroadcastDelta()
    {
        IReadOnlyList<Session> observers = AuthoritativeMoveWorldProvider.Players.EnumerateAvailable(observer => !observer.IsDisposed);
        AuthoritativeMoveWorldProvider.SyncSystem.BroadcastDelta(observers);
    }
}
