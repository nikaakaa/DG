using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;
using UnityEngine;

namespace DG.Map
{
    public sealed class G2C_WorldSnapshotNotifyHandler : Message<G2C_WorldSnapshotNotify>
    {
        protected override async FTask Run(Session session, G2C_WorldSnapshotNotify message)
        {
            bool applied = ClientMoveNetworkRuntime.ApplyWorldSnapshot(message.ServerTick, message.Entities);
            if (!applied)
            {
                Debug.LogWarning($"[ClientWorldSnapshot] apply failed serverTick:{message.ServerTick} entities:{message.Entities.Count}");
                await FTask.CompletedTask;
                return;
            }

            // Debug.Log($"[ClientWorldSnapshot] applied serverTick:{message.ServerTick} entities:{message.Entities.Count}");
            await FTask.CompletedTask;
        }
    }
}


