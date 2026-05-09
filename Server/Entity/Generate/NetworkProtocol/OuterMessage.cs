using LightProto;
using System;
using MemoryPack;
using System.Collections.Generic;
using Fantasy;
using Fantasy.Pool;
using Fantasy.Network.Interface;
using Fantasy.Serialize;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8618
// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable RedundantTypeArgumentsOfMethod
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PreferConcreteValueOverDefault
// ReSharper disable RedundantNameQualifier
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable RedundantUsingDirective
namespace Fantasy
{
    [Serializable]
    [ProtoContract]
    public partial class C2G_TestMessage : AMessage, IMessage
    {
        public static C2G_TestMessage Create()
        {
            return MessageObjectPool<C2G_TestMessage>.Rent();
        }

        public void Dispose()
        {
            Tag = default;
            MessageObjectPool<C2G_TestMessage>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_TestMessage; } 
        [ProtoMember(1)]
        public string Tag { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_TestRequest : AMessage, IRequest
    {
        public static C2G_TestRequest Create()
        {
            return MessageObjectPool<C2G_TestRequest>.Rent();
        }

        public void Dispose()
        {
            Tag = default;
            MessageObjectPool<C2G_TestRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_TestRequest; } 
        [ProtoIgnore]
        public G2C_TestResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public string Tag { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_TestResponse : AMessage, IResponse
    {
        public static G2C_TestResponse Create()
        {
            return MessageObjectPool<G2C_TestResponse>.Rent();
        }

        public void Dispose()
        {
            Tag = default;
            MessageObjectPool<G2C_TestResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_TestResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public string Tag { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_JoinWorldRequest : AMessage, IRequest
    {
        public static C2G_JoinWorldRequest Create()
        {
            return MessageObjectPool<C2G_JoinWorldRequest>.Rent();
        }

        public void Dispose()
        {
            MessageObjectPool<C2G_JoinWorldRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_JoinWorldRequest; } 
        [ProtoIgnore]
        public G2C_JoinWorldResponse ResponseType { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_JoinWorldResponse : AMessage, IResponse
    {
        public static G2C_JoinWorldResponse Create()
        {
            return MessageObjectPool<G2C_JoinWorldResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            CurrentX = default;
            CurrentY = default;
            Reason = default;
            MessageObjectPool<G2C_JoinWorldResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_JoinWorldResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public int CurrentX { get; set; }
        [ProtoMember(5)]
        public int CurrentY { get; set; }
        [ProtoMember(6)]
        public string Reason { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_MoveRequest : AMessage, IRequest
    {
        public static C2G_MoveRequest Create()
        {
            return MessageObjectPool<C2G_MoveRequest>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            TargetX = default;
            TargetY = default;
            ClientTick = default;
            MessageObjectPool<C2G_MoveRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_MoveRequest; } 
        [ProtoIgnore]
        public G2C_MoveResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public int TargetX { get; set; }
        [ProtoMember(3)]
        public int TargetY { get; set; }
        [ProtoMember(4)]
        public long ClientTick { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_MoveResponse : AMessage, IResponse
    {
        public static G2C_MoveResponse Create()
        {
            return MessageObjectPool<G2C_MoveResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            FinalX = default;
            FinalY = default;
            MoveErrorCode = default;
            Reason = default;
            ClientTick = default;
            MessageObjectPool<G2C_MoveResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_MoveResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public int FinalX { get; set; }
        [ProtoMember(5)]
        public int FinalY { get; set; }
        [ProtoMember(6)]
        public int MoveErrorCode { get; set; }
        [ProtoMember(7)]
        public string Reason { get; set; }
        [ProtoMember(8)]
        public long ClientTick { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_RegisterMoveObserverRequest : AMessage, IRequest
    {
        public static C2G_RegisterMoveObserverRequest Create()
        {
            return MessageObjectPool<C2G_RegisterMoveObserverRequest>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            MessageObjectPool<C2G_RegisterMoveObserverRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_RegisterMoveObserverRequest; } 
        [ProtoIgnore]
        public G2C_RegisterMoveObserverResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public long EntityId { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_RegisterMoveObserverResponse : AMessage, IResponse
    {
        public static G2C_RegisterMoveObserverResponse Create()
        {
            return MessageObjectPool<G2C_RegisterMoveObserverResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            CurrentX = default;
            CurrentY = default;
            Reason = default;
            MessageObjectPool<G2C_RegisterMoveObserverResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_RegisterMoveObserverResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public int CurrentX { get; set; }
        [ProtoMember(5)]
        public int CurrentY { get; set; }
        [ProtoMember(6)]
        public string Reason { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_EntityMovedNotify : AMessage, IMessage
    {
        public static G2C_EntityMovedNotify Create()
        {
            return MessageObjectPool<G2C_EntityMovedNotify>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            FinalX = default;
            FinalY = default;
            ServerTick = default;
            ClientTick = default;
            MessageObjectPool<G2C_EntityMovedNotify>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_EntityMovedNotify; } 
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public int FinalX { get; set; }
        [ProtoMember(3)]
        public int FinalY { get; set; }
        [ProtoMember(4)]
        public long ServerTick { get; set; }
        [ProtoMember(5)]
        public long ClientTick { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_WorldEntityState : AMessage
    {
        public static G2C_WorldEntityState Create()
        {
            return MessageObjectPool<G2C_WorldEntityState>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            ConfigId = default;
            ArchetypeId = default;
            EntityTarget = default;
            X = default;
            Y = default;
            Direction = default;
            HasCollider = default;
            Blocking = default;
            Bouncable = default;
            AutoMove = default;
            PlayerControlled = default;
            Pushable = default;
            PortLocalPorts = default;
            HasMovementPermission = default;
            CanMove = default;
            CanBePushed = default;
            AutoMoveIntervalTicks = default;
            MessageObjectPool<G2C_WorldEntityState>.Return(this);
        }
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public int ConfigId { get; set; }
        [ProtoMember(3)]
        public int ArchetypeId { get; set; }
        [ProtoMember(4)]
        public int EntityTarget { get; set; }
        [ProtoMember(5)]
        public int X { get; set; }
        [ProtoMember(6)]
        public int Y { get; set; }
        [ProtoMember(7)]
        public int Direction { get; set; }
        [ProtoMember(8)]
        public bool HasCollider { get; set; }
        [ProtoMember(9)]
        public bool Blocking { get; set; }
        [ProtoMember(10)]
        public bool Bouncable { get; set; }
        [ProtoMember(11)]
        public bool AutoMove { get; set; }
        [ProtoMember(12)]
        public bool PlayerControlled { get; set; }
        [ProtoMember(13)]
        public bool Pushable { get; set; }
        [ProtoMember(14)]
        public int PortLocalPorts { get; set; }
        [ProtoMember(15)]
        public bool HasMovementPermission { get; set; }
        [ProtoMember(16)]
        public bool CanMove { get; set; }
        [ProtoMember(17)]
        public bool CanBePushed { get; set; }
        [ProtoMember(18)]
        public int AutoMoveIntervalTicks { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_WorldSnapshotNotify : AMessage, IMessage
    {
        public static G2C_WorldSnapshotNotify Create()
        {
            return MessageObjectPool<G2C_WorldSnapshotNotify>.Rent();
        }

        public void Dispose()
        {
            ServerTick = default;
            Entities.Clear();
            MessageObjectPool<G2C_WorldSnapshotNotify>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_WorldSnapshotNotify; } 
        [ProtoMember(1)]
        public long ServerTick { get; set; }
        [ProtoMember(2)]
        public List<G2C_WorldEntityState> Entities { get; set; } = new List<G2C_WorldEntityState>();
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_WorldDeltaNotify : AMessage, IMessage
    {
        public static G2C_WorldDeltaNotify Create()
        {
            return MessageObjectPool<G2C_WorldDeltaNotify>.Rent();
        }

        public void Dispose()
        {
            ServerTick = default;
            Entities.Clear();
            RemovedEntityIds.Clear();
            MessageObjectPool<G2C_WorldDeltaNotify>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_WorldDeltaNotify; } 
        [ProtoMember(1)]
        public long ServerTick { get; set; }
        [ProtoMember(2)]
        public List<G2C_WorldEntityState> Entities { get; set; } = new List<G2C_WorldEntityState>();
        [ProtoMember(3)]
        public List<long> RemovedEntityIds { get; set; } = new List<long>();
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_DebugSpawnEntityRequest : AMessage, IRequest
    {
        public static C2G_DebugSpawnEntityRequest Create()
        {
            return MessageObjectPool<C2G_DebugSpawnEntityRequest>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            ConfigId = default;
            X = default;
            Y = default;
            Direction = default;
            PlayerId = default;
            AutoMoveIntervalTicks = default;
            MessageObjectPool<C2G_DebugSpawnEntityRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_DebugSpawnEntityRequest; } 
        [ProtoIgnore]
        public G2C_DebugSpawnEntityResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public int ConfigId { get; set; }
        [ProtoMember(3)]
        public int X { get; set; }
        [ProtoMember(4)]
        public int Y { get; set; }
        [ProtoMember(5)]
        public int Direction { get; set; }
        [ProtoMember(6)]
        public long PlayerId { get; set; }
        [ProtoMember(7)]
        public int AutoMoveIntervalTicks { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_DebugSpawnEntityResponse : AMessage, IResponse
    {
        public static G2C_DebugSpawnEntityResponse Create()
        {
            return MessageObjectPool<G2C_DebugSpawnEntityResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            Reason = default;
            MessageObjectPool<G2C_DebugSpawnEntityResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_DebugSpawnEntityResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public string Reason { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_DebugMoveEntityRequest : AMessage, IRequest
    {
        public static C2G_DebugMoveEntityRequest Create()
        {
            return MessageObjectPool<C2G_DebugMoveEntityRequest>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            TargetX = default;
            TargetY = default;
            MessageObjectPool<C2G_DebugMoveEntityRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_DebugMoveEntityRequest; } 
        [ProtoIgnore]
        public G2C_DebugMoveEntityResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public int TargetX { get; set; }
        [ProtoMember(3)]
        public int TargetY { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_DebugMoveEntityResponse : AMessage, IResponse
    {
        public static G2C_DebugMoveEntityResponse Create()
        {
            return MessageObjectPool<G2C_DebugMoveEntityResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            FinalX = default;
            FinalY = default;
            Reason = default;
            MessageObjectPool<G2C_DebugMoveEntityResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_DebugMoveEntityResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public int FinalX { get; set; }
        [ProtoMember(5)]
        public int FinalY { get; set; }
        [ProtoMember(6)]
        public string Reason { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_DebugRemoveEntityRequest : AMessage, IRequest
    {
        public static C2G_DebugRemoveEntityRequest Create()
        {
            return MessageObjectPool<C2G_DebugRemoveEntityRequest>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            MessageObjectPool<C2G_DebugRemoveEntityRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_DebugRemoveEntityRequest; } 
        [ProtoIgnore]
        public G2C_DebugRemoveEntityResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public long EntityId { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_DebugRemoveEntityResponse : AMessage, IResponse
    {
        public static G2C_DebugRemoveEntityResponse Create()
        {
            return MessageObjectPool<G2C_DebugRemoveEntityResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            Reason = default;
            MessageObjectPool<G2C_DebugRemoveEntityResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_DebugRemoveEntityResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public string Reason { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_DebugSetEntityTagRequest : AMessage, IRequest
    {
        public static C2G_DebugSetEntityTagRequest Create()
        {
            return MessageObjectPool<C2G_DebugSetEntityTagRequest>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            Tag = default;
            Enabled = default;
            MessageObjectPool<C2G_DebugSetEntityTagRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_DebugSetEntityTagRequest; } 
        [ProtoIgnore]
        public G2C_DebugSetEntityTagResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public int Tag { get; set; }
        [ProtoMember(3)]
        public bool Enabled { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_DebugSetEntityTagResponse : AMessage, IResponse
    {
        public static G2C_DebugSetEntityTagResponse Create()
        {
            return MessageObjectPool<G2C_DebugSetEntityTagResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            Tag = default;
            Enabled = default;
            Reason = default;
            MessageObjectPool<G2C_DebugSetEntityTagResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_DebugSetEntityTagResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public int Tag { get; set; }
        [ProtoMember(5)]
        public bool Enabled { get; set; }
        [ProtoMember(6)]
        public string Reason { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_DebugApplyRuntimeEffectRequest : AMessage, IRequest
    {
        public static C2G_DebugApplyRuntimeEffectRequest Create()
        {
            return MessageObjectPool<C2G_DebugApplyRuntimeEffectRequest>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            EffectKind = default;
            AutoMoveIntervalTicks = default;
            PortLocalPorts = default;
            ExpireTick = default;
            MessageObjectPool<C2G_DebugApplyRuntimeEffectRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_DebugApplyRuntimeEffectRequest; } 
        [ProtoIgnore]
        public G2C_DebugApplyRuntimeEffectResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public int EffectKind { get; set; }
        [ProtoMember(3)]
        public int AutoMoveIntervalTicks { get; set; }
        [ProtoMember(4)]
        public int PortLocalPorts { get; set; }
        [ProtoMember(5)]
        public long ExpireTick { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_DebugApplyRuntimeEffectResponse : AMessage, IResponse
    {
        public static G2C_DebugApplyRuntimeEffectResponse Create()
        {
            return MessageObjectPool<G2C_DebugApplyRuntimeEffectResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            EffectKind = default;
            RuntimeEffectId = default;
            Reason = default;
            MessageObjectPool<G2C_DebugApplyRuntimeEffectResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_DebugApplyRuntimeEffectResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public int EffectKind { get; set; }
        [ProtoMember(5)]
        public long RuntimeEffectId { get; set; }
        [ProtoMember(6)]
        public string Reason { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class C2G_DebugRemoveRuntimeEffectRequest : AMessage, IRequest
    {
        public static C2G_DebugRemoveRuntimeEffectRequest Create()
        {
            return MessageObjectPool<C2G_DebugRemoveRuntimeEffectRequest>.Rent();
        }

        public void Dispose()
        {
            EntityId = default;
            EffectKind = default;
            RuntimeEffectId = default;
            MessageObjectPool<C2G_DebugRemoveRuntimeEffectRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_DebugRemoveRuntimeEffectRequest; } 
        [ProtoIgnore]
        public G2C_DebugRemoveRuntimeEffectResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public long EntityId { get; set; }
        [ProtoMember(2)]
        public int EffectKind { get; set; }
        [ProtoMember(3)]
        public long RuntimeEffectId { get; set; }
    }

    [Serializable]
    [ProtoContract]
    public partial class G2C_DebugRemoveRuntimeEffectResponse : AMessage, IResponse
    {
        public static G2C_DebugRemoveRuntimeEffectResponse Create()
        {
            return MessageObjectPool<G2C_DebugRemoveRuntimeEffectResponse>.Rent();
        }

        public void Dispose()
        {
            Success = default;
            EntityId = default;
            EffectKind = default;
            RuntimeEffectId = default;
            Reason = default;
            MessageObjectPool<G2C_DebugRemoveRuntimeEffectResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_DebugRemoveRuntimeEffectResponse; } 
        [ProtoMember(1)]
        public uint ErrorCode { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
        [ProtoMember(3)]
        public long EntityId { get; set; }
        [ProtoMember(4)]
        public int EffectKind { get; set; }
        [ProtoMember(5)]
        public long RuntimeEffectId { get; set; }
        [ProtoMember(6)]
        public string Reason { get; set; }
    }

}