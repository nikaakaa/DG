using System;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_DebugRemoveEntityRequestHandler : MessageRPC<C2G_DebugRemoveEntityRequest, G2C_DebugRemoveEntityResponse>
{
    protected override async FTask Run(Session session, C2G_DebugRemoveEntityRequest request, G2C_DebugRemoveEntityResponse response, Action reply)
    {
        bool success = AuthoritativeMoveWorldProvider.DebugEdit.Enabled;
        string reason = success ? string.Empty : "debug edit disabled";
        if (success)
        {
            AuthoritativeDebugActionInput input = AuthoritativeMoveWorldProvider.InputQueue.EnqueueDebugRemove(request.EntityId);
            MoveResult result = await input.WaitAsync();
            success = result.Success;
            reason = result.Reason;
        }

        response.Success = success;
        response.EntityId = request.EntityId;
        response.Reason = reason;

        Log.Info(
            "[C2G_DebugRemoveEntityRequestHandler] success:{0} entity:{1} reason:{2}",
            response.Success,
            response.EntityId,
            response.Reason);

        reply();

        await FTask.CompletedTask;
    }
}
