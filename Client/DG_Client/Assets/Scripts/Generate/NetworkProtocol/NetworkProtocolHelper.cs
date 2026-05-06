using System.Runtime.CompilerServices;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using System.Collections.Generic;
#pragma warning disable CS8618
namespace Fantasy
{
   public static class NetworkProtocolHelper
   {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestMessage(this Session session, C2G_TestMessage message)
		{
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestMessage(this Session session, string tag)
		{
			using var message = Fantasy.C2G_TestMessage.Create();
			message.Tag = tag;
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TestResponse> C2G_TestRequest(this Session session, C2G_TestRequest request)
		{
			return (G2C_TestResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TestResponse> C2G_TestRequest(this Session session, string tag)
		{
			using var request = Fantasy.C2G_TestRequest.Create();
			request.Tag = tag;
			return (G2C_TestResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_JoinWorldResponse> C2G_JoinWorldRequest(this Session session, C2G_JoinWorldRequest request)
		{
			return (G2C_JoinWorldResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_JoinWorldResponse> C2G_JoinWorldRequest(this Session session)
		{
			using var request = Fantasy.C2G_JoinWorldRequest.Create();
			return (G2C_JoinWorldResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_MoveResponse> C2G_MoveRequest(this Session session, C2G_MoveRequest request)
		{
			return (G2C_MoveResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_MoveResponse> C2G_MoveRequest(this Session session, long entityId, int targetX, int targetY, long clientTick)
		{
			using var request = Fantasy.C2G_MoveRequest.Create();
			request.EntityId = entityId;
			request.TargetX = targetX;
			request.TargetY = targetY;
			request.ClientTick = clientTick;
			return (G2C_MoveResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RegisterMoveObserverResponse> C2G_RegisterMoveObserverRequest(this Session session, C2G_RegisterMoveObserverRequest request)
		{
			return (G2C_RegisterMoveObserverResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_RegisterMoveObserverResponse> C2G_RegisterMoveObserverRequest(this Session session, long entityId)
		{
			using var request = Fantasy.C2G_RegisterMoveObserverRequest.Create();
			request.EntityId = entityId;
			return (G2C_RegisterMoveObserverResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_EntityMovedNotify(this Session session, G2C_EntityMovedNotify message)
		{
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_EntityMovedNotify(this Session session, long entityId, int finalX, int finalY, long serverTick, long clientTick)
		{
			using var message = Fantasy.G2C_EntityMovedNotify.Create();
			message.EntityId = entityId;
			message.FinalX = finalX;
			message.FinalY = finalY;
			message.ServerTick = serverTick;
			message.ClientTick = clientTick;
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_WorldSnapshotNotify(this Session session, G2C_WorldSnapshotNotify message)
		{
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_WorldSnapshotNotify(this Session session, long serverTick, List<G2C_WorldEntityState> entities)
		{
			using var message = Fantasy.G2C_WorldSnapshotNotify.Create();
			message.ServerTick = serverTick;
			message.Entities = entities;
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_WorldDeltaNotify(this Session session, G2C_WorldDeltaNotify message)
		{
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_WorldDeltaNotify(this Session session, long serverTick, List<G2C_WorldEntityState> entities, List<long> removedEntityIds)
		{
			using var message = Fantasy.G2C_WorldDeltaNotify.Create();
			message.ServerTick = serverTick;
			message.Entities = entities;
			message.RemovedEntityIds = removedEntityIds;
			session.Send(message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DebugSpawnEntityResponse> C2G_DebugSpawnEntityRequest(this Session session, C2G_DebugSpawnEntityRequest request)
		{
			return (G2C_DebugSpawnEntityResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DebugSpawnEntityResponse> C2G_DebugSpawnEntityRequest(this Session session, long entityId, int configId, int x, int y, int direction, long playerId, int autoMoveIntervalTicks)
		{
			using var request = Fantasy.C2G_DebugSpawnEntityRequest.Create();
			request.EntityId = entityId;
			request.ConfigId = configId;
			request.X = x;
			request.Y = y;
			request.Direction = direction;
			request.PlayerId = playerId;
			request.AutoMoveIntervalTicks = autoMoveIntervalTicks;
			return (G2C_DebugSpawnEntityResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DebugMoveEntityResponse> C2G_DebugMoveEntityRequest(this Session session, C2G_DebugMoveEntityRequest request)
		{
			return (G2C_DebugMoveEntityResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DebugMoveEntityResponse> C2G_DebugMoveEntityRequest(this Session session, long entityId, int targetX, int targetY)
		{
			using var request = Fantasy.C2G_DebugMoveEntityRequest.Create();
			request.EntityId = entityId;
			request.TargetX = targetX;
			request.TargetY = targetY;
			return (G2C_DebugMoveEntityResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DebugRemoveEntityResponse> C2G_DebugRemoveEntityRequest(this Session session, C2G_DebugRemoveEntityRequest request)
		{
			return (G2C_DebugRemoveEntityResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DebugRemoveEntityResponse> C2G_DebugRemoveEntityRequest(this Session session, long entityId)
		{
			using var request = Fantasy.C2G_DebugRemoveEntityRequest.Create();
			request.EntityId = entityId;
			return (G2C_DebugRemoveEntityResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DebugSetEntityTagResponse> C2G_DebugSetEntityTagRequest(this Session session, C2G_DebugSetEntityTagRequest request)
		{
			return (G2C_DebugSetEntityTagResponse)await session.Call(request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_DebugSetEntityTagResponse> C2G_DebugSetEntityTagRequest(this Session session, long entityId, int tag, bool enabled)
		{
			using var request = Fantasy.C2G_DebugSetEntityTagRequest.Create();
			request.EntityId = entityId;
			request.Tag = tag;
			request.Enabled = enabled;
			return (G2C_DebugSetEntityTagResponse)await session.Call(request);
		}

   }
}