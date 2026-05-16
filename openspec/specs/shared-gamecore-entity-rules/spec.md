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
系统 SHALL 将玩家输入、自动 tick、机关推动和后续配置化行为统一表示为服务端 tick 中的 action input 与 action unit，并通过统一的 state-driven rule system 修改 world 状态。旧 `MoveCommand` / `MovementResolveSystem` 路径 MAY remain only as a temporary compatibility or test helper during migration and MUST NOT be the long-term server-authoritative rule truth.

#### Scenario: 玩家输入生成移动 action
- **WHEN** 已 Join 玩家请求向某方向移动
- **THEN** 服务端网络入口提交一个来源为玩家输入的 movement action input
- **AND** Handler 不直接修改 entity 坐标

#### Scenario: 自动移动生成自动 action
- **WHEN** 服务端 tick 到达拥有 `AutoMoveComponent` 的 entity 的执行间隔
- **THEN** server-authoritative tick creates an auto movement action input from that entity's direction state
- **AND** auto movement does not directly bypass arbitration or commit

#### Scenario: 移动 action unit 统一裁决
- **WHEN** the state-driven rule system processes ready action units
- **THEN** it arbitrates by `ActionSpec`, final Component, tag, world state, claims, priority, policy, and pending unit state
- **AND** it plans accepted action unit claims and commits through the conflict resolver
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
系统 SHALL 使用通用 `PushOnEnterComponent` 表达进入或停留在地格上会触发推动的能力，MUST NOT 使用 `ConveyorComponent` 这类物体种类组件作为核心规则判断。`PushOnEnterComponent` 的输出 action spec 和输出 cost SHALL come from Luban Excel entity/component configuration rather than a hard-coded action id in component application code.

#### Scenario: 传送带由通用组件组合
- **WHEN** 配置创建传送带 entity
- **THEN** 该 entity 拥有 `PositionComponent`
- **AND** 该 entity 拥有 `DirectionComponent`
- **AND** 该 entity 拥有 `ColliderComponent`
- **AND** 该 entity 拥有 `PushOnEnterComponent`
- **AND** 该 entity 不拥有 `BlockingComponent`
- **AND** 传送带种类通过配置标识或 tag 表达
- **AND** 传送带输出 action spec 和 cost 来自 Luban 生成配置数据

#### Scenario: 推动能力不是业务种类
- **WHEN** 后续配置创建风场、水流或弹簧地格
- **THEN** 这些 entity 可以复用 `PushOnEnterComponent`
- **AND** 规则系统不依赖 entity 名字判断是否推动
- **AND** 组件应用层不通过写死 `"mechanism_push"` 或其他普通 action id 决定输出行为
- **AND** fallback provider 不作为正式 PushOnEnter 输出数据来源

### Requirement: 进入推动统一移动裁决
系统 SHALL 在服务端 tick 中把进入推动效果转换为 state-driven mechanism source action input，并交给统一的 action unit / arbitration / plan / commit / deferred-output 管线裁决。PushOnEnter output MUST use configured output action data and MUST NOT bypass the action pipeline or create push pending child units.

#### Scenario: 站上传送带被推动
- **WHEN** 一个拥有 `PositionComponent` 的 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **AND** 该地格拥有向右的 `DirectionComponent`
- **AND** the `PushOnEnterComponent` declares a configured output action spec
- **THEN** server-authoritative tick creates a mechanism source move or push action input for that entity from the configured output
- **AND** the state-driven rule pipeline decides whether the entity may enter the right-side coordinate

#### Scenario: 阻挡目标格拒绝或输出 deferred
- **WHEN** 被推动 entity 的目标格存在拥有 `BlockingComponent` 的 entity
- **THEN** the state-driven rule pipeline rejects, emits deferred output, or fails the movement according to `ActionSpec` policy and final Component state
- **AND** 被推动 entity 不会被 execution 层直接绕过阻挡规则修改坐标
- **AND** push continuation does not create pending child units

#### Scenario: 单 tick 防止链式重复推动
- **WHEN** 一个 entity 在同一服务端 tick 中已经 consumed a ready action subject or produced a deferred output
- **THEN** 该 tick 内其他进入推动地格不会再次直接推动该 entity
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

### Requirement: ActionSpec 策略定义注册表
Shared GameCore SHALL use `ActionSpec` as the behavior policy registry for authoritative movement actions. `ActionSpec` MUST define primitive, source, priority, tags, target rule, blocked policy, handoff policy, conflict policy, interrupt policy, merge policy, plan rule, commit rule, subject policy, and default cost policy for ordinary runtime behavior. Existing `ActionSubjectKind` values MAY remain as the implementation name during migration, but their required semantics are `HitEntity` for single entity subject and `ConnectedBodyIfAny` for connected body subject. `ActionSpecId` MUST be used only to look up policy data; rules MUST NOT infer subject behavior, handoff behavior, priority, or commit behavior from action names.

