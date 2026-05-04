# Minestom 风格网络同步重构路径

## 目标

把当前 DG 的网络同步从“移动请求后全员广播 WorldDelta”的最小切片，重构为接近 Ref/Minestom 的服务端权威同步骨架。

最终目标数据流：

```text
Client Input
  -> C2G Intent
  -> Server Input Queue
  -> Authoritative Tick
  -> GameWorld / Components / Movement
  -> DirtyTracker
  -> VisibilityTracker
  -> SyncManager
  -> Per-session Packet Buffer
  -> Flush
  -> Client Visible Mirror
  -> Unity View Registry
```

## 当前状态判断

当前 DG 已经具备这些基础：

- `Shared/DG.GameCore` 有 `GameWorld`、`GameEntity`、`ComponentStore`、`SpatialEntityIndex`、`DirtyChange`、`WorldDelta`、`EntitySnapshot`。
- 服务端已有 `AuthoritativeMoveWorldProvider`、`C2G_MoveRequestHandler`、`AuthoritativeWorldSyncSystem`。
- 客户端已有 `ClientMapWorld` 作为本地镜像 facade，`ClientMapEntity` 只作为展示 handle。
- 当前移动链路已经是服务端权威，客户端只应用服务端结果。

当前主要问题：

- 同步仍是粗粒度广播，不是按玩家视野发送。
- `MoveObserverRegistry` 只是注册表，不是 Minestom 式 `EntityTracker`。
- Handler 仍在请求内直接触发移动和广播，没有统一 tick flush 边界。
- 协议还没有完整的 spawn / despawn / move / state delta 生命周期。
- 客户端没有明确的 visible mirror 生命周期，实体离开视野时的销毁边界还不完整。

## Ref 到 DG 的映射

```text
Minestom ServerProcessImpl.tick
  -> DG AuthoritativeServerLoop / AuthoritativeWorldTickRunner

Minestom Instance
  -> DG AuthoritativeWorld / GameWorld

Minestom Entity
  -> DG GameEntity + ComponentStore

Minestom EntityTracker
  -> DG SpatialEntityIndex + VisibilityTracker

Minestom Viewable
  -> DG ViewableEntity / ViewableChunk / VisibleSession

Minestom EntityView
  -> DG EntityViewSyncState / PlayerVisibilityState

Minestom PacketViewableUtils.prepareViewablePacket / flush
  -> DG SyncPacketBuffer / SyncFlushSystem

Minestom SpawnEntityPacket / DestroyEntitiesPacket
  -> DG G2C_EntitySpawnNotify / G2C_EntityDespawnNotify

Minestom EntityPositionPacket / EntityTeleportPacket
  -> DG G2C_EntityMoveDeltaNotify / G2C_EntityTeleportNotify

Minestom EntityMetaDataPacket
  -> DG G2C_EntityStateDeltaNotify

Minestom ChunkDataPacket / UnloadChunkPacket
  -> DG G2C_ChunkEnterNotify / G2C_ChunkLeaveNotify / G2C_CellBatchNotify
```

借鉴重点：

- 运行时骨架。
- 世界 / 实体 / 空间索引 / 同步管理器边界。
- Viewable / viewer 关系。
- Tick 末统一 flush。
- 进入视野、离开视野、视野内增量三个同步阶段。

不复制的内容：

- Minecraft 协议本身。
- 3D chunk section / palette 细节。
- Minecraft 实体继承树。
- Acquirable 线程模型。
- 具体 Java 包结构。

## 阶段 1：服务端 Tick / Flush 骨架

### 目标

把当前“请求来了立即改世界并广播”的模式，改成“请求只提交意图，服务端 tick 统一处理世界和网络 flush”。

当前模式：

```text
C2G_MoveRequestHandler
  -> MovementResolveSystem.Resolve
  -> BroadcastDelta
```

目标模式：

```text
C2G_MoveRequestHandler
  -> 校验 session / entity ownership
  -> 写入 AuthoritativeInputQueue
  -> 返回接收或等待 tick 结果

AuthoritativeWorldTickRunner.Tick
  -> 消费 input queue
  -> 运行 MovementResolveSystem
  -> 运行世界系统
  -> 收集 dirty
  -> AuthoritativeSyncManager.Flush
```

### 文件建议

