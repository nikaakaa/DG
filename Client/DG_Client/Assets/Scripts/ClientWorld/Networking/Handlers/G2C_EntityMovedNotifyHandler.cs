using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using UnityEngine;

namespace DG.Map
{
    public sealed class G2C_EntityMovedNotifyHandler : Message<G2C_EntityMovedNotify>
    {
        protected override async FTask Run(Session session, G2C_EntityMovedNotify message)
        {
            bool applied = ClientMoveNetworkRuntime.ApplyMovedNotify(message.EntityId, message.FinalX, message.FinalY);
            if (!applied)
            {
                Debug.LogWarning($"[ClientMoveNotify] apply failed ClientMapEntity:{message.EntityId} final:({message.FinalX},{message.FinalY}) serverTick:{message.ServerTick} clientTick:{message.ClientTick}");
                await FTask.CompletedTask;
                return;
            }

            // Debug.Log($"[ClientMoveNotify] applied ClientMapEntity:{message.EntityId} final:({message.FinalX},{message.FinalY}) serverTick:{message.ServerTick} clientTick:{message.ClientTick}");
            await FTask.CompletedTask;
        }
    }
}