#### Scenario: 现有行为拥有显式 ActionSpec
- **WHEN** player move, player push, auto move, mechanism push, debug move, configured wind push, or connected body move enters the rules layer
- **THEN** its default source tag, ability tag, blocked tags, required tags, target rule, blocked policy, handoff policy, conflict policy, interrupt policy, merge policy, plan rule, commit rule, subject policy, and default cost policy come from `ActionSpec`
- **AND** the rules layer does not require `BehaviorIntentKind` or an intent definition registry
- **AND** the rules layer does not branch on the `ActionSpecId` string to choose subject or handoff behavior

#### Scenario: 新行为不新增业务 kind
- **WHEN** a new ordinary move-like behavior is added
- **THEN** it is expressed through `ActionSpec`, source context, action unit lifecycle, and policy data
- **AND** it does not require a new `WorldActionKind` or `BehaviorIntentKind`
- **AND** it does not require a new `connected_body_xxx` action solely to opt into connected body subject selection

#### Scenario: 未注册 ActionSpec 显式失败
- **WHEN** runtime code attempts to resolve an unknown `ActionSpecId`
- **THEN** Shared GameCore fails with a clear error
- **AND** the action is not treated as player movement or player push by default

#### Scenario: subject policy 可迁移到现有配置
- **WHEN** an existing move-like `ActionSpecId` is configured with `ConnectedBodyIfAny`
- **AND** the entry entity belongs to a port connected body
- **THEN** its resolved `ActionSpec` allows the rules layer to resolve the connected body view as the action subject
- **AND** no action-name-specific branch is required for that source

#### Scenario: WorldTag 不替代 subject policy
- **WHEN** an entity has source, ability, state, immunity, or blocker tags
- **THEN** those tags may be used as rule inputs or filters
- **AND** they do not cause connected body subject expansion unless the resolved `ActionSpec` subject policy allows it

#### Scenario: CompositeEntity policy deferred
- **WHEN** subject policy is extended in this change
- **THEN** `CompositeEntityIfAny` remains a future design boundary
- **AND** Shared GameCore does not create a persistent composite entity for ordinary port connections

#### Scenario: handoff policy 可迁移到现有配置
- **WHEN** a blocked action is configured to start push if pushable
- **THEN** the derived action spec, derived subject policy, and handoff branch behavior are resolved from explicit `ActionSpec` handoff policy data
- **AND** the rules layer does not choose `"player_push"` or `"connected_body_move"` by matching connected body state inside resolver code

### Requirement: System 层规则职责边界
Shared GameCore SHALL organize movement and behavior rule code around system-layer responsibilities: action intake, action unit lifecycle, tag/component gate, subject selection, target selection, strategy execution, claim arbitration, planning, deferred output, conflict/commit, and result application. Component and tag reads inside these systems are allowed as rule inputs, but system code MUST NOT depend on concrete entity names, ordinary action ids, gameplay-specific names, Unity runtime objects, Fantasy runtime objects, protocol generated types, Ability state, RuntimeEffect state, editor scanning APIs, or runtime reflection to choose ordinary behavior strategy.

#### Scenario: 规则 system 读取 component 而不读取实体种类
- **WHEN** player movement, push, auto movement, mechanism push, configured wind push, or future ordinary behavior is evaluated
- **THEN** the rule system may read final components such as `PositionComponent`, `BlockingComponent`, `PushableComponent`, `PlayerControlComponent`, `DirectionComponent`, `AutoMoveComponent`, `PushOnEnterComponent`, and `PortConnectorComponent`
- **AND** the rule system does not branch on entity class names, demo-only entity type names, or ordinary action id strings to decide behavior strategy
- **AND** the rule system does not query Ability or RuntimeEffect state

#### Scenario: tag 只作为事实输入
- **WHEN** rules evaluate tags such as source, ability, state, immunity, or blocker tags
- **THEN** those tags are used only as facts for explicit policy conditions or filters
- **AND** tag combinations do not replace `ActionSpec`, strategy key, or reusable policy fields

#### Scenario: 规则职责拆分后行为保持一致
- **WHEN** the state-driven rule system processes the same world state and queued actions as before a pipeline refactor
- **THEN** it produces equivalent accepted action units, rejected reasons, deferred outputs, move plans, commit results, dirty changes, and owner action results for existing covered scenarios

### Requirement: Legacy movement resolver 迁移边界
Shared GameCore SHALL migrate production rule execution away from `Movement/Legacy/Systems.cs` before deleting or isolating that file. The server-authoritative path MUST use the state-driven action / action unit / arbitration / plan / commit pipeline as the rule truth, and legacy resolver APIs MUST NOT remain required by server-authoritative runtime construction after migration.

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
The rule pipeline SHALL keep action definitions, policy data models, request models, tag/component gates, subject selection, target selection, strategy registry, strategy modules, claim arbitration, planning, conflict resolution, deferred output enqueue, and commit in separate source file boundaries while preserving the same runtime behavior. No single central class SHALL own all ordinary behavior construction, blocked outcome execution, claim arbitration, and commit mutation.

