## Context
当前代码已经有三个可复用事实：

- `AuthoritativeMoveWorld` 证明了服务端可以裁决玩家移动、拒绝阻挡格和玩家占位。
- `multiplayer-entity-management` 证明了 JoinWorld、session 与 player entity 绑定、observer 广播和多客户端显示。
- `BouncingDemoWorld` 证明了服务端固定 tick 可以主动推进实体并通过 dirty 同步到客户端。

问题是这三个事实没有落在同一个实体模型中。玩家存在于 `AuthoritativeMoveWorld` 的 `Dictionary<long, MoveCoord>`；反弹球和边界存在于 `BouncingDemoWorld` 的 demo 私有实体；客户端又通过 `G2C_EntityMovedNotify` 与 `G2C_DemoEntityChangedNotify` 两套路径应用结果。这个结构不适合继续扩展物体间规则。

## Goals
- 建立一个服务端和客户端都能引用的纯 C# GameCore。
- 用一个服务端权威 GameWorld 同时容纳玩家、自动移动球和阻挡体。
- 用组件/tag/配置表达实体能力，规则不依赖 entity 名字或 demo 类型。
- 用 MoveCommand 作为所有移动来源的统一入口，包括玩家输入和实体 tick 自发移动。
- 第一版明确不引入 Velocity；反弹球用 Direction + AutoMove + Bouncable 表达。
- 统一移动裁决、碰撞、反弹、dirty 和 snapshot/delta 同步边界。
- 保留当前可用的 Fantasy 连接入口和 Unity 手动验收场景入口，但替换旧 world、旧规则和旧同步路径。

## Non-Goals
- 不实现连续物理、浮点速度、Rigidbody 风格 velocity、加速度或摩擦。
- 不实现客户端预测、插值、回滚。
- 不实现完整 GameplayTag 编辑器。
- 不实现通用动态组件二进制序列化；第一版可以使用固定字段 snapshot/delta。
- 不引入完整 AOI；仍可使用当前 observer/session 集合作为第一版广播边界。
- 不把 Fantasy Handler 变成规则执行位置。

## Decisions

### Decision: 新增 Shared GameCore，而不是复制服务端规则到客户端
新增 `Shared/DG.GameCore`，目标为可被 .NET 服务端和 Unity 客户端共同引用的纯 C# 内核。它只能依赖 BCL，不引用 `Fantasy`、`UnityEngine`、`MonoBehaviour`、`Session` 或网络协议生成代码。

`Shared` 与 `Client`、`Server` 同级放置：

```text
D:\Unity_Project_1\DG
  Client/
  Server/
  Shared/
    DG.GameCore/
      DG.GameCore.csproj
  Tools/
  openspec/
```

第一版优先采用独立 `netstandard2.1` 项目，服务端通过 `ProjectReference` 引用。Unity 客户端可以先通过编译后的 DLL 接入，也可以在 asmdef/csproj 不稳定时采用源码链接方式接入；无论采用哪种接入方式，GameCore 依然必须保持纯 C#，不能反向依赖客户端、服务端、Fantasy、UnityEngine 或生成协议。

依赖方向固定为：

```text
Server -> Shared/DG.GameCore
Client -> Shared/DG.GameCore
Server/Client -> GeneratedProtocol
Shared/DG.GameCore -> BCL only
```

GameCore 包含：
- `GridCoord`
- `Direction`
- `GameEntity`
- `GameWorld`
- `PositionComponent`
- `DirectionComponent`
- `ColliderComponent`
- `BlockingComponent`
- `BouncableComponent`
- `AutoMoveComponent`
- `PlayerControlComponent`
- `MoveCommand`
- `MoveResult`
- `CollisionInfo`
- `DirtyChange`
- `EntitySnapshot`
- `WorldDelta`

### Decision: MoveCommand 是统一移动入口
玩家输入、自动 tick、未来的传送带/推进器/AI 都不直接修改坐标，而是产生 `MoveCommand`。`MovementResolveSystem` 负责：
- 查询当前坐标。
- 根据方向或目标坐标计算目标格。
- 查询目标格实体。
- 根据 `ColliderComponent`、`BlockingComponent`、`BouncableComponent` 等组件裁决。
- 写入最终 position、direction 和 dirty。

### Decision: 不使用 Velocity 作为第一版核心概念
反弹球不是物理体。第一版采用：
- `DirectionComponent` 表示朝向。
- `AutoMoveComponent` 表示每隔固定逻辑 tick 生成一次 MoveCommand。
- `BouncableComponent` 表示阻挡碰撞后反向。

