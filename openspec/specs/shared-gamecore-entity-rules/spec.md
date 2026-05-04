# shared-gamecore-entity-rules Specification

## Purpose
TBD - created by archiving change refactor-shared-gamecore-move-rules. Update Purpose after archive.
## Requirements
### Requirement: 共享 GameCore 边界
系统 SHALL 提供一个服务端和 Unity 客户端都可引用的纯 C# GameCore，用于承载统一实体、组件、tag、坐标、移动命令、移动结果、dirty 和 snapshot/delta 数据模型。

#### Scenario: GameCore 不依赖运行时外壳
- **WHEN** 编译 Shared GameCore
- **THEN** GameCore 不引用 `Fantasy`
- **AND** GameCore 不引用 `UnityEngine`
- **AND** GameCore 不包含 `Session`、`MonoBehaviour` 或协议生成类型

#### Scenario: 服务端和客户端共享规则语言
- **WHEN** 服务端创建一个 player、ball 或 blocking entity
- **THEN** 该 entity 使用 GameCore 中定义的组件、tag、配置标识和坐标模型
- **AND** Unity 客户端能够使用同一套数据定义理解服务端 snapshot/delta

### Requirement: 组件驱动实体能力
系统 SHALL 用组件和 tag 表达实体能力，规则 MUST NOT 依赖 entity 名字或 demo 专用类型判断实体行为。实体能力组合 MUST 由 archetype/build 边界创建，并应用到 `GameWorld` 的 component store。

#### Scenario: 玩家实体能力
- **WHEN** 配置创建玩家 entity
- **THEN** 玩家 entity 拥有 `PositionComponent`
- **AND** 玩家 entity 拥有 `ColliderComponent`
- **AND** 玩家 entity 拥有 `BlockingComponent`
- **AND** 玩家 entity 拥有 `PlayerControlComponent`
- **AND** 这些组件由 entity builder 或等价组合边界应用，而不是由 `GameWorld` 内部 if 链组装

#### Scenario: 反弹球实体能力
- **WHEN** 配置创建自动反弹球 entity
- **THEN** 球 entity 拥有 `PositionComponent`
- **AND** 球 entity 拥有 `DirectionComponent`
- **AND** 球 entity 拥有 `ColliderComponent`
- **AND** 球 entity 拥有 `AutoMoveComponent`
- **AND** 球 entity 拥有 `BouncableComponent`
- **AND** 这些组件由 entity builder 或等价组合边界应用，而不是由 `GameWorld` 内部 if 链组装

#### Scenario: 阻挡体能力
- **WHEN** 配置创建墙或阻挡体 entity
- **THEN** 该 entity 拥有 `PositionComponent`
- **AND** 该 entity 拥有 `ColliderComponent`
- **AND** 该 entity 拥有 `BlockingComponent`
- **AND** 这些组件由 entity builder 或等价组合边界应用，而不是由 `GameWorld` 内部 if 链组装

### Requirement: MoveCommand 统一移动入口
系统 SHALL 将玩家输入、自动 tick 和后续机关移动统一表示为 MoveCommand，并通过统一移动裁决系统修改 world 状态。

#### Scenario: 玩家输入生成移动命令
- **WHEN** 已 Join 玩家请求向某方向移动
- **THEN** 服务端网络入口生成或提交一个来源为玩家输入的 MoveCommand
- **AND** Handler 不直接修改 entity 坐标

#### Scenario: 自动移动生成移动命令
- **WHEN** 服务端 tick 到达拥有 `AutoMoveComponent` 的 entity 的执行间隔
- **THEN** AutoMoveSystem 读取该 entity 的 `DirectionComponent`
- **AND** AutoMoveSystem 生成一个来源为自动 tick 的 MoveCommand
- **AND** AutoMoveSystem 不直接修改 entity 坐标

#### Scenario: 移动命令统一裁决
- **WHEN** MovementResolveSystem 处理 MoveCommand
- **THEN** 它通过 GameWorld 查询 entity 当前坐标和目标格实体
- **AND** 它根据组件/tag 裁决成功移动、阻挡、碰撞或反弹
- **AND** 它在状态变化后记录 dirty

### Requirement: 第一版不引入 Velocity 核心概念
系统 SHALL 在第一版共享实体规则中使用 Direction 与 MoveCommand 表达格子移动，MUST NOT 将 Velocity 作为自动移动或反弹的必要底层概念。

#### Scenario: 反弹球方向状态
- **WHEN** 球需要持续朝一个方向移动
- **THEN** 系统使用 `DirectionComponent` 表示该方向
- **AND** 系统使用 `AutoMoveComponent` 表示逻辑 tick 执行间隔
- **AND** 系统不要求球拥有 VelocityComponent

#### Scenario: 碰撞反弹更新方向
- **WHEN** 球的 MoveCommand 被阻挡体或玩家阻挡
- **THEN** 系统反转球的 `DirectionComponent`
- **AND** 球不进入被阻挡目标格
- **AND** 球的方向变化进入 dirty/delta