#### Scenario: Action definitions are separate from execution
- **WHEN** a new `ActionSpec` default policy is reviewed
- **THEN** its source, required tags, blocked tags, cost, strategy, blocked result policy, merge policy, interrupt policy, plan rule, and commit rule are found in the action definition or policy boundary
- **AND** central arbitration execution is not the source of those policy values

#### Scenario: Strategy modules are separate from central arbitration
- **WHEN** a behavior needs a new reusable low-level capability
- **THEN** the code adds or updates a strategy module and registry entry
- **AND** the registry entry is produced from strategy metadata into explicit generated C# registration code
- **AND** central arbitration continues to compare produced action units and claims

#### Scenario: Generated registration is outside rule decisions
- **WHEN** generated strategy registration code is reviewed
- **THEN** it only maps typed strategy keys to strategy module construction or registration
- **AND** it does not contain ordinary action id branches, world-state decisions, tag-condition evaluation, planning, or commit logic
- **AND** rule systems consume the completed registry through dependency injection

#### Scenario: Planning and commit remain distinct
- **WHEN** accepted action claims are available
- **THEN** planning turns them into move/spawn/remove/component/effect proposals
- **AND** commit applies proposals atomically
- **AND** conflict resolution remains responsible for same-tick atomic commit decisions

#### Scenario: Deferred output is separate from commit
- **WHEN** an action emits deferred output
- **THEN** deferred output enqueue is represented as structured future action input
- **AND** commit does not own push propagation lifecycle
- **AND** removed pending retry code is not used as a fallback

### Requirement: Port Graph Composition Boundary
系统 SHALL treat port as an entity connection capability, not as an entity hierarchy. Runtime port connectivity MUST be resolved as graph connectivity and exposed to rules as a flat connected body view. Port connectivity MUST NOT automatically propagate member components across the connected body.

#### Scenario: port 表达连接能力
- **WHEN** an entity has final `PortConnectorComponent`
- **THEN** the component describes available local ports and compatibility-relevant connection capability
- **AND** it does not describe parent, child, control ownership, action waiting, or action lifecycle relationships

#### Scenario: 图连通块形成 connected body view
- **WHEN** port-compatible entities are adjacent through matching world ports
- **THEN** the connection system resolves a connected component graph
- **AND** the rule layer receives a flat connected body view with a stable root and members
- **AND** the root is a representative, not a parent entity

#### Scenario: 环形连接不需要树结构
- **WHEN** port connections form a cycle
- **THEN** the connection system still resolves one connected body view
- **AND** rule evaluation does not require choosing parent-child relationships between members

#### Scenario: linked body 不传播 component
- **WHEN** entity A has `PushableComponent`
- **AND** entity B does not have `PushableComponent`
- **AND** A and B are connected through matching ports
- **THEN** the connected body view includes both A and B
- **AND** B does not gain `PushableComponent`
- **AND** snapshots and deltas still report each member's own final components

### Requirement: Composite Body Entity Deferral
系统 SHALL NOT introduce a persistent `CompositeBodyEntity` for basic port-connected movement. A persistent composite body entity MAY be proposed later only when body-level long-lived state is required.

#### Scenario: 临时 connected body 不创建持久组合体
- **WHEN** entities are connected only to move, collide, or be evaluated together for the current tick
- **THEN** the system uses connected body view
- **AND** it does not create a persistent `CompositeBodyEntity`

#### Scenario: 长期 body 状态需要新 proposal
- **WHEN** future behavior needs body-level hp、energy、inventory、owner、save/load identity, or state split policy
- **THEN** the persistent `CompositeBodyEntity` design requires a separate OpenSpec proposal
- **AND** that proposal must define how state survives attach, detach, split, merge, and sync

### Requirement: Action Subject Resolver Boundary
Shared GameCore SHALL resolve the action subject from entry entity, resolved `ActionSpec` subject policy, final component/tag state, and port connected body view. Subject resolution MUST be read-only and MUST NOT perform planning, blocker resolution, handoff creation, commit, or world mutation.

#### Scenario: resolver produces single entity subject
- **WHEN** an action's resolved subject policy is `HitEntity`
- **THEN** subject resolution returns only the entry entity as the action subject
- **AND** port connectivity does not expand the subject for that action

#### Scenario: resolver produces connected body subject
- **WHEN** an action's resolved subject policy is `ConnectedBodyIfAny`
- **AND** the entry entity belongs to a port connected body with multiple members
- **THEN** subject resolution returns the connected body view as the action subject
- **AND** the connected body remains a runtime view, not a persistent world entity

