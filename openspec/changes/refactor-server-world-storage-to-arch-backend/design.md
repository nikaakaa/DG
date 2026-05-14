# Design: Fantasy 服务端 + Arch ECS Backend + 统一权威链路

## Context

DG 当前架构已经确定为服务端权威：

```text
Unity Client
  输入 / 镜像 / 表现
  |
  v
Fantasy Server
  Session / Handler / Scene / lifecycle / broadcast
  |
  v
Shared DG.GameCore
  GameWorld / action / rule / commit / snapshot / delta
```

现有 `GameWorld` 已经有 `IWorldDataStorage` 边界和 `IndexedWorldDataStorage`，但默认存储仍是自建 indexed storage。要把服务端真正推进到 ECS 存储，Arch 应该进入 `GameWorld` 内部 storage backend，而不是进入 Fantasy Handler、规则层或协议层。

## Goals

- 服务端权威 `GameWorld` 默认使用 Arch ECS backend。
- 规则层继续只依赖 `GameWorld` public API 和 DG 组件类型。
- Fantasy 继续只负责连接、session、handler、world provider、tick runner 和广播。
- DG `EntityId` 继续是协议、snapshot、delta、session ownership、日志和测试的唯一外部 id。
- Arch 只负责权威实体组件主存储和组件组合查询。
- DG 继续负责 spatial、dirty、runtime effect final component、WorldDelta、animation metadata 和客户端镜像收敛。
- 服务端 tick、移动结算速度、action cost 和客户端表现速度有统一权威模型。

## Non-Goals

- 不让 gameplay 规则直接保存 Arch row、chunk、entity 或 query handle。
- 不把 Arch 引入客户端权威裁决。
- 不让 Arch 替代 spatial grid、WorldDelta、runtime effect 或 observer/session 管理。
- 不把本变更扩展成完整 system scheduler。

## Architecture

最终链路为：

```text
Fantasy Scene
  owns AuthoritativeWorldTickRunner

AuthoritativeWorldTickRunner
  owns / accesses GameWorld

GameWorld
  public facade
  spatial index
  dirty journal
  runtime effect resolver
  snapshot / delta builder
  IWorldDataStorage

ArchWorldDataStorage
  Arch.World
  EntityId <-> Arch.Entity map
  component set/get/remove
  component query
```

允许依赖：

```text
Server/Hotfix -> Shared/DG.GameCore
Shared/DG.GameCore -> Arch
Unity Client -> Shared/DG.GameCore
```

禁止依赖：

```text
Rules -> Arch
Fantasy Handler -> Arch
Outer Protocol -> Arch
Client presentation -> Arch authority
Arch -> Fantasy Session
Arch -> UnityEngine
```

如果 Unity 编译链无法稳定接受 Arch 包依赖，则将 Arch backend 拆到服务端专用项目：

```text
Shared/DG.GameCore
  interfaces, GameWorld facade, rules, protocol data

Shared/DG.GameCore.ArchStorage
  ArchWorldDataStorage

Server/Hotfix
  references DG.GameCore and DG.GameCore.ArchStorage
```

该拆分只影响依赖布局，不改变权威模型。

## Entity Identity

DG `EntityId` 继续是外部合同。Arch entity 只作为内部 storage handle。

```text
DG EntityId -> Arch.Entity
Arch.Entity -> DG EntityId
```

要求：

- 创建 entity 时必须同时建立双向映射。
- 删除 entity 时必须同时删除映射和 Arch entity。
- snapshot/delta/log/session ownership 只能输出 DG `EntityId`。
- Arch entity id 不能进入协议、配置、日志合同或客户端 mirror。

## Component Storage

Arch backend 需要覆盖当前权威热路径组件：

```text
PositionComponent
DirectionComponent
AutoMoveComponent
PushOnEnterComponent
ColliderComponent
BlockingComponent
PushableComponent
PortConnectorComponent
PlayerControlComponent
MovementPermissionComponent
TagSetComponent
```

`SetComponent` 和 `RemoveComponent` 仍由 `GameWorld` 外层负责同步派生状态：

```text
component storage -> Arch
spatial occupancy -> DG SpatialEntityIndex
dirty -> DG DirtyWorldJournal
runtime final merge -> DG ComponentStateResolver
delta -> DG WorldDelta builder
```

## Query Model

Arch backend 应该优先实现真实 hot path，而不是生成通用 query view。

