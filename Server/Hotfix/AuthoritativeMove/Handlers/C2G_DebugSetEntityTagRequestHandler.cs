using System;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_DebugSetEntityTagRequestHandler : MessageRPC<C2G_DebugSetEntityTagRequest, G2C_DebugSetEntityTagResponse>
{
    protected override async FTask Run(Session session, C2G_DebugSetEntityTagRequest request, G2C_DebugSetEntityTagResponse response, Action reply)
    {
        WorldTag tag = (WorldTag)request.Tag;
        AuthoritativeDebugActionInput input = AuthoritativeMoveWorldProvider.DebugEdit.EnqueueSetTag(request.EntityId, tag, request.Enabled);
        MoveResult result = await input.WaitAsync();

        response.Success = result.Success;
        response.EntityId = request.EntityId;
        response.Tag = request.Tag;
        response.Enabled = request.Enabled;
        response.Reason = result.Reason;

        Log.Info(
            "[C2G_DebugSetEntityTagRequestHandler] success:{0} entity:{1} tag:{2} enabled:{3} reason:{4}",
            response.Success,
            response.EntityId,
            tag,
            response.Enabled,
            response.Reason);

        reply();

        await FTask.CompletedTask;
    }
}