```text
Server/Hotfix/AuthoritativeMove/Runtime/
  AuthoritativeServerLoop.cs
  AuthoritativeWorldTickRunner.cs
  AuthoritativeInputQueue.cs
  AuthoritativeSyncFlushSystem.cs
```

### 细分任务

- 1.1 定义 `AuthoritativeInputQueue`，先只支持 move intent。
- 1.2 `C2G_MoveRequestHandler` 只做校验和入队，不直接广播。
- 1.3 `AuthoritativeWorldTickRunner` 每 tick 消费输入。
- 1.4 `MovementResolveSystem.Resolve` 只在 tick 内执行。
- 1.5 tick 末调用同步系统 flush。
- 1.6 保留现有请求回包语义，避免客户端立刻断。

### 验证

- 服务端测试：move request 入队后，tick 前世界不变，tick 后世界变化。
- 服务端测试：失败移动不会产生 dirty delta。
- 手动测试：单客户端移动仍能收到服务端最终位置。

## 阶段 2：EntityTracker / VisibilityTracker

### 目标

服务端为每个玩家维护可见实体集合，移动后只给可见玩家发送同步。

核心状态：

```text
PlayerVisibilityState
  Session
  PlayerEntityId
  CenterCoord
  VisibleEntityIds
  VisibleChunkKeys
```

每次刷新：

```text
oldVisible = state.VisibleEntityIds
newVisible = QueryVisibleEntities(center, range)

entered = newVisible - oldVisible
left = oldVisible - newVisible
stayed = oldVisible ∩ newVisible
```

### 文件建议

```text
Server/Hotfix/AuthoritativeMove/Sync/
  VisibilityTracker.cs
  PlayerVisibilityState.cs
  VisibilityDiff.cs
```

### Shared 支撑

优先复用：

```text
Shared/DG.GameCore/Spatial/SpatialEntityIndex.cs
Shared/DG.GameCore/Spatial/SpatialVisibilityDelta.cs
Shared/DG.GameCore/World/GameWorld.cs
```

需要确认的 API：

- `GameWorld.GetEntitiesAround(...)`
- `GameWorld.DiffVisibility(...)`
- `EntityTarget` 分区查询是否保留实体类型。

### 细分任务

- 2.1 新增 `PlayerVisibilityState`。
- 2.2 新增 `VisibilityDiff`。
- 2.3 新增 `VisibilityTracker.RefreshPlayerVisibility(...)`。
- 2.4 JoinWorld 后初始化该玩家 visible set。
- 2.5 玩家移动后刷新该玩家 visible set。
- 2.6 其他实体移动后，找出受影响玩家。
- 2.7 暂时使用固定 view range，后续再做配置。

### 验证

- 服务端测试：A 和 B 在范围内，A 的 visible set 包含 B。
- 服务端测试：B 移出范围，A 的 diff.left 包含 B。
- 服务端测试：C 不在范围内，C 不收到 B 的变化。

## 阶段 3：协议拆分

### 目标

从粗粒度 `WorldDeltaNotify` 走向实体生命周期同步。

短期过渡协议：

```protobuf
message G2C_VisibilitySyncNotify {
  int64 ServerTick = 1;
  repeated G2C_WorldEntityState EnteredEntities = 2;
  repeated int64 LeftEntityIds = 3;
  repeated G2C_WorldEntityState ChangedEntities = 4;
}
```

长期协议：

```text
G2C_EntitySpawnNotify
G2C_EntityDespawnNotify
G2C_EntityMoveDeltaNotify
G2C_EntityTeleportNotify
G2C_EntityStateDeltaNotify
G2C_ChunkEnterNotify
G2C_ChunkLeaveNotify
G2C_CellBatchNotify
```

### 文件

```text
Tools/NetworkProtocol/Outer/OuterMessage.proto
Tools/ProtocolExportTool/ExporterSettings.json
Client/DG_Client/Assets/Scripts/Generate/NetworkProtocol/
Server/Entity/Generate/NetworkProtocol/
```

### 细分任务

- 3.1 先增加 `G2C_VisibilitySyncNotify`。
- 3.2 复用现有 `G2C_WorldEntityState`。
- 3.3 跑协议导出。
- 3.4 确认客户端生成代码存在。
- 3.5 确认服务端生成代码存在。
- 3.6 后续再拆 spawn/despawn/move/state delta 独立包。

### 验证

- 协议导出成功。
- Server/Entity 编译成功。
- Client 生成协议编译成功。