#### Scenario: resolver ignores action name for subject choice
- **WHEN** two move-like actions have different `ActionSpecId` values but the same resolved subject policy
- **THEN** subject resolution uses the same subject selection behavior for both actions
- **AND** differences between those actions come from explicit `ActionSpec` fields, not hidden name branches

#### Scenario: resolver is read-only
- **WHEN** subject resolution runs
- **THEN** it does not modify `GameWorld`
- **AND** it does not create a `MovePlan`
- **AND** it does not inspect target blocker outcomes
- **AND** it does not create a handoff action

### Requirement: Port Linked Body Capability Resolution
系统 SHALL resolve linked body behavior through body-level capability resolution. `PushableComponent` SHALL be treated as a push entry capability on the touched member, while body movement SHALL be decided from the connected body view and final member components.

#### Scenario: pushable entry moves linked body
- **WHEN** entity A has `PushableComponent`
- **AND** entity B does not have `PushableComponent`
- **AND** A and B are connected through matching ports
- **AND** an external action pushes A in a valid direction
- **THEN** A is accepted as the push entry
- **AND** the system resolves A-B as one connected body subject
- **AND** planning creates movement claims for all moved body members
- **AND** commit moves A and B together only if every required claim is valid

#### Scenario: non-pushable entry rejects linked body push
- **WHEN** entity A has `PushableComponent`
- **AND** entity B does not have `PushableComponent`
- **AND** A and B are connected through matching ports
- **AND** an external action pushes B
- **THEN** B is not accepted as a push entry
- **AND** the system does not create a linked body push action from B
- **AND** A and B remain at their original coordinates

#### Scenario: member movement permission blocks body movement
- **WHEN** a linked body contains multiple members
- **AND** any member has final movement permission that forbids movement
- **AND** a push entry on that body is otherwise valid
- **THEN** body movement is rejected
- **AND** no member position is committed

#### Scenario: linked body commit is all-or-nothing
- **WHEN** a linked body move action is accepted for planning
- **AND** any required member claim is blocked by an external blocker or invalid target
- **THEN** the action unit fails or waits according to policy
- **AND** no partial member movement is reported as success

#### Scenario: capability resolver 不依赖 port graph 扩散能力
- **WHEN** rules evaluate push entry, movement permission, blocking, or control for a linked body
- **THEN** the rule layer uses final member components and body capability resolution
- **AND** the port graph only supplies connectivity and member membership

### Requirement: Port Graph Cache Boundary
系统 MAY cache port graph and connected body views for performance, but cache state MUST be rebuildable from final components and authoritative world state. Cache invalidation MUST be driven by changes to port connector, position, direction, entity spawn, and entity removal.

#### Scenario: cache 可重建
- **WHEN** a port graph or connected body cache is cleared
- **THEN** the system can rebuild equivalent linked body views from final `PortConnectorComponent`, `PositionComponent`, `DirectionComponent`, and current entity membership

#### Scenario: dirty source 触发重算
- **WHEN** an entity's port connector, position, direction, spawn state, or removal state changes
- **THEN** the affected port graph or connected body view is marked dirty
- **AND** later rule evaluation observes the updated connectivity

#### Scenario: cache 不引入持久组合体身份
- **WHEN** a connected body cache stores body id, root, members, or version
- **THEN** the cached body remains a runtime view
- **AND** it does not become a persistent `CompositeBodyEntity`

### Requirement: Body Push Chain Capability Boundary
系统 SHALL evaluate connected body push propagation through final components, connected body view, and body capability resolution. A connected body MAY be pushed by another action unit only through a legal touched member push entry, and the body MUST move as one action subject.

#### Scenario: body push entry is touched member
- **WHEN** connected body A-B has `PushableComponent` only on A
- **AND** another body pushes A
- **THEN** A is accepted as the push entry for A-B
- **AND** A-B may be moved as one connected body subject

#### Scenario: non-pushable member rejects body push
- **WHEN** connected body A-B has `PushableComponent` only on A
- **AND** another body pushes B
- **THEN** B is not accepted as the push entry for A-B
- **AND** A-B does not move because of A's component

#### Scenario: component does not propagate through chain
- **WHEN** connected body A-B pushes connected body C-D
- **AND** only C has `PushableComponent`
- **THEN** C-D may be pushed only when the touched blocker member is C
- **AND** D does not gain push entry behavior from C

### Requirement: Body Push Chain Verification Boundary
系统 SHALL verify body push chain behavior with Unity TestFramework EditMode tests and server authoritative verification. Unity Player build MUST NOT be required for automated validation, and end-to-end runtime sync MUST be left to manual Play Mode verification.

#### Scenario: automated body chain validation
- **WHEN** automated tests run
- **THEN** they cover connected body pushing connected body, connected body chain retry, non-pushable entry rejection, movement permission failure, cycle guard, max depth guard, and no partial body commit

