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
        bool hasBefore = TryGetCoord(request.EntityId, out GridCoord beforeCoord);
        Direction direction = ResolveDirection(beforeCoord, new GridCoord(request.TargetX, request.TargetY));
        InputIntent intent = InputIntent.PlayerMove(
            request.EntityId,
            direction,
            request.BeatTick,
            request.ClientInputId,
            request.ClientTick,
            AuthoritativeMoveWorldProvider.World.ServerTick);
        var authorization = new PlayerIntentAuthorization<Session>(AuthoritativeMoveWorldProvider.Players);
        InputIntentAuthorizationResult authorizationResult = authorization.Authorize(session, intent, out long boundEntityId);
        if (!authorizationResult.Accepted)
        {
            GridCoord boundCoord = default;
            bool hasBoundCoord = TryGetCoord(boundEntityId, out boundCoord);
            response.Success = false;
            response.EntityId = request.EntityId;
            response.FinalX = boundCoord.X;
            response.FinalY = boundCoord.Y;
            response.MoveErrorCode = (int)authorizationResult.ErrorCode;
            response.Reason = authorizationResult.Reason;
            response.ClientTick = request.ClientTick;
            response.ClientInputId = request.ClientInputId;
            response.BeatTick = request.BeatTick;
            response.InputStatus = (int)AuthoritativePlayerInputStatus.Rejected;
            response.Direction = (int)Direction.None;
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

        Log.Info(
            "[C2G_MoveRequestHandler] request entity:{0} target:({1},{2}) beat:{3} direction:{4} input:{5} clientTick:{6} hasBefore:{7} before:({8},{9})",
            request.EntityId,
            request.TargetX,
            request.TargetY,
            request.BeatTick,
            direction,
            request.ClientInputId,
            request.ClientTick,
            hasBefore,
            beforeCoord.X,
            beforeCoord.Y);

        AuthoritativeMoveInput input = AuthoritativeMoveWorldProvider.InputQueue.EnqueueIntent(
            intent,
            AuthoritativeMoveWorldProvider.World.ServerTick);
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
        response.ClientInputId = request.ClientInputId;
        response.BeatTick = input.BeatTick;
        response.InputStatus = (int)input.Status;
        response.Direction = (int)input.Direction;

        Log.Info(
            "[C2G_MoveRequestHandler] response entity:{0} status:{1} beat:{2} direction:{3} success:{4} final:({5},{6}) moveErrorCode:{7} reason:{8} clientTick:{9} hasAfter:{10} after:({11},{12})",
            response.EntityId,
            input.Status,
            input.BeatTick,
            input.Direction,
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

    private static Direction ResolveDirection(GridCoord current, GridCoord target)
    {
        int dx = target.X - current.X;
        int dy = target.Y - current.Y;
        if (dx == -1 && dy == 0)
        {
            return Direction.Left;
        }

        if (dx == 1 && dy == 0)
        {
            return Direction.Right;
        }

        if (dx == 0 && dy == 1)
        {
            return Direction.Up;
        }

        if (dx == 0 && dy == -1)
        {
            return Direction.Down;
        }

        return Direction.None;
    }
}
