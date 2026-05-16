## MODIFIED Requirements
### Requirement: 组件驱动实体能力
系统 SHALL 用组件和 tag 表达实体能力，规则 MUST NOT 依赖 entity 名字或 demo 专用类型判断实体行为。实体能力组合 MUST 由 archetype/build 边界创建，并应用到 `GameWorld` 的 component store。正式实体 archetype SHALL reference component ids resolved by configuration/provider data; `ComponentKind` enum MUST NOT remain in formal Luban authoring, fallback provider ordinary construction, or new tests, and MUST NOT be required for adding new component applicators.

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

#### Scenario: 新静态 component 不修改 ComponentKind
- **WHEN** a new static component applicator is added for an archetype
- **THEN** the developer adds a component id, applicator module, registration, config row, and tests
- **AND** `EntityBuilder` applies it through component id registry
- **AND** the change does not add a new `ComponentKind` enum member

### Requirement: System 层规则职责边界
Shared GameCore SHALL organize movement and behavior rule code around system-layer responsibilities: action intake, action unit lifecycle, tag/component gate, subject selection, target selection, strategy execution, claim arbitration, planning, deferred output, conflict/commit, and result application. Component and tag reads inside these systems are allowed as rule inputs, but system code MUST NOT depend on concrete entity names, ordinary action ids, gameplay-specific names, Unity runtime objects, Fantasy runtime objects, protocol generated types, Ability state, RuntimeEffect state, editor scanning APIs, runtime reflection, or gameplay-extension enum members to choose ordinary behavior strategy. Component presence checks used by targeting, blocked conditions, and debug authoring filters SHALL go through a component fact query registry keyed by component id rather than central `ComponentKind` switches.

#### Scenario: 规则 system 读取 component 而不读取实体种类
- **WHEN** player movement, push, auto movement, mechanism push, configured wind push, or future ordinary behavior is evaluated
- **THEN** the rule system may read final components such as `PositionComponent`, `BlockingComponent`, `PushableComponent`, `PlayerControlComponent`, `DirectionComponent`, `AutoMoveComponent`, `PushOnEnterComponent`, and `PortConnectorComponent`
- **AND** the rule system does not branch on entity class names, demo-only entity type names, ordinary action id strings, or gameplay-extension enum members to decide behavior strategy
- **AND** the rule system does not query Ability or RuntimeEffect state

#### Scenario: tag 只作为事实输入
- **WHEN** rules evaluate tags such as source, ability, state, immunity, or blocker tags
- **THEN** those tags are used only as facts for explicit policy fields
- **AND** tag combinations do not choose action strategy, target selector, commit handler, component applicator, effect payload mapper, or result resolver

#### Scenario: 新可查询 component 不修改 targeting
- **WHEN** a new component needs to be referenced by target filter or blocked condition
- **THEN** the developer adds a component fact query module, component id registration, config data, and tests
- **AND** target filtering and blocked condition code does not add a new `ComponentKind` switch case

### Requirement: GameCore Runtime ID Boundary
Shared GameCore SHALL convert configuration-readable action, policy, style, component, effect payload, target selector, commit handler, snapshot payload, and tag names into stable runtime identifiers before authoritative rules consume them. Runtime arbitration, targeting, planning, commit, pending, deferred output, component application, effect settlement, snapshot projection, and component result resolution MUST consume typed or numeric identifiers and final component/tag values rather than comparing raw strings or requiring gameplay-extension enum members. Formal Luban source tables, generated formal config, fallback provider ordinary paths, and new tests MUST NOT use gameplay-extension enums for capabilities that can grow through gameplay content.

#### Scenario: 配置名在导入边界解析
- **WHEN** Luban action, blocked policy, push-on-enter output, entity component, entity tag, effect payload, target selector, commit handler, snapshot payload, or animation style data is loaded
- **THEN** the provider or registry resolves readable names into runtime identifiers or stable internal values
- **AND** rule execution receives the resolved identifier or enum value only when that enum is a stable internal state
- **AND** the readable name remains available only for authoring, diagnostics, or presentation metadata

#### Scenario: 规则层不比较字符串 spec
- **WHEN** player move, auto move, mechanism push, configured wind push, debug action, or push handoff enters arbitration
- **THEN** subject selection, blocked result, handoff, priority, conflict, merge, planning, and commit decisions are selected from typed policy data
- **AND** the rules layer does not compare raw action spec strings such as `player_move`, `mechanism_push`, `auto_move`, or future ordinary behavior names

#### Scenario: tag 作为位标记进入规则层
- **WHEN** action policy or entity archetype data contains source, ability, state, immunity, or blocker tags
- **THEN** configuration import resolves those tags to `WorldTag` or an equivalent typed tag set
- **AND** authoritative rules read final tag/component facts rather than raw string tag names

#### Scenario: 扩展 enum 不作为入口
- **WHEN** a feature adds a new action strategy, target selector, commit handler, component applicator, component result resolver, effect payload mapper, or snapshot payload projector
- **THEN** it uses stable id registration and config/provider data
- **AND** it does not add members to `ActionPrimitive`, `ActionTargetRule`, `CommitProposalKind`, `ComponentKind`, `ComponentResultKind`, `EffectKind`, or `RuntimeEffectKind`
- **AND** formal Luban source tables and fallback provider ordinary paths do not use those enums as authoring or construction inputs

#### Scenario: 稳定内部 enum 可以保留
- **WHEN** code uses fixed internal states such as direction, lifecycle state, duration policy, stack policy, remove policy, ordering policy, or fixed error code
- **THEN** those values MAY remain enums
- **AND** they are not used as the sole registry of gameplay extension modules

#### Scenario: fallback 只保留兼容专项
- **WHEN** compatibility tests verify old enum data migration
- **THEN** they MAY construct legacy enum fixtures inside an explicit compatibility test boundary
- **AND** ordinary fallback provider data, new gameplay tests, and runtime examples use id-first data

### Requirement: 统一 Dirty 与 WorldSnapshot/WorldDelta
系统 SHALL 用统一 dirty、WorldSnapshot 和 WorldDelta 数据模型表达玩家、球、阻挡体、进入推动地格以及调试编辑实体的服务端权威状态变化。WorldDelta SHALL 同时支持仍然存在的 changed entity snapshot 和已经被移除的 removed entity id。新增需要同步的 component or presentation payload SHALL use explicit snapshot payload id and registered projector/applier rather than central snapshot enum or component-name branches.

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

#### Scenario: 新同步 payload 使用注册
- **WHEN** a new component or presentation payload must be synchronized
- **THEN** the developer adds a snapshot payload id, projector/applier modules, registration, protocol/payload mapping when needed, and tests
- **AND** `GameWorld.CreateSnapshot`, `ApplySnapshot`, or delta construction does not gain a gameplay-specific enum branch