#### Scenario: manual body chain validation
- **WHEN** the user manually runs server-authoritative Play Mode with two clients
- **THEN** a horizontal row of pushable connected bodies can be pushed from the left
- **AND** the rightmost connected body remains a body subject rather than splitting into ordinary single entity push
- **AND** both clients observe the same final server WorldDelta

### Requirement: Luban ActionPolicy 配置来源
系统 SHALL use Luban exported from Excel as the formal source for ordinary action policy configuration. Action policy Excel data MUST produce generated JSON and provider code that can build an `ActionSpecRegistry` for Shared GameCore. Shared runtime rules MUST consume the mapped `ActionSpecRegistry` abstraction and MUST NOT directly depend on Luban generated table types.

#### Scenario: 导出 action policy 配置
- **WHEN** the Luban export command runs
- **THEN** it exports action policy table data from Excel into generated JSON
- **AND** it generates provider code that can load every ordinary runtime `ActionSpec`
- **AND** existing entity archetype, world spawn, player spawn rule, and port connector exports remain intact

#### Scenario: 运行时通过 provider 构造 registry
- **WHEN** server or client runtime initializes Shared GameCore rules
- **THEN** it can construct `ActionSpecRegistry` from the Luban-backed provider
- **AND** rules consume only `ActionSpecRegistry`, `ActionSpec`, and rule-layer enums
- **AND** `GameWorld`, arbiter, planner, pending state, and commit code do not reference Luban generated namespaces or concrete table classes

#### Scenario: 测试 fallback 不成为正式路径
- **WHEN** tests need to construct action specs without generated data files
- **THEN** they may use an in-memory fallback registry
- **AND** production server/client initialization fails clearly when required Luban action policy data is missing or invalid

### Requirement: ActionPolicy 聚合边界
系统 SHALL keep ordinary behavior policy highly cohesive in action policy data and keep runtime rule modules low-coupled. The cohesive action policy data MUST own primitive, source, priority, tag filters, target rule, blocked policy, handoff policy, conflict policy, interrupt policy, merge policy, subject policy, plan rule, commit rule, and cost policy. Runtime modules MUST consume these fields without adding action-name branches.

#### Scenario: 高聚合策略字段
- **WHEN** a designer changes a move-like action from single entity subject to connected body subject, or from reject-on-block to push handoff
- **THEN** the change is made in action policy data
- **AND** no core rule module adds a branch for that action name

#### Scenario: 低耦合运行时模块
- **WHEN** the arbiter, subject resolver, body capability resolver, pending store, planner, or commit code evaluates an action
- **THEN** it uses explicit policy fields and final component/tag/world state
- **AND** it does not infer ordinary behavior by action name, entity name, or tag combination

### Requirement: Tick-Based Emergent Device Boundary
Shared GameCore SHALL model closed-loop and continuous devices as tick-based emergent behavior over finite atomic transactions. A device that continues to output push MUST do so by creating structured deferred outputs that later become action inputs, not by creating pending child push units or requiring one action to solve an unbounded chain.

#### Scenario: Closed loop output cadence
- **WHEN** a closed-loop connected body receives an input push with cost `a`
- **AND** its topology continues to produce an output after the first finite transaction
- **THEN** the next output is eligible no earlier than `currentTick + a`
- **AND** repeated output cadence is produced by repeated tick processing

#### Scenario: Device body may remain stationary
- **WHEN** a closed-loop connected body routes push back into itself
- **AND** there is no valid external movement commit for the body in the current transaction
- **THEN** the body is not required to move
- **AND** the device may still emit future push output according to explicit output policy
- **AND** the current transaction does not fail solely because the loop body did not translate

#### Scenario: Output target distinguishes no-output from deferred-output
- **WHEN** a closed-loop connected body routes feedback into itself
- **AND** no valid external output target exists
- **THEN** the transaction may complete with `bounded/no-output`
- **WHEN** a valid external output target exists
- **THEN** the transaction MUST record deferred output or an equivalent bounded deferred-output diagnosis
- **AND** it MUST NOT report `bounded/no-output`

#### Scenario: Surging device is not implicit strength amplifier
- **WHEN** a closed-loop connected body repeatedly emits output every `a` ticks
- **AND** no explicit strength policy is present
- **THEN** output strength does not grow simply because topology contains feedback
- **AND** future strength amplification requires explicit strength policy data

### Requirement: Shared Rule Truth For Atomic Emergence
Shared GameCore SHALL keep atomic transaction and emergent motion rules in the server-authoritative rule pipeline. Unity client mirror code MUST NOT locally decide loop continuation, same-tick push merging, or closed-loop output.

#### Scenario: Server owns loop continuation
- **WHEN** a loop device produces a future output action
- **THEN** the server-authoritative Shared GameCore rule pipeline decides that output
- **AND** Unity client state changes only through authoritative snapshot or delta results

