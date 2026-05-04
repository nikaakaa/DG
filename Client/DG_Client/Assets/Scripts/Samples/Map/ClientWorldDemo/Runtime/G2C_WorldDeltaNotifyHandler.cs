using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using UnityEngine;

namespace DG.Map
{
    public sealed class G2C_WorldDeltaNotifyHandler : Message<G2C_WorldDeltaNotify>
    {
        protected override async FTask Run(Session session, G2C_WorldDeltaNotify message)
        {
            bool applied = ClientMoveNetworkRuntime.ApplyWorldDelta(message.ServerTick, message.Entities, message.RemovedEntityIds);
            if (!applied)
            {
                Debug.LogWarning($"[ClientWorldDelta] apply failed serverTick:{message.ServerTick} entities:{message.Entities.Count} removed:{message.RemovedEntityIds.Count}");
                await FTask.CompletedTask;
                return;
            }

            Debug.Log($"[ClientWorldDelta] applied serverTick:{message.ServerTick} entities:{message.Entities.Count} removed:{message.RemovedEntityIds.Count}");
            await FTask.CompletedTask;
        }
    }
}


