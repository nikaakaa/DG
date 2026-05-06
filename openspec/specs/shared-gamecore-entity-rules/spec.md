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

### Requirement: State-driven 统一规则入口
系统 SHALL 将玩家输入、自动 tick 和后续机关移动统一表示为服务端 tick 中的 action 或 intent，并通过统一的 state-driven rule system 修改 world 状态。旧 `MoveCommand` / `MovementResolveSystem` 路径 MAY remain only as a temporary compatibility or test helper during migration and MUST NOT be the long-term server-authoritative rule truth.

#### Scenario: 玩家输入生成移动 action
- **WHEN** 已 Join 玩家请求向某方向移动
- **THEN** 服务端网络入口提交一个来源为玩家输入的 movement action
- **AND** Handler 不直接修改 entity 坐标

#### Scenario: 自动移动生成自动 action
- **WHEN** 服务端 tick 到达拥有 `AutoMoveComponent` 的 entity 的执行间隔
- **THEN** server-authoritative tick creates an auto movement action or intent from that entity's direction state
- **AND** auto movement does not directly bypass arbitration or commit

#### Scenario: 移动 action 统一裁决
- **WHEN** the state-driven rule system processes queued movement actions
- **THEN** it creates behavior intents, arbitrates by body/tag/priority/policy, plans accepted movement, and commits through the conflict resolver
- **AND** state changes record dirty data for snapshot/delta

### Requirement: 第一版不引入 Velocity 核心概念
系统 SHALL 在第一版共享实体规则中使用 Direction 与 action / intent 表达格子移动，MUST NOT 将 Velocity 作为自动移动或反弹的必要底层概念。

#### Scenario: 反弹球方向状态
- **WHEN** 球需要持续朝一个方向移动
- **THEN** 系统使用 `DirectionComponent` 表示该方向
- **AND** 系统使用 `AutoMoveComponent` 表示逻辑 tick 执行间隔
- **AND** 系统不要求球拥有 VelocityComponent

#### Scenario: 碰撞反弹更新方向
- **WHEN** 球的移动 action 或 intent 被阻挡体或玩家阻挡
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
- **WHEN** 球的 movement action 目标格存在拥有 `BlockingComponent` 的玩家 entity
- **THEN** state-driven rule system 裁决该移动被阻挡
- **AND** 球的 `DirectionComponent` 被反转
- **AND** 玩家坐标保持不变
- **AND** 球坐标保持在碰撞前坐标

#### Scenario: 球碰墙反弹
- **WHEN** 球的 movement action 目标格存在拥有 `BlockingComponent` 的墙或阻挡体 entity
- **THEN** 球按同一反弹规则反向
- **AND** 规则不检查该阻挡体的业务名字

### Requirement: 统一 Dirty 与 WorldSnapshot/WorldDelta
系统 SHALL 用统一 dirty、WorldSnapshot 和 WorldDelta 数据模型表达玩家、球、阻挡体、进入推动地格以及调试编辑实体的服务端权威状态变化。WorldDelta SHALL 同时支持仍然存在的 changed entity snapshot 和已经被移除的 removed entity id。

#### Scenario: 玩家移动产生 delta
- **WHEN** 玩家 movement action 成功移动
- **THEN** GameWorld 标记该玩家 entity 的位置变化
- **AND** 同步层可以从 dirty 中构造该玩家 entity 的 WorldDelta changed entity

#### Scenario: 球自动移动产生 delta
- **WHEN** state-driven auto movement 成功移动或反弹
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
系统 SHALL 在服务端 tick 中把进入推动效果转换为 state-driven mechanism push action 或 intent，并交给统一的 action / intent / plan / commit 管线裁决。

#### Scenario: 站上传送带被推动
- **WHEN** 一个拥有 `PositionComponent` 的 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **AND** 该地格拥有向右的 `DirectionComponent`
- **THEN** server-authoritative tick creates a mechanism push action or intent for that entity
- **AND** the state-driven rule pipeline decides whether the entity may enter the right-side coordinate

#### Scenario: 阻挡目标格拒绝推动
- **WHEN** 被推动 entity 的目标格存在拥有 `BlockingComponent` 的 entity
- **THEN** the state-driven rule pipeline rejects the movement
- **AND** 被推动 entity 保持原坐标
- **AND** 推动系统不直接绕过阻挡规则修改坐标

#### Scenario: 单 tick 防止链式重复推动
- **WHEN** 一个 entity 在同一服务端 tick 中已经处于 pending push 或已被推动
- **THEN** 该 tick 内其他进入推动地格不会再次推动该 entity
- **AND** 下一服务端 tick 可以重新评估该 entity 是否继续被推动

### Requirement: 共享空间索引
Shared GameCore SHALL provide the authoritative spatial indexing used by both server-side rules and Unity-side shared-rule execution.

#### Scenario: GameWorld uses shared spatial index
- **WHEN** `GameWorld` registers, moves, queries, or removes an entity with `PositionComponent` and `ColliderComponent`
- **THEN** the operation uses the shared GameCore spatial index
- **AND** `GameWorld` does not maintain a separate ad hoc coordinate dictionary for the same entity occupancy

