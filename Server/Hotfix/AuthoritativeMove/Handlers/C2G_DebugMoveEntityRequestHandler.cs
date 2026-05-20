using System;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_DebugMoveEntityRequestHandler : MessageRPC<C2G_DebugMoveEntityRequest, G2C_DebugMoveEntityResponse>
{
    protected override async FTask Run(Session session, C2G_DebugMoveEntityRequest request, G2C_DebugMoveEntityResponse response, Action reply)
    {
        AuthoritativeDebugActionInput input = AuthoritativeMoveWorldProvider.DebugEdit.EnqueueMove(
            request.EntityId,
            new GridCoord(request.TargetX, request.TargetY));
        MoveResult result = await input.WaitAsync();

        response.Success = result.Success;
        response.EntityId = request.EntityId;
        response.FinalX = result.FinalCoord.X;
        response.FinalY = result.FinalCoord.Y;
        response.Reason = result.Reason;

        Log.Info(
            "[C2G_DebugMoveEntityRequestHandler] success:{0} entity:{1} final:({2},{3}) reason:{4}",
            response.Success,
            response.EntityId,
            response.FinalX,
            response.FinalY,
            response.Reason);

        reply();

        await FTask.CompletedTask;
    }
}
