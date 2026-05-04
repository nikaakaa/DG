using System;
using System.Collections.Generic;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace Fantasy;

public sealed class C2G_DebugRemoveEntityRequestHandler : MessageRPC<C2G_DebugRemoveEntityRequest, G2C_DebugRemoveEntityResponse>
{
    protected override async FTask Run(Session session, C2G_DebugRemoveEntityRequest request, G2C_DebugRemoveEntityResponse response, Action reply)
    {
        AuthoritativeMoveWorldProvider.NextServerTick();
        bool success = AuthoritativeMoveWorldProvider.DebugEdit.TryRemove(request.EntityId, out string reason);

        response.Success = success;
        response.EntityId = request.EntityId;
        response.Reason = reason;

        Log.Info(
            "[C2G_DebugRemoveEntityRequestHandler] success:{0} entity:{1} reason:{2}",
            response.Success,
            response.EntityId,
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