#### Scenario: Manual sync validation
- **WHEN** two clients observe a loop device in Play Mode
- **THEN** both clients receive the same server-produced WorldDelta sequence
- **AND** neither client locally simulates extra loop output

### Requirement: Push Pending Boundary
Shared GameCore SHALL remove push propagation from parent-child pending handoff semantics. Push continuation MUST be represented as finite action unit output and deferred output that does not make the source action wait for downstream result success. The action policy pipeline MUST NOT recreate `PendingRuleStates`, `PendingActionState`, or `PendingActionUnit` as the default mechanism for push composition.

#### Scenario: Push does not create pending child handoff
- **WHEN** a source action is blocked and emits push output
- **THEN** the output is a structured deferred action or equivalent future ready action input
- **AND** the source action does not wait for the downstream action result through `PendingRuleStates`

#### Scenario: 组合不能通过旧 pending chain 完成
- **WHEN** same-tick composition, multi-contact fanout, closed-loop feedback, or long push propagation is evaluated
- **THEN** it is represented through finite current-tick claims and bounded deferred output
- **AND** it does not use parent retry or child completion from the removed pending chain

#### Scenario: Future waiting action requires a separate proposal
- **WHEN** a future feature truly needs a waiting action lifecycle
- **THEN** it requires a separate OpenSpec change with explicit lifecycle, tests, and manual verification
- **AND** it MUST NOT reuse the removed push pending chain as an implicit fallback

### Requirement: GameCore Runtime ID Boundary
Shared GameCore SHALL convert configuration-readable action, policy, style, and tag names into stable runtime identifiers before authoritative rules consume them. Runtime arbitration, planning, commit, pending, deferred output, and component result resolution MUST consume typed or numeric identifiers and final component/tag values rather than comparing raw strings.

#### Scenario: 配置名在导入边界解析
- **WHEN** Luban action, blocked policy, push-on-enter output, entity tag, or animation style data is loaded
- **THEN** the provider or registry resolves readable names into runtime identifiers or enums
- **AND** rule execution receives the resolved identifier or enum value
- **AND** the readable name remains available only for authoring, diagnostics, or presentation metadata

#### Scenario: 规则层不比较字符串 spec
- **WHEN** player move, auto move, mechanism push, configured wind push, debug action, or push handoff enters arbitration
- **THEN** subject selection, blocked result, handoff, priority, conflict, merge, planning, and commit decisions are selected from typed policy data
- **AND** the rules layer does not compare raw action spec strings such as `player_move`, `mechanism_push`, `auto_move`, or future ordinary behavior names

#### Scenario: tag 作为位标记进入规则层
- **WHEN** action policy or entity archetype data contains source, ability, state, immunity, or blocker tags
- **THEN** configuration import resolves those tags to `WorldTag` or an equivalent typed tag set
- **AND** authoritative rules read final tag/component facts rather than raw string tag names

### Requirement: ECS Storage Abstraction Boundary
Shared GameCore SHALL expose world, entity, component, query, spatial, dirty, snapshot, and delta semantics through stable APIs that do not reveal whether component data is stored in dictionaries, arrays, indexed pools, third-party ECS storage, or archetype chunks. `GameWorld` SHALL own the only public access boundary for rule, runtime effect, debug edit, server sync, and client mirror code. External rule modules MUST NOT depend on dictionary key order, per-query array allocation, concrete `ComponentStore<T>` internals, concrete indexed storage internals, storage adapter types, or third-party ECS types.

#### Scenario: 外部系统不依赖字典顺序
- **WHEN** a rule module needs to enumerate entities
- **THEN** it uses an API whose ordering contract is explicit
- **AND** unordered iteration is not treated as entity id sorted order
- **AND** sorted iteration is requested only when deterministic output requires it

#### Scenario: 查询语义不依赖内部存储
- **WHEN** a rule module queries entities with position, collider, auto move, push-on-enter, or port connector data
- **THEN** it uses a `GameWorld` query or subset boundary
- **AND** the result is valid whether the internal storage is dictionary-backed, indexed-pool-backed, third-party-ECS-backed, or archetype-backed
- **AND** tests assert behavior and world state rather than internal chunk, row, pool, mask, query cache, or dictionary layout

#### Scenario: snapshot/delta 语义保持
- **WHEN** GameCore changes its internal component storage implementation
- **THEN** `EntitySnapshot`, `WorldDelta`, dirty entity ids, changed cells, changed chunks, animation metadata, and removed entity ids preserve their existing external semantics
- **AND** server-authoritative sync consumers do not need to know the internal storage layout

#### Scenario: storage adapter 只在 GameWorld 内部
- **WHEN** Shared GameCore introduces an `IWorldDataStorage` or equivalent storage adapter contract
- **THEN** `GameWorld` owns that adapter
- **AND** rule modules, server Hotfix code, Unity client mirror code, protocol handlers, planner, commit resolver, arbiter, and runtime effect consumers do not directly reference the adapter
- **AND** the adapter does not become a replacement public API for `GameWorld`