## 阶段 4：AuthoritativeSyncManager

### 目标

把所有同步发送集中到一个管理器中，Handler 和世界系统不直接发同步包。

### 文件建议

```text
Server/Hotfix/AuthoritativeMove/Sync/
  AuthoritativeSyncManager.cs
  EntitySyncPacketBuilder.cs
  ChunkSyncPacketBuilder.cs
  SyncPacketBuffer.cs
```

### 职责

```text
AuthoritativeSyncManager
  RegisterPlayer(session, entityId)
  RemovePlayer(session)
  MarkDirty(entityId)
  RefreshVisibility(session)
  Flush()

EntitySyncPacketBuilder
  BuildEnteredEntity(snapshot)
  BuildLeftEntity(entityId)
  BuildChangedEntity(snapshot)
  BuildSpawn(snapshot)
  BuildDespawn(entityId)
  BuildMoveDelta(snapshot, previous)
  BuildTeleport(snapshot)
  BuildStateDelta(snapshot, dirtyMask)

SyncPacketBuffer
  Queue(session, message)
  Flush()
```

### 细分任务

- 4.1 `AuthoritativeMoveWorldProvider` 持有 `AuthoritativeSyncManager`。
- 4.2 JoinWorld 注册 player visibility。
- 4.3 Move 成功后只 mark dirty，不直接 broadcast。
- 4.4 tick 末 `Flush()`。
- 4.5 `Flush()` 对每个玩家分别计算 entered / left / changed。
- 4.6 只给对应 session 发送对应包。
- 4.7 移除或降级 `AuthoritativeWorldSyncSystem.BroadcastDelta`。

### 验证

- A/B/C 三客户端或三 session 模拟测试。
- B 移动时，只有看得见 B 的玩家收到 changed。
- B 离开 A 视野，A 收到 left。
- B 回到 A 视野，A 收到 entered。

## 阶段 5：客户端 Visible Mirror

### 目标

客户端不再只是“把所有收到的 snapshot 塞进本地 GameWorld”，而是维护服务端授予的可见实体镜像。

客户端处理：

```text
EnteredEntities
  -> ClientMapWorld.ApplySnapshot
  -> ClientEntityViewRegistry.CreateOrUpdate

LeftEntityIds
  -> ClientMapWorld.RemoveEntity
  -> ClientEntityViewRegistry.Destroy

ChangedEntities
  -> ClientMapWorld.ApplySnapshot
  -> ClientEntityViewRegistry.Update
```

### 文件建议

```text
Client/DG_Client/Assets/Scripts/Map/Network/
  G2C_VisibilitySyncNotifyHandler.cs

Client/DG_Client/Assets/Scripts/Map/View/
  ClientMapEntity.cs
  ClientEntityViewRegistry.cs
  ClientWorldVisuals.cs

Client/DG_Client/Assets/Scripts/Map/World/
  ClientMapWorld.cs
```

### 细分任务

- 5.1 新增 `G2C_VisibilitySyncNotifyHandler`。
- 5.2 `EnteredEntities` 调 `ClientMapWorld.ApplySnapshot`。
- 5.3 `LeftEntityIds` 调 `ClientMapWorld.RemoveEntity`。
- 5.4 `ChangedEntities` 调 `ClientMapWorld.ApplySnapshot`。
- 5.5 `ClientWorldVisuals` 删除离开视野的 GameObject。
- 5.6 `ClientMapEntity` 继续只保留 view handle，不加逻辑状态。

### 验证

- Unity EditMode：apply entered 后本地世界出现实体。
- Unity EditMode：apply changed 后坐标更新。
- Unity EditMode：apply left 后本地世界和 view registry 都删除实体。
- 手动测试：远端玩家离开视野后消失，回来后重新出现。

## 阶段 6：Chunk / Cell 同步

### 目标

在实体同步稳定后，把 chunk/cell 的进入、离开、变更也接入同一套 visible sync。

对应 Minestom：

```text
ChunkDataPacket
UnloadChunkPacket
BlockUpdate / Batch
```

DG 对应：

```text
G2C_ChunkEnterNotify
G2C_ChunkLeaveNotify
G2C_CellBatchNotify
```

### 细分任务

