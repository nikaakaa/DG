## MODIFIED Requirements
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

### Requirement: 进入推动统一移动裁决
系统 SHALL 在服务端 tick 中把进入推动效果转换为 state-driven mechanism source action input，并交给统一的 action unit / arbitration / plan / commit / pending retry 管线裁决。

#### Scenario: 站上传送带被推动
- **WHEN** 一个拥有 `PositionComponent` 的 entity 位于拥有 `PushOnEnterComponent` 的地格坐标
- **AND** 该地格拥有向右的 `DirectionComponent`
- **THEN** server-authoritative tick creates a mechanism source move or push action input for that entity
- **AND** the state-driven rule pipeline decides whether the entity may enter the right-side coordinate

#### Scenario: 阻挡目标格拒绝推动
- **WHEN** 被推动 entity 的目标格存在拥有 `BlockingComponent` 的 entity
- **THEN** the state-driven rule pipeline rejects, waits for derived action, or fails the movement according to `ActionSpec` policy and final Component state
- **AND** 被推动 entity 不会被 execution 层直接绕过阻挡规则修改坐标

#### Scenario: 单 tick 防止链式重复推动
- **WHEN** 一个 entity 在同一服务端 tick 中已经处于 waiting action unit、pending retry、或已被推动
- **THEN** 该 tick 内其他进入推动地格不会再次直接推动该 entity
- **AND** 下一服务端 tick 可以重新评估该 entity 是否继续被推动

### Requirement: Intent 策略定义注册表
Shared GameCore MAY keep the existing intent definition registry as a migration compatibility boundary for old `BehaviorIntentKind` defaults, but new authoritative movement behavior SHALL use `ActionSpec` and action unit policy as the rule source. The system MUST NOT infer defaults through multiple independent switch statements, and an unknown legacy intent kind MUST fail explicitly instead of silently falling back to player semantics.

#### Scenario: 现有 intent kind 拥有显式定义
- **WHEN** compatibility code creates a `BehaviorIntent` for `Move`, `Push`, `AutoMove`, `MechanismPush`, or `DebugMove`
- **THEN** its legacy default source tag, ability tag, blocked tags, required tags, and cancel policy come from the intent definition registry
- **AND** the compatibility behavior matches the current player move, player push, auto move, mechanism push, and debug move behavior

#### Scenario: 新行为不新增 intent kind
- **WHEN** a new ordinary move-like behavior is added
- **THEN** it is expressed through `ActionSpec`, source context, action unit lifecycle, and policy data
- **AND** it does not require a new `BehaviorIntentKind`

#### Scenario: 未注册 intent kind 显式失败
- **WHEN** compatibility code attempts to create or resolve a `BehaviorIntent` whose kind has no registered definition
- **THEN** Shared GameCore fails with a clear error
- **AND** the intent is not treated as `SourcePlayer` by default

### Requirement: System 层规则职责边界
Shared GameCore SHALL organize movement rule code around system-layer responsibilities: action intake, action unit lifecycle, arbitration, planning, conflict/commit, pending retry, and result application. Component reads inside these systems are allowed as rule inputs, but system code MUST NOT depend on concrete entity names, demo-specific entity types, Unity runtime objects, Fantasy runtime objects, protocol generated types, Ability state, or RuntimeEffect state.

#### Scenario: 规则 system 读取 component 而不读取实体种类
- **WHEN** player movement, push, auto movement, or mechanism push is evaluated
- **THEN** the rule system may read final components such as `PositionComponent`, `BlockingComponent`, `PushableComponent`, `PlayerControlComponent`, `DirectionComponent`, `AutoMoveComponent`, `PushOnEnterComponent`, and `PortConnectorComponent`
- **AND** the rule system does not branch on entity class names or demo-only entity type names to decide movement behavior
- **AND** the rule system does not query Ability or RuntimeEffect state

#### Scenario: 规则职责拆分后行为保持一致
- **WHEN** the state-driven rule system processes the same world state and queued actions as before the refactor
- **THEN** it produces equivalent accepted action units, rejected reasons, move plans, commit results, dirty changes, and owner action results for existing covered scenarios

### Requirement: Rule Pipeline File Boundaries
The rule pipeline SHALL keep action definitions, action unit lifecycle, arbitration, planning, conflict resolution, pending retry, and rule execution in separate source file boundaries while preserving the same runtime behavior.

#### Scenario: Action definitions are separate from arbitration execution
- **WHEN** a new `ActionSpec` default policy is reviewed
- **THEN** its source, required components/tags, blocked components/tags, cost, blocked policy, merge policy, interrupt policy, plan rule, and commit rule are found in the action definition boundary
- **AND** the arbiter does not contain per-kind switch inference logic

#### Scenario: Planning and commit remain distinct
- **WHEN** a move action unit is accepted by arbitration
- **THEN** planning produces a move plan before commit
- **AND** conflict resolution remains responsible for same-tick atomic commit decisions

#### Scenario: Pending retry is separate from commit
- **WHEN** a derived action unit succeeds or fails
- **THEN** pending retry state updates parent action unit status outside the conflict resolver
- **AND** the conflict resolver does not create derived action units