首批 query：

```text
Position + Direction + AutoMove
Position + Direction + PushOnEnter
Position + Collider
Position + PortConnector
Position + Blocking
Position + Pushable
Position + MovementPermission
TagSet
```

规则层仍调用：

```text
GameWorld.QueryAutoMove()
GameWorld.QueryPushOnEnter()
GameWorld.GetColliderEntitiesAt(...)
GameWorld.TryGetFirstBlockingAt(...)
GameWorld.GetPushableEntitiesAt(...)
GameWorld.TryGetComponent(...)
```

调用方不得看到 Arch query 类型。

## Spatial And ECS Chunk Boundary

空间 chunk 和 ECS archetype chunk 必须分离：

```text
Spatial chunk
  grid/cell/chunk visibility, occupancy, neighbor lookup

ECS chunk
  component layout, archetype grouping, iteration locality
```

Arch 只负责 ECS chunk。格子坐标、目标层、占用、AOI 未来扩展仍由 `SpatialEntityIndex` 和 DG spatial model 维护。

## Unified Authority And Speed Model

统一权威速度模型：

```text
server tick interval
  authoritative settlement cadence

action cost
  determines ready tick and logical movement frequency

grid movement
  one successful movement commit changes one cell unless ActionSpec explicitly defines another policy

WorldDelta server tick
  tells clients when the authoritative state changed

animation metadata
  tells clients how to present the authoritative movement
```

要求：

- 服务端 tick 是唯一权威结算节拍。
- 玩家移动、自动移动、机关推动、connected body movement 都必须进入 action cost / ready tick / commit 边界。
- 同一类 action 的默认逻辑速度来自 ActionSpec 或配置化 cost policy，不来自客户端帧率。
- 客户端表现速度只能来自服务端 delta、server tick、animation metadata 或表现配置。
- 客户端不得为了画面顺滑而提前决定最终坐标。
- 如果客户端表现需要插值、追帧、压缩播放，必须保证最终落点等于服务端 WorldDelta。

## Fantasy Integration

Fantasy 层只负责：

```text
Scene lifecycle
Session binding
JoinWorld
C2G input/debug request
AuthoritativeWorldTickRunner start/stop
observer enumeration
WorldDelta broadcast
```

Fantasy 层不直接：

```text
create Arch.Entity
query Arch.World
set Arch component
decide movement commit
merge runtime final component
```

`AuthoritativeMoveWorldProvider` 是服务端切到 Arch backend 的接线点。

## Client Boundary

Unity 客户端继续：

```text
send input
receive snapshot/delta
apply ClientMapWorld mirror
play ClientWorldVisuals / ClientAnimationLayer
show debug tools
```

Unity 客户端不得：

```text
run Arch authoritative storage
settle action cost
resolve blocked / push / handoff / connected body
produce WorldDelta
override final coordinate
```

## Migration Plan

1. 确认 Arch 依赖接入路径。
2. 新增 `ArchWorldDataStorage`，先跑 storage equivalence tests。
3. 将服务端 `AuthoritativeMoveWorldProvider` 切到 Arch backend。
4. 保持 Unity/Shared 测试通过。
5. 跑服务端 authoritative verification。
6. 用户手动跑端到端。

## Risks And Mitigation

- Risk: Arch 包依赖影响 Unity 编译。
  Mitigation: 拆服务端专用 `DG.GameCore.ArchStorage` 项目，Shared 保持接口和规则核心。
- Risk: 两套 id 泄漏。
  Mitigation: tests 覆盖协议/snapshot/delta/log 不出现 Arch entity id。
- Risk: dirty/spatial 与 Arch component storage 不一致。
  Mitigation: tests 覆盖 Set/Remove/Move/Delete 后 spatial、dirty、delta 同步。
- Risk: query 迁移改变规则顺序。
  Mitigation: explicit `EntityIterationOrder.EntityId` tests 覆盖确定性顺序。
- Risk: 客户端误以为也要接 Arch。
  Mitigation: spec 明确客户端只镜像服务端权威结果。

## Stop Conditions

出现以下情况必须停止并回到本设计：

```text
规则层开始保存 Arch.Entity
Fantasy Handler 直接 query Arch.World
客户端用 Arch 计算最终坐标
WorldDelta 带出 Arch 内部 id
空间 chunk 和 ECS chunk 被合并
为了接 Arch 重写 push / blocked / connected body 语义
```