#### Scenario: indexed adapter 保持当前语义
- **WHEN** the current storage implementation is expressed as an indexed or dense-pool adapter
- **THEN** entity id lookup, component add/update/remove, component query, entity enumeration order, and entity remove/recreate semantics remain equivalent to the existing `GameWorld` behavior
- **AND** old entity ids do not read newly reused internal rows

#### Scenario: Arch 不能污染规则层
- **WHEN** a future proposal evaluates Arch or another third-party ECS as an internal storage adapter
- **THEN** third-party ECS types remain behind the `GameWorld` storage adapter boundary
- **AND** `ActionArbiter`, `RulePlanning`, `CommitRules`, `BodyCapabilityResolver`, `StateDrivenRules`, `AuthoritativeWorldTickRunner`, `AuthoritativeWorldSyncSystem`, and `ClientMapWorld` do not reference third-party ECS types
- **AND** `WorldDelta` remains constructed from DG-owned dirty and snapshot semantics

#### Scenario: 本变更不引入第三方 ECS
- **WHEN** this storage adapter boundary change is implemented
- **THEN** Shared GameCore does not add Arch, Unity DOTS, DefaultEcs, Flecs, or another third-party ECS dependency
- **AND** any third-party ECS evaluation requires a separate OpenSpec proposal

#### Scenario: storage adapter 不拥有 DG 权威语义
- **WHEN** `GameWorld` delegates entity, component, or component-query operations to a storage adapter
- **THEN** `GameWorld` still owns spatial occupancy synchronization, dirty journal updates, changed cell/chunk tracking, runtime effect final component resolution, snapshot creation, and delta flushing
- **AND** the adapter does not decide push, blocked result, connected body, action arbitration, Fantasy session ownership, or client mirror convergence

### Requirement: Runtime Storage Observability
Shared GameCore SHALL provide a way to observe hot path storage and query costs before and during ECS storage migration. The observation surface MUST distinguish rule execution cost from debug logging, snapshot construction, spatial queries, and component lookup counts.

#### Scenario: 热路径统计可读
- **WHEN** automated tests or diagnostics run a server-authoritative tick
- **THEN** GameCore can report counts for entity enumeration, component lookup, spatial query, and snapshot/delta construction paths
- **AND** those counts can be disabled or ignored in normal gameplay

#### Scenario: 迁移收益需要证据
- **WHEN** an implementation claims a storage-layer optimization or archetype migration
- **THEN** it includes automated semantic regression tests
- **AND** it includes before/after observation for the relevant hot path
- **AND** compile success alone is not considered proof of migration safety

### Requirement: Shared Targeting Runtime Boundary
Shared GameCore SHALL own authoritative targeting runtime semantics as pure C# rule code. Targeting runtime code MUST NOT reference UnityEngine, Fantasy, protocol generated types, Unity presentation objects, or client-local mirror-only state.

#### Scenario: Shared targeting has no runtime shell dependency
- **WHEN** `Shared/DG.GameCore` is built after adding targeting runtime support
- **THEN** targeting code does not reference `UnityEngine`, `MonoBehaviour`, `GameObject`, `Session`, Fantasy Handler types, or generated protocol message types
- **AND** server and Unity client mirror code can both understand target data types without owning targeting authority on the client

#### Scenario: client does not decide authoritative hits
- **WHEN** Unity client displays an action result or animation
- **THEN** it consumes server snapshot/delta and metadata
- **AND** it does not locally decide which entities were hit by authoritative targeting

### Requirement: Targeting Uses GameWorld Query Boundary
Targeting selectors SHALL query entities, components, tags, positions, spatial occupancy, and connected-body metadata through stable `GameWorld` or rule-service APIs. They MUST NOT depend on concrete component store internals, dictionary order, storage adapter types, or future ECS implementation details.

#### Scenario: selector spatial query uses stable API
- **WHEN** a target selector scans one or more cells from a source entity
- **THEN** it queries spatial occupancy through `GameWorld` or a stable query service
- **AND** deterministic ordering is applied explicitly after query

#### Scenario: selector classes remain behind targeting boundary
- **WHEN** a new target algorithm is added as a selector class or extension
- **THEN** rule modules access it through the targeting registry
- **AND** server Hotfix code and Unity client mirror code do not instantiate selector implementation classes directly

#### Scenario: storage migration preserves targeting semantics
- **WHEN** `GameWorld` internal storage changes from dictionary-backed storage to indexed or ECS-like storage
- **THEN** targeting output for the same authoritative world state remains semantically equivalent
- **AND** tests assert target data and final rule results rather than internal storage layout

### Requirement: Authoritative Action Pipeline Foundation

