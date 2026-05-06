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
        bool success = AuthoritativeMoveWorldProvider.DebugEdit.Enabled;
        string reason = success ? string.Empty : "debug edit disabled";
        GridCoord finalCoord = new(request.TargetX, request.TargetY);
        if (success)
        {
            AuthoritativeDebugActionInput input = AuthoritativeMoveWorldProvider.InputQueue.EnqueueDebugMove(
                request.EntityId,
                new GridCoord(request.TargetX, request.TargetY));
            MoveResult result = await input.WaitAsync();
            success = result.Success;
            finalCoord = result.FinalCoord;
            reason = result.Reason;
        }

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

        await FTask.CompletedTask;
    }
}
