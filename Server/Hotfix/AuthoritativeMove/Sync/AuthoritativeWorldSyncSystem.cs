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

    public WorldDelta LastDelta { get; private set; }
    public int LastDeltaObserverCount { get; private set; }
    public bool LastDeltaBroadcasted { get; private set; }
    public bool LastDeltaSkippedNoObservers { get; private set; }

    public void SendSnapshot(Session session)
    {
        IReadOnlyList<EntitySnapshot> snapshots = World.CreateSnapshot();
        AuthoritativeWorldProtocolSender.SendSnapshot(session, snapshots, World.ServerTick);
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
            AuthoritativeWorldProtocolSender.SendSnapshot(observers[i], snapshots, World.ServerTick);
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
        LastDelta = delta;
        LastDeltaObserverCount = observers.Count;
        LastDeltaBroadcasted = false;
        LastDeltaSkippedNoObservers = false;
        if (delta.ChangedEntities.Count == 0 && delta.RemovedEntityIds.Count == 0 && delta.PresentationFacts.Count == 0)
        {
            return delta;
        }

        if (observers.Count == 0)
        {
            LastDeltaSkippedNoObservers = true;
            Info(
                "[AuthoritativeWorldSyncSystem] skip delta serverTick:{0} entities:{1} removed:{2} observers:0",
                delta.ServerTick,
                delta.ChangedEntities.Count,
                delta.RemovedEntityIds.Count);
            return delta;
        }

        for (int i = 0; i < observers.Count; i++)
        {
            AuthoritativeWorldProtocolSender.SendDelta(observers[i], delta);
        }

        LastDeltaBroadcasted = true;
        Info(
            "[AuthoritativeWorldSyncSystem] broadcast delta serverTick:{0} entities:{1} removed:{2} observers:{3}",
            delta.ServerTick,
            delta.ChangedEntities.Count,
            delta.RemovedEntityIds.Count,
            observers.Count);
        return delta;
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

internal static class AuthoritativeWorldProtocolSender
{
    public static void SendSnapshot(Session session, IReadOnlyList<EntitySnapshot> snapshots, long serverTick)
    {
        session.Send(CreateSnapshotNotify(snapshots, serverTick));
    }

    public static void SendDelta(Session session, WorldDelta delta)
    {
        session.Send(CreateDeltaNotify(delta));
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
        for (int i = 0; i < delta.PresentationFacts.Count; i++)
        {
            notify.PresentationFacts.Add(CreatePresentationFact(delta.PresentationFacts[i]));
        }

        return notify;
    }

    private static G2C_PresentationFact CreatePresentationFact(PresentationFact presentationFact)
    {
        var message = new G2C_PresentationFact
        {
            FactId = presentationFact.FactId,
            ServerTick = presentationFact.ServerTick,
            FactType = (int)presentationFact.FactType,
            ResultKind = (int)presentationFact.ResultKind,
            SourceActionId = presentationFact.SourceActionId,
            ClientInputId = presentationFact.ClientInputId,
            SourceEntityId = presentationFact.SourceEntityId,
            FromX = presentationFact.From.X,
            FromY = presentationFact.From.Y,
            ToX = presentationFact.To.X,
            ToY = presentationFact.To.Y,
            Direction = (int)presentationFact.Direction,
            StartTick = presentationFact.StartTick,
            ContactTick = presentationFact.ContactTick,
            EndTick = presentationFact.EndTick,
            ContactProgress = presentationFact.ContactProgress,
            EffectiveCostTicks = presentationFact.EffectiveCostTicks,
            PivotEntityId = presentationFact.PivotEntityId,
            PivotX = presentationFact.PivotCoord.X,
            PivotY = presentationFact.PivotCoord.Y,
            RotateDirection = (int)presentationFact.RotateDirection
        };
        for (int i = 0; i < presentationFact.SubjectEntityIds.Count; i++)
        {
            message.SubjectEntityIds.Add(presentationFact.SubjectEntityIds[i]);
        }
        for (int i = 0; i < presentationFact.Members.Count; i++)
        {
            PresentationFactMember member = presentationFact.Members[i];
            message.Members.Add(new G2C_PresentationFactMember
            {
                EntityId = member.EntityId,
                FromX = member.From.X,
                FromY = member.From.Y,
                ToX = member.To.X,
                ToY = member.To.Y,
                FromDirection = (int)member.FromDirection,
                ToDirection = (int)member.ToDirection,
                FromPortLocalPorts = (int)member.FromPortLocalPorts,
                ToPortLocalPorts = (int)member.ToPortLocalPorts
            });
        }
        for (int i = 0; i < presentationFact.Impacts.Count; i++)
        {
            PresentationFactImpact impact = presentationFact.Impacts[i];
            message.Impacts.Add(new G2C_PresentationFactImpact
            {
                BlockerEntityId = impact.BlockerEntityId,
                ImpactMemberId = impact.ImpactMemberId,
                ImpactFromX = impact.ImpactFrom.X,
                ImpactFromY = impact.ImpactFrom.Y,
                ImpactToX = impact.ImpactTo.X,
                ImpactToY = impact.ImpactTo.Y,
                PushDirection = (int)impact.PushDirection
            });
        }

        return message;
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
            AutoMoveIntervalTicks = snapshot.AutoMoveIntervalTicks,
            RotatePivot = snapshot.RotatePivot
        };
    }

}
