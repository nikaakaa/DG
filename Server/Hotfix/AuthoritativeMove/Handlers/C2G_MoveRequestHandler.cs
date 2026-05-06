using System;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_MoveRequestHandler : MessageRPC<C2G_MoveRequest, G2C_MoveResponse>
{
    protected override async FTask Run(Session session, C2G_MoveRequest request, G2C_MoveResponse response, Action reply)
    {
        if (!AuthoritativeMoveWorldProvider.Players.TryAuthorizeMove(session, request.EntityId, out long boundEntityId, out MoveErrorCode authErrorCode, out string authReason))
        {
            GridCoord boundCoord = default;
            bool hasBoundCoord = TryGetCoord(boundEntityId, out boundCoord);
            response.Success = false;
            response.EntityId = request.EntityId;
            response.FinalX = boundCoord.X;
            response.FinalY = boundCoord.Y;
            response.MoveErrorCode = (int)authErrorCode;
            response.Reason = authReason;
            response.ClientTick = request.ClientTick;
            Log.Info(
                "[C2G_MoveRequestHandler] rejected auth requestEntity:{0} boundEntity:{1} moveErrorCode:{2} reason:{3} hasBound:{4} bound:({5},{6}) clientTick:{7}",
                request.EntityId,
                boundEntityId,
                response.MoveErrorCode,
                response.Reason,
                hasBoundCoord,
                boundCoord.X,
                boundCoord.Y,
                request.ClientTick);
            reply();
            await FTask.CompletedTask;
            return;
        }

        bool hasBefore = TryGetCoord(request.EntityId, out GridCoord beforeCoord);

        Log.Info(
            "[C2G_MoveRequestHandler] request entity:{0} target:({1},{2}) clientTick:{3} hasBefore:{4} before:({5},{6})",
            request.EntityId,
            request.TargetX,
            request.TargetY,
            request.ClientTick,
            hasBefore,
            beforeCoord.X,
            beforeCoord.Y);

        AuthoritativeMoveInput input = AuthoritativeMoveWorldProvider.InputQueue.EnqueueMove(
            request.EntityId,
            new GridCoord(request.TargetX, request.TargetY),
            request.ClientTick);
        DG.GameCore.MoveResult result = await input.WaitAsync();
        bool hasAfter = TryGetCoord(request.EntityId, out GridCoord afterCoord);
        AuthoritativeMoveWorldProvider.Observers.RefreshOwner(request.EntityId, session);

        response.Success = result.Success;
        response.EntityId = request.EntityId;
        response.FinalX = result.FinalCoord.X;
        response.FinalY = result.FinalCoord.Y;
        response.MoveErrorCode = (int)result.ErrorCode;
        response.Reason = result.Reason;
        response.ClientTick = request.ClientTick;

        Log.Info(
            "[C2G_MoveRequestHandler] response entity:{0} success:{1} final:({2},{3}) moveErrorCode:{4} reason:{5} clientTick:{6} hasAfter:{7} after:({8},{9})",
            response.EntityId,
            response.Success,
            response.FinalX,
            response.FinalY,
            response.MoveErrorCode,
            response.Reason,
            response.ClientTick,
            hasAfter,
            afterCoord.X,
            afterCoord.Y);

        if (!response.Success)
        {
            reply();
            return;
        }

        reply();
    }

    private static bool TryGetCoord(long entityId, out GridCoord coord)
    {
        if (entityId != 0 &&
            AuthoritativeMoveWorldProvider.World.TryGetEntity(entityId, out GameEntity entity) &&
            AuthoritativeMoveWorldProvider.World.TryGetComponent(entity, out PositionComponent position))
        {
            coord = position.Coord;
            return true;
        }

        coord = default;
        return false;
    }
}
