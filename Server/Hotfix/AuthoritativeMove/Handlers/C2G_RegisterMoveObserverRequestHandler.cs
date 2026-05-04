using System;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_RegisterMoveObserverRequestHandler : MessageRPC<C2G_RegisterMoveObserverRequest, G2C_RegisterMoveObserverResponse>
{
    protected override async FTask Run(Session session, C2G_RegisterMoveObserverRequest request, G2C_RegisterMoveObserverResponse response, Action reply)
    {
        AuthoritativeMoveWorldProvider.Observers.RegisterObserver(session, request.EntityId);
        AuthoritativeMoveWorldProvider.Observers.RefreshOwner(request.EntityId, session);

        response.Success = true;
        response.EntityId = request.EntityId;
        if (TryGetCoord(request.EntityId, out GridCoord coord))
        {
            response.CurrentX = coord.X;
            response.CurrentY = coord.Y;
            response.Reason = string.Empty;
        }
        else
        {
            response.Success = false;
            response.Reason = "unknown player";
        }

        Log.Info(
            "[C2G_RegisterMoveObserverRequestHandler] entity:{0} success:{1} coord:({2},{3}) observers:{4} reason:{5}",
            response.EntityId,
            response.Success,
            response.CurrentX,
            response.CurrentY,
            AuthoritativeMoveWorldProvider.Observers.ObserverCount,
            response.Reason);

        bool success = response.Success;
        reply();
        if (!success)
        {
            await FTask.CompletedTask;
            return;
        }

        IReadOnlyList<Session> observers = AuthoritativeMoveWorldProvider.Observers.EnumerateAvailable(observer => !observer.IsDisposed);
        AuthoritativeMoveWorldProvider.SyncSystem.BroadcastSnapshot(observers);

        await FTask.CompletedTask;
    }

    private static bool TryGetCoord(long entityId, out GridCoord coord)
    {
        if (AuthoritativeMoveWorldProvider.World.TryGetEntity(entityId, out DG.GameCore.GameEntity entity) &&
            AuthoritativeMoveWorldProvider.World.TryGetComponent(entity, out PositionComponent position))
        {
            coord = position.Coord;
            return true;
        }

        coord = default;
        return false;
    }
}
