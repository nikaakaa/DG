## MODIFIED Requirements

### Requirement: 组件驱动实体能力
系统 SHALL 用组件和 tag 表达实体能力，规则 MUST NOT 依赖 entity 名字或 demo 专用类型判断实体行为。实体能力组合 MUST 由 archetype/build 边界创建，并应用到 `GameWorld` 的 component store。组件挂载 SHALL be selected through stable component ids and registered component applicators. New static component types MUST be addable by adding an applicator module, config data, and tests without editing `EntityBuilder` or `GameWorld` entity creation flow.

#### Scenario: 玩家实体能力
- **WHEN** 配置创建玩家 entity
- **THEN** 玩家 entity 拥有 `PositionComponent`
- **AND** 玩家 entity 拥有 `ColliderComponent`
- **AND** 玩家 entity 拥有 `BlockingComponent`
- **AND** 玩家 entity 拥有 `PlayerControlComponent`
- **AND** 这些组件由 registered component applicators or equivalent build boundary 应用，而不是由 `GameWorld` 内部 if 链组装

#### Scenario: 反弹球实体能力
- **WHEN** 配置创建自动反弹球 entity
- **THEN** 球 entity 拥有 `PositionComponent`
- **AND** 球 entity 拥有 `DirectionComponent`
- **AND** 球 entity 拥有 `ColliderComponent`
- **AND** 球 entity 拥有 `AutoMoveComponent`
- **AND** 球 entity 拥有 `BouncableComponent`
- **AND** 这些组件由 registered component applicators or equivalent build boundary 应用，而不是由 `GameWorld` 内部 if 链组装

#### Scenario: 阻挡体能力
- **WHEN** 配置创建墙或阻挡体 entity
- **THEN** 该 entity 拥有 `PositionComponent`
- **AND** 该 entity 拥有 `ColliderComponent`
- **AND** 该 entity 拥有 `BlockingComponent`
- **AND** 这些组件由 registered component applicators or equivalent build boundary 应用，而不是由 `GameWorld` 内部 if 链组装

#### Scenario: 新静态 component 不修改 EntityBuilder
- **WHEN** a new static component type is introduced for authoritative rules
- **THEN** the developer adds the component struct, component id, applicator module, config reference, and Unity TestFramework EditMode tests
- **AND** `EntityBuilder` continues to iterate configured component ids and dispatch through the applicator registry
- **AND** `GameWorld` entity creation flow does not gain a new component-specific branch

### Requirement: 实体组合构建边界
系统 SHALL 将实体组件组合规则放在 GameCore 的 archetype/build 边界中，而不是放在 `GameWorld` 容器内部。`GameWorld` MUST 只负责 entity 存储、component store、空间索引、dirty 和 snapshot/delta。Archetype component lists SHALL reference stable component ids, and authoring/provider import SHALL resolve readable component names before rule/runtime code consumes them.

#### Scenario: GameWorld 不组装组件组合
- **WHEN** 通过配置创建 player、ball 或 blocker entity
- **THEN** 组件组合由 archetype/build 边界决定
- **AND** `GameWorld` 不通过 `HasPosition`、`HasCollider`、`HasAutoMove` 或类似布尔字段分支决定组件集合

#### Scenario: 新增组合不修改 GameWorld
- **WHEN** 后续新增一个由已有组件组成的实体 archetype
- **THEN** 系统通过新增或修改 archetype 配置表达组合
- **AND** 不需要修改 `GameWorld` 的实体创建代码

#### Scenario: 未知 component id 显式失败
- **WHEN** archetype configuration references an unknown component id
- **THEN** provider or entity build fails with a clear error
- **AND** the entity is not silently created with a partial or fallback component set

### Requirement: System 层规则职责边界
Shared GameCore SHALL organize movement and behavior rule code around system-layer responsibilities: action intake, action unit lifecycle, tag/component gate, subject selection, target selection, strategy execution, claim arbitration, planning, deferred output, conflict/commit, and result application. Component and tag reads inside these systems are allowed as rule inputs, but system code MUST NOT depend on concrete entity names, ordinary action ids, gameplay-specific names, Unity runtime objects, Fantasy runtime objects, protocol generated types, Ability state, RuntimeEffect state, editor scanning APIs, or runtime assembly scanning to choose ordinary behavior strategy. Component presence checks used by targeting, blocked conditions, and debug authoring filters SHALL go through a component fact query registry keyed by component id rather than central `ComponentKind` switches.

#### Scenario: 规则 system 读取 component 而不读取实体种类
- **WHEN** player movement, push, auto movement, mechanism push, configured wind push, or future ordinary behavior is evaluated
- **THEN** the rule system may read final components such as `PositionComponent`, `BlockingComponent`, `PushableComponent`, `PlayerControlComponent`, `DirectionComponent`, `AutoMoveComponent`, `PushOnEnterComponent`, and `PortConnectorComponent`
- **AND** the rule system does not branch on entity class names, demo-only entity type names, or ordinary action id strings to decide behavior strategy
- **AND** the rule system does not query Ability or RuntimeEffect state

#### Scenario: tag 只作为事实输入
- **WHEN** rules evaluate tags such as source, ability, state, immunity, or blocker tags
- **THEN** those tags are used only as facts for explicit policy checks
- **AND** tag combinations do not choose hidden action strategy, subject policy, target policy, or commit behavior

#### Scenario: 新可查询 component 不修改 targeting
- **WHEN** a new final component should be usable in target filters or blocked result conditions
- **THEN** the developer adds a component fact query registration for its component id
- **AND** targeting and blocked resolver modules do not gain a component-specific switch branch
- **AND** unknown query component ids fail clearly

### Requirement: 统一 Dirty 与 WorldSnapshot/WorldDelta
系统 SHALL 用统一 dirty、WorldSnapshot 和 WorldDelta 数据模型表达玩家、球、阻挡体、进入推动地格以及调试编辑实体的服务端权威状态变化。WorldDelta SHALL 同时支持仍然存在的 changed entity snapshot 和已经被移除的 removed entity id。Snapshot/delta component projection SHALL be explicit and registered: new component data enters sync only through projector/applier modules or an equivalent declared payload boundary, not by adding ad hoc fields directly to `GameWorld.CreateSnapshot` and mirror application code.

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

#### Scenario: 新同步 component 使用投影注册
- **WHEN** a new component must be visible to Unity mirror or server sync consumers
- **THEN** the developer adds a snapshot projector/applier or equivalent declared payload mapping
- **AND** `GameWorld.CreateSnapshot` and `GameWorld.ApplySnapshot` remain orchestration boundaries rather than component-specific switch bodies
- **AND** components without registered sync projection remain server-authoritative only and are not silently serialized