- 6.1 `PlayerVisibilityState` 增加 `VisibleChunkKeys`。
- 6.2 玩家移动后计算 entered chunks / left chunks。
- 6.3 entered chunk 发送 chunk/cell snapshot。
- 6.4 left chunk 通知客户端卸载。
- 6.5 dirty cells 只发给可见该 chunk 的玩家。
- 6.6 客户端维护可见 cell/chunk 镜像。

### 验证

- 玩家进入新 chunk 后收到 chunk snapshot。
- 玩家离开 chunk 后客户端卸载对应 chunk。
- cell 变化只发给看得见该 cell 的玩家。

## 阶段 7：预测、回滚、插值

### 目标

在服务端权威同步骨架稳定后，补客户端表现体验。

顺序：

```text
1. 插值
2. 客户端预测
3. 服务端 reconciliation
4. rollback buffer
```

### 文件建议

```text
Client/DG_Client/Assets/Scripts/Map/Prediction/
  ClientPredictionBuffer.cs
  ServerReconciliationSystem.cs
  InterpolationSystem.cs
```

### 非当前阶段目标

不要在 AOI / Spawn / Despawn 未稳定前做预测和回滚。

## 推荐 OpenSpec Change

建议 change id：

```text
refactor-minestom-style-network-sync
```

### Proposal 范围

目标：

- 借鉴 Minestom 的 `Instance / EntityTracker / Viewable / Packet flush` 模型。
- 服务端按 tick 统一处理世界变化和网络同步。
- 服务端按玩家可见范围发送 entered / left / changed。
- 客户端只维护服务端授权的可见镜像。

非目标：

- 不复制 Minecraft 协议。
- 不做 3D chunk section / palette。
- 不做 Acquirable 多线程模型。
- 第一阶段不做客户端预测和回滚。
- 第一阶段不做完整 chunk streaming。

## 第一批 Apply 切片

第一批不要做完整七阶段，只做这个最小闭环：

```text
1. 协议增加 G2C_VisibilitySyncNotify
2. 服务端新增 PlayerVisibilityState / VisibilityTracker
3. 服务端新增 AuthoritativeSyncManager
4. JoinWorld 初始化 visible set 并发送 EnteredEntities
5. Move 成功后按玩家视野发送 ChangedEntities / LeftEntityIds / EnteredEntities
6. 客户端新增 G2C_VisibilitySyncNotifyHandler
7. 客户端 apply entered / changed / left
8. 测试 A/B/C 三玩家视野同步
```

第一批完成后的验收标准：

- 两个玩家在视野内，移动能互相看到。
- 玩家离开视野，远端客户端删除该实体。
- 玩家回到视野，远端客户端重新创建该实体。
- 第三个不在视野内的玩家不会收到无关实体变化。
- 服务端测试覆盖 entered / left / changed。
- Unity EditMode 覆盖客户端 visible sync apply。

## 验证命令

不执行 Unity build。

建议每个阶段至少跑：

```powershell
openspec validate refactor-minestom-style-network-sync --strict --no-interactive
dotnet build Shared\DG.GameCore\DG.GameCore.csproj -v minimal
dotnet build Server\Hotfix\Hotfix.csproj --no-restore -v minimal
dotnet run --project Server\Tests\AuthoritativeMoveVerification\AuthoritativeMoveVerification.csproj --no-restore
dotnet build Client\DG_Client\DG.GameCore.csproj --no-restore -v minimal /m:1
dotnet build Client\DG_Client\Assembly-CSharp.csproj --no-restore -v minimal /m:1
dotnet build Client\DG_Client\Assembly-CSharp-Editor.csproj --no-restore -v minimal /m:1
```

Unity 内手动验证：

```text
1. 刷新 Unity 项目。
2. 运行 Unity Test Framework EditMode。
3. 启动服务端。
4. 启动两个客户端，确认 join 成功。
5. 两个玩家在视野内互相移动，双方可见。
6. 移出视野，远端实体消失。
7. 移回视野，远端实体重现。
8. 第三个客户端在远处，不收到无关实体同步。
```

## 关键原则

- Handler 只处理协议入口，不做世界同步管理器。
- 服务端 `GameWorld` 是唯一权威状态。
- 客户端 `GameWorld` 是服务端可见镜像，不是权威世界。
- `ClientMapEntity` 只做 view handle，不重新加逻辑状态。
- Dirty 是世界变更事实，Visibility 是每个玩家的可见过滤，两者不能混在一起。
- 先做可见同步，再做 chunk/cell，再做预测回滚。