这避免 `Velocity` 带来的每秒速度、浮点积分、多格穿透、斜向碰撞等不必要复杂度。

### Decision: 玩家和反弹球进入同一个服务端 GameWorld
`AuthoritativeMoveWorld` 和 `BouncingDemoWorld` 不再作为两个权威空间长期并存。新的服务端权威 GameWorld 应该同时拥有：
- 玩家 entity：`Position + Collider + Blocking + PlayerControl`
- 球 entity：`Position + Direction + Collider + AutoMove + Bouncable`
- 阻挡体或墙 entity：`Position + Collider + Blocking`

球碰玩家反弹必须是普通碰撞规则结果，而不是 demo 系统查询玩家 world 的特判。

### Decision: 不兼容旧 demo world 和旧 demo notify
本变更不保留 `BouncingDemoWorld`、`BouncingDemoEntity`、`BouncingDemoTickSystem`、`G2C_DemoEntitySnapshotNotify`、`G2C_DemoEntityChangedNotify` 作为新架构兼容层。apply 阶段应把它们替换为 GameCore GameWorld、AutoMoveSystem、MovementResolveSystem 和统一 WorldSnapshot/WorldDelta。

旧 `G2C_EntityMovedNotify` 也不作为新同步主路径。玩家移动、球移动、球反弹、阻挡体 snapshot 都应走统一 WorldSnapshot/WorldDelta。

### Decision: 服务端权威，客户端镜像
服务端 GameWorld 是唯一权威状态。Unity 客户端可以引用 GameCore 数据结构，但第一版只应用服务端 snapshot/delta，不在本地推进权威球位置，不用本地规则推导远端玩家坐标。

### Decision: 同步协议替换为固定字段 WorldSnapshot/WorldDelta
第一版新增通用 `G2C_WorldSnapshotNotify` 和 `G2C_WorldDeltaNotify`，字段覆盖当前需要：
- `EntityId`
- `ConfigId`
- `ArchetypeId`
- `EntityTarget`
- `X`
- `Y`
- `Direction`
- `HasCollider`
- `Blocking`
- `Bouncable`
- `AutoMove`
- `ServerTick`

不要求第一版实现任意组件列表序列化。协议仍必须从 `Tools/NetworkProtocol/Outer/OuterMessage.proto` 生成，不手改生成目录。

## Migration Plan
1. 添加 Shared GameCore 数据模型和最小系统，但不接入网络。
2. 用服务端验证证明 GameCore 能处理玩家占位、普通移动、自动移动球、球撞阻挡体反弹、球撞玩家反弹。
3. 将 JoinWorld 的玩家注册替换为创建 GameCore player entity。
4. 将反弹球 demo 的球和边界替换为 GameCore 配置实体。
5. 协议优先新增统一 WorldSnapshot/WorldDelta。
6. 服务端同步替换为统一 world snapshot/delta，停止使用玩家 move notify 和 demo notify 作为新链路。
7. Unity 客户端应用统一 snapshot/delta，并保留当前场景、FantasyRuntime 和显示入口。
8. 完成自动化测试和手动端到端验证后，归档或废弃 `add-bouncing-ball-tick-demo` 的 demo 专用能力。

## Risks / Trade-offs
- 风险：一次性迁移玩家和球会扩大改动面。
  Mitigation: 先落 GameCore 纯规则测试，再接服务端 Handler，最后接客户端显示。
- 风险：通用 snapshot/delta 过早抽象。
  Mitigation: 第一版使用固定字段，先覆盖玩家、球、阻挡体。
- 风险：与已存在 `add-bouncing-ball-tick-demo` active change 重叠。
  Mitigation: 本 change 明确替代旧 demo 专用方向；apply 时不继续补旧 demo 验收，而是以统一 GameCore 验收为准。
- 风险：Unity 引用 Shared 项目可能出现 csproj/asmdef 导入问题。
  Mitigation: 任务中保留 Unity 编译和 TestFramework 验证；如项目引用不稳定，第一版可用 Unity 可编译源码链接方式，但 GameCore 仍保持无 Unity/Fantasy 依赖。

## Open Questions
- 无。Shared GameCore 第一版采用与 `Client`、`Server` 同级的 `Shared/DG.GameCore` 独立 `netstandard2.1` 项目；Unity 接入方式在 apply 阶段按编译稳定性选择 DLL 或源码链接，但不能改变 GameCore 的依赖边界。