Shared GameCore SHALL expose the authoritative action pipeline as explicit server-side data stages: `ActionContext -> TargetData -> ExecutionOutput -> Claim -> Arbitration -> Planning -> Commit`. Each stage MUST have a bounded responsibility, and server-authoritative behavior MUST NOT be decided by Unity client code, action-name string branches, entity-name branches, or hidden pending parent/child chains.

#### Scenario: Stage ownership is explicit
- **WHEN** a move-like action enters the shared rules pipeline
- **THEN** context construction owns source/target/cost/causality facts
- **AND** targeting owns read-only target data query
- **AND** execution owns structured output candidates
- **AND** claim arbitration owns conflict resolution
- **AND** planning and commit own final world mutation safety

#### Scenario: Client mirrors final result
- **WHEN** server action stages produce committed world changes
- **THEN** `ClientMapWorld` mirrors snapshot/delta results
- **AND** client code does not compute authoritative TargetData, ExecutionOutput, claim winners, blocked result, or final commit

#### Scenario: No hidden pending chain
- **WHEN** an action emits blocked, deferred, or multi-target output
- **THEN** continuation is represented through explicit action context and deferred output data
- **AND** the shared rules pipeline does not rely on removed `PendingRuleStates` as the ordinary push path

### Requirement: Front Target Movement Foundation

Shared GameCore SHALL support a minimal authoritative front-target movement foundation. A configured action MAY query a finite set of entities or occupied cells in front of its source, convert those target data items into execution output, generate one or more claims, arbitrate them, and commit only accepted plans.

#### Scenario: Move all front objects succeeds
- **WHEN** an action targets all movable objects in front of the source
- **AND** every required target movement claim can be accepted and committed
- **THEN** all required target movements are committed by the server
- **AND** observers receive final server delta rather than client-predicted positions

#### Scenario: Move all front objects blocked
- **WHEN** one required front target movement claim is blocked under default all-or-nothing policy
- **THEN** the action does not report overall success
- **AND** partial movement is not accepted unless explicit partial success policy is configured

#### Scenario: Target fanout stays deterministic
- **WHEN** multiple front targets are discovered
- **THEN** their target data and generated claims are ordered deterministically
- **AND** repeated runs with the same world state produce the same arbitration and commit result

### Requirement: Shared GameCore Source Layout Semantics
Shared GameCore SHALL organize source files by stable gameplay and runtime semantics rather than by incidental implementation history. The layout MUST keep pure domain data, configuration import, rule execution, world storage, spatial indexing, snapshot/delta contracts, runtime effects, and test-only helpers discoverable as separate responsibilities.

#### Scenario: Developer finds the rule boundary
- **WHEN** a developer needs to modify action intake, targeting, arbitration, execution strategy, planning, commit, connectivity, or deferred output behavior
- **THEN** the relevant source files are located under a `Rules` subdirectory whose folder name identifies that responsibility
- **AND** the developer does not need to inspect config provider, Unity, Fantasy, generated Luban, or storage adapter folders to find ordinary rule code

#### Scenario: Domain and runtime shell stay separated
- **WHEN** Shared GameCore source files are moved or split
- **THEN** files under Shared GameCore still compile without `Fantasy`, `Session`, protocol generated types, `UnityEngine`, `MonoBehaviour`, or `GameObject`
- **AND** the move does not create a new runtime-shell dependency

#### Scenario: Generated configuration stays isolated
- **WHEN** Luban generated C# files exist in Shared GameCore
- **THEN** they remain under an explicit generated configuration subtree
- **AND** rule modules consume runtime provider or registry abstractions instead of generated table classes

### Requirement: Shared Rule File Split Preserves Behavior
Shared GameCore file splitting SHALL preserve existing public rule behavior, storage boundary behavior, snapshot/delta semantics, and runtime effect final component semantics. Splitting a large file into semantic files MUST NOT introduce a new gameplay branch, new action policy, or new storage backend behavior.

#### Scenario: Action pipeline split is semantic only
- **WHEN** `ActionPipeline` responsibilities are split into targeting, claim, blocked outcome, strategy registration, and concrete strategy files
- **THEN** existing action requests produce the same accepted claims, rejected claims, commits, deferred outputs, and result metadata for the same world state
- **AND** action behavior is still selected by `ActionSpec` policy and registered strategies rather than action name strings

#### Scenario: GameWorld remains the public boundary
- **WHEN** world storage or query files are moved under a clearer source layout
- **THEN** external rule modules, server Hotfix code, Unity mirror code, and protocol mapping code still use `GameWorld` public APIs
- **AND** they do not directly reference storage adapter internals, component pool internals, or third-party ECS types

#### Scenario: Automated semantic regression
- **WHEN** Unity TestFramework EditMode tests run after the layout migration
- **THEN** tests cover representative action targeting, strategy execution, claim arbitration, commit, runtime effect final component resolution, snapshot/delta, and spatial query behavior
- **AND** those tests assert behavior and world state rather than the physical file path of implementation classes

