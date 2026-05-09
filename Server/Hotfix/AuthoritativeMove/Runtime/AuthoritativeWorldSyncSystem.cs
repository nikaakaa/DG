using Fantasy.Network;
using DG.GameCore;

namespace Fantasy;

public sealed class AuthoritativeWorldSyncSystem
{
    private readonly GameWorld World;

    public AuthoritativeWorldSyncSystem(GameWorld world)
    {
        World = world;
    }

    public void SendSnapshot(Session session)
    {
        IReadOnlyList<EntitySnapshot> snapshots = World.CreateSnapshot();
        session.Send(CreateSnapshotNotify(snapshots, World.ServerTick));
        Log.Info(
            "[AuthoritativeWorldSyncSystem] snapshot serverTick:{0} entities:{1}",
            World.ServerTick,
            snapshots.Count);
    }

    public void BroadcastSnapshot(IReadOnlyList<Session> observers)
    {
        if (observers.Count == 0)
        {
            return;
        }

        IReadOnlyList<EntitySnapshot> snapshots = World.CreateSnapshot();
        for (int i = 0; i < observers.Count; i++)
        {
            observers[i].Send(CreateSnapshotNotify(snapshots, World.ServerTick));
        }

        Log.Info(
            "[AuthoritativeWorldSyncSystem] broadcast snapshot serverTick:{0} entities:{1} observers:{2}",
            World.ServerTick,
            snapshots.Count,
            observers.Count);
    }

    public WorldDelta BroadcastDelta(IReadOnlyList<Session> observers)
    {
        WorldDelta delta = World.FlushDelta();
        if (delta.ChangedEntities.Count == 0 && delta.RemovedEntityIds.Count == 0)
        {
            return delta;
        }

        if (observers.Count == 0)
        {
            Info(
                "[AuthoritativeWorldSyncSystem] skip delta serverTick:{0} entities:{1} removed:{2} observers:0",
                delta.ServerTick,
                delta.ChangedEntities.Count,
                delta.RemovedEntityIds.Count);
            return delta;
        }

        for (int i = 0; i < observers.Count; i++)
        {
            observers[i].Send(CreateDeltaNotify(delta));
        }

        Info(
            "[AuthoritativeWorldSyncSystem] broadcast delta serverTick:{0} entities:{1} removed:{2} observers:{3}",
            delta.ServerTick,
            delta.ChangedEntities.Count,
            delta.RemovedEntityIds.Count,
            observers.Count);
        return delta;
    }

    private static G2C_WorldSnapshotNotify CreateSnapshotNotify(IReadOnlyList<EntitySnapshot> snapshots, long serverTick)
    {
        var notify = new G2C_WorldSnapshotNotify
        {
            ServerTick = serverTick
        };
        for (int i = 0; i < snapshots.Count; i++)
        {
            notify.Entities.Add(CreateState(snapshots[i]));
        }

        return notify;
    }

    private static G2C_WorldDeltaNotify CreateDeltaNotify(WorldDelta delta)
    {
        var notify = new G2C_WorldDeltaNotify
        {
            ServerTick = delta.ServerTick
        };
        for (int i = 0; i < delta.ChangedEntities.Count; i++)
        {
            notify.Entities.Add(CreateState(delta.ChangedEntities[i]));
        }
        for (int i = 0; i < delta.RemovedEntityIds.Count; i++)
        {
            notify.RemovedEntityIds.Add(delta.RemovedEntityIds[i]);
        }

        return notify;
    }

    private static G2C_WorldEntityState CreateState(EntitySnapshot snapshot)
    {
        return new G2C_WorldEntityState
        {
            EntityId = snapshot.EntityId,
            ConfigId = snapshot.ConfigId,
            ArchetypeId = snapshot.ArchetypeId,
            EntityTarget = snapshot.EntityTarget,
            X = snapshot.X,
            Y = snapshot.Y,
            Direction = (int)snapshot.Direction,
            HasCollider = snapshot.HasCollider,
            Blocking = snapshot.Blocking,
            Bouncable = snapshot.Bouncable,
            AutoMove = snapshot.AutoMove,
            PlayerControlled = snapshot.PlayerControlled,
            Pushable = snapshot.Pushable,
            PortLocalPorts = (int)snapshot.PortLocalPorts,
            HasMovementPermission = snapshot.HasMovementPermission,
            CanMove = snapshot.CanMove,
            CanBePushed = snapshot.CanBePushed,
            AutoMoveIntervalTicks = snapshot.AutoMoveIntervalTicks
        };
    }

    private static void Info(string message, params object[] args)
    {
        try
        {
            Log.Info(message, args);
        }
        catch (NullReferenceException)
        {
        }
    }
}
