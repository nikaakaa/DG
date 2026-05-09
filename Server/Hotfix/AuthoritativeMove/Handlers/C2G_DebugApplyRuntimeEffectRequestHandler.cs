using System;
using System.Collections.Generic;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using DG.GameCore;

namespace Fantasy;

public sealed class C2G_DebugApplyRuntimeEffectRequestHandler : MessageRPC<C2G_DebugApplyRuntimeEffectRequest, G2C_DebugApplyRuntimeEffectResponse>
{
    protected override async FTask Run(Session session, C2G_DebugApplyRuntimeEffectRequest request, G2C_DebugApplyRuntimeEffectResponse response, Action reply)
    {
        RuntimeEffectKind kind = (RuntimeEffectKind)request.EffectKind;
        bool success = AuthoritativeMoveWorldProvider.DebugEdit.TryApplyRuntimeEffect(
            request.EntityId,
            kind,
            request.AutoMoveIntervalTicks,
            (DirectionMask)request.PortLocalPorts,
            request.ExpireTick,
            out RuntimeEffectId effectId,
            out string reason);

        response.Success = success;
        response.EntityId = request.EntityId;
        response.EffectKind = request.EffectKind;
        response.RuntimeEffectId = effectId.Value;
        response.Reason = reason;

        Log.Info(
            "[C2G_DebugApplyRuntimeEffectRequestHandler] success:{0} entity:{1} effect:{2} effectId:{3} reason:{4}",
            response.Success,
            response.EntityId,
            kind,
            response.RuntimeEffectId,
            response.Reason);

        reply();

        BroadcastDelta();
        await FTask.CompletedTask;
    }

    private static void BroadcastDelta()
    {
        IReadOnlyList<Session> observers = AuthoritativeMoveWorldProvider.Players.EnumerateAvailable(observer => !observer.IsDisposed);
        AuthoritativeMoveWorldProvider.SyncSystem.BroadcastDelta(observers);
    }
}