### Requirement: 单一服务端权威 GameWorld
系统 SHALL 使用一个服务端权威 GameWorld 同时容纳玩家、自动移动球和阻挡体，使物体间规则通过同一空间查询自然发生。

#### Scenario: 玩家和球处于同一空间
- **WHEN** 服务端已经创建玩家 entity 和球 entity
- **THEN** 两者都存在于同一个 GameWorld
- **AND** GameWorld 可以通过坐标查询到对应 entity

#### Scenario: 球碰玩家反弹
- **WHEN** 球的 MoveCommand 目标格存在拥有 `BlockingComponent` 的玩家 entity
- **THEN** MovementResolveSystem 裁决该移动被阻挡
- **AND** 球的 `DirectionComponent` 被反转
- **AND** 玩家坐标保持不变
- **AND** 球坐标保持在碰撞前坐标

#### Scenario: 球碰墙反弹
- **WHEN** 球的 MoveCommand 目标格存在拥有 `BlockingComponent` 的墙或阻挡体 entity
- **THEN** 球按同一反弹规则反向
- **AND** 规则不检查该阻挡体的业务名字

### Requirement: 统一 Dirty 与 WorldSnapshot/WorldDelta
系统 SHALL 用统一 dirty、WorldSnapshot 和 WorldDelta 数据模型表达玩家、球、阻挡体、进入推动地格以及调试编辑实体的服务端权威状态变化。WorldDelta SHALL 同时支持仍然存在的 changed entity snapshot 和已经被移除的 removed entity id。

#### Scenario: 玩家移动产生 delta
- **WHEN** 玩家 MoveCommand 成功移动
- **THEN** GameWorld 标记该玩家 entity 的位置变化
- **AND** 同步层可以从 dirty 中构造该玩家 entity 的 WorldDelta changed entity

#### Scenario: 球自动移动产生 delta
- **WHEN** AutoMoveSystem 驱动球成功移动或反弹
- **THEN** GameWorld 标记该球 entity 的位置或方向变化
- **AND** 同步层可以从 dirty 中构造该球 entity 的 WorldDelta changed entity

#### Scenario: Join 后发送统一 snapshot
- **WHEN** 客户端 JoinWorld 成功
- **THEN** 服务端发送当前 GameWorld 中相关 entity 的 WorldSnapshot
- **AND** WorldSnapshot 可以包含玩家、球、阻挡体和进入推动地格

#### Scenario: 删除实体产生 removed delta
- **WHEN** 服务端从 GameWorld 删除一个已存在 entity
- **THEN** GameWorld 记录该 entity id 为 removed entity id
- **AND** 下一次 FlushDelta 返回的 WorldDelta 包含该 removed entity id
- **AND** 该 removed entity id 不需要对应 changed entity snapshot

#### Scenario: 删除不存在实体不产生 delta
- **WHEN** 服务端请求删除一个 GameWorld 中不存在的 entity id
- **THEN** GameWorld 不记录 removed entity id
- **AND** 下一次 FlushDelta 不因为该请求产生 WorldDelta 内容

#### Scenario: 同一 delta 中删除和变更可区分
- **WHEN** 同一服务端 tick 中既有实体状态变更也有实体删除
- **THEN** WorldDelta 分别暴露 changed entity snapshots 和 removed entity ids
- **AND** 消费方不需要通过缺失 snapshot 推断实体删除

### Requirement: 进入推动能力组件
系统 SHALL 使用通用 `PushOnEnterComponent` 表达进入或停留在地格上会触发推动的能力，MUST NOT 使用 `ConveyorComponent` 这类物体种类组件作为核心规则判断。

#### Scenario: 传送带由通用组件组合
- **WHEN** 配置创建传送带 entity
- **THEN** 该 entity 拥有 `PositionComponent`
- **AND** 该 entity 拥有 `DirectionComponent`
- **AND** 该 entity 拥有 `ColliderComponent`
- **AND** 该 entity 拥有 `PushOnEnterComponent`
- **AND** 该 entity 不拥有 `BlockingComponent`
- **AND** 传送带种类通过配置标识或 tag 表达

#### Scenario: 推动能力不是业务种类
- **WHEN** 后续配置创建风场、水流或弹簧地格
- **THEN** 这些 entity 可以复用 `PushOnEnterComponent`
- **AND** 规则系统不依赖 entity 名字判断是否推动

### Requirement: 进入推动统一移动裁决
系统 SHALL 在服务端 tick 中把进入推动效果转换为 `MoveCommand`，并交给 `MovementResolveSystem` 统一裁决。

#### Scenario: 站上传送带被推动
- **WHEN** 一个拥有 `PositionComponent` 的 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **AND** 该地格拥有向右的 `DirectionComponent`
- **THEN** `PushOnEnterSystem` 生成向右的 `MoveCommand`
- **AND** `MovementResolveSystem` 裁决该 entity 是否可以进入右侧坐标

#### Scenario: 阻挡目标格拒绝推动
- **WHEN** 被推动 entity 的目标格存在拥有 `BlockingComponent` 的 entity
- **THEN** `MovementResolveSystem` 拒绝该移动
- **AND** 被推动 entity 保持原坐标
- **AND** 推动系统不直接绕过阻挡规则修改坐标

