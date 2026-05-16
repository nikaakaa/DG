using System;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_DebugSpawnEntityRequestHandler : MessageRPC<C2G_DebugSpawnEntityRequest, G2C_DebugSpawnEntityResponse>
{
    protected override async FTask Run(Session session, C2G_DebugSpawnEntityRequest request, G2C_DebugSpawnEntityResponse response, Action reply)
    {
        long entityId = AuthoritativeMoveWorldProvider.DebugEdit.ResolveSpawnEntityId(request.EntityId);
        bool success = AuthoritativeMoveWorldProvider.DebugEdit.Enabled;
        string reason = success ? string.Empty : "debug edit disabled";
        if (success)
        {
            AuthoritativeDebugActionInput input = AuthoritativeMoveWorldProvider.InputQueue.EnqueueDebugSpawn(
                entityId,
                request.ConfigId,
                new GridCoord(request.X, request.Y),
                (Direction)request.Direction,
                request.PlayerId,
                request.AutoMoveIntervalTicks,
                request.RotatePivot);
            MoveResult result = await input.WaitAsync();
            success = result.Success;
            reason = result.Reason;
        }

        response.Success = success;
        response.EntityId = entityId;
        response.Reason = reason;

        Log.Info(
            "[C2G_DebugSpawnEntityRequestHandler] success:{0} entity:{1} config:{2} coord:({3},{4}) direction:{5} rotatePivot:{6} reason:{7}",
            response.Success,
            response.EntityId,
            request.ConfigId,
            request.X,
            request.Y,
            request.Direction,
            request.RotatePivot,
            response.Reason);

        reply();

        await FTask.CompletedTask;
    }
}
