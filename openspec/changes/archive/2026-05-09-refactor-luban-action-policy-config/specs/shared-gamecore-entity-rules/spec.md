## MODIFIED Requirements
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

## ADDED Requirements
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
