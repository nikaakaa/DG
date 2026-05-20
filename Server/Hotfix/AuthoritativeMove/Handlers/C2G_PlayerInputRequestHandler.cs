using System;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_PlayerInputRequestHandler : MessageRPC<C2G_PlayerInputRequest, G2C_PlayerInputResponse>
{
    protected override async FTask Run(Session session, C2G_PlayerInputRequest request, G2C_PlayerInputResponse response, Action reply)
    {
        Direction direction = (Direction)request.Direction;
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
            TryGetCoord(boundEntityId, out boundCoord);
            FillResponse(response, request, false, boundCoord, authorizationResult.ErrorCode, authorizationResult.Reason, AuthoritativePlayerInputStatus.Rejected);
            reply();
            await FTask.CompletedTask;
            return;
        }

        AuthoritativeMoveInput input = AuthoritativeMoveWorldProvider.InputQueue.EnqueueIntent(
            intent,
            AuthoritativeMoveWorldProvider.World.ServerTick);

        MoveResult result = await input.WaitAsync();
        AuthoritativeMoveWorldProvider.Observers.RefreshOwner(request.EntityId, session);
        FillResponse(response, request, result.Success, result.FinalCoord, result.ErrorCode, result.Reason, input.Status);
        response.BeatTick = input.BeatTick;
        reply();
    }

    private static void FillResponse(G2C_PlayerInputResponse response, C2G_PlayerInputRequest request, bool success, GridCoord finalCoord, MoveErrorCode errorCode, string reason, AuthoritativePlayerInputStatus status)
    {
        response.Success = success;
        response.EntityId = request.EntityId;
        response.FinalX = finalCoord.X;
        response.FinalY = finalCoord.Y;
        response.MoveErrorCode = (int)errorCode;
        response.Reason = reason;
        response.ClientTick = request.ClientTick;
        response.ClientInputId = request.ClientInputId;
        response.BeatTick = request.BeatTick;
        response.InputStatus = (int)status;
        response.Direction = request.Direction;
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