#### Scenario: Movement rule queries shared spatial index
- **WHEN** the state-driven rule pipeline plans a movement intent
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

### Requirement: Intent 策略定义注册表
Shared GameCore SHALL use a single intent definition registry to define the default source tag, ability tag, required tags, blocked tags, and cancel policy for every supported `BehaviorIntentKind`. The system MUST NOT infer these defaults through multiple independent switch statements, and an unknown intent kind MUST fail explicitly instead of silently falling back to player semantics.

#### Scenario: 现有 intent kind 拥有显式定义
- **WHEN** Shared GameCore creates a `BehaviorIntent` for `Move`, `Push`, `AutoMove`, `MechanismPush`, or `DebugMove`
- **THEN** its default source tag, ability tag, blocked tags, required tags, and cancel policy come from the intent definition registry
- **AND** the resulting arbitration behavior matches the current player move, player push, auto move, mechanism push, and debug move behavior

#### Scenario: 未注册 intent kind 显式失败
- **WHEN** code attempts to create or resolve a `BehaviorIntent` whose kind has no registered definition
- **THEN** Shared GameCore fails with a clear error
- **AND** the intent is not treated as `SourcePlayer` by default

### Requirement: System 层规则职责边界
Shared GameCore SHALL organize movement rule code around system-layer responsibilities: action intake, intent creation, arbitration, planning, conflict/commit, and pending state. Component reads inside these systems are allowed as rule inputs, but system code MUST NOT depend on concrete entity names, demo-specific entity types, Unity runtime objects, Fantasy runtime objects, or protocol generated types.

#### Scenario: 规则 system 读取 component 而不读取实体种类
- **WHEN** player movement, push, auto movement, or mechanism push is evaluated
- **THEN** the rule system may read components such as `PositionComponent`, `BlockingComponent`, `PushableComponent`, `PlayerControlComponent`, `DirectionComponent`, `AutoMoveComponent`, `PushOnEnterComponent`, and `PortConnectorComponent`
- **AND** the rule system does not branch on entity class names or demo-only entity type names to decide movement behavior

#### Scenario: 规则职责拆分后行为保持一致
- **WHEN** the state-driven rule system processes the same world state and queued actions as before the refactor
- **THEN** it produces the same accepted intents, rejected reasons, move plans, commit results, dirty changes, and move results for existing covered scenarios

### Requirement: Legacy movement resolver 迁移边界
Shared GameCore SHALL migrate production rule execution away from `Movement/Legacy/Systems.cs` before deleting or isolating that file. The server-authoritative path MUST use the state-driven action / intent / plan / commit pipeline as the rule truth, and legacy resolver APIs MUST NOT remain required by server-authoritative runtime construction after migration.

#### Scenario: Legacy 删除前完成依赖迁移
- **WHEN** `Movement/Legacy/Systems.cs` is removed or isolated
- **THEN** no production server-authoritative code depends on `MovementResolveSystem`, `AutoMoveSystem`, or `PushOnEnterSystem`
- **AND** equivalent state-driven tests cover the movement, auto movement, push-on-enter, port-connected body, and conflict cases previously validated through the legacy resolver

#### Scenario: 服务端权威路径不构造 legacy resolver
- **WHEN** the authoritative move world provider creates the server tick runner
- **THEN** the server-authoritative construction path does not require a `MovementResolveSystem`
- **AND** auto move and mechanism push are generated as state-driven actions or intents before arbitration and commit

### Requirement: Shared Rules Source Layout
Shared GameCore rule execution code SHALL live under a directory that represents world rules instead of a single movement mechanic.

#### Scenario: Rules pipeline has domain-level folder
- **WHEN** a developer looks for action, intent, planning, arbitration, pending state, or commit code
- **THEN** those files are discoverable under `Shared/DG.GameCore/Rules`
- **AND** `Shared/DG.GameCore/Movement` is not the primary entry point for the rule pipeline

#### Scenario: Legacy movement folder is not a rule entry point
- **WHEN** the legacy movement resolver has no production references
- **THEN** empty or obsolete `Movement/Legacy` folders do not remain as an apparent extension point

### Requirement: Rule Pipeline File Boundaries
The rule pipeline SHALL keep intent definition, arbitration, planning, conflict resolution, pending state, and rule execution in separate source file boundaries while preserving the same runtime behavior.

#### Scenario: Intent definitions are separate from arbitration execution
- **WHEN** a new `BehaviorIntentKind` default policy is reviewed
- **THEN** its source tag, ability tag, required tags, blocked tags, and cancel policy are found in the intent definition boundary
- **AND** the arbiter does not contain per-kind switch inference logic

#### Scenario: Planning and commit remain distinct
- **WHEN** a move intent is accepted by arbitration
- **THEN** planning produces a move plan before commit
- **AND** conflict resolution remains responsible for same-tick atomic commit decisions