#### Scenario: 单 tick 防止链式重复推动
- **WHEN** 一个 entity 在同一服务端 tick 中已经被 `PushOnEnterSystem` 推动过
- **THEN** 该 tick 内其他进入推动地格不会再次推动该 entity
- **AND** 下一服务端 tick 可以重新评估该 entity 是否继续被推动

### Requirement: 共享空间索引
Shared GameCore SHALL provide the authoritative spatial indexing used by both server-side rules and Unity-side shared-rule execution.

#### Scenario: GameWorld uses shared spatial index
- **WHEN** `GameWorld` registers, moves, queries, or removes an entity with `PositionComponent` and `ColliderComponent`
- **THEN** the operation uses the shared GameCore spatial index
- **AND** `GameWorld` does not maintain a separate ad hoc coordinate dictionary for the same entity occupancy

#### Scenario: Movement rule queries shared spatial index
- **WHEN** `MovementResolveSystem` resolves a `MoveCommand`
- **THEN** target-cell entity lookup is answered by the shared GameCore spatial index
- **AND** blocking, occupied, and bouncable behavior remains component-driven

### Requirement: Shared GameCore remains runtime-shell independent
Shared GameCore MUST remain usable by Fantasy server code and Unity client code without depending on either runtime shell.

#### Scenario: No Fantasy dependency
- **WHEN** `Shared/DG.GameCore` is compiled
- **THEN** it does not reference `Fantasy`, `Session`, Fantasy Handler types, or generated protocol message types

#### Scenario: No UnityEngine dependency
- **WHEN** `Shared/DG.GameCore` is compiled
- **THEN** it does not reference `UnityEngine`, `Vector2Int`, `MonoBehaviour`, `GameObject`, or Unity logging APIs

#### Scenario: Unity boundary conversion
- **WHEN** Unity code passes map coordinates into Shared GameCore
- **THEN** Unity code converts between `Vector2Int` and Shared `GridCoord` at the boundary
- **AND** Shared GameCore stores and evaluates rules with `GridCoord`

### Requirement: 实体组合构建边界
系统 SHALL 将实体组件组合规则放在 GameCore 的 archetype/build 边界中，而不是放在 `GameWorld` 容器内部。`GameWorld` MUST 只负责 entity 存储、component store、空间索引、dirty 和 snapshot/delta。

#### Scenario: GameWorld 不组装组件组合
- **WHEN** 通过配置创建 player、ball 或 blocker entity
- **THEN** 组件组合由 archetype/build 边界决定
- **AND** `GameWorld` 不通过 `HasPosition`、`HasCollider`、`HasAutoMove` 或类似布尔字段分支决定组件集合

#### Scenario: 新增组合不修改 GameWorld
- **WHEN** 后续新增一个由已有组件组成的实体 archetype
- **THEN** 系统通过新增或修改 archetype 配置表达组合
- **AND** 不需要修改 `GameWorld` 的实体创建代码

### Requirement: Archetype 与 Spawn Spec 分离
系统 SHALL 分离实体类型组合定义和实体实例生成参数。Archetype SHALL 描述 `ConfigId`、`ArchetypeId`、`EntityTarget`、组件 kind 集合和 tags；spawn/build spec SHALL 描述 `EntityId`、位置、方向、playerId、自动移动间隔等实例参数。

#### Scenario: 同一 archetype 创建多个实例
- **WHEN** 两个不同 player entity 使用同一个 player archetype 创建
- **THEN** 两个 entity 拥有相同组件组合
- **AND** 两个 entity 可以拥有不同 `EntityId`、坐标和 `PlayerControlComponent.PlayerId`

#### Scenario: 实例参数不污染 archetype
- **WHEN** 自动移动实体使用同一个 archetype 但以不同位置生成
- **THEN** 位置来自 spawn/build spec
- **AND** archetype 不为每个实例复制一份坐标配置

### Requirement: Luban 实体配置导出
系统 SHALL 直接通过 Luban 从 Excel 源表导出实体 archetype、world spawn 和 player spawn rule 配置，并通过生成的 provider/registry 作为正式实体配置来源。`GameWorld`、移动系统和空间系统 MUST NOT 直接依赖 Luban 生成类型。

#### Scenario: 导出基础实体配置
- **WHEN** 运行 Luban 导出命令
- **THEN** 生成结果包含 player、ball 和 blocker 的 archetype 数据
- **AND** 生成结果包含 demo world spawn 数据
- **AND** 生成结果包含 player spawn rule 数据

#### Scenario: 使用 Luban provider
- **WHEN** 服务端或客户端需要按 `ConfigId` 创建或应用实体
- **THEN** 运行时代码通过 Luban provider 查询 archetype
- **AND** `GameWorld` 不引用 Luban 生成命名空间或具体表类型

#### Scenario: 测试 fallback 不成为正式路径
- **WHEN** 测试环境需要不依赖 Luban 文件直接构造实体
- **THEN** 系统可以使用 fallback provider
- **AND** 正式服务端世界初始化不调用 `DefaultWorldConfig.CreatePlayer/CreateBall/CreateBlocker`

